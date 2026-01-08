#!/bin/bash
#############################################################################
# PixlPunkt macOS Installer
# Creates an app bundle and registers file associations
#############################################################################

set -e

APP_NAME="PixlPunkt"
BUNDLE_ID="com.pixlpunkt.app"
APP_BUNDLE="$APP_NAME.app"
INSTALL_DIR="/Applications"

# Get the directory where the script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo ""
echo "========================================"
echo "     PixlPunkt macOS Installer"
echo "========================================"
echo ""

# Create app bundle structure
echo "[1/4] Creating app bundle..."
BUNDLE_PATH="$INSTALL_DIR/$APP_BUNDLE"
CONTENTS_PATH="$BUNDLE_PATH/Contents"
MACOS_PATH="$CONTENTS_PATH/MacOS"
RESOURCES_PATH="$CONTENTS_PATH/Resources"

mkdir -p "$MACOS_PATH"
mkdir -p "$RESOURCES_PATH"

# Copy application files
echo "[2/4] Copying application files..."
cp -r "$SCRIPT_DIR"/* "$MACOS_PATH/" 2>/dev/null || true
rm -f "$MACOS_PATH/install.sh" "$MACOS_PATH/uninstall.sh"

# Make executable
chmod +x "$MACOS_PATH/PixlPunkt.Uno"

# Create Info.plist with file associations
echo "[3/4] Creating Info.plist..."
cat > "$CONTENTS_PATH/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleExecutable</key>
    <string>PixlPunkt.Uno</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>CFBundleIdentifier</key>
    <string>$BUNDLE_ID</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>$APP_NAME</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0</string>
    <key>CFBundleVersion</key>
    <string>1</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSHumanReadableCopyright</key>
    <string>Copyright © 2024 PixlPunkt</string>
    
    <!-- File Type Associations -->
    <key>CFBundleDocumentTypes</key>
    <array>
        <!-- PixlPunkt Document -->
        <dict>
            <key>CFBundleTypeName</key>
            <string>PixlPunkt Document</string>
            <key>CFBundleTypeRole</key>
            <string>Editor</string>
            <key>LSHandlerRank</key>
            <string>Owner</string>
            <key>CFBundleTypeExtensions</key>
            <array>
                <string>pxp</string>
            </array>
            <key>LSItemContentTypes</key>
            <array>
                <string>com.pixlpunkt.document</string>
            </array>
        </dict>
        
        <!-- PixlPunkt Animation Reel -->
        <dict>
            <key>CFBundleTypeName</key>
            <string>PixlPunkt Animation Reel</string>
            <key>CFBundleTypeRole</key>
            <string>Editor</string>
            <key>LSHandlerRank</key>
            <string>Owner</string>
            <key>CFBundleTypeExtensions</key>
            <array>
                <string>pxpr</string>
            </array>
            <key>LSItemContentTypes</key>
            <array>
                <string>com.pixlpunkt.reel</string>
            </array>
        </dict>
        
        <!-- PixlPunkt Tileset -->
        <dict>
            <key>CFBundleTypeName</key>
            <string>PixlPunkt Tileset</string>
            <key>CFBundleTypeRole</key>
            <string>Editor</string>
            <key>LSHandlerRank</key>
            <string>Owner</string>
            <key>CFBundleTypeExtensions</key>
            <array>
                <string>pxpt</string>
            </array>
            <key>LSItemContentTypes</key>
            <array>
                <string>com.pixlpunkt.tileset</string>
            </array>
        </dict>
        
        <!-- PixlPunkt Brush -->
        <dict>
            <key>CFBundleTypeName</key>
            <string>PixlPunkt Brush</string>
            <key>CFBundleTypeRole</key>
            <string>Editor</string>
            <key>LSHandlerRank</key>
            <string>Owner</string>
            <key>CFBundleTypeExtensions</key>
            <array>
                <string>pbx</string>
            </array>
            <key>LSItemContentTypes</key>
            <array>
                <string>com.pixlpunkt.brush</string>
            </array>
        </dict>
    </array>
    
    <!-- UTI Declarations -->
    <key>UTExportedTypeDeclarations</key>
    <array>
        <dict>
            <key>UTTypeIdentifier</key>
            <string>com.pixlpunkt.document</string>
            <key>UTTypeDescription</key>
            <string>PixlPunkt Document</string>
            <key>UTTypeConformsTo</key>
            <array>
                <string>public.data</string>
            </array>
            <key>UTTypeTagSpecification</key>
            <dict>
                <key>public.filename-extension</key>
                <array>
                    <string>pxp</string>
                </array>
            </dict>
        </dict>
        <dict>
            <key>UTTypeIdentifier</key>
            <string>com.pixlpunkt.reel</string>
            <key>UTTypeDescription</key>
            <string>PixlPunkt Animation Reel</string>
            <key>UTTypeConformsTo</key>
            <array>
                <string>public.data</string>
            </array>
            <key>UTTypeTagSpecification</key>
            <dict>
                <key>public.filename-extension</key>
                <array>
                    <string>pxpr</string>
                </array>
            </dict>
        </dict>
        <dict>
            <key>UTTypeIdentifier</key>
            <string>com.pixlpunkt.tileset</string>
            <key>UTTypeDescription</key>
            <string>PixlPunkt Tileset</string>
            <key>UTTypeConformsTo</key>
            <array>
                <string>public.data</string>
            </array>
            <key>UTTypeTagSpecification</key>
            <dict>
                <key>public.filename-extension</key>
                <array>
                    <string>pxpt</string>
                </array>
            </dict>
        </dict>
        <dict>
            <key>UTTypeIdentifier</key>
            <string>com.pixlpunkt.brush</string>
            <key>UTTypeDescription</key>
            <string>PixlPunkt Brush</string>
            <key>UTTypeConformsTo</key>
            <array>
                <string>public.data</string>
            </array>
            <key>UTTypeTagSpecification</key>
            <dict>
                <key>public.filename-extension</key>
                <array>
                    <string>pbx</string>
                </array>
            </dict>
        </dict>
    </array>
</dict>
</plist>
EOF

# Copy icon if exists
if [ -f "$SCRIPT_DIR/Assets/Icons/Icon.png" ]; then
    # For a proper app, you'd convert to .icns format
    cp "$SCRIPT_DIR/Assets/Icons/Icon.png" "$RESOURCES_PATH/AppIcon.png"
fi

# Register with Launch Services
echo "[4/4] Registering with Launch Services..."
/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister -f "$BUNDLE_PATH"

echo ""
echo "========================================"
echo "  Installation Complete!"
echo "========================================"
echo ""
echo "  PixlPunkt has been installed to:"
echo "  $BUNDLE_PATH"
echo ""
echo "  You can now:"
echo "  - Launch from Applications folder"
echo "  - Double-click .pxp files to open"
echo ""
echo "  Note: You may need to allow the app in"
echo "  System Preferences > Security & Privacy"
echo ""
