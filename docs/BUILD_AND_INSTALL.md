# PHANTOMVAULT Build and Installation Guide

## 1. System Requirements

- **Operating System**: Windows 11 (all editions) or Windows 10 version 1809 (Build 17763) or later.
- **Architecture**: x64, ARM64, or x86.
- **Runtime**: .NET 8.0 Runtime or higher with Windows Desktop Pack.
- **Windows App SDK**: 1.6+ runtime dependencies.

---

## 2. Developer Toolchain Setup

To develop, compile, or test PHANTOMVAULT:

1. **Install .NET 8 SDK**:
   ```powershell
   winget install Microsoft.DotNet.SDK.8
   ```
2. **Install Visual Studio 2022** (v17.8+) or Visual Studio Build Tools with:
   - *.NET Desktop Development* workload
   - *Windows App SDK C# Templates*
3. **Verify Tooling in Terminal**:
   ```powershell
   dotnet --info
   ```

---

## 3. Building the Solution

### Clean Build
```powershell
dotnet clean PHANTOMVAULT.sln
dotnet build PHANTOMVAULT.sln -c Release
```

### Build Output Locations
- Executable binary: `src/PhantomVault.App/bin/Release/net8.0-windows10.0.26100.0/win-x64/PhantomVault.App.exe`
- Class libraries: `src/<Project>/bin/Release/net8.0/<Project>.dll`

---

## 4. Packaging as MSIX

To produce a signed MSIX package for enterprise distribution or local sideloading:

```powershell
dotnet publish src/PhantomVault.App/PhantomVault.App.csproj `
    -c Release `
    -r win-x64 `
    /p:GenerateAppxPackageOnBuild=true `
    /p:AppxPackageDir="D:\projects and certificates\projects\apps\PHANTOMVAULT\publish\MSIX" `
    /p:AppxPackageSigningEnabled=false
```

To run unpacked directly during development:
```powershell
dotnet run --project src/PhantomVault.App/PhantomVault.App.csproj
```
