# Installation

How to download and install PixlPunkt on your system.

---

## System Requirements

### Windows
- **OS:** Windows 10 version 1809 (build 17763) or later
- **Architecture:** x64 or ARM64
- **.NET:** .NET 10 Runtime (included in installer)

### macOS
- **OS:** macOS 10.15 (Catalina) or later
- **Architecture:** Intel (x64) or Apple Silicon (ARM64)

### Linux
- **Desktop:** X11-based environment
- **Architecture:** x64
- **Dependencies:** Standard GTK/X11 libraries

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/arrow_download_16.png" width="16"> Download

Get the latest release from the [GitHub Releases](https://github.com/ChadRoesler/PixlPunkt/releases) page.

### Windows Downloads

| Package | Description |
|---------|-------------|
| `PixlPunkt-X.X.X-Windows-Setup.exe` | **Recommended** - Installer with auto-updates |
| `PixlPunkt-X.X.X-Desktop-Windows-x64-Portable.zip` | Portable version, no installation required |
| `PixlPunkt-X.X.X-Desktop-Windows-arm64-Portable.zip` | Portable for Windows on ARM |

### macOS Downloads

| Package | Description |
|---------|-------------|
| `PixlPunkt-X.X.X-macOS-arm64.dmg` | Apple Silicon (M1/M2/M3) |
| `PixlPunkt-X.X.X-macOS-x64.dmg` | Intel Macs |

### Linux Downloads

| Package | Description |
|---------|-------------|
| `pixlpunkt_X.X.X_amd64.deb` | Debian/Ubuntu package |
| `pixlpunkt-X.X.X-1.x86_64.rpm` | Fedora/RHEL package |
| `pixlpunkt-X.X.X-desktop-linux-x64.tar.gz` | Portable tarball |

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/wrench_16.png" width="16"> Windows Installation

### Using the Installer (Recommended)

1. Download `PixlPunkt-X.X.X-Windows-Setup.exe`
2. Run the installer
3. Follow the prompts
4. Launch from Start Menu or Desktop shortcut

**Features:**
- Automatic updates via Velopack
- File association for `.pxp`, `.pbx` files
- Start Menu integration

### Portable Version

1. Download the `.zip` file
2. Extract to any folder (e.g., `C:\Tools\PixlPunkt`)
3. Run `PixlPunkt.exe`

> **Tip:** The portable version stores settings in the same folder, making it great for USB drives.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/wrench_16.png" width="16"> macOS Installation

### Using DMG

1. Download the `.dmg` file for your Mac type
2. Open the DMG
3. Drag PixlPunkt to your **Applications** folder
4. First launch: Right-click → **Open** (to bypass Gatekeeper)

### Homebrew (Coming Soon)

```bash
brew install --cask pixlpunkt
```

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/wrench_16.png" width="16"> Linux Installation

### Debian/Ubuntu (.deb)

```bash
sudo dpkg -i pixlpunkt_X.X.X_amd64.deb
sudo apt-get install -f  # Install dependencies if needed
```

### Fedora/RHEL (.rpm)

```bash
sudo rpm -i pixlpunkt-X.X.X-1.x86_64.rpm
```

### Portable (tarball)

```bash
tar -xzf pixlpunkt-X.X.X-desktop-linux-x64.tar.gz
cd pixlpunkt
./PixlPunkt
```

### Make it Executable

```bash
chmod +x PixlPunkt
```

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/code_16.png" width="16"> Build from Source

For developers who want to build PixlPunkt themselves:

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Git

### Clone and Build

```bash
git clone https://github.com/ChadRoesler/PixlPunkt.git
cd PixlPunkt
dotnet build
```

### Run

```bash
dotnet run --project PixlPunkt/PixlPunkt.csproj
```

### Build for Specific Platform

```bash
# Windows (Skia Desktop)
dotnet publish PixlPunkt/PixlPunkt.csproj -c Release -f net10.0-desktop -r win-x64

# Linux
dotnet publish PixlPunkt/PixlPunkt.csproj -c Release -f net10.0-desktop -r linux-x64

# macOS (Apple Silicon)
dotnet publish PixlPunkt/PixlPunkt.csproj -c Release -f net10.0-desktop -r osx-arm64
```

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/arrow_sync_16.png" width="16"> Updating

### Windows Installer Version
- Updates are automatic via Velopack
- You'll see a notification when updates are available
- Restart to apply updates

### Portable/Other Versions
- Download the new version from GitHub Releases
- Replace the old files (keep your settings!)

---

## File Locations

### Windows
- **Settings:** `%LocalAppData%\PixlPunkt\settings.json`
- **Plugins:** `%LocalAppData%\PixlPunkt\Plugins\`
- **Custom Brushes:** `%LocalAppData%\PixlPunkt\Brushes\`
- **Palettes:** `%LocalAppData%\PixlPunkt\Palettes\`
- **Logs:** `%LocalAppData%\PixlPunkt\Logs\`

### macOS
- **Settings:** `~/Library/Application Support/PixlPunkt/`

### Linux
- **Settings:** `~/.local/share/PixlPunkt/`

---

## Troubleshooting Installation

### Windows: "Windows protected your PC"
- Click **More info** → **Run anyway**
- This appears because the app isn't code-signed (yet)

### macOS: "App is damaged" or "Can't be opened"
- Right-click the app → **Open**
- Or run: `xattr -cr /Applications/PixlPunkt.app`

### Linux: Missing Libraries
```bash
# Debian/Ubuntu
sudo apt-get install libx11-6 libgtk-3-0

# Fedora
sudo dnf install gtk3 libX11
```

---

## See Also

- [[Quick Start|Quick-Start]] - Get started quickly
- [[Interface]] - Learn the UI
- [[Troubleshooting]] - Common issues
