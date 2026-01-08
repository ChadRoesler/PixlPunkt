#############################################################################
# PixlPunkt Windows Installer (PowerShell)
# Installs the portable app and registers file associations
#############################################################################

param(
    [switch]$Uninstall,
    [string]$InstallPath = "$env:LOCALAPPDATA\PixlPunkt"
)

$ErrorActionPreference = "Stop"

$AppName = "PixlPunkt"
$ExeName = "PixlPunkt.Uno.exe"
$Publisher = "PixlPunkt"

# File type associations
$FileTypes = @(
    @{ Extension = ".pxp";  ProgId = "PixlPunkt.Document";      Description = "PixlPunkt Document" },
    @{ Extension = ".pxpr"; ProgId = "PixlPunkt.AnimationReel"; Description = "PixlPunkt Animation Reel" },
    @{ Extension = ".pxpt"; ProgId = "PixlPunkt.Tileset";       Description = "PixlPunkt Tileset" },
    @{ Extension = ".pbx";  ProgId = "PixlPunkt.Brush";         Description = "PixlPunkt Brush" }
)

function Install-PixlPunkt {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "     PixlPunkt Installer" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

    # Get script directory (where the app files are)
    $SourceDir = $PSScriptRoot

    # Create installation directory
    Write-Host "[1/4] Creating installation directory..." -ForegroundColor Yellow
    if (!(Test-Path $InstallPath)) {
        New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
    }

    # Copy application files
    Write-Host "[2/4] Copying application files..." -ForegroundColor Yellow
    Get-ChildItem -Path $SourceDir -Exclude "Install-PixlPunkt.ps1", "Uninstall-PixlPunkt.ps1" | 
        Copy-Item -Destination $InstallPath -Recurse -Force

    $ExePath = Join-Path $InstallPath $ExeName

    if (!(Test-Path $ExePath)) {
        Write-Host "Error: $ExeName not found in source directory!" -ForegroundColor Red
        exit 1
    }

    # Register file associations
    Write-Host "[3/4] Registering file associations..." -ForegroundColor Yellow
    
    foreach ($ft in $FileTypes) {
        $ext = $ft.Extension
        $progId = $ft.ProgId
        $desc = $ft.Description

        # Create ProgId
        $progIdPath = "HKCU:\Software\Classes\$progId"
        New-Item -Path $progIdPath -Force | Out-Null
        Set-ItemProperty -Path $progIdPath -Name "(Default)" -Value $desc
        
        # Create shell\open\command
        $commandPath = "$progIdPath\shell\open\command"
        New-Item -Path $commandPath -Force | Out-Null
        Set-ItemProperty -Path $commandPath -Name "(Default)" -Value "`"$ExePath`" `"%1`""

        # Set default icon
        $iconPath = "$progIdPath\DefaultIcon"
        New-Item -Path $iconPath -Force | Out-Null
        Set-ItemProperty -Path $iconPath -Name "(Default)" -Value "`"$ExePath`",0"

        # Associate extension with ProgId
        $extPath = "HKCU:\Software\Classes\$ext"
        New-Item -Path $extPath -Force | Out-Null
        Set-ItemProperty -Path $extPath -Name "(Default)" -Value $progId

        Write-Host "      Registered $ext -> $desc" -ForegroundColor DarkGray
    }

    # Create Start Menu shortcut
    Write-Host "[4/4] Creating Start Menu shortcut..." -ForegroundColor Yellow
    $StartMenuPath = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs"
    $ShortcutPath = Join-Path $StartMenuPath "PixlPunkt.lnk"
    
    $WshShell = New-Object -ComObject WScript.Shell
    $Shortcut = $WshShell.CreateShortcut($ShortcutPath)
    $Shortcut.TargetPath = $ExePath
    $Shortcut.WorkingDirectory = $InstallPath
    $Shortcut.Description = "PixlPunkt - Pixel Art Editor"
    $Shortcut.Save()

    # Notify shell of changes
    $code = @"
[System.Runtime.InteropServices.DllImport("shell32.dll")]
public static extern void SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);
"@
    $shell = Add-Type -MemberDefinition $code -Name "Shell32" -Namespace "Win32" -PassThru
    $shell::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Installation Complete!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  PixlPunkt has been installed to:"
    Write-Host "  $InstallPath" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  You can now:"
    Write-Host "  - Launch from Start Menu"
    Write-Host "  - Double-click .pxp files to open"
    Write-Host ""
}

function Uninstall-PixlPunkt {
    Write-Host ""
    Write-Host "Uninstalling PixlPunkt..." -ForegroundColor Yellow
    Write-Host ""

    # Remove file associations
    foreach ($ft in $FileTypes) {
        $ext = $ft.Extension
        $progId = $ft.ProgId

        Remove-Item -Path "HKCU:\Software\Classes\$progId" -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -Path "HKCU:\Software\Classes\$ext" -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "  Removed $ext association" -ForegroundColor DarkGray
    }

    # Remove Start Menu shortcut
    $ShortcutPath = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\PixlPunkt.lnk"
    if (Test-Path $ShortcutPath) {
        Remove-Item $ShortcutPath -Force
        Write-Host "  Removed Start Menu shortcut" -ForegroundColor DarkGray
    }

    # Remove installation directory
    if (Test-Path $InstallPath) {
        Remove-Item -Path $InstallPath -Recurse -Force
        Write-Host "  Removed installation directory" -ForegroundColor DarkGray
    }

    # Notify shell of changes
    $code = @"
[System.Runtime.InteropServices.DllImport("shell32.dll")]
public static extern void SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);
"@
    $shell = Add-Type -MemberDefinition $code -Name "Shell32" -Namespace "Win32" -PassThru
    $shell::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)

    Write-Host ""
    Write-Host "PixlPunkt uninstalled successfully." -ForegroundColor Green
    Write-Host ""
}

# Main
if ($Uninstall) {
    Uninstall-PixlPunkt
} else {
    Install-PixlPunkt
}
