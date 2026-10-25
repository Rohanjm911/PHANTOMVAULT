# 🚀 PHANTOMVAULT — Execution & How-To-Run Guide

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

</div>

---

## 📋 Table of Contents
1. [💻 System Requirements Overview](#-system-requirements-overview)
2. [🚀 How to Run PHANTOMVAULT](#-how-to-run-phantomvault)
   - [Option 1: Direct Standalone Launch (Root Folder — Instant)](#option-1-direct-standalone-launch-root-folder--instant)
   - [Option 2: Run via Developer CLI (`dotnet run`)](#option-2-run-via-developer-cli-dotnet-run)
   - [Option 3: Compile & Publish Production Standalone Binaries](#option-3-compile--publish-production-standalone-binaries)
   - [Option 4: Run the Complete Automated Test Suite](#option-4-run-the-complete-automated-test-suite)
3. [🧭 Quick-Start Operational Walkthrough](#-quick-start-operational-walkthrough)
4. [❓ Troubleshooting & FAQ](#-troubleshooting--faq)
5. [📦 Required Packages Reference](#-required-packages-reference)

---

## 💻 System Requirements Overview

| Specification | Minimum Requirement | Recommended Specification |
| :--- | :--- | :--- |
| **Operating System** | Windows 10 (version 1809+ / build 17763) | Windows 11 (build 22000+) with Mica Alt support |
| **Architecture** | 64-bit (`win-x64` or `win-arm64`) | 64-bit Intel / AMD x64 or ARM64 |
| **System Memory (RAM)** | 4 GB RAM | 8 GB+ RAM (enables instant multi-file chunking) |
| **Disk Storage** | 350 MB free space | 1 GB+ free space for encrypted vaults |
| **Biometric Sensor** | None (Password authentication) | Windows Hello compatible fingerprint or IR camera |
| **Developer Tools** | [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) *(Only if building from source)* | Visual Studio 2022 v17.10+ / Windows App SDK |

---

## 🚀 How to Run PHANTOMVAULT

You have 4 different ways to run, build, or test the application depending on whether you want an **instant launch**, **CLI execution**, or **full compilation from source**.

### Option 1: Direct Standalone Launch (Root Folder — Instant)

The application has been compiled into a standalone, unpackaged release that runs immediately with **zero setup or compilation**:

1. **Option 1A (Native Executable Launcher)**:
   - Double-click [`PhantomVault.exe`](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/PhantomVault.exe) in the project root folder.
2. **Option 1B (Windows Direct Shortcut)**:
   - Double-click [`PHANTOMVAULT.lnk`](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/PHANTOMVAULT.lnk) in the project root folder.
3. **Option 1C (Batch Launcher)**:
   - Double-click [`Launch-PHANTOMVAULT.bat`](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/Launch-PHANTOMVAULT.bat) in the project root folder.
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
dotnet publish src/PhantomVault.App/PhantomVault.App.csproj -c Release -p:Platform=x64 -p:PublishProfile=win-x64 -o "./publish"
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

## 📦 Required Packages Reference

For the comprehensive breakdown of all required NuGet packages with their real-life logos and security justifications, see:
👉 **[REQUIRED-PACKAGES.md](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/REQUIRED-PACKAGES.md)**

---

<div align="center">

**PHANTOMVAULT** • *Trust Nothing. Reveal Nothing.*

Designed for Windows 10 & 11 • Local-First Cryptographic Privacy

</div>
