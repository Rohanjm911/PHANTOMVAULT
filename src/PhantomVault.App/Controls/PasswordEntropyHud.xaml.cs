using System;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PhantomVault.Core.Models;

namespace PhantomVault_App.Controls;

public sealed partial class PasswordEntropyHud : UserControl
{
    private static readonly SolidColorBrush BorderBrushColor = new(ColorHelper.FromArgb(255, 37, 44, 56));
    private static readonly SolidColorBrush ErrorBrushColor = new(ColorHelper.FromArgb(255, 240, 106, 117));
    private static readonly SolidColorBrush WarningBrushColor = new(ColorHelper.FromArgb(255, 242, 184, 75));
    private static readonly SolidColorBrush TealBrushColor = new(ColorHelper.FromArgb(255, 57, 208, 200));
    private static readonly SolidColorBrush EmeraldBrushColor = new(ColorHelper.FromArgb(255, 54, 201, 143));
    private static readonly SolidColorBrush MutedBrushColor = new(ColorHelper.FromArgb(255, 105, 115, 134));
    private static readonly SolidColorBrush ActiveBadgeBg = new(ColorHelper.FromArgb(40, 57, 208, 200));
    private static readonly SolidColorBrush InactiveBadgeBg = new(ColorHelper.FromArgb(255, 17, 21, 29));

    public PasswordEntropyHud()
    {
        InitializeComponent();
        ResetHud();
    }

    public void ResetHud()
    {
        Bar1.Background = BorderBrushColor;
        Bar2.Background = BorderBrushColor;
        Bar3.Background = BorderBrushColor;
        Bar4.Background = BorderBrushColor;

        StrengthLabelText.Text = "PASSWORD STRENGTH: NOT EVALUATED";
        StrengthLabelText.Foreground = MutedBrushColor;
        StrengthIcon.Foreground = MutedBrushColor;
        MetricsText.Text = "Entropy: 0 bits • Crack Time: instant";

        SetBadgeState(BadgeLength, BadgeLengthIcon, BadgeLengthText, false, "12+ Chars");
        SetBadgeState(BadgeCase, BadgeCaseIcon, BadgeCaseText, false, "A-Z & a-z");
        SetBadgeState(BadgeSymbols, BadgeSymbolsIcon, BadgeSymbolsText, false, "0-9 & Symbols");
        SetBadgeState(BadgeEntropy, BadgeEntropyIcon, BadgeEntropyText, false, "60+ Bits");
    }

    public void UpdatePassword(string password, PasswordStrengthResult? result)
    {
        if (string.IsNullOrEmpty(password))
        {
            ResetHud();
            return;
        }

        bool hasLength = password.Length >= 12;
        bool hasUpper = password.Any(char.IsUpper);
        bool hasLower = password.Any(char.IsLower);
        bool hasDigit = password.Any(char.IsDigit);
        bool hasSpecial = password.Any(ch => !char.IsLetterOrDigit(ch));
        bool hasMixedCase = hasUpper && hasLower;
        bool hasNumbersAndSymbols = hasDigit && hasSpecial;

        double entropy = result?.EstimatedEntropyBits ?? EstimateEntropy(password);
        int score = result?.Score ?? (entropy < 30 ? 1 : entropy < 50 ? 2 : entropy < 70 ? 3 : 4);
        bool hasGoodEntropy = entropy >= 60;

        // Update 4 Requirement Badges
        SetBadgeState(BadgeLength, BadgeLengthIcon, BadgeLengthText, hasLength, hasLength ? $"{password.Length} Chars" : "12+ Chars");
        SetBadgeState(BadgeCase, BadgeCaseIcon, BadgeCaseText, hasMixedCase, "A-Z & a-z");
        SetBadgeState(BadgeSymbols, BadgeSymbolsIcon, BadgeSymbolsText, hasNumbersAndSymbols, "0-9 & Symbols");
        SetBadgeState(BadgeEntropy, BadgeEntropyIcon, BadgeEntropyText, hasGoodEntropy, $"{entropy:F0}+ Bits");

        // Update Progress Bars & Strength Theme
        switch (score)
        {
            case 1:
                Bar1.Background = ErrorBrushColor;
                Bar2.Background = BorderBrushColor;
                Bar3.Background = BorderBrushColor;
                Bar4.Background = BorderBrushColor;
                StrengthLabelText.Text = "CRITICAL: WEAK — INSUFFICIENT ENTROPY";
                StrengthLabelText.Foreground = ErrorBrushColor;
                StrengthIcon.Foreground = ErrorBrushColor;
                break;

            case 2:
                Bar1.Background = WarningBrushColor;
                Bar2.Background = WarningBrushColor;
                Bar3.Background = BorderBrushColor;
                Bar4.Background = BorderBrushColor;
                StrengthLabelText.Text = "MODERATE: VULNERABLE TO TARGETED BRUTE-FORCE";
                StrengthLabelText.Foreground = WarningBrushColor;
                StrengthIcon.Foreground = WarningBrushColor;
                break;

            case 3:
                Bar1.Background = TealBrushColor;
                Bar2.Background = TealBrushColor;
                Bar3.Background = TealBrushColor;
                Bar4.Background = BorderBrushColor;
                StrengthLabelText.Text = "STRONG: RESISTANT TO GPU CLUSTER ATTACKS";
                StrengthLabelText.Foreground = TealBrushColor;
                StrengthIcon.Foreground = TealBrushColor;
                break;

            case 4:
            default:
                Bar1.Background = EmeraldBrushColor;
                Bar2.Background = EmeraldBrushColor;
                Bar3.Background = EmeraldBrushColor;
                Bar4.Background = EmeraldBrushColor;
                StrengthLabelText.Text = "MAXIMUM SECURITY: QUANTUM & ASIC RESISTANT";
                StrengthLabelText.Foreground = EmeraldBrushColor;
                StrengthIcon.Foreground = EmeraldBrushColor;
                break;
        }

        string crackTime = EstimateCrackTime(entropy);
        MetricsText.Text = $"Entropy: ~{entropy:F1} bits • Crack Time: {crackTime}";
    }

    private static void SetBadgeState(Border badge, TextBlock icon, TextBlock text, bool satisfied, string label)
    {
        if (satisfied)
        {
            badge.Background = ActiveBadgeBg;
            badge.BorderBrush = TealBrushColor;
            icon.Text = "✔";
            icon.Foreground = TealBrushColor;
            text.Text = label;
            text.Foreground = TealBrushColor;
        }
        else
        {
            badge.Background = InactiveBadgeBg;
            badge.BorderBrush = BorderBrushColor;
            icon.Text = "✖";
            icon.Foreground = MutedBrushColor;
            text.Text = label;
            text.Foreground = MutedBrushColor;
        }
    }

    private static double EstimateEntropy(string password)
    {
        if (string.IsNullOrEmpty(password)) return 0;
        int poolSize = 0;
        if (password.Any(char.IsLower)) poolSize += 26;
        if (password.Any(char.IsUpper)) poolSize += 26;
        if (password.Any(char.IsDigit)) poolSize += 10;
        if (password.Any(ch => !char.IsLetterOrDigit(ch))) poolSize += 33;
        if (poolSize == 0) poolSize = 1;
        return password.Length * Math.Log(poolSize, 2);
    }

    private static string EstimateCrackTime(double entropyBits)
    {
        if (entropyBits < 30) return "< 1 second";
        if (entropyBits < 45) return "~3 minutes";
        if (entropyBits < 55) return "~18 days";
        if (entropyBits < 65) return "~500 years";
        if (entropyBits < 80) return "~4.2 × 10⁶ years";
        return "> 10¹² years";
    }
}
