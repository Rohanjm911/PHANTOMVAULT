using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using PhantomVault.Core.Exceptions;
using PhantomVault.Core.Interfaces;
using PhantomVault.Core.Models;

namespace PhantomVault.Storage;

/// <summary>
/// Encrypted SQLite metadata catalog managing directory records and file headers.
/// Protects filenames, paths, and attributes using AES-256-GCM with the vault DEK.
/// </summary>
public sealed class MetadataRepository
{
    private readonly string _databasePath;
    private readonly ICryptographyService _crypto;

    public MetadataRepository(string vaultDirectoryPath, ICryptographyService crypto)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vaultDirectoryPath);
        _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
        _databasePath = Path.Combine(vaultDirectoryPath, "catalog.db");
    }

    private string ConnectionString => $"Data Source={_databasePath};Mode=ReadWriteCreate;";

    public async Task InitializeSchemaAsync()
    {
        using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;

            CREATE TABLE IF NOT EXISTS VaultEntries (
                Id TEXT PRIMARY KEY,
                ParentId TEXT NULL,
                EncryptedName BLOB NOT NULL,
                NameTag BLOB NOT NULL,
                NameNonce BLOB NOT NULL,
                EncryptedMetadata BLOB NOT NULL,
                MetaTag BLOB NOT NULL,
                MetaNonce BLOB NOT NULL,
                FileSize INTEGER NOT NULL,
                PlaintextSize INTEGER NOT NULL,
                Sha256Checksum TEXT NOT NULL,
                IsDirectory INTEGER NOT NULL,
                CreatedAt INTEGER NOT NULL,
                ModifiedAt INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS VaultProperties (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_VaultEntries_ParentId ON VaultEntries(ParentId);
        ";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task AddEntryAsync(VaultEntry entry, byte[] vaultKey)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(vaultKey);

        byte[] nameBytes = Encoding.UTF8.GetBytes(entry.Name);
        byte[] nameAad = Encoding.UTF8.GetBytes($"Entry:{entry.Id}:Name");
        byte[] nameNonce = _crypto.GenerateSecureRandomBytes(12);
        byte[] encName = _crypto.EncryptBytes(nameBytes, vaultKey, nameNonce, nameAad, out byte[] nameTag);

        var metaDto = new EntryMetadataDto
        {
            RelativePath = entry.RelativePath,
            MimeType = entry.MimeType ?? "application/octet-stream"
        };
        byte[] metaBytes = JsonSerializer.SerializeToUtf8Bytes(metaDto);
        byte[] metaAad = Encoding.UTF8.GetBytes($"Entry:{entry.Id}:Meta");
        byte[] metaNonce = _crypto.GenerateSecureRandomBytes(12);
        byte[] encMeta = _crypto.EncryptBytes(metaBytes, vaultKey, metaNonce, metaAad, out byte[] metaTag);

        using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        using var tx = connection.BeginTransaction();

        using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = @"
            INSERT INTO VaultEntries (
                Id, ParentId, EncryptedName, NameTag, NameNonce, 
                EncryptedMetadata, MetaTag, MetaNonce, FileSize, PlaintextSize, 
                Sha256Checksum, IsDirectory, CreatedAt, ModifiedAt
            ) VALUES (
                @id, @parentId, @encName, @nameTag, @nameNonce, 
                @encMeta, @metaTag, @metaNonce, @fileSize, @plaintextSize, 
                @sha256, @isDir, @createdAt, @modifiedAt
            );
        ";

        command.Parameters.AddWithValue("@id", entry.Id.ToString());
        command.Parameters.AddWithValue("@parentId", entry.ParentId.HasValue ? entry.ParentId.Value.ToString() : DBNull.Value);
        command.Parameters.AddWithValue("@encName", encName);
        command.Parameters.AddWithValue("@nameTag", nameTag);
        command.Parameters.AddWithValue("@nameNonce", nameNonce);
        command.Parameters.AddWithValue("@encMeta", encMeta);
        command.Parameters.AddWithValue("@metaTag", metaTag);
        command.Parameters.AddWithValue("@metaNonce", metaNonce);
        command.Parameters.AddWithValue("@fileSize", entry.CiphertextSizeBytes);
        command.Parameters.AddWithValue("@plaintextSize", entry.PlaintextSizeBytes);
        command.Parameters.AddWithValue("@sha256", entry.Sha256Hash);
        command.Parameters.AddWithValue("@isDir", entry.IsDirectory ? 1 : 0);
        command.Parameters.AddWithValue("@createdAt", new DateTimeOffset(entry.CreatedAtUtc).ToUnixTimeSeconds());
        command.Parameters.AddWithValue("@modifiedAt", new DateTimeOffset(entry.ModifiedAtUtc).ToUnixTimeSeconds());

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        await tx.CommitAsync().ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<VaultEntry>> GetEntriesAsync(byte[] vaultKey, Guid? parentFolderId = null)
    {
        ArgumentNullException.ThrowIfNull(vaultKey);

        var entries = new List<VaultEntry>();

        using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        using var command = connection.CreateCommand();
        if (parentFolderId.HasValue)
        {
            command.CommandText = "SELECT * FROM VaultEntries WHERE ParentId = @parentId ORDER BY IsDirectory DESC, CreatedAt DESC;";
            command.Parameters.AddWithValue("@parentId", parentFolderId.Value.ToString());
        }
        else
        {
            command.CommandText = "SELECT * FROM VaultEntries ORDER BY IsDirectory DESC, CreatedAt DESC;";
        }

        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            Guid id = Guid.Parse(reader.GetString(0));
            Guid? parentId = reader.IsDBNull(1) ? null : Guid.Parse(reader.GetString(1));

            byte[] encName = (byte[])reader["EncryptedName"];
            byte[] nameTag = (byte[])reader["NameTag"];
            byte[] nameNonce = (byte[])reader["NameNonce"];

            byte[] encMeta = (byte[])reader["EncryptedMetadata"];
            byte[] metaTag = (byte[])reader["MetaTag"];
            byte[] metaNonce = (byte[])reader["MetaNonce"];

            long fileSize = reader.GetInt64(8);
            long plaintextSize = reader.GetInt64(9);
            string sha256 = reader.GetString(10);
            bool isDir = reader.GetInt32(11) == 1;
            long createdAt = reader.GetInt64(12);
            long modifiedAt = reader.GetInt64(13);

            // Decrypt Name
            byte[] nameAad = Encoding.UTF8.GetBytes($"Entry:{id}:Name");
            string decryptedName;
            try
            {
                byte[] namePlain = _crypto.DecryptBytes(encName, vaultKey, nameNonce, nameTag, nameAad);
                decryptedName = Encoding.UTF8.GetString(namePlain);
            }
            catch (Exception ex)
            {
                throw new CryptographicIntegrityException($"Failed to decrypt entry metadata for record {id}", ex);
            }

            // Decrypt Metadata
            byte[] metaAad = Encoding.UTF8.GetBytes($"Entry:{id}:Meta");
            string relativePath = decryptedName;
            string? mimeType = null;
            try
            {
                byte[] metaPlain = _crypto.DecryptBytes(encMeta, vaultKey, metaNonce, metaTag, metaAad);
                var metaDto = JsonSerializer.Deserialize<EntryMetadataDto>(metaPlain);
                if (metaDto != null)
                {
                    relativePath = metaDto.RelativePath;
                    mimeType = metaDto.MimeType;
                }
            }
            catch
            {
                // Fallback to name if metadata block fails
            }

            entries.Add(new VaultEntry
            {
                Id = id,
                ParentId = parentId,
                Name = decryptedName,
                RelativePath = relativePath,
                CiphertextSizeBytes = fileSize,
                PlaintextSizeBytes = plaintextSize,
                Sha256Hash = sha256,
                IsDirectory = isDir,
                CreatedAtUtc = DateTimeOffset.FromUnixTimeSeconds(createdAt).UtcDateTime,
                ModifiedAtUtc = DateTimeOffset.FromUnixTimeSeconds(modifiedAt).UtcDateTime,
                MimeType = mimeType
            });
        }

        return entries;
    }

    public async Task<VaultEntry?> GetEntryByIdAsync(Guid entryId, byte[] vaultKey)
    {
        ArgumentNullException.ThrowIfNull(vaultKey);

        using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM VaultEntries WHERE Id = @id LIMIT 1;";
        command.Parameters.AddWithValue("@id", entryId.ToString());

        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        if (!await reader.ReadAsync().ConfigureAwait(false))
            return null;

        Guid id = Guid.Parse(reader.GetString(0));
        Guid? parentId = reader.IsDBNull(1) ? null : Guid.Parse(reader.GetString(1));

        byte[] encName = (byte[])reader["EncryptedName"];
        byte[] nameTag = (byte[])reader["NameTag"];
        byte[] nameNonce = (byte[])reader["NameNonce"];

        byte[] encMeta = (byte[])reader["EncryptedMetadata"];
        byte[] metaTag = (byte[])reader["MetaTag"];
        byte[] metaNonce = (byte[])reader["MetaNonce"];

        long fileSize = reader.GetInt64(8);
        long plaintextSize = reader.GetInt64(9);
        string sha256 = reader.GetString(10);
        bool isDir = reader.GetInt32(11) == 1;
        long createdAt = reader.GetInt64(12);
        long modifiedAt = reader.GetInt64(13);

        byte[] nameAad = Encoding.UTF8.GetBytes($"Entry:{id}:Name");
        byte[] namePlain = _crypto.DecryptBytes(encName, vaultKey, nameNonce, nameTag, nameAad);
        string decryptedName = Encoding.UTF8.GetString(namePlain);

        byte[] metaAad = Encoding.UTF8.GetBytes($"Entry:{id}:Meta");
        byte[] metaPlain = _crypto.DecryptBytes(encMeta, vaultKey, metaNonce, metaTag, metaAad);
        var metaDto = JsonSerializer.Deserialize<EntryMetadataDto>(metaPlain);

        return new VaultEntry
        {
            Id = id,
            ParentId = parentId,
            Name = decryptedName,
            RelativePath = metaDto?.RelativePath ?? decryptedName,
            CiphertextSizeBytes = fileSize,
            PlaintextSizeBytes = plaintextSize,
            Sha256Hash = sha256,
            IsDirectory = isDir,
            CreatedAtUtc = DateTimeOffset.FromUnixTimeSeconds(createdAt).UtcDateTime,
            ModifiedAtUtc = DateTimeOffset.FromUnixTimeSeconds(modifiedAt).UtcDateTime,
            MimeType = metaDto?.MimeType
        };
    }

    public async Task DeleteEntryAsync(Guid entryId)
    {
        using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM VaultEntries WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", entryId.ToString());
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task RenameEntryAsync(Guid entryId, string newName, byte[] vaultKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        ArgumentNullException.ThrowIfNull(vaultKey);

        byte[] nameBytes = Encoding.UTF8.GetBytes(newName);
        byte[] nameAad = Encoding.UTF8.GetBytes($"Entry:{entryId}:Name");
        byte[] nameNonce = _crypto.GenerateSecureRandomBytes(12);
        byte[] encName = _crypto.EncryptBytes(nameBytes, vaultKey, nameNonce, nameAad, out byte[] nameTag);

        using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE VaultEntries 
            SET EncryptedName = @encName, NameTag = @nameTag, NameNonce = @nameNonce, ModifiedAt = @modifiedAt
            WHERE Id = @id;
        ";
        command.Parameters.AddWithValue("@id", entryId.ToString());
        command.Parameters.AddWithValue("@encName", encName);
        command.Parameters.AddWithValue("@nameTag", nameTag);
        command.Parameters.AddWithValue("@nameNonce", nameNonce);
        command.Parameters.AddWithValue("@modifiedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task<(int count, long totalCipher, long totalPlain)> GetStatsAsync()
    {
        using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*), TOTAL(FileSize), TOTAL(PlaintextSize) FROM VaultEntries;";

        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        if (await reader.ReadAsync().ConfigureAwait(false))
        {
            int count = reader.GetInt32(0);
            long totalCipher = Convert.ToInt64(reader.GetDouble(1));
            long totalPlain = Convert.ToInt64(reader.GetDouble(2));
            return (count, totalCipher, totalPlain);
        }
        return (0, 0, 0);
    }

    private class EntryMetadataDto
    {
        public string RelativePath { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
    }
}
