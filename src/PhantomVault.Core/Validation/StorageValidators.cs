using System;
using System.IO;
using PhantomVault.Core.Exceptions;

namespace PhantomVault.Core.Validation;

/// <summary>
/// Security validators for file paths and names, guaranteeing path-traversal prevention.
/// </summary>
public static class StorageValidators
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    /// <summary>
    /// Validates that a filename does not contain directory separators or illegal characters.
    /// </summary>
    public static string ValidateAndSanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new PhantomVaultException("File name cannot be empty or whitespace.");

        string sanitized = Path.GetFileName(fileName);
        if (sanitized != fileName && !sanitized.Equals(fileName, StringComparison.OrdinalIgnoreCase))
        {
            // If Path.GetFileName stripped path elements, a directory traversal or subpath was supplied
            throw new PathTraversalException(fileName);
        }

        if (sanitized.Contains("..", StringComparison.Ordinal) || sanitized.IndexOfAny(InvalidFileNameChars) >= 0)
        {
            throw new PathTraversalException(fileName);
        }

        return sanitized;
    }

    /// <summary>
    /// Validates that a relative directory path remains strictly within the intended root.
    /// </summary>
    public static string ValidateRelativePath(string relativePath, string rootPath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return string.Empty;

        // Prevent leading slashes or driving root
        if (Path.IsPathRooted(relativePath))
            throw new PathTraversalException(relativePath);

        string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        string combined = Path.GetFullPath(Path.Combine(rootPath, normalized));
        string normalizedRoot = Path.GetFullPath(rootPath);

        if (!normalizedRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            normalizedRoot += Path.DirectorySeparatorChar;
        }

        if (!combined.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase) && combined != Path.GetFullPath(rootPath))
        {
            throw new PathTraversalException(relativePath);
        }

        return normalized;
    }
}
