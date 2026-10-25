using PhantomVault.Security.PasswordProtection;
using Xunit;

namespace PhantomVault.Security.Tests;

public class PasswordValidatorTests
{
    private readonly PasswordPolicyValidator _validator = new();

    [Theory]
    [InlineData("", 0, "Empty")]
    [InlineData("password", 0, "Common / Guessable")]
    [InlineData("123456", 0, "Common / Guessable")]
    [InlineData("short", 0, "Very Weak")]
    [InlineData("shortlow", 1, "Weak")]
    [InlineData("Pass12345678", 2, "Fair")]
    [InlineData("Correct-Horse-Battery-Staple-2026!", 4, "Very Strong")]
    public void EvaluatePassword_AssessesStrengthCorrectly(string password, int minScore, string expectedLabel)
    {
        var result = _validator.EvaluatePassword(password);
        Assert.Equal(expectedLabel, result.Label);
        Assert.True(result.Score >= minScore);
    }
}
