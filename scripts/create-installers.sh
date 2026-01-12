#!/bin/bash
#############################################################################
# PixlPunkt - Create All Installers Script (Linux/macOS)
#############################################################################
# Creates distributable installers for all platforms:
# - Linux: DEB, RPM, and tarball
# - macOS: App Bundle, ZIP, and DMG
# - Windows: Tarball only (use create-installers.ps1 for Velopack)
#
# Usage:
#   ./create-installers.sh                     # All platforms for current OS
#   ./create-installers.sh linux               # Linux only
#   ./create-installers.sh mac                 # macOS only
#   ./create-installers.sh --version 1.2.3     # Specify version
#   ./create-installers.sh --skip-build        # Skip dotnet publish
#   ./create-installers.sh --clean             # Clean before building
#############################################################################

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
PROJECT_DIR="$REPO_ROOT/PixlPunkt"
PROJECT_FILE="$PROJECT_DIR/PixlPunkt.csproj"
PUBLISH_BASE="$PROJECT_DIR/bin/publish"
INSTALLER_BASE="$SCRIPT_DIR/installers"
ICON_PATH="$PROJECT_DIR/Assets/Icons"

VERSION="1.0.0"
PLATFORM="auto"
SKIP_BUILD=false
CLEAN=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        linux|mac|all)
            PLATFORM="$1"
            shift
            ;;
        --version)
            VERSION="$2"
            shift 2
            ;;
        --skip-build)
            SKIP_BUILD=true
            shift
            ;;
        --clean)
            CLEAN=true
            shift
            ;;
        *)
            shift
            ;;
    esac
done

# Auto-detect platform
if [ "$PLATFORM" = "auto" ]; then
    case "$(uname -s)" in
        Linux*)  PLATFORM="linux" ;;
        Darwin*) PLATFORM="mac" ;;
        *)       PLATFORM="linux" ;;
    esac
fi

echo ""
echo "╔═══════════════════════════════════════════════════════════════════╗"
echo "║           PixlPunkt Installer Creation Script                     ║"
echo "║                     Version: $VERSION                                ║"
echo "╚═══════════════════════════════════════════════════════════════════╝"
echo ""

# Clean if requested
if [ "$CLEAN" = true ]; then
    echo "🧹 Cleaning directories..."
    rm -rf "$INSTALLER_BASE"
    rm -rf "$PUBLISH_BASE"
    echo "✓ Clean complete"
    echo ""
fi

# Create directories
mkdir -p "$INSTALLER_BASE/linux"
mkdir -p "$INSTALLER_BASE/macos"
mkdir -p "$INSTALLER_BASE/windows"

#############################################################################
# LINUX INSTALLER (DEB/RPM/TAR.GZ)
#############################################################################
create_linux_installer() {
    echo ""
    echo "═══════════════════════════════════════════════════════════════════"
    echo "  LINUX INSTALLER (DEB/RPM/TAR.GZ)"
    echo "═══════════════════════════════════════════════════════════════════"
    
    local LINUX_PUBLISH="$PUBLISH_BASE/linux-x64"
    local LINUX_INSTALLER="$INSTALLER_BASE/linux"
    
    # Build if needed
    if [ "$SKIP_BUILD" = false ]; then
        echo "📦 Building Linux x64..."
        
        dotnet publish "$PROJECT_FILE" \
            --configuration Release \
            --framework net10.0-desktop \
            --runtime linux-x64 \
            --self-contained true \
            --output "$LINUX_PUBLISH" \
            -p:SkiaOnly=true \
            -p:PublishReadyToRun=true \
            -p:Version=$VERSION
        
        echo "✓ Build complete"
    fi
    
    # Make executable
    chmod +x "$LINUX_PUBLISH/PixlPunkt"
    
    # Check for fpm
    if ! command -v fpm &> /dev/null; then
        echo "📥 Installing fpm..."
        if command -v apt-get &> /dev/null; then
            sudo apt-get update
            sudo apt-get install -y ruby ruby-dev rubygems build-essential rpm
        elif command -v dnf &> /dev/null; then
            sudo dnf install -y ruby ruby-devel rubygems rpm-build
        elif command -v brew &> /dev/null; then
            # macOS - for cross-building Linux packages
            brew install gnu-tar rpm
        fi
        sudo gem install --no-document fpm
    fi
    
    # Create package structure
    local PKG_ROOT="/tmp/pixlpunkt-pkg-$$"
    rm -rf "$PKG_ROOT"
    mkdir -p "$PKG_ROOT/opt/pixlpunkt"
    mkdir -p "$PKG_ROOT/usr/share/applications"
    mkdir -p "$PKG_ROOT/usr/share/icons/hicolor/256x256/apps"
    mkdir -p "$PKG_ROOT/usr/share/mime/packages"
    
    # Copy files
    cp -r "$LINUX_PUBLISH"/* "$PKG_ROOT/opt/pixlpunkt/"
    chmod +x "$PKG_ROOT/opt/pixlpunkt/PixlPunkt"
    
    # Create desktop entry
    cat > "$PKG_ROOT/usr/share/applications/com.pixlpunkt.app.desktop" << 'DESKTOP'
[Desktop Entry]
Version=1.0
Type=Application
Name=PixlPunkt
Comment=Pixel Art Editor
Exec=/opt/pixlpunkt/PixlPunkt %F
Icon=pixlpunkt
Terminal=false
Categories=Graphics;2DGraphics;RasterGraphics;
MimeType=application/x-pixlpunkt;
DESKTOP

    # Create MIME type definition
    cat > "$PKG_ROOT/usr/share/mime/packages/com.pixlpunkt.app.xml" << 'MIME'
<?xml version="1.0" encoding="UTF-8"?>
<mime-info xmlns="http://www.freedesktop.org/standards/shared-mime-info">
    <mime-type type="application/x-pixlpunkt">
        <comment>PixlPunkt Document</comment>
        <glob pattern="*.pxp"/>
    </mime-type>
</mime-info>
MIME

    # Copy icon
    if [ -f "$ICON_PATH/Icon.png" ]; then
        cp "$ICON_PATH/Icon.png" "$PKG_ROOT/usr/share/icons/hicolor/256x256/apps/pixlpunkt.png"
    fi
    
    # Create post-install script
    cat > /tmp/after-install.sh << 'SCRIPT'
#!/bin/bash
update-mime-database /usr/share/mime 2>/dev/null || true
update-desktop-database /usr/share/applications 2>/dev/null || true
gtk-update-icon-cache /usr/share/icons/hicolor 2>/dev/null || true
ln -sf /opt/pixlpunkt/PixlPunkt /usr/local/bin/pixlpunkt
SCRIPT

    # Create post-remove script  
    cat > /tmp/after-remove.sh << 'SCRIPT'
#!/bin/bash
rm -f /usr/local/bin/pixlpunkt
update-mime-database /usr/share/mime 2>/dev/null || true
update-desktop-database /usr/share/applications 2>/dev/null || true
SCRIPT

    chmod +x /tmp/after-install.sh /tmp/after-remove.sh

    # Create DEB
    echo "📦 Creating DEB package..."
    fpm -s dir -t deb \
        --name pixlpunkt \
        --version "$VERSION" \
        --architecture amd64 \
        --description "PixlPunkt - Modern Pixel Art Editor" \
        --url "https://github.com/ChadRoesler/PixlPunkt" \
        --license "MIT" \
        --depends "libx11-6" --depends "libxrandr2" --depends "libxinerama1" \
        --depends "libxcursor1" --depends "libxi6" --depends "libgl1" \
        --depends "libfontconfig1" \
        --after-install /tmp/after-install.sh \
        --after-remove /tmp/after-remove.sh \
        --package "$LINUX_INSTALLER/pixlpunkt_${VERSION}_amd64.deb" \
        -C "$PKG_ROOT" opt usr
    
    echo "✓ DEB created: pixlpunkt_${VERSION}_amd64.deb"
    
    # Create RPM
    echo "📦 Creating RPM package..."
    fpm -s dir -t rpm \
        --name pixlpunkt \
        --version "$VERSION" \
        --architecture x86_64 \
        --description "PixlPunkt - Modern Pixel Art Editor" \
        --url "https://github.com/ChadRoesler/PixlPunkt" \
        --license "MIT" \
        --depends "libX11" --depends "libXrandr" --depends "libXinerama" \
        --depends "libXcursor" --depends "libXi" --depends "mesa-libGL" \
        --depends "fontconfig" \
        --after-install /tmp/after-install.sh \
        --after-remove /tmp/after-remove.sh \
        --package "$LINUX_INSTALLER/pixlpunkt-${VERSION}-1.x86_64.rpm" \
        -C "$PKG_ROOT" opt usr
    
    echo "✓ RPM created: pixlpunkt-${VERSION}-1.x86_64.rpm"
    
    # Create tarball
    echo "📦 Creating tarball..."
    cd "$PUBLISH_BASE"
    tar -czvf "$LINUX_INSTALLER/PixlPunkt-$VERSION-Linux-x64.tar.gz" linux-x64
    cd - > /dev/null
    echo "✓ Tarball created: PixlPunkt-$VERSION-Linux-x64.tar.gz"
    
    # Cleanup
    rm -rf "$PKG_ROOT"
    rm -f /tmp/after-install.sh /tmp/after-remove.sh
    
    echo ""
    echo "Linux installers created in: $LINUX_INSTALLER"
    ls -la "$LINUX_INSTALLER"
}

#############################################################################
# MACOS INSTALLER (App Bundle + DMG)
#############################################################################
create_mac_installer() {
    local ARCH="$1"
    local RID="osx-$ARCH"
    
    echo ""
    echo "═══════════════════════════════════════════════════════════════════"
    echo "  MACOS INSTALLER ($ARCH) - App Bundle + DMG"
    echo "═══════════════════════════════════════════════════════════════════"
    
    local MAC_PUBLISH="$PUBLISH_BASE/$RID"
    local MAC_INSTALLER="$INSTALLER_BASE/macos"
    
    # Build if needed
    if [ "$SKIP_BUILD" = false ]; then
        echo "📦 Building macOS $ARCH..."
        
        dotnet publish "$PROJECT_FILE" \
            --configuration Release \
            --framework net10.0-desktop \
            --runtime "$RID" \
            --self-contained true \
            --output "$MAC_PUBLISH" \
            -p:SkiaOnly=true \
            -p:PublishReadyToRun=true \
            -p:Version=$VERSION
        
        echo "✓ Build complete"
    fi
    
    # Make executable
    chmod +x "$MAC_PUBLISH/PixlPunkt"
    
    # Create app bundle
    echo "📦 Creating macOS App Bundle..."
    
    local APP_BUNDLE="$MAC_INSTALLER/PixlPunkt.app"
    rm -rf "$APP_BUNDLE"
    mkdir -p "$APP_BUNDLE/Contents/MacOS"
    mkdir -p "$APP_BUNDLE/Contents/Resources"
    
    # Copy all files to MacOS directory
    cp -r "$MAC_PUBLISH"/* "$APP_BUNDLE/Contents/MacOS/"
    chmod +x "$APP_BUNDLE/Contents/MacOS/PixlPunkt"
    
    # CRITICAL: Create Resources symlink or copy for Uno Platform
    # Uno Platform on macOS expects resources at Contents/MacOS/Resources/
    # but also needs Uno.Fonts.* folders accessible from the executable directory
    if [ ! -d "$APP_BUNDLE/Contents/MacOS/Resources" ]; then
        mkdir -p "$APP_BUNDLE/Contents/MacOS/Resources"
    fi
    
    # Move Uno resource folders into Resources/ directory
    for folder in "$APP_BUNDLE/Contents/MacOS/Uno.Fonts."* "$APP_BUNDLE/Contents/MacOS/FluentIcons.Resources."*; do
        if [ -d "$folder" ]; then
            local folder_name=$(basename "$folder")
            echo "  Moving $folder_name to Resources/"
            mv "$folder" "$APP_BUNDLE/Contents/MacOS/Resources/"
        fi
    done
    
    # Also handle Assets folders if they need to be in Resources
    if [ -d "$APP_BUNDLE/Contents/MacOS/Assets" ]; then
        # Keep Assets in MacOS for the app icon, but also copy to Resources
        cp -r "$APP_BUNDLE/Contents/MacOS/Assets" "$APP_BUNDLE/Contents/MacOS/Resources/" 2>/dev/null || true
    fi
    
    # Copy icon to Resources for macOS
    if [ -f "$ICON_PATH/Icon.png" ]; then
        cp "$ICON_PATH/Icon.png" "$APP_BUNDLE/Contents/Resources/AppIcon.png"
    fi
    
    # Create Info.plist
    cat > "$APP_BUNDLE/Contents/Info.plist" << PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key><string>PixlPunkt</string>
    <key>CFBundleIdentifier</key><string>com.pixlpunkt.app</string>
    <key>CFBundleName</key><string>PixlPunkt</string>
    <key>CFBundleDisplayName</key><string>PixlPunkt</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleShortVersionString</key><string>$VERSION</string>
    <key>CFBundleVersion</key><string>$VERSION</string>
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
PLIST

    echo "✓ App bundle created"
    
    # Create ZIP
    echo "📦 Creating ZIP..."
    local ZIP_NAME="PixlPunkt-$VERSION-macOS-$ARCH.zip"
    cd "$MAC_INSTALLER"
    zip -r "$ZIP_NAME" "PixlPunkt.app"
    cd - > /dev/null
    echo "✓ ZIP created: $ZIP_NAME"
    
    # Create DMG (macOS only)
    if [ "$(uname -s)" = "Darwin" ]; then
        # Install create-dmg if needed
        if ! command -v create-dmg &> /dev/null; then
            echo "📥 Installing create-dmg..."
            brew install create-dmg
        fi
        
        echo "📦 Creating DMG..."
        local DMG_NAME="PixlPunkt-$VERSION-macOS-$ARCH.dmg"
        
        create-dmg \
            --volname "PixlPunkt $VERSION" \
            --window-pos 200 120 \
            --window-size 600 400 \
            --icon-size 100 \
            --icon "PixlPunkt.app" 150 185 \
            --hide-extension "PixlPunkt.app" \
            --app-drop-link 450 185 \
            "$MAC_INSTALLER/$DMG_NAME" \
            "$APP_BUNDLE" || true  # create-dmg returns non-zero even on success sometimes
        
        if [ -f "$MAC_INSTALLER/$DMG_NAME" ]; then
            echo "✓ DMG created: $DMG_NAME"
        else
            echo "⚠ DMG creation failed (but ZIP is available)"
        fi
    else
        echo "ℹ DMG creation skipped (requires macOS)"
        echo "  Copy the app bundle to a Mac and run:"
        echo "  brew install create-dmg && create-dmg --volname 'PixlPunkt' PixlPunkt.dmg PixlPunkt.app"
    fi
    
    # Rename app bundle to include arch
    mv "$APP_BUNDLE" "$MAC_INSTALLER/PixlPunkt-$ARCH.app"
    
    echo ""
    echo "macOS installer created in: $MAC_INSTALLER"
    ls -la "$MAC_INSTALLER"
}

#############################################################################
# MAIN EXECUTION
#############################################################################

SUMMARY=()

if [ "$PLATFORM" = "all" ] || [ "$PLATFORM" = "linux" ]; then
    create_linux_installer
    SUMMARY+=("Linux (DEB/RPM/TAR)")
fi

if [ "$PLATFORM" = "all" ] || [ "$PLATFORM" = "mac" ]; then
    # Detect Mac architecture or build both
    if [ "$(uname -s)" = "Darwin" ]; then
        if [ "$(uname -m)" = "arm64" ]; then
            create_mac_installer "arm64"
            SUMMARY+=("macOS ARM64")
        else
            create_mac_installer "x64"
            SUMMARY+=("macOS x64")
        fi
    else
        # Cross-compile both
        create_mac_installer "x64"
        create_mac_installer "arm64"
        SUMMARY+=("macOS (x64 + ARM64)")
    fi
fi

echo ""
echo "═══════════════════════════════════════════════════════════════════"
echo "                    INSTALLER CREATION COMPLETE"
echo "═══════════════════════════════════════════════════════════════════"
echo ""
echo "Created installers for: ${SUMMARY[*]}"
echo ""
echo "Output directory: $INSTALLER_BASE"
echo ""
ls -la "$INSTALLER_BASE"/*/ 2>/dev/null || true
echo ""
