using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using PhantomVault.Core.Enums;
using PhantomVault.Core.Interfaces;
using PhantomVault.Core.Models;
using PhantomVault.Storage;

namespace PhantomVault.Infrastructure.Logging;

/// <summary>
/// Privacy-preserving local audit logging service with automated secret sanitization.
/// </summary>
public sealed class ActivityLogService : IActivityLogService
{
    private readonly string _dbPath;

    public ActivityLogService(string? customDbPath = null)
    {
        _dbPath = customDbPath ?? StoragePaths.ActivityLogPath;
        InitializeDatabase();
    }

    private string ConnectionString => $"Data Source={_dbPath};Mode=ReadWriteCreate;";

    private void InitializeDatabase()
    {
        string? dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS ActivityLogs (
                Id TEXT PRIMARY KEY,
                TimestampUtc INTEGER NOT NULL,
                EventType INTEGER NOT NULL,
                Severity INTEGER NOT NULL,
                VaultName TEXT NOT NULL,
                Message TEXT NOT NULL,
                Details TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_ActivityLogs_Timestamp ON ActivityLogs(TimestampUtc DESC);
        ";
        cmd.ExecuteNonQuery();
    }

    public async Task LogEventAsync(ActivityEventType eventType, ActivitySeverity severity, string vaultName, string message, string details = "")
    {
        string sanitizedMsg = Sanitize(message);
        string sanitizedDetails = Sanitize(details);

        using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync().ConfigureAwait(false);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO ActivityLogs (Id, TimestampUtc, EventType, Severity, VaultName, Message, Details)
            VALUES (@id, @ts, @type, @sev, @vault, @msg, @details);
        ";
        cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("@ts", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("@type", (int)eventType);
        cmd.Parameters.AddWithValue("@sev", (int)severity);
        cmd.Parameters.AddWithValue("@vault", vaultName ?? string.Empty);
        cmd.Parameters.AddWithValue("@msg", sanitizedMsg);
        cmd.Parameters.AddWithValue("@details", sanitizedDetails);

        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ActivityLogRecord>> GetLogsAsync(ActivityEventType? filterType = null, int maxCount = 500)
    {
        var logs = new List<ActivityLogRecord>();

        using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync().ConfigureAwait(false);

        using var cmd = conn.CreateCommand();
        if (filterType.HasValue)
        {
            cmd.CommandText = "SELECT * FROM ActivityLogs WHERE EventType = @type ORDER BY TimestampUtc DESC LIMIT @max;";
            cmd.Parameters.AddWithValue("@type", (int)filterType.Value);
        }
        else
        {
            cmd.CommandText = "SELECT * FROM ActivityLogs ORDER BY TimestampUtc DESC LIMIT @max;";
        }
        cmd.Parameters.AddWithValue("@max", maxCount);

        using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            logs.Add(new ActivityLogRecord
            {
                Id = Guid.Parse(reader.GetString(0)),
                TimestampUtc = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(1)).UtcDateTime,
                EventType = (ActivityEventType)reader.GetInt32(2),
                Severity = (ActivitySeverity)reader.GetInt32(3),
                VaultName = reader.GetString(4),
                Message = reader.GetString(5),
                Details = reader.GetString(6)
            });
        }

        return logs;
    }

    public async Task ClearLogsAsync()
    {
        using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync().ConfigureAwait(false);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM ActivityLogs;";
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Redacts sensitive path fragments, passwords, key representations, and user profile names.
    /// </summary>
    public static string Sanitize(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        string sanitized = input;

        // Redact Windows user profile names in absolute paths (e.g. C:\Users\Username\... -> C:\Users\[REDACTED]\...)
        sanitized = Regex.Replace(sanitized, @"([a-zA-Z]:\\Users\\)[^\\]+(\\)", "$1[USER]$2", RegexOptions.IgnoreCase);

        // Redact potential password parameters (password=..., pass:..., secret:...)
        sanitized = Regex.Replace(sanitized, @"(password|secret|key|token|auth)\s*[:=]\s*[^\s,;]+", "$1=[REDACTED]", RegexOptions.IgnoreCase);

        return sanitized;
    }
}
