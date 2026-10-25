# 📦 PHANTOMVAULT — Required Packages & How-To-Run Guide

<div align="center">

```
██████╗ ██╗  ██╗ █████╗ ███╗   ██╗████████╗ ██████╗ ███╗   ███╗██╗   ██╗ █████╗ ██╗   ██╗██╗  ████████╗
██╔══██╗██║  ██║██╔══██╗████╗  ██║╚══██╔══╝██╔═══██╗████╗ ████║██║   ██║██╔══██╗██║   ██║██║  ╚══██╔══╝
██████╔╝███████║███████║██╔██╗ ██║   ██║   ██║   ██║██╔████╔██║██║   ██║███████║██║   ██║██║     ██║   
██╔═══╝ ██╔══██║██╔══██║██║╚██╗██║   ██║   ██║   ██║██║╚██╔╝██║╚██╗ ██╔╝██╔══██║██║   ██║██║     ██║   
██║     ██║  ██║██║  ██║██║ ╚████║   ██║   ╚██████╔╝██║ ╚═╝ ██║ ╚████╔╝ ██║  ██║╚██████╔╝███████╗██║   
╚═╝     ╚═╝  ╚═╝╚═╝  ╚═╝╚═╝  ╚═══╝   ╚═╝    ╚═════╝ ╚═╝     ╚═╝  ╚═══╝  ╚═╝  ╚═╝ ╚═════╝ ╚══════╝╚═╝   
```

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
3. [🚀 How to Run PHANTOMVAULT](#-how-to-run-phantomvault)
   - [Option 1: Direct Standalone Launch (Root Folder — Instant)](#option-1-direct-standalone-launch-root-folder--instant)
   - [Option 2: Run via Developer CLI (`dotnet run`)](#option-2-run-via-developer-cli-dotnet-run)
   - [Option 3: Compile & Publish Production Standalone Binaries](#option-3-compile--publish-production-standalone-binaries)
   - [Option 4: Run the Complete Automated Test Suite](#option-4-run-the-complete-automated-test-suite)
4. [🧭 Quick-Start Operational Walkthrough](#-quick-start-operational-walkthrough)
5. [❓ Troubleshooting & FAQ](#-troubleshooting--faq)

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

## 🚀 How to Run PHANTOMVAULT

You have 4 different ways to run, build, or test the application depending on whether you want an **instant launch**, **CLI execution**, or **full compilation from source**.

### Option 1: Direct Standalone Launch (Root Folder — Instant)

The application has been compiled into a standalone, unpackaged release that runs immediately with **zero setup or compilation**:

1. **Option 1A (Native Executable)**:
   - Double-click [`PhantomVault.exe`](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/PhantomVault.exe) in the project root folder.
2. **Option 1B (Batch Launcher)**:
   - Double-click [`Launch-PHANTOMVAULT.bat`](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/Launch-PHANTOMVAULT.bat) in the project root folder.
3. **Option 1C (Desktop Shortcut)**:
   - Double-click [`PhantomVault.lnk`](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/PhantomVault.lnk) if present.
4. **Option 1D (Published Binaries Directly)**:
   - Run [`publish/PhantomVault.App.exe`](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/publish/PhantomVault.App.exe).

---

### Option 2: Run via Developer CLI (`dotnet run`)

If you want to run the application from source in developer mode using the .NET command line:

```powershell
# 1. Open PowerShell in the PHANTOMVAULT root folder
cd "d:\projects and certificates\projects\apps\PHANTOMVAULT"

# 2. Run the application in Release mode
dotnet run --project src/PhantomVault.App/PhantomVault.App.csproj -c Release
```

> [!TIP]
> If running in Debug mode for breakpoints:
> ```powershell
> dotnet run --project src/PhantomVault.App/PhantomVault.App.csproj -c Debug
> ```

---

### Option 3: Compile & Publish Production Standalone Binaries

To build a fresh, optimized standalone distribution package:

```powershell
# Clean and restore dependencies
dotnet clean PHANTOMVAULT.sln
dotnet restore PHANTOMVAULT.sln

# Build entire solution with 0 warnings
dotnet build PHANTOMVAULT.sln -c Release

# Publish self-contained 64-bit binaries to the publish directory
dotnet publish src/PhantomVault.App/PhantomVault.App.csproj -c Release -r win-x64 -o "./publish"
```

The resulting binaries in `./publish/` contain all WinUI 3 dependencies, assets, and runtime libraries and can be distributed or moved anywhere on Windows 10/11 machines.

---

### Option 4: Run the Complete Automated Test Suite

PHANTOMVAULT includes **49 unit and integration tests** verifying cryptographic framing, tamper detection, KDF validation, stego embedding/extraction, and database atomicity:

```powershell
# Run all tests across all test projects
dotnet test PHANTOMVAULT.sln -c Release

# Expected output:
# Passed!  - Failed: 0, Passed: 15 (PhantomVault.Core.Tests.dll)
# Passed!  - Failed: 0, Passed: 21 (PhantomVault.Security.Tests.dll)
# Passed!  - Failed: 0, Passed:  5 (PhantomVault.Steganography.Tests.dll)
# Passed!  - Failed: 0, Passed:  2 (PhantomVault.Integration.Tests.dll)
# Passed!  - Failed: 0, Passed:  6 (PhantomVault.Storage.Tests.dll)
# Total: 49 Passed, 0 Failed (100% Success)
```

---

## 🧭 Quick-Start Operational Walkthrough

Once the application launches, follow these steps to explore all core capabilities:

1. **Initialize Your First Vault**:
   - Go to **My Vaults** -> click **`+ New Vault`**.
   - Type a vault name and choose a strong Master Password. Watch the live entropy HUD score.
   - Click **`Create Vault`**.
2. **Import Files via Drag & Drop**:
   - Unlock the vault using your password or **Windows Hello Biometrics**.
   - Navigate to **Vault Workspace**.
   - Drag and drop files from Windows Explorer onto the illuminated drop zone. Files are immediately encrypted into chunked ciphertext.
3. **Zero-Disk In-Memory Preview**:
   - Double-click or click **Preview** on any encrypted image, text document, or code file.
   - The file decrypts directly into RAM (zero temporary files written to disk) with a raw 16-byte hex forensics inspector.
4. **Configure Plausible Deniability (Coercion Defense)**:
   - On the Vault Workspace toolbar, click **`Plausible Deniability...`**.
   - Set a secondary secret Duress Password.
   - When unlocked with the duress password, an isolated decoy enclave opens with realistic decoy files (`Tax_Filing_Summary_2024.txt`, etc.), while the real vault remains completely hidden.
5. **Covert Steganography Studio**:
   - Go to **Steganography** -> Select a carrier (PNG image or WAV audio).
   - For WAV files, click **`▶ Play Carrier Audio`** to audition the pristine audio.
   - Select a payload file and set an encryption password -> Click **`Embed & Save Stego File`**.
   - Click **`▶ Audition Stego Audio`** to verify 0.0000% audible distortion.
6. **DoD 5220.22-M File Shredder**:
   - In Vault Workspace, click **`Shred External File`**.
   - Select any unencrypted sensitive file on your PC to sanitize it with 3-pass cryptographic overwriting, timestamp scrambling, and MFT GUID renaming.
7. **Emergency Lockdown**:
   - Press **`Win + L`** or click the red **`EMERGENCY LOCK`** button at any time to instantly purge keys from RAM and seal all vaults.

---

## ❓ Troubleshooting & FAQ

### Q1: Windows Defender SmartScreen shows "Windows protected your PC"?
> **Solution**: Because PHANTOMVAULT is an unpackaged local build without an enterprise commercial EV Code Signing Certificate, Windows SmartScreen may show an initial warning. Click **"More info"** -> click **"Run anyway"**. The application is 100% open source, local-first, and contains zero remote telemetry.

### Q2: What happens if I forget my Master Password?
> **Answer**: Your data is **permanently and mathematically unrecoverable**. PHANTOMVAULT maintains zero central servers, zero cloud key escrows, and zero recovery backdoors. Keep your master password safe.

### Q3: Does PHANTOMVAULT write decrypted temporary files to the disk?
> **Answer**: **No.** All previews and audio playbacks operate strictly in RAM using `MemoryStream` and byte arrays. Memory is immediately wiped using `CryptographicOperations.ZeroMemory()` upon closing the preview.

### Q4: How do I audition or disable the Tactical Cyber Sound Effects?
> **Answer**: Go to **Settings** -> scroll to **"TACTICAL CYBER SOUND EFFECTS & ACOUSTIC FEEDBACK"**. You can toggle sound effects on/off or click the audition buttons (`Unlock Chime`, `Lock Arming`, `DoD Shred`, `Stego Chirp`, `Lockdown Alarm`) to test them.

---

<div align="center">

**PHANTOMVAULT** • *Trust Nothing. Reveal Nothing.*

Designed for Windows 10 & 11 • Local-First Cryptographic Privacy

</div>
