#!/bin/bash
#############################################################################
# PixlPunkt Linux Uninstaller
#############################################################################

set -e

APP_ID="com.pixlpunkt.app"
INSTALL_DIR="/opt/pixlpunkt"

if [ "$EUID" -ne 0 ]; then
    echo "Please run as root (sudo)"
    exit 1
fi

echo "Uninstalling PixlPunkt..."

# Remove application files
rm -rf "$INSTALL_DIR"

# Remove symlink
rm -f /usr/local/bin/pixlpunkt

# Remove desktop entry
rm -f "/usr/share/applications/${APP_ID}.desktop"

# Remove MIME types
rm -f "/usr/share/mime/packages/${APP_ID}.xml"

# Remove icon
rm -f /usr/share/icons/hicolor/256x256/apps/pixlpunkt.png

# Update databases
update-mime-database /usr/share/mime 2>/dev/null || true
update-desktop-database /usr/share/applications 2>/dev/null || true

echo "PixlPunkt uninstalled successfully."
