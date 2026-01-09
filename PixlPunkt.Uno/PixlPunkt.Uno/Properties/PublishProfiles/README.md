# PixlPunkt Local Publish Profiles

This directory contains publish profiles for building and testing PixlPunkt locally on different platforms.

## Quick Start - Creating Installers

### Windows (PowerShell)

```powershell
# Create all installers (Windows Velopack + Linux DEB/RPM via WSL + macOS bundles)
.\create-installers.ps1 -Platform all -Version 1.0.0

# Windows only (Velopack Setup.exe + Portable ZIP)
.\create-installers.ps1 -Platform win -Version 1.0.0

# Linux only (requires WSL with fpm installed)
.\create-installers.ps1 -Platform linux -Version 1.0.0

# macOS only (creates app bundle + ZIP, DMG requires actual Mac)
.\create-installers.ps1 -Platform mac -Version 1.0.0

# Skip rebuild (use existing publish output)
.\create-installers.ps1 -Platform win -SkipBuild

# Clean and rebuild
.\create-installers.ps1 -Platform all -Clean
```

### Linux (Bash)

```bash
# Make script executable
chmod +x create-installers.sh

# Create Linux installers (DEB, RPM, tarball)
./create-installers.sh linux --version 1.0.0

# Create macOS installers (on Mac: includes DMG)
./create-installers.sh mac --version 1.0.0

# Skip rebuild
./create-installers.sh linux --skip-build

# Clean first
./create-installers.sh linux --clean
```

### macOS (Bash)

```bash
chmod +x create-installers.sh

# Create macOS DMG + ZIP (auto-detects your Mac's architecture)
./create-installers.sh mac --version 1.0.0

# Create Linux packages (cross-compile)
./create-installers.sh linux --version 1.0.0
```

## Output Structure

After running the installer scripts, you'll find:

```
installers/
├── windows/
│   ├── PixlPunkt-1.0.0-Windows-Setup.exe    # Velopack installer (auto-updates!)
│   ├── PixlPunkt-1.0.0-Windows-x64-Portable.zip
│   ├── PixlPunkt-1.0.0-full.nupkg           # Velopack update package
│   └── RELEASES                              # Velopack release manifest
│
├── linux/
│   ├── pixlpunkt_1.0.0_amd64.deb            # Debian/Ubuntu package
│   ├── pixlpunkt-1.0.0-1.x86_64.rpm         # Fedora/RHEL package
│   └── PixlPunkt-1.0.0-Linux-x64.tar.gz     # Portable tarball
│
└── macos/
    ├── PixlPunkt-x64.app/                   # App bundle (Intel)
    ├── PixlPunkt-arm64.app/                 # App bundle (Apple Silicon)
    ├── PixlPunkt-1.0.0-macOS-x64.zip        # Zipped app bundle
    ├── PixlPunkt-1.0.0-macOS-arm64.zip
    ├── PixlPunkt-1.0.0-macOS-x64.dmg        # DMG (created on Mac only)
    └── PixlPunkt-1.0.0-macOS-arm64.dmg
```

## Testing the Installers

### Windows - Velopack Installer

```powershell
# Run the setup (installs to %LocalAppData%\PixlPunkt)
.\installers\windows\PixlPunkt-1.0.0-Windows-Setup.exe

# Or use portable version
Expand-Archive .\installers\windows\PixlPunkt-1.0.0-Windows-x64-Portable.zip -DestinationPath .\test
.\test\PixlPunkt.Uno.exe
```

### Linux - DEB Package (Ubuntu/Debian)

```bash
# Install
sudo dpkg -i installers/linux/pixlpunkt_1.0.0_amd64.deb
sudo apt-get install -f  # Install dependencies if needed

# Run
pixlpunkt
# or
/opt/pixlpunkt/PixlPunkt.Uno

# Uninstall
sudo apt remove pixlpunkt
```

### Linux - RPM Package (Fedora/RHEL)

```bash
# Install
sudo dnf install installers/linux/pixlpunkt-1.0.0-1.x86_64.rpm

# Run
pixlpunkt

# Uninstall
sudo dnf remove pixlpunkt
```

### Linux - Tarball

```bash
# Extract
tar -xzf installers/linux/PixlPunkt-1.0.0-Linux-x64.tar.gz

# Run
./linux-x64/PixlPunkt.Uno
```

### macOS - DMG

```bash
# Mount the DMG
hdiutil attach installers/macos/PixlPunkt-1.0.0-macOS-arm64.dmg

# Drag PixlPunkt.app to Applications in the window that opens

# Or copy from command line
cp -r /Volumes/PixlPunkt\ 1.0.0/PixlPunkt.app /Applications/

# Unmount
hdiutil detach /Volumes/PixlPunkt\ 1.0.0

# Run (first time, right-click > Open to bypass Gatekeeper)
open /Applications/PixlPunkt.app
```

### macOS - ZIP

```bash
# Extract
unzip installers/macos/PixlPunkt-1.0.0-macOS-arm64.zip

# Remove quarantine attribute
xattr -cr PixlPunkt-arm64.app

# Run
open PixlPunkt-arm64.app
```

## Prerequisites

### Windows
- .NET 10 SDK
- Velopack CLI: `dotnet tool install -g vpk`

### Linux (for creating DEB/RPM)
- .NET 10 SDK
- Ruby: `sudo apt install ruby ruby-dev rubygems build-essential rpm`
- FPM: `sudo gem install --no-document fpm`

### macOS (for creating DMG)
- .NET 10 SDK
- Homebrew: `/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"`
- create-dmg: `brew install create-dmg`

## Available Publish Profiles

| Profile | Platform | Runtime | Framework | Description |
|---------|----------|---------|-----------|-------------|
| `Skia-win-x64` | Windows | win-x64 | net10.0-desktop | Windows Desktop (Skia renderer) |
| `Skia-linux-x64` | Linux | linux-x64 | net10.0-desktop | Linux Desktop |
| `Skia-osx-x64` | macOS | osx-x64 | net10.0-desktop | macOS Intel |
| `Skia-osx-arm64` | macOS | osx-arm64 | net10.0-desktop | macOS Apple Silicon (M1/M2/M3) |
| `win-x64` | Windows | win-x64 | net10.0-windows10.0.26100 | Windows (WinAppSdk native) |
| `win-arm64` | Windows | win-arm64 | net10.0-windows10.0.26100 | Windows ARM64 (WinAppSdk) |

## Manual Publishing (dotnet CLI)

If you prefer manual control:

```bash
# Windows x64 (Skia Desktop)
dotnet publish PixlPunkt.Uno/PixlPunkt.Uno/PixlPunkt.Uno.csproj \
  -c Release -f net10.0-desktop -r win-x64 --self-contained \
  -o publish/win-x64 -p:SkiaOnly=true -p:Version=1.0.0

# Linux x64
dotnet publish PixlPunkt.Uno/PixlPunkt.Uno/PixlPunkt.Uno.csproj \
  -c Release -f net10.0-desktop -r linux-x64 --self-contained \
  -o publish/linux-x64 -p:SkiaOnly=true -p:Version=1.0.0

# macOS ARM64 (Apple Silicon)
dotnet publish PixlPunkt.Uno/PixlPunkt.Uno/PixlPunkt.Uno.csproj \
  -c Release -f net10.0-desktop -r osx-arm64 --self-contained \
  -o publish/osx-arm64 -p:SkiaOnly=true -p:Version=1.0.0
```

## Troubleshooting

### Windows: "vpk not found"
```powershell
dotnet tool install -g vpk
# Restart terminal or run:
$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
```

### Linux: "fpm not found"
```bash
sudo apt-get install -y ruby ruby-dev rubygems build-essential
sudo gem install --no-document fpm
```

### macOS: "App is damaged" / "Unidentified developer"
```bash
# Remove quarantine attribute
xattr -cr /path/to/PixlPunkt.app
# Or right-click > Open in Finder
```

### macOS: DMG creation fails
```bash
# Make sure create-dmg is installed
brew install create-dmg
# Check for errors
create-dmg --help
```

### WSL: fpm fails with permission error
```bash
# Make sure you're running with proper permissions
sudo gem install fpm
