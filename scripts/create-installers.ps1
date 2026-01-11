#############################################################################
# PixlPunkt - Create All Installers Script (Windows)
#############################################################################
# Creates distributable installers for all platforms:
# - Windows: Velopack Setup.exe + Portable ZIP
# - Linux: DEB, RPM, and tarball (requires WSL with fpm)
# - macOS: DMG and ZIP (creates bundle, DMG creation requires macOS)
#
# Usage:
#   .\create-installers.ps1                    # All platforms
#   .\create-installers.ps1 -Platform win      # Windows only
#   .\create-installers.ps1 -Platform linux    # Linux only (needs WSL)
#   .\create-installers.ps1 -Platform mac      # macOS only
#   .\create-installers.ps1 -Version 1.2.3     # Specify version
#############################################################################

param(
    [ValidateSet("all", "win", "linux", "mac")]
    [string]$Platform = "all",
    
    [string]$Version = "1.0.0",
    
    [switch]$SkipBuild,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$ScriptDir = $PSScriptRoot
$RepoRoot = Split-Path $ScriptDir -Parent
$ProjectDir = "$RepoRoot\PixlPunkt"
$PublishBase = "$ProjectDir\bin\publish"
$InstallerBase = "$ScriptDir\installers"
$IconPath = "$ProjectDir\Assets\Icons"

Write-Host ""
Write-Host "╔═══════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║           PixlPunkt Installer Creation Script                     ║" -ForegroundColor Cyan
Write-Host "║                     Version: $Version                               ║" -ForegroundColor Cyan
Write-Host "╚═══════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Clean if requested
if ($Clean) {
    Write-Host "🧹 Cleaning installer directories..." -ForegroundColor Yellow
    if (Test-Path $InstallerBase) { Remove-Item -Path $InstallerBase -Recurse -Force }
    if (Test-Path $PublishBase) { Remove-Item -Path $PublishBase -Recurse -Force }
    Write-Host "✓ Clean complete" -ForegroundColor Green
    Write-Host ""
}

# Create directories
New-Item -ItemType Directory -Path "$InstallerBase\windows" -Force | Out-Null
New-Item -ItemType Directory -Path "$InstallerBase\linux" -Force | Out-Null
New-Item -ItemType Directory -Path "$InstallerBase\macos" -Force | Out-Null

#############################################################################
# WINDOWS INSTALLER (Velopack)
#############################################################################
function Create-WindowsInstaller {
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  WINDOWS INSTALLER (Velopack)" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    
    $winPublish = "$PublishBase\win-x64"
    $winInstaller = "$InstallerBase\windows"
    
    # Build if needed
    if (-not $SkipBuild) {
        Write-Host "📦 Building Windows x64 (Skia Desktop)..." -ForegroundColor Yellow
        
        dotnet publish "$ProjectDir\PixlPunkt.csproj" `
            --configuration Release `
            --framework net10.0-desktop `
            --runtime win-x64 `
            --self-contained true `
            --output $winPublish `
            -p:SkiaOnly=true `
            -p:PublishReadyToRun=true `
            -p:Version=$Version
        
        if ($LASTEXITCODE -ne 0) { throw "Windows build failed" }
        Write-Host "✓ Build complete" -ForegroundColor Green
    }
    
    # Check for Velopack
    $vpkExists = Get-Command vpk -ErrorAction SilentlyContinue
    if (-not $vpkExists) {
        Write-Host "📥 Installing Velopack CLI..." -ForegroundColor Yellow
        dotnet tool install -g vpk
    }
    
    # Create Velopack installer
    Write-Host "📦 Creating Velopack installer..." -ForegroundColor Yellow
    
    $vpkArgs = @(
        "pack",
        "--packId", "PixlPunkt",
        "--packVersion", $Version,
        "--packDir", $winPublish,
        "--mainExe", "PixlPunkt.exe",
        "--outputDir", $winInstaller,
        "--packTitle", "PixlPunkt",
        "--packAuthors", "PixlPunkt"
    )
    
    # Add icon if available
    if (Test-Path "$IconPath\PixlPunkt.ico") {
        $vpkArgs += "--icon"
        $vpkArgs += "$IconPath\PixlPunkt.ico"
    }
    
    & vpk @vpkArgs
    
    if ($LASTEXITCODE -ne 0) { throw "Velopack packaging failed" }
    
    # Rename setup for clarity
    $setupExe = Get-ChildItem -Path $winInstaller -Filter "*Setup.exe" | Select-Object -First 1
    if ($setupExe) {
        $newName = "PixlPunkt-$Version-Windows-Setup.exe"
        Copy-Item $setupExe.FullName -Destination "$winInstaller\$newName"
        Write-Host "✓ Created: $newName" -ForegroundColor Green
    }
    
    # Create portable ZIP
    Write-Host "📦 Creating portable ZIP..." -ForegroundColor Yellow
    $zipName = "PixlPunkt-$Version-Windows-x64-Portable.zip"
    Compress-Archive -Path "$winPublish\*" -DestinationPath "$winInstaller\$zipName" -Force
    Write-Host "✓ Created: $zipName" -ForegroundColor Green
    
    Write-Host ""
    Write-Host "Windows installers created in: $winInstaller" -ForegroundColor Gray
    Get-ChildItem $winInstaller | ForEach-Object { Write-Host "  - $($_.Name)" -ForegroundColor Gray }
}

#############################################################################
# LINUX INSTALLER (DEB/RPM via WSL)
#############################################################################
function Create-LinuxInstaller {
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  LINUX INSTALLER (DEB/RPM/TAR.GZ)" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    
    $linuxPublish = "$PublishBase\linux-x64"
    $linuxInstaller = "$InstallerBase\linux"
    
    # Build if needed
    if (-not $SkipBuild) {
        Write-Host "📦 Building Linux x64..." -ForegroundColor Yellow
        
        dotnet publish "$ProjectDir\PixlPunkt.csproj" `
            --configuration Release `
            --framework net10.0-desktop `
            --runtime linux-x64 `
            --self-contained true `
            --output $linuxPublish `
            -p:SkiaOnly=true `
            -p:PublishReadyToRun=true `
            -p:Version=$Version
        
        if ($LASTEXITCODE -ne 0) { throw "Linux build failed" }
        Write-Host "✓ Build complete" -ForegroundColor Green
    }
    
    # Check for WSL
    $wslExists = Get-Command wsl -ErrorAction SilentlyContinue
    if (-not $wslExists) {
        Write-Host "⚠ WSL not available. Creating tarball only..." -ForegroundColor Yellow
        
        # Create tarball using PowerShell
        $tarName = "PixlPunkt-$Version-Linux-x64.tar.gz"
        
        # Use tar if available (Windows 10+)
        Push-Location "$PublishBase"
        tar -czvf "$linuxInstaller\$tarName" linux-x64
        Pop-Location
        
        Write-Host "✓ Created: $tarName" -ForegroundColor Green
        Write-Host "  Note: DEB/RPM packages require WSL with fpm installed" -ForegroundColor Yellow
        return
    }
    
    # Convert paths to WSL format
    $wslRepoRoot = wsl wslpath -u ($RepoRoot -replace '\\', '/')
    $wslPublish = wsl wslpath -u ($linuxPublish -replace '\\', '/')
    $wslInstaller = wsl wslpath -u ($linuxInstaller -replace '\\', '/')
    $wslIconPath = wsl wslpath -u ($IconPath -replace '\\', '/')
    
    Write-Host "📦 Creating Linux packages via WSL..." -ForegroundColor Yellow
    
    # Create the Linux packages in WSL
    $wslScript = @"
#!/bin/bash
set -e

VERSION="$Version"
PUBLISH_DIR="$wslPublish"
OUTPUT_DIR="$wslInstaller"
ICON_PATH="$wslIconPath"

# Check for fpm
if ! command -v fpm &> /dev/null; then
    echo "Installing fpm..."
    sudo apt-get update
    sudo apt-get install -y ruby ruby-dev rubygems build-essential rpm
    sudo gem install --no-document fpm
fi

# Create package structure
PKG_ROOT="/tmp/pixlpunkt-pkg"
rm -rf "`$PKG_ROOT"
mkdir -p "`$PKG_ROOT/opt/pixlpunkt"
mkdir -p "`$PKG_ROOT/usr/share/applications"
mkdir -p "`$PKG_ROOT/usr/share/icons/hicolor/256x256/apps"

# Copy files
cp -r "`$PUBLISH_DIR"/* "`$PKG_ROOT/opt/pixlpunkt/"
chmod +x "`$PKG_ROOT/opt/pixlpunkt/PixlPunkt"

# Create desktop entry
cat > "`$PKG_ROOT/usr/share/applications/com.pixlpunkt.app.desktop" << 'DESKTOP'
[Desktop Entry]
Version=1.0
Type=Application
Name=PixlPunkt
Comment=Pixel Art Editor
Exec=/opt/pixlpunkt/PixlPunkt %F
Icon=pixlpunkt
Terminal=false
Categories=Graphics;2DGraphics;RasterGraphics;
DESKTOP

# Copy icon
if [ -f "`$ICON_PATH/Icon.png" ]; then
    cp "`$ICON_PATH/Icon.png" "`$PKG_ROOT/usr/share/icons/hicolor/256x256/apps/pixlpunkt.png"
fi

# Create post-install script
cat > /tmp/after-install.sh << 'SCRIPT'
#!/bin/bash
update-mime-database /usr/share/mime 2>/dev/null || true
update-desktop-database /usr/share/applications 2>/dev/null || true
ln -sf /opt/pixlpunkt/PixlPunkt /usr/local/bin/pixlpunkt
SCRIPT

# Create post-remove script
cat > /tmp/after-remove.sh << 'SCRIPT'
#!/bin/bash
rm -f /usr/local/bin/pixlpunkt
update-mime-database /usr/share/mime 2>/dev/null || true
update-desktop-database /usr/share/applications 2>/dev/null || true
SCRIPT

# Create DEB
echo "Creating DEB package..."
fpm -s dir -t deb \
    --name pixlpunkt \
    --version "`$VERSION" \
    --architecture amd64 \
    --description "PixlPunkt - Modern Pixel Art Editor" \
    --license "MIT" \
    --depends "libx11-6" --depends "libxrandr2" --depends "libxinerama1" \
    --depends "libxcursor1" --depends "libxi6" --depends "libgl1" \
    --after-install /tmp/after-install.sh \
    --after-remove /tmp/after-remove.sh \
    --package "`$OUTPUT_DIR/pixlpunkt_`${VERSION}_amd64.deb" \
    -C "`$PKG_ROOT" opt usr

echo "✓ DEB created"

# Create RPM
echo "Creating RPM package..."
fpm -s dir -t rpm \
    --name pixlpunkt \
    --version "`$VERSION" \
    --architecture x86_64 \
    --description "PixlPunkt - Modern Pixel Art Editor" \
    --license "MIT" \
    --depends "libX11" --depends "libXrandr" --depends "libXinerama" \
    --depends "libXcursor" --depends "libXi" --depends "mesa-libGL" \
    --after-install /tmp/after-install.sh \
    --after-remove /tmp/after-remove.sh \
    --package "`$OUTPUT_DIR/pixlpunkt-`${VERSION}-1.x86_64.rpm" \
    -C "`$PKG_ROOT" opt usr

echo "✓ RPM created"

# Create tarball
echo "Creating tarball..."
cd "`$PUBLISH_DIR/.."
tar -czvf "`$OUTPUT_DIR/PixlPunkt-`$VERSION-Linux-x64.tar.gz" linux-x64
echo "✓ Tarball created"

# Cleanup
rm -rf "`$PKG_ROOT"

echo ""
echo "Linux packages created:"
ls -la "`$OUTPUT_DIR"
"@
    
    # Run the script in WSL
    $wslScript | wsl bash
    
    if ($LASTEXITCODE -ne 0) { throw "Linux packaging failed" }
    
    Write-Host ""
    Write-Host "Linux installers created in: $linuxInstaller" -ForegroundColor Gray
    Get-ChildItem $linuxInstaller | ForEach-Object { Write-Host "  - $($_.Name)" -ForegroundColor Gray }
}

#############################################################################
# MACOS INSTALLER (App Bundle + DMG prep)
#############################################################################
function Create-MacInstaller {
    param(
        [string]$Arch = "x64"
    )
    
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  MACOS INSTALLER ($Arch) - App Bundle + ZIP" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    
    $rid = if ($Arch -eq "arm64") { "osx-arm64" } else { "osx-x64" }
    $macPublish = "$PublishBase\$rid"
    $macInstaller = "$InstallerBase\macos"
    
    # Build if needed
    if (-not $SkipBuild) {
        Write-Host "📦 Building macOS $Arch..." -ForegroundColor Yellow
        
        dotnet publish "$ProjectDir\PixlPunkt.csproj" `
            --configuration Release `
            --framework net10.0-desktop `
            --runtime $rid `
            --self-contained true `
            --output $macPublish `
            -p:SkiaOnly=true `
            -p:PublishReadyToRun=true `
            -p:Version=$Version
        
        if ($LASTEXITCODE -ne 0) { throw "macOS build failed" }
        Write-Host "✓ Build complete" -ForegroundColor Green
    }
    
    # Create app bundle structure
    Write-Host "📦 Creating macOS App Bundle..." -ForegroundColor Yellow
    
    $appBundle = "$macInstaller\PixlPunkt-$Arch.app"
    $contentsDir = "$appBundle\Contents"
    $macOSDir = "$contentsDir\MacOS"
    $resourcesDir = "$contentsDir\Resources"
    
    # Clean and create directories
    if (Test-Path $appBundle) { Remove-Item -Path $appBundle -Recurse -Force }
    New-Item -ItemType Directory -Path $macOSDir -Force | Out-Null
    New-Item -ItemType Directory -Path $resourcesDir -Force | Out-Null
    
    # Copy executable and dependencies
    Copy-Item -Path "$macPublish\*" -Destination $macOSDir -Recurse
    
    # Copy icon
    if (Test-Path "$IconPath\Icon.png") {
        Copy-Item "$IconPath\Icon.png" "$resourcesDir\AppIcon.png"
    }
    
    # Create Info.plist
    $plist = @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key><string>PixlPunkt</string>
    <key>CFBundleIdentifier</key><string>com.pixlpunkt.app</string>
    <key>CFBundleName</key><string>PixlPunkt</string>
    <key>CFBundleDisplayName</key><string>PixlPunkt</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleShortVersionString</key><string>$Version</string>
    <key>CFBundleVersion</key><string>$Version</string>
    <key>LSMinimumSystemVersion</key><string>10.15</string>
    <key>NSHighResolutionCapable</key><true/>
    <key>CFBundleIconFile</key><string>AppIcon</string>
    <key>CFBundleDocumentTypes</key>
    <array>
        <dict>
            <key>CFBundleTypeName</key><string>PixlPunkt Document</string>
            <key>CFBundleTypeRole</key><string>Editor</string>
            <key>LSHandlerRank</key><string>Owner</string>
            <key>CFBundleTypeExtensions</key>
            <array><string>pxp</string></array>
        </dict>
    </array>
</dict>
</plist>
"@
    
    $plist | Out-File -FilePath "$contentsDir\Info.plist" -Encoding utf8
    
    Write-Host "✓ App bundle created: $appBundle" -ForegroundColor Green
    
    # Create ZIP of the app bundle
    $zipName = "PixlPunkt-$Version-macOS-$Arch.zip"
    Write-Host "📦 Creating ZIP..." -ForegroundColor Yellow
    Compress-Archive -Path $appBundle -DestinationPath "$macInstaller\$zipName" -Force
    Write-Host "✓ Created: $zipName" -ForegroundColor Green
    
    # Create DMG creation script for macOS
    $dmgScript = @"
#!/bin/bash
# Run this script on macOS to create the DMG
# Usage: ./create-dmg.sh

VERSION="$Version"
ARCH="$Arch"

# Install create-dmg if needed
if ! command -v create-dmg &> /dev/null; then
    echo "Installing create-dmg via Homebrew..."
    brew install create-dmg
fi

# Create DMG
create-dmg \
    --volname "PixlPunkt `$VERSION" \
    --window-pos 200 120 \
    --window-size 600 400 \
    --icon-size 100 \
    --icon "PixlPunkt-`$ARCH.app" 150 185 \
    --hide-extension "PixlPunkt-`$ARCH.app" \
    --app-drop-link 450 185 \
    "PixlPunkt-`$VERSION-macOS-`$ARCH.dmg" \
    "PixlPunkt-`$ARCH.app"

echo "DMG created: PixlPunkt-`$VERSION-macOS-`$ARCH.dmg"
"@
    
    $dmgScript | Out-File -FilePath "$macInstaller\create-dmg-$Arch.sh" -Encoding utf8 -NoNewline
    
    Write-Host ""
    Write-Host "macOS bundle created in: $macInstaller" -ForegroundColor Gray
    Write-Host "  - PixlPunkt-$Arch.app (App Bundle)" -ForegroundColor Gray
    Write-Host "  - $zipName (ZIP Archive)" -ForegroundColor Gray
    Write-Host "  - create-dmg-$Arch.sh (Run on macOS to create DMG)" -ForegroundColor Gray
}

#############################################################################
# MAIN EXECUTION
#############################################################################

$summary = @()

try {
    if ($Platform -eq "all" -or $Platform -eq "win") {
        Create-WindowsInstaller
        $summary += "Windows"
    }
    
    if ($Platform -eq "all" -or $Platform -eq "linux") {
        Create-LinuxInstaller
        $summary += "Linux"
    }
    
    if ($Platform -eq "all" -or $Platform -eq "mac") {
        Create-MacInstaller -Arch "x64"
        Create-MacInstaller -Arch "arm64"
        $summary += "macOS (x64 + ARM64)"
    }
    
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host "                    INSTALLER CREATION COMPLETE" -ForegroundColor Green
    Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host ""
    Write-Host "Created installers for: $($summary -join ', ')" -ForegroundColor White
    Write-Host ""
    Write-Host "Output directory: $InstallerBase" -ForegroundColor Gray
    Write-Host ""
    
    # Open explorer to the installers folder
    explorer $InstallerBase
}
catch {
    Write-Host ""
    Write-Host "✗ Error: $_" -ForegroundColor Red
    exit 1
}
