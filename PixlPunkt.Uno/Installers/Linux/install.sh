#!/bin/bash
#############################################################################
# PixlPunkt Linux Installer
# Installs the application and registers file associations
#############################################################################

set -e

APP_NAME="PixlPunkt"
APP_ID="com.pixlpunkt.app"
INSTALL_DIR="/opt/pixlpunkt"
BIN_NAME="PixlPunkt.Uno"
DESKTOP_FILE="/usr/share/applications/${APP_ID}.desktop"
MIME_FILE="/usr/share/mime/packages/${APP_ID}.xml"

# Check for root
if [ "$EUID" -ne 0 ]; then
    echo "Please run as root (sudo)"
    exit 1
fi

echo "Installing PixlPunkt..."

# Get the directory where the script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Create installation directory
mkdir -p "$INSTALL_DIR"

# Copy application files
echo "Copying application files..."
cp -r "$SCRIPT_DIR"/* "$INSTALL_DIR/"
chmod +x "$INSTALL_DIR/$BIN_NAME"

# Create symlink in /usr/local/bin
ln -sf "$INSTALL_DIR/$BIN_NAME" /usr/local/bin/pixlpunkt

# Install icon (if exists)
if [ -f "$INSTALL_DIR/Assets/Icons/Icon.png" ]; then
    mkdir -p /usr/share/icons/hicolor/256x256/apps
    cp "$INSTALL_DIR/Assets/Icons/Icon.png" /usr/share/icons/hicolor/256x256/apps/pixlpunkt.png
fi

# Create .desktop file for application launcher
echo "Creating desktop entry..."
cat > "$DESKTOP_FILE" << EOF
[Desktop Entry]
Version=1.0
Type=Application
Name=PixlPunkt
Comment=Pixel Art Editor
Exec=$INSTALL_DIR/$BIN_NAME %F
Icon=pixlpunkt
Terminal=false
Categories=Graphics;2DGraphics;RasterGraphics;
MimeType=application/x-pixlpunkt;application/x-pixlpunkt-reel;application/x-pixlpunkt-tileset;application/x-pixlpunkt-brush;
StartupWMClass=PixlPunkt
EOF

# Create MIME type definitions for file associations
echo "Registering file types..."
cat > "$MIME_FILE" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<mime-info xmlns="http://www.freedesktop.org/standards/shared-mime-info">
    <!-- PixlPunkt Document -->
    <mime-type type="application/x-pixlpunkt">
        <comment>PixlPunkt Document</comment>
        <glob pattern="*.pxp"/>
        <icon name="pixlpunkt"/>
    </mime-type>
    
    <!-- PixlPunkt Animation Reel -->
    <mime-type type="application/x-pixlpunkt-reel">
        <comment>PixlPunkt Animation Reel</comment>
        <glob pattern="*.pxpr"/>
        <icon name="pixlpunkt"/>
    </mime-type>
    
    <!-- PixlPunkt Tileset -->
    <mime-type type="application/x-pixlpunkt-tileset">
        <comment>PixlPunkt Tileset</comment>
        <glob pattern="*.pxpt"/>
        <icon name="pixlpunkt"/>
    </mime-type>
    
    <!-- PixlPunkt Brush -->
    <mime-type type="application/x-pixlpunkt-brush">
        <comment>PixlPunkt Brush</comment>
        <glob pattern="*.pbx"/>
        <icon name="pixlpunkt"/>
    </mime-type>
</mime-info>
EOF

# Update MIME database
echo "Updating MIME database..."
update-mime-database /usr/share/mime

# Update desktop database
echo "Updating desktop database..."
update-desktop-database /usr/share/applications

# Update icon cache
if command -v gtk-update-icon-cache &> /dev/null; then
    gtk-update-icon-cache -f /usr/share/icons/hicolor
fi

echo ""
echo "============================================"
echo "  PixlPunkt installed successfully!"
echo "============================================"
echo ""
echo "  You can now:"
echo "  - Launch from Applications menu"
echo "  - Run 'pixlpunkt' from terminal"
echo "  - Double-click .pxp files to open"
echo ""
