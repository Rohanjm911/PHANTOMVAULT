# 📦 PHANTOMVAULT — Required Packages & Dependencies Specification

<div align="center">

<p align="center">
  <img src="assets/logo.png" alt="PHANTOMVAULT Official Logo" width="200" style="border-radius: 16px; box-shadow: 0 8px 24px rgba(0,0,0,0.5);" />
</p>

### *Trust Nothing. Reveal Nothing.*

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-0B0D12?style=for-the-badge&logo=windows11&logoColor=39D0C8)](https://microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-8.0%20LTS-0B0D12?style=for-the-badge&logo=dotnet&logoColor=7C83FF)](https://dotnet.microsoft.com/)
[![WinUI 3](https://img.shields.io/badge/UI-WinUI%203%20Windows%20App%20SDK-0B0D12?style=for-the-badge&logo=windows&logoColor=39D0C8)](https://learn.microsoft.com/windows/apps/winui/winui3/)
[![AES-256-GCM](https://img.shields.io/badge/Cipher-AES--256--GCM-0B0D12?style=for-the-badge&logo=security&logoColor=36C98F)](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/README.md)
[![Argon2id](https://img.shields.io/badge/KDF-Argon2id%20(64%20MiB)-0B0D12?style=for-the-badge&logo=1password&logoColor=7C83FF)](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/README.md)
[![License](https://img.shields.io/badge/License-MIT-0B0D12?style=for-the-badge&logo=github&logoColor=F2B84B)](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/README.md)

<br/>

<p align="center">
  <img src="https://skillicons.dev/icons?i=cs,dotnet,windows,sqlite,git,visualstudio" alt="PHANTOMVAULT Core Stack" />
</p>

</div>

---

## 📋 Table of Contents
1. [💻 System Requirements](#-system-requirements)
2. [📦 Required Packages & Dependencies (With Real-Life Logos)](#-required-packages--dependencies-with-real-life-logos)
   - [Desktop UI & Presentation Shell](#1-desktop-ui--presentation-shell)
   - [Cryptography, Security & Hardware Biometrics](#2-cryptography-security--hardware-biometrics)
   - [Storage, In-Memory Steganography & Audio Subsystem](#3-storage-in-memory-steganography--audio-subsystem)
   - [Automated Testing & Verification Suite](#4-automated-testing--verification-suite)
3. [📥 Dependency Restoration & Verification](#-dependency-restoration--verification)
4. [🚀 How to Run Guide Reference](#-how-to-run-guide-reference)

---

## 💻 System Requirements

| Specification | Minimum Requirement | Recommended Specification |
| :--- | :--- | :--- |
| **Operating System** | Windows 10 (version 1809+ / build 17763) | Windows 11 (build 22000+) with Mica Alt support |
| **Architecture** | 64-bit (`win-x64` or `win-arm64`) | 64-bit Intel / AMD x64 or ARM64 |
| **Processor** | Dual-Core 2.0 GHz CPU | Quad-Core 2.8+ GHz (accelerates Argon2id lanes) |
| **System Memory (RAM)** | 4 GB RAM | 8 GB+ RAM (enables instant multi-file chunking) |
| **Disk Storage** | 350 MB free space | 1 GB+ free space for encrypted vaults |
| **Biometric Sensor** | None (Password authentication) | Windows Hello compatible fingerprint or IR camera |
| **Audio Hardware** | Standard Windows audio driver | 16-bit 44.1 kHz PCM playback device |
| **Developer Tools** | [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) *(Only if building from source)* | Visual Studio 2022 v17.10+ / Windows App SDK |

---

## 📦 Required Packages & Dependencies (With Real-Life Logos)

All external dependencies in PHANTOMVAULT have been strictly audited for **zero remote telemetry, zero analytics tracking, and zero network socket leakage**.

### 1. Desktop UI & Presentation Shell

| Real-Life Logo | Package / Dependency | Version | Target Project | Purpose & Security Rationale |
| :---: | :--- | :---: | :--- | :--- |
| <img src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/windows11/windows11-original.svg" width="48" height="48" alt="Windows App SDK" /> | **[Microsoft.WindowsAppSDK](https://www.nuget.org/packages/Microsoft.WindowsAppSDK/)** | `1.6+` | `PhantomVault.App` | Core native Windows desktop application platform providing modern WinUI 3 controls, dynamic `MicaKind.BaseAlt` deep obsidian backdrop, and fluid system compositor. |
| <img src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/windows11/windows11-original.svg" width="48" height="48" alt="Windows SDK Build Tools" /> | **[Microsoft.Windows.SDK.BuildTools](https://www.nuget.org/packages/Microsoft.Windows.SDK.BuildTools/)** | `10.0.*` | `PhantomVault.App` | Native Windows header compilation, XAML compiler Pass 1/2 markup generation, and MSIX-free unpackaged Win32 bootstrapper. |
| <img src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/dotnetcore/dotnetcore-original.svg" width="48" height="48" alt="CommunityToolkit.Mvvm" /> | **[CommunityToolkit.Mvvm](https://www.nuget.org/packages/CommunityToolkit.Mvvm/)** | `8.3.2` | `PhantomVault.App` | High-performance, source-generated MVVM pattern implementation (`ObservableObject`, `ObservableProperty`, `RelayCommand`) with zero reflection overhead. |
| <img src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/dotnetcore/dotnetcore-original.svg" width="48" height="48" alt="Dependency Injection" /> | **[Microsoft.Extensions.DependencyInjection](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection/)** | `8.0.0` | `PhantomVault.App` | Enterprise Inversion-of-Control (IoC) container managing decoupled cryptographic services, repository lifetimes, and view models. |

---

### 2. Cryptography, Security & Hardware Biometrics

| Real-Life Logo | Package / Dependency | Version | Target Project | Purpose & Security Rationale |
| :---: | :--- | :---: | :--- | :--- |
| <img src="https://raw.githubusercontent.com/FortAwesome/Font-Awesome/6.x/svgs/solid/lock.svg" width="48" height="48" alt="Argon2id" /> | **[Konscious.Security.Cryptography.Argon2](https://www.nuget.org/packages/Konscious.Security.Cryptography.Argon2/)** | `1.3.1` | `PhantomVault.Security` | Winner of the Password Hashing Competition (RFC 9106). Configured with **64 MiB memory hardness, 4 iterations, 4 parallelism lanes** to render GPU/ASIC attacks computationally prohibitive. |
| <img src="https://raw.githubusercontent.com/FortAwesome/Font-Awesome/6.x/svgs/solid/shield-halved.svg" width="48" height="48" alt="AES-256-GCM" /> | **[System.Security.Cryptography.AesGcm](https://learn.microsoft.com/dotnet/api/system.security.cryptography.aesgcm)** | `Built-in .NET 8` | `PhantomVault.Security` | Authenticated symmetric cipher encrypting files in **64 KiB chunks** with 128-bit authentication tags, chunk-index binding, and unique CSPRNG nonces per chunk. |
| <img src="https://raw.githubusercontent.com/FortAwesome/Font-Awesome/6.x/svgs/solid/fingerprint.svg" width="48" height="48" alt="Windows Hello" /> | **[Windows.Security.Credentials.UI (UserConsentVerifier)](https://learn.microsoft.com/uwp/api/windows.security.credentials.ui.userconsentverifier)** | `WinRT Native` | `PhantomVault.Infrastructure` | Hardware-backed biometric identity authentication (Fingerprint, IR Facial recognition, PIN) backed by TPM to securely unseal vaults. |
| <img src="https://raw.githubusercontent.com/FortAwesome/Font-Awesome/6.x/svgs/solid/memory.svg" width="48" height="48" alt="Memory Zeroization" /> | **[CryptographicOperations.ZeroMemory](https://learn.microsoft.com/dotnet/api/system.security.cryptography.cryptographicoperations.zeromemory)** | `Built-in .NET 8` | `PhantomVault.Security` | Explicit memory zeroization wiping plaintext keys, derived key material, and unencrypted RAM buffers immediately after use. |
| <img src="https://raw.githubusercontent.com/FortAwesome/Font-Awesome/6.x/svgs/solid/biohazard.svg" width="48" height="48" alt="DoD Shredder" /> | **DoD 5220.22-M Sanitizer** | `Native Custom` | `PhantomVault.Infrastructure` | Military-grade file shredding: 3-pass overwrite (0x00, 0xFF, CSPRNG noise), file truncation, timestamp scrambling, and MFT GUID renaming. |

---

### 3. Storage, In-Memory Steganography & Audio Subsystem

| Real-Life Logo | Package / Dependency | Version | Target Project | Purpose & Security Rationale |
| :---: | :--- | :---: | :--- | :--- |
| <img src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/sqlite/sqlite-original.svg" width="48" height="48" alt="SQLite" /> | **[Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite/)** | `8.0.10` | `PhantomVault.Storage` | Local zero-configuration database engine providing atomic ACID transactions, isolated per-vault catalogs, and encrypted indexing. |
| <img src="https://raw.githubusercontent.com/FortAwesome/Font-Awesome/6.x/svgs/solid/image.svg" width="48" height="48" alt="System.Drawing" /> | **[System.Drawing.Common](https://www.nuget.org/packages/System.Drawing.Common/)** | `8.0.10` | `PhantomVault.Steganography` | High-fidelity pixel manipulation for PNG scanlines, extracting and embedding encrypted bits into least significant bits (LSB) with zero perceptual degradation. |
| <img src="https://raw.githubusercontent.com/FortAwesome/Font-Awesome/6.x/svgs/solid/volume-high.svg" width="48" height="48" alt="Windows Multimedia Audio" /> | **Win32 Multimedia API (`winmm.dll`)** | `Native Windows` | `PhantomVault.App` | Ultra-low latency, zero-disk-trace audio engine. Synthesizes 16-bit PCM 44.1 kHz WAV streams directly in RAM for tactical cyber acoustic feedback. |
| <img src="https://raw.githubusercontent.com/FortAwesome/Font-Awesome/6.x/svgs/solid/mask.svg" width="48" height="48" alt="Plausible Deniability" /> | **Decoy Vault Engine** | `Native Custom` | `PhantomVault.Infrastructure` | Plausible deniability subsystem that seamlessly unseals realistic decoy files under duress, concealing authentic vaults with zero key exposure. |

---

### 4. Automated Testing & Verification Suite

| Real-Life Logo | Package / Dependency | Version | Target Project | Purpose & Security Rationale |
| :---: | :--- | :---: | :--- | :--- |
| <img src="https://raw.githubusercontent.com/xunit/media/master/dotnet-foundation-xunit-logo.svg" width="48" height="48" alt="xUnit" /> | **[xunit](https://www.nuget.org/packages/xunit/)** | `2.9.3` | `PhantomVault.*.Tests` | Industry-standard unit test framework verifying cryptographic correctness, chunk ordering, tamper rejection, and stego integrity. |
| <img src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/visualstudio/visualstudio-original.svg" width="48" height="48" alt="xUnit Runner" /> | **[xunit.runner.visualstudio](https://www.nuget.org/packages/xunit.runner.visualstudio/)** | `3.1.5` | `PhantomVault.*.Tests` | Visual Studio and CLI test discovery and runner integration for `dotnet test`. |
| <img src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/dotnetcore/dotnetcore-original.svg" width="48" height="48" alt="Test SDK" /> | **[Microsoft.NET.Test.Sdk](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/)** | `17.14.1` | `PhantomVault.*.Tests` | Core MSBuild targets and entry points required for test execution. |
| <img src="https://raw.githubusercontent.com/FortAwesome/Font-Awesome/6.x/svgs/solid/chart-pie.svg" width="48" height="48" alt="Coverlet" /> | **[coverlet.collector](https://www.nuget.org/packages/coverlet.collector/)** | `6.0.4` | `PhantomVault.*.Tests` | Cross-platform code coverage analysis ensuring full test coverage across all security interfaces. |

---

## 📥 Dependency Restoration & Verification

To restore and verify all packages across the entire solution:

```powershell
# Restore all NuGet packages for all projects
dotnet restore PHANTOMVAULT.sln

# Build solution in Release mode to ensure all package assemblies resolve
dotnet build PHANTOMVAULT.sln -c Release --no-restore
```

---

## 🚀 How to Run Guide Reference

For execution instructions, standalone launch options, CLI developer mode, and operational guides, see:
👉 **[HOW-TO-RUN.md](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/HOW-TO-RUN.md)**

---

<div align="center">

**PHANTOMVAULT** • *Trust Nothing. Reveal Nothing.*

Designed for Windows 10 & 11 • Local-First Cryptographic Privacy

</div>
