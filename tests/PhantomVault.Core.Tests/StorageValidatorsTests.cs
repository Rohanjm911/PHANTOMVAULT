using System;
using System.IO;
using PhantomVault.Core.Exceptions;
using PhantomVault.Core.Validation;
using Xunit;

namespace PhantomVault.Core.Tests;

public class StorageValidatorsTests
{
    [Theory]
    [InlineData("document.pdf", "document.pdf")]
    [InlineData("my_research_2026.docx", "my_research_2026.docx")]
    [InlineData("archive.tar.gz", "archive.tar.gz")]
    public void ValidateAndSanitizeFileName_ValidNames_Succeeds(string input, string expected)
    {
        string sanitized = StorageValidators.ValidateAndSanitizeFileName(input);
        Assert.Equal(expected, sanitized);
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("..\\secret.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\System32\\cmd.exe")]
    [InlineData("folder/file.txt")]
    [InlineData("folder\\file.txt")]
    [InlineData("test..file")]
    public void ValidateAndSanitizeFileName_TraversalOrSubpaths_ThrowsPathTraversalException(string input)
    {
        Assert.Throws<PathTraversalException>(() =>
            StorageValidators.ValidateAndSanitizeFileName(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ValidateAndSanitizeFileName_EmptyOrNull_ThrowsPhantomVaultException(string? input)
    {
        Assert.ThrowsAny<PhantomVaultException>(() =>
            StorageValidators.ValidateAndSanitizeFileName(input!));
    }

    [Fact]
    public void ValidateRelativePath_EscapingRoot_ThrowsPathTraversalException()
    {
        string root = Path.Combine(Path.GetTempPath(), "VaultRoot");

        Assert.Throws<PathTraversalException>(() =>
            StorageValidators.ValidateRelativePath("../../sensitive.txt", root));

        Assert.Throws<PathTraversalException>(() =>
            StorageValidators.ValidateRelativePath("..\\..\\Windows\\System32", root));
    }

    [Fact]
    public void ValidateRelativePath_InsideRoot_ReturnsNormalizedPath()
    {
        string root = Path.Combine(Path.GetTempPath(), "VaultRoot");
        string relative = "subfolder/nested/file.txt";

        string normalized = StorageValidators.ValidateRelativePath(relative, root);
        Assert.False(string.IsNullOrEmpty(normalized));
        Assert.DoesNotContain("..", normalized);
    }
}
