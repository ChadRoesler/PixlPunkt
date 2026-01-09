#!/bin/bash
#############################################################################
# PixlPunkt Local Build & Publish Script (Linux/macOS)
#############################################################################
# Builds and publishes PixlPunkt for local testing on various platforms.
#
# Usage:
#   ./publish-local.sh                     # Build for current platform
#   ./publish-local.sh win                 # Windows x64 (Skia)
#   ./publish-local.sh linux               # Linux x64
#   ./publish-local.sh osx                 # macOS x64
#   ./publish-local.sh osx-arm             # macOS ARM64
#   ./publish-local.sh all                 # All platforms
#   ./publish-local.sh linux --run         # Build and run immediately
#   ./publish-local.sh linux --debug       # Debug build
#############################################################################

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR/PixlPunkt.Uno/PixlPunkt.Uno"
PROJECT_FILE="$PROJECT_DIR/PixlPunkt.Uno.csproj"
PUBLISH_BASE="$PROJECT_DIR/bin/publish"

PLATFORM="${1:-auto}"
CONFIGURATION="Release"
RUN_AFTER_BUILD=false

# Parse additional flags
shift || true
while [[ $# -gt 0 ]]; do
    case $1 in
        --run)
            RUN_AFTER_BUILD=true
            shift
            ;;
        --debug)
            CONFIGURATION="Debug"
            shift
            ;;
        --clean)
            echo "?? Cleaning publish directory..."
            rm -rf "$PUBLISH_BASE"
            echo "? Clean complete"
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
        Darwin*) 
            if [ "$(uname -m)" = "arm64" ]; then
                PLATFORM="osx-arm"
            else
                PLATFORM="osx"
            fi
            ;;
        MINGW*|CYGWIN*|MSYS*) PLATFORM="win" ;;
        *) PLATFORM="linux" ;;
    esac
fi

echo "?????????????????????????????????????????????????????????????????"
echo "?         PixlPunkt Local Build & Publish Script                ?"
echo "?????????????????????????????????????????????????????????????????"
echo ""

publish_platform() {
    local NAME="$1"
    local RUNTIME_ID="$2"
    local FRAMEWORK="$3"
    local OUTPUT_DIR="$4"
    local EXTRA_PROPS="$5"
    
    echo "???????????????????????????????????????????????????????????????"
    echo "?? Building: $NAME"
    echo "   Runtime: $RUNTIME_ID"
    echo "   Framework: $FRAMEWORK"
    echo "   Output: $OUTPUT_DIR"
    echo ""
    
    local START_TIME=$(date +%s)
    
    dotnet publish "$PROJECT_FILE" \
        --configuration "$CONFIGURATION" \
        --framework "$FRAMEWORK" \
        --runtime "$RUNTIME_ID" \
        --self-contained true \
        --output "$OUTPUT_DIR" \
        -p:PublishReadyToRun=true \
        -p:PublishSingleFile=false \
        $EXTRA_PROPS
    
    local END_TIME=$(date +%s)
    local ELAPSED=$((END_TIME - START_TIME))
    
    echo ""
    echo "? $NAME built successfully in ${ELAPSED}s"
    echo "  Output: $OUTPUT_DIR"
    
    # Make executable on Linux/macOS
    if [ -f "$OUTPUT_DIR/PixlPunkt.Uno" ]; then
        chmod +x "$OUTPUT_DIR/PixlPunkt.Uno"
    fi
}

BUILT_PATHS=()
BUILT_NAMES=()

# Windows Skia Desktop
if [ "$PLATFORM" = "win" ] || [ "$PLATFORM" = "all" ]; then
    publish_platform \
        "Windows x64 (Skia Desktop)" \
        "win-x64" \
        "net10.0-desktop" \
        "$PUBLISH_BASE/win-x64" \
        "-p:SkiaOnly=true"
    
    BUILT_PATHS+=("$PUBLISH_BASE/win-x64")
    BUILT_NAMES+=("Windows Skia")
fi

# Linux x64
if [ "$PLATFORM" = "linux" ] || [ "$PLATFORM" = "all" ]; then
    publish_platform \
        "Linux x64" \
        "linux-x64" \
        "net10.0-desktop" \
        "$PUBLISH_BASE/linux-x64" \
        "-p:SkiaOnly=true"
    
    BUILT_PATHS+=("$PUBLISH_BASE/linux-x64")
    BUILT_NAMES+=("Linux")
fi

# macOS x64 (Intel)
if [ "$PLATFORM" = "osx" ] || [ "$PLATFORM" = "all" ]; then
    publish_platform \
        "macOS x64 (Intel)" \
        "osx-x64" \
        "net10.0-desktop" \
        "$PUBLISH_BASE/osx-x64" \
        "-p:SkiaOnly=true"
    
    BUILT_PATHS+=("$PUBLISH_BASE/osx-x64")
    BUILT_NAMES+=("macOS Intel")
fi

# macOS ARM64 (Apple Silicon)
if [ "$PLATFORM" = "osx-arm" ] || [ "$PLATFORM" = "all" ]; then
    publish_platform \
        "macOS ARM64 (Apple Silicon)" \
        "osx-arm64" \
        "net10.0-desktop" \
        "$PUBLISH_BASE/osx-arm64" \
        "-p:SkiaOnly=true"
    
    BUILT_PATHS+=("$PUBLISH_BASE/osx-arm64")
    BUILT_NAMES+=("macOS ARM")
fi

echo ""
echo "???????????????????????????????????????????????????????????????"
echo "                        BUILD SUMMARY"
echo "???????????????????????????????????????????????????????????????"

if [ ${#BUILT_PATHS[@]} -eq 0 ]; then
    echo "No builds completed successfully."
    exit 1
fi

for i in "${!BUILT_PATHS[@]}"; do
    echo "? ${BUILT_NAMES[$i]}: ${BUILT_PATHS[$i]}"
done

echo ""
echo "Publish outputs are in: $PUBLISH_BASE"

# Run if requested
if [ "$RUN_AFTER_BUILD" = true ] && [ ${#BUILT_PATHS[@]} -gt 0 ]; then
    TO_RUN="${BUILT_PATHS[0]}"
    
    echo ""
    echo "?? Launching from $TO_RUN..."
    
    if [ -f "$TO_RUN/PixlPunkt.Uno" ]; then
        "$TO_RUN/PixlPunkt.Uno" &
    elif [ -f "$TO_RUN/PixlPunkt.Uno.exe" ]; then
        "$TO_RUN/PixlPunkt.Uno.exe" &
    else
        echo "? Executable not found in $TO_RUN"
    fi
fi

echo ""
