<#
.SYNOPSIS
    Downloads Fluent UI System Icons and converts them to PNG for documentation.

.DESCRIPTION
    This script:
    1. Downloads SVG icons from Microsoft's Fluent UI System Icons repo
    2. Converts them to PNG at specified sizes (16x16, 20x20)
    3. Outputs white-colored icons (for dark theme compatibility)

.NOTES
    Requires: Inkscape for SVG to PNG conversion
    Install: winget install Inkscape.Inkscape

.EXAMPLE
    .\export-doc-icons.ps1
    .\export-doc-icons.ps1 -Size 24
    .\export-doc-icons.ps1 -IconsOnly "play,pause,stop"
#>

param(
    [int[]]$Sizes = @(16, 20),
    [string]$OutputDir = "$PSScriptRoot\..\docs\assets\icons",
    [string]$IconsOnly = "",  # Comma-separated list to export only specific icons
    [switch]$Force,           # Re-download even if exists
    [string]$Color = "white"  # Icon color (white for dark themes, black for light)
)

# ============================================================================
# ICON MANIFEST - Extracted from PixlPunkt source code
# Format: "local_name" = "FluentIconName"
# ============================================================================

$IconManifest = @{
    # ========================================================================
    # PLAYBACK / TIMELINE CONTROLS
    # ========================================================================
    "play"                    = "Play"
    "pause"                   = "Pause"
    "stop"                    = "Stop"
    "previous"                = "Previous"
    "next"                    = "Next"
    "arrow_repeat_all"        = "ArrowRepeatAll"
    
    # ========================================================================
    # TOOLS - DRAWING (from Core/Tools/Settings/*.cs)
    # ========================================================================
    "edit"                    = "Edit"                # Brush tool
    "eraser"                  = "Eraser"
    "paint_bucket"            = "PaintBucket"         # Fill tool
    "eyedropper"              = "Eyedropper"          # Dropper/Color picker
    "drop"                    = "Drop"                # Blur tool
    "pen_sync"                = "PenSync"             # Replacer tool
    "hand_draw"               = "HandDraw"            # Smudge tool
    "tap_double"              = "TapDouble"           # Jumble tool
    "color_line"              = "ColorLine"           # Gradient tool
    "data_sunburst"           = "DataSunburst"        # Gradient fill
    "calendar_pattern"        = "CalendarPattern"     # Fill tool pattern
    "syringe"                 = "Syringe"             # Color picker inject
    "arrow_swap"              = "ArrowSwap"           # Swap colors
    
    # ========================================================================
    # TOOLS - SELECTION (from Core/Tools/Settings/*.cs)
    # ========================================================================
    "select_object"           = "SelectObject"        # Rectangle select
    "wand"                    = "Wand"                # Magic wand
    "lasso"                   = "Lasso"
    "border_none"             = "BorderNone"          # Paint selection
    
    # ========================================================================
    # TOOLS - SHAPES
    # ========================================================================
    "circle"                  = "Circle"
    "square"                  = "Square"
    "shape_organic"           = "ShapeOrganic"        # Shape category
    "circle_hint"             = "CircleHint"          # Ellipse select (plugin)
    "star"                    = "Star"                # Star shape (plugin)
    
    # ========================================================================
    # TOOLS - UTILITY
    # ========================================================================
    "hand_left"               = "HandLeft"            # Pan tool
    "zoom_in"                 = "ZoomIn"
    "zoom_out"                = "ZoomOut"
    "zoom_fit"                = "ZoomFit"
    "wrench"                  = "Wrench"              # Utility category
    "search_info"             = "SearchInfo"          # Info tool (plugin)
    
    # ========================================================================
    # TOOLS - TILE
    # ========================================================================
    "table"                   = "Table"               # Tile category
    "table_edit"              = "TableEdit"           # Tile stamper
    "table_cell_edit"         = "TableCellEdit"       # Tile modifier
    "table_lightning"         = "TableLightning"      # Tile animation
    "table_dismiss"           = "TableDismiss"        # Tile bucket (plugin)
    
    # ========================================================================
    # TOOLS - SYMMETRY
    # ========================================================================
    "flip_horizontal"         = "FlipHorizontal"
    "flip_vertical"           = "FlipVertical"
    "align_center_vertical"   = "AlignCenterVertical" # Center axis
    
    # ========================================================================
    # LAYER PANEL (from UI/*.xaml)
    # ========================================================================
    "add"                     = "Add"
    "add_square"              = "AddSquare"
    "stack_add"               = "StackAdd"
    "folder"                  = "Folder"
    "folder_add"              = "FolderAdd"
    "folder_open"             = "FolderOpen"
    "delete"                  = "Delete"
    "copy"                    = "Copy"
    "eye"                     = "Eye"                 # Visibility on
    "eye_off"                 = "EyeOff"              # Visibility off
    "eye_tracking"            = "EyeTracking"         # Solo mode
    "lock_closed"             = "LockClosed"          # Locked
    "lock_open"               = "LockOpen"            # Unlocked
    "glasses"                 = "Glasses"             # Effects on
    "glasses_off"             = "GlassesOff"          # Effects off
    "settings"                = "Settings"
    "layer"                   = "Layer"
    "layer_diagonal"          = "LayerDiagonal"       # Layers panel
    
    # ========================================================================
    # EDIT OPERATIONS
    # ========================================================================
    "arrow_undo"              = "ArrowUndo"
    "arrow_redo"              = "ArrowRedo"
    "cut"                     = "Cut"
    "clipboard_paste"         = "ClipboardPaste"
    "broom"                   = "Broom"               # Clear
    
    # ========================================================================
    # VIEW / WINDOW
    # ========================================================================
    "arrow_maximize"          = "ArrowMaximize"
    "arrow_minimize_vertical" = "ArrowMinimizeVertical"
    "resize_small"            = "ResizeSmall"
    "resize_large"            = "ResizeLarge"
    "window_multiple_swap"    = "WindowMultipleSwap"
    "chevron_down"            = "ChevronDown"
    "chevron_left"            = "ChevronLeft"
    "chevron_right"           = "ChevronRight"
    "reorder_dots_vertical"   = "ReOrderDotsVertical"
    
    # ========================================================================
    # FILE OPERATIONS
    # ========================================================================
    "document"                = "Document"
    "document_fit"            = "DocumentFit"
    "open"                    = "Open"
    "arrow_import"            = "ArrowImport"
    "arrow_upload"            = "ArrowUpload"
    
    # ========================================================================
    # PANELS / SECTIONS
    # ========================================================================
    "preview_link"            = "PreviewLink"         # Preview panel
    "color"                   = "Color"               # Palette panel
    "grid"                    = "Grid"                # Tiles panel
    "clock"                   = "Clock"               # History panel
    "history"                 = "History"
    
    # ========================================================================
    # CAMERA / VIDEO
    # ========================================================================
    "camera"                  = "Camera"
    "camera_switch"           = "CameraSwitch"
    "video_clip"              = "VideoClip"
    
    # ========================================================================
    # EFFECTS / SPECIAL
    # ========================================================================
    "image_sparkle"           = "ImageSparkle"        # Effects
    "sparkle"                 = "Sparkle"             # Sparkle tool (plugin)
    "square_shadow"           = "SquareShadow"        # Drop shadow
    
    # ========================================================================
    # STATUS / UI
    # ========================================================================
    "warning"                 = "Warning"
    "checkmark"               = "Checkmark"
    "dismiss"                 = "Dismiss"
    "dismiss_circle"          = "DismissCircle"
    "link"                    = "Link"                # Snap
    "link_multiple"           = "LinkMultiple"
    "apps"                    = "Apps"                # Default tool icon
    "app_generic"             = "AppGeneric"
    
    # ========================================================================
    # SETTINGS PAGE
    # ========================================================================
    "keyboard"                = "Keyboard"
    "keyboard_shift"          = "KeyboardShiftUppercase"
    "dock_row"                = "DockRow"
    "paint_brush"             = "PaintBrush"          # Brush category
    "plug_disconnected"       = "PlugDisconnected"    # Plugins
    
    # ========================================================================
    # MISC
    # ========================================================================
    "globe"                   = "Globe"
    "globe_off"               = "GlobeOff"
    "arrow_circle_up"         = "ArrowCircleUp"
    "arrow_circle_down"       = "ArrowCircleDown"
}

# ============================================================================
# FIND INKSCAPE
# ============================================================================

function Find-Inkscape {
    # Check if inkscape is in PATH first
    $inPath = Get-Command "inkscape" -ErrorAction SilentlyContinue
    if ($inPath) {
        return $inPath.Source
    }
    
    # Common installation paths for Inkscape
    $possiblePaths = @(
        # Windows Store / winget install location
        "$env:LOCALAPPDATA\Microsoft\WindowsApps\inkscape.exe"
        
        # Standard Program Files locations
        "$env:ProgramFiles\Inkscape\bin\inkscape.exe"
        "${env:ProgramFiles(x86)}\Inkscape\bin\inkscape.exe"
        "$env:ProgramFiles\Inkscape\inkscape.exe"
        "${env:ProgramFiles(x86)}\Inkscape\inkscape.exe"
        
        # Older Inkscape versions
        "$env:ProgramFiles\Inkscape\bin\inkscape.com"
        "${env:ProgramFiles(x86)}\Inkscape\bin\inkscape.com"
        
        # Scoop install
        "$env:USERPROFILE\scoop\apps\inkscape\current\bin\inkscape.exe"
        
        # Chocolatey install
        "$env:ChocolateyInstall\bin\inkscape.exe"
        "C:\tools\inkscape\bin\inkscape.exe"
        
        # Portable install in common locations
        "C:\Inkscape\bin\inkscape.exe"
        "D:\Inkscape\bin\inkscape.exe"
    )
    
    foreach ($path in $possiblePaths) {
        if (Test-Path $path) {
            Write-Host "Found Inkscape at: $path" -ForegroundColor Green
            return $path
        }
    }
    
    # Try to find via registry (winget/MSI installs)
    try {
        $regPaths = @(
            "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\inkscape.exe"
            "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\inkscape.exe"
        )
        
        foreach ($regPath in $regPaths) {
            if (Test-Path $regPath) {
                $inkscapePath = (Get-ItemProperty $regPath).'(Default)'
                if ($inkscapePath -and (Test-Path $inkscapePath)) {
                    Write-Host "Found Inkscape via registry: $inkscapePath" -ForegroundColor Green
                    return $inkscapePath
                }
            }
        }
    }
    catch {
        # Registry lookup failed, continue
    }
    
    # Try to find via where.exe (searches PATH and App Paths)
    try {
        $whereResult = & where.exe inkscape.exe 2>$null
        if ($whereResult -and (Test-Path $whereResult[0])) {
            Write-Host "Found Inkscape via where.exe: $($whereResult[0])" -ForegroundColor Green
            return $whereResult[0]
        }
    }
    catch {
        # where.exe failed, continue
    }
    
    return $null
}

# ============================================================================
# HELPER FUNCTIONS
# ============================================================================

function Get-FluentIconUrl {
    param(
        [string]$IconName, 
        [int]$Size, 
        [string]$Variant = "regular"
    )
    
    # Convert PascalCase to snake_case for filename
    # DataSunburst -> data_sunburst
    $snakeName = ($IconName -creplace '([A-Z])', '_$1').TrimStart('_').ToLower()
    
    # Convert PascalCase to "Spaced Words" for folder name
    # DataSunburst -> "Data Sunburst"
    $folderName = ($IconName -creplace '([A-Z])', ' $1').TrimStart(' ')
    
    # URL encode the folder name (spaces become %20)
    $encodedFolderName = [System.Uri]::EscapeDataString($folderName)
    
    # Base URL for raw GitHub content
    $baseUrl = "https://raw.githubusercontent.com/microsoft/fluentui-system-icons/main/assets"
    
    # Icon naming: ic_fluent_{name}_{size}_{variant}.svg
    # Size is passed directly - no mapping here
    $iconFileName = "ic_fluent_${snakeName}_${Size}_${Variant}.svg"
    
    return "$baseUrl/$encodedFolderName/SVG/$iconFileName"
}

function Convert-SvgToPng {
    param(
        [string]$SvgPath,
        [string]$PngPath,
        [int]$Size,
        [string]$FillColor = "white",
        [string]$InkscapePath
    )
    
    if (-not $InkscapePath) {
        Write-Warning "  Inkscape path not provided"
        return $false
    }
    
    # Read SVG and replace fill color
    $svgContent = Get-Content $SvgPath -Raw -Encoding UTF8
    
    # Replace existing fill colors
    $svgContent = $svgContent -replace 'fill="#[0-9a-fA-F]+"', "fill=`"$FillColor`""
    $svgContent = $svgContent -replace 'fill="rgb\([^)]+\)"', "fill=`"$FillColor`""
    $svgContent = $svgContent -replace 'fill:[^;]+;', "fill:$FillColor;"
    
    # If no fill attribute exists, add to path/rect/circle elements
    if ($svgContent -notmatch 'fill=') {
        $svgContent = $svgContent -replace '<path', "<path fill=`"$FillColor`""
        $svgContent = $svgContent -replace '<rect', "<rect fill=`"$FillColor`""
        $svgContent = $svgContent -replace '<circle', "<circle fill=`"$FillColor`""
    }
    
    $tempSvg = [System.IO.Path]::GetTempFileName() -replace '\.tmp$', '.svg'
    $svgContent | Set-Content $tempSvg -Encoding UTF8 -NoNewline
    
    # Run Inkscape - use Start-Process to capture output properly
    try {
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = $InkscapePath
        $psi.Arguments = "`"$tempSvg`" --export-filename=`"$PngPath`" --export-width=$Size --export-height=$Size"
        $psi.UseShellExecute = $false
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        $psi.CreateNoWindow = $true
        
        $process = [System.Diagnostics.Process]::Start($psi)
        $process.WaitForExit(30000) # 30 second timeout
        
        Remove-Item $tempSvg -ErrorAction SilentlyContinue
        return (Test-Path $PngPath)
    }
    catch {
        Write-Warning "  Inkscape execution failed: $_"
        Remove-Item $tempSvg -ErrorAction SilentlyContinue
        return $false
    }
}

function Download-FluentIcon {
    param(
        [string]$LocalName,
        [string]$FluentName,
        [int]$Size,
        [string]$OutputPath,
        [string]$Color,
        [string]$InkscapePath
    )
    
    $pngFile = Join-Path $OutputPath "${LocalName}_${Size}.png"
    
    # Skip if exists and not forcing
    if ((Test-Path $pngFile) -and -not $Force) {
        Write-Host "  Skipping $LocalName ($Size) - exists" -ForegroundColor DarkGray
        return @{ Success = $true; TriedUrls = @() }
    }
    
    $tempSvg = [System.IO.Path]::GetTempFileName() -replace '\.tmp$', '.svg'
    $triedUrls = @()
    
    # Try different URL patterns - these are the actual sizes available in the repo
    $variants = @("regular", "filled")
    $availableSizes = @(16, 20, 24, 28, 32, 48)
    
    foreach ($variant in $variants) {
        foreach ($trySize in $availableSizes) {
            $url = Get-FluentIconUrl -IconName $FluentName -Size $trySize -Variant $variant
            $triedUrls += $url
            
            try {
                # Use Invoke-WebRequest for better error handling
                $null = Invoke-WebRequest -Uri $url -OutFile $tempSvg -ErrorAction Stop
                
                if ((Test-Path $tempSvg) -and (Get-Item $tempSvg).Length -gt 100) {
                    $success = Convert-SvgToPng -SvgPath $tempSvg -PngPath $pngFile -Size $Size -FillColor $Color -InkscapePath $InkscapePath
                    
                    if ($success -and (Test-Path $pngFile)) {
                        Write-Host "  OK: ${LocalName}_${Size}.png (from ${trySize}px $variant)" -ForegroundColor Green
                        Remove-Item $tempSvg -ErrorAction SilentlyContinue
                        return @{ Success = $true; TriedUrls = @() }
                    }
                }
            }
            catch {
                # Try next variant/size
                continue
            }
        }
    }
    
    Remove-Item $tempSvg -ErrorAction SilentlyContinue
    return @{ Success = $false; TriedUrls = $triedUrls; LocalName = $LocalName; FluentName = $FluentName; Size = $Size }
}

# ============================================================================
# MAIN SCRIPT
# ============================================================================

Write-Host "`n========================================" -ForegroundColor Yellow
Write-Host "  PixlPunkt Doc Icon Exporter" -ForegroundColor Yellow  
Write-Host "  Icons extracted from source code" -ForegroundColor Yellow
Write-Host "========================================`n" -ForegroundColor Yellow

# Find Inkscape
Write-Host "Looking for Inkscape..." -ForegroundColor Cyan
$inkscapePath = Find-Inkscape

if (-not $inkscapePath) {
    Write-Host "`n[ERROR] Inkscape not found!" -ForegroundColor Red
    Write-Host "`nInkscape is required for SVG to PNG conversion." -ForegroundColor Yellow
    Write-Host "Install with one of these methods:" -ForegroundColor Yellow
    Write-Host "  winget install Inkscape.Inkscape" -ForegroundColor White
    Write-Host "  choco install inkscape" -ForegroundColor White
    Write-Host "  scoop install inkscape" -ForegroundColor White
    Write-Host "  Or download from: https://inkscape.org/release/`n" -ForegroundColor White
    exit 1
}

Write-Host "Using Inkscape: $inkscapePath`n" -ForegroundColor Green

# Ensure output directory exists
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    Write-Host "Created: $OutputDir" -ForegroundColor Green
}

# Filter icons if specified
$iconsToExport = $IconManifest
if ($IconsOnly) {
    $filterList = $IconsOnly.Split(',') | ForEach-Object { $_.Trim().ToLower() }
    $iconsToExport = @{}
    foreach ($key in $IconManifest.Keys) {
        if ($filterList -contains $key.ToLower()) {
            $iconsToExport[$key] = $IconManifest[$key]
        }
    }
}

Write-Host "Exporting $($iconsToExport.Count) icons at sizes: $($Sizes -join ', ')px`n" -ForegroundColor Cyan

# Export each icon
$successCount = 0
$failCount = 0
$failures = @()

foreach ($localName in ($iconsToExport.Keys | Sort-Object)) {
    $fluentName = $iconsToExport[$localName]
    Write-Host "[$localName] -> $fluentName" -ForegroundColor White
    
    foreach ($size in $Sizes) {
        $result = Download-FluentIcon -LocalName $localName -FluentName $fluentName `
                                       -Size $size -OutputPath $OutputDir -Color $Color `
                                       -InkscapePath $inkscapePath
        
        if ($result.Success) { 
            $successCount++ 
        } else { 
            $failCount++
            $failures += $result
            Write-Host "  FAILED: ${localName}_${size}.png" -ForegroundColor Red
        }
    }
}

# Summary
Write-Host "`n========================================" -ForegroundColor Yellow
Write-Host "  Export Complete!" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "  Success: $successCount" -ForegroundColor Green
if ($failCount -gt 0) {
    Write-Host "  Failed:  $failCount" -ForegroundColor Red
}
Write-Host "  Output:  $OutputDir" -ForegroundColor Cyan

# Show failure details
if ($failures.Count -gt 0) {
    Write-Host "`n========================================" -ForegroundColor Red
    Write-Host "  FAILED ICONS" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    
    # Group failures by icon name
    $groupedFailures = $failures | Group-Object -Property FluentName
    
    foreach ($group in $groupedFailures) {
        $first = $group.Group[0]
        Write-Host "`n[$($first.LocalName)] -> $($first.FluentName)" -ForegroundColor Yellow
        Write-Host "  Sizes attempted: $($group.Group.Size -join ', ')px" -ForegroundColor Gray
        Write-Host "  URLs tried:" -ForegroundColor Gray
        
        # Show unique URLs (dedupe since multiple sizes try same URLs)
        $uniqueUrls = $first.TriedUrls | Select-Object -Unique | Select-Object -First 6
        foreach ($url in $uniqueUrls) {
            Write-Host "    $url" -ForegroundColor DarkGray
        }
        if ($first.TriedUrls.Count -gt 6) {
            Write-Host "    ... and $($first.TriedUrls.Count - 6) more" -ForegroundColor DarkGray
        }
    }
    
    Write-Host "`n[TIP] Check if these icons exist in the Fluent repo:" -ForegroundColor Cyan
    Write-Host "  https://github.com/microsoft/fluentui-system-icons/tree/main/assets" -ForegroundColor White
}

Write-Host "`n"
