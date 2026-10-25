using System;
using System.Collections.Generic;
using System.Linq;
using PhantomVault.Core.Interfaces;
using PhantomVault.Core.Models;

namespace PhantomVault.Security.PasswordProtection;

/// <summary>
/// Password complexity and entropy evaluation engine.
/// Enforces strong passphrase guidance without artificial master backdoors.
/// </summary>
public sealed class PasswordPolicyValidator : IPasswordPolicyValidator
{
    private static readonly HashSet<string> CommonPassphrases = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "123456", "12345678", "qwerty", "admin", "welcome", "phantomvault",
        "secret", "master", "iloveyou", "password123", "letmein", "changeme", "default"
    };

    public PasswordStrengthResult EvaluatePassword(string password)
    {
        var result = new PasswordStrengthResult();

        if (string.IsNullOrEmpty(password))
        {
            result.Score = 0;
            result.Label = "Empty";
            result.EstimatedEntropyBits = 0;
            result.Suggestions.Add("Please enter a master password.");
            return result;
        }

        if (CommonPassphrases.Contains(password))
        {
            result.Score = 0;
            result.Label = "Common / Guessable";
            result.EstimatedEntropyBits = 5;
            result.Suggestions.Add("This password is on the list of most commonly breached passwords. Choose a unique passphrase.");
            return result;
        }

        bool hasLower = password.Any(char.IsLower);
        bool hasUpper = password.Any(char.IsUpper);
        bool hasDigit = password.Any(char.IsDigit);
        bool hasSpecial = password.Any(c => !char.IsLetterOrDigit(c));

        int poolSize = 0;
        if (hasLower) poolSize += 26;
        if (hasUpper) poolSize += 26;
        if (hasDigit) poolSize += 10;
        if (hasSpecial) poolSize += 33;

        poolSize = Math.Max(poolSize, 10);
        double entropyBits = password.Length * (Math.Log(poolSize) / Math.Log(2));
        result.EstimatedEntropyBits = Math.Round(entropyBits, 1);

        int score = 0;
        if (password.Length >= 8) score++;
        if (password.Length >= 12 && (hasLower && hasUpper && (hasDigit || hasSpecial))) score++;
        if (password.Length >= 16 && (hasLower && hasUpper && hasDigit && hasSpecial)) score++;
        if (entropyBits >= 60 && password.Length >= 14) score++;

        // Deduct points for obvious repetition
        if (HasObviousRepetition(password))
        {
            score = Math.Max(1, score - 1);
            result.Suggestions.Add("Avoid repeating characters sequentially (e.g. 'aaaa', '1111').");
        }

        // Suggestions
        if (password.Length < 12)
        {
            result.Suggestions.Add("Use at least 12 characters (16+ characters or a multi-word passphrase is recommended).");
        }
        if (!hasUpper)
        {
            result.Suggestions.Add("Include uppercase letters (A-Z).");
        }
        if (!hasDigit)
        {
            result.Suggestions.Add("Include numbers (0-9).");
        }
        if (!hasSpecial)
        {
            result.Suggestions.Add("Include symbols or special characters (!@#$%^&*).");
        }

        result.Score = Math.Clamp(score, 0, 4);
        result.Label = result.Score switch
        {
            0 => "Very Weak",
            1 => "Weak",
            2 => "Fair",
            3 => "Strong",
            4 => "Very Strong",
            _ => "Unknown"
        };

        return result;
    }

    private static bool HasObviousRepetition(string s)
    {
        for (int i = 0; i < s.Length - 3; i++)
        {
            if (s[i] == s[i + 1] && s[i] == s[i + 2] && s[i] == s[i + 3])
                return true;
        }
        return false;
    }
}
