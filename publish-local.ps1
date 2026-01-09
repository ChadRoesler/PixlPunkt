#############################################################################
# PixlPunkt Local Build & Publish Script
#############################################################################
# Builds and publishes PixlPunkt for local testing on various platforms.
#
# Usage:
#   .\publish-local.ps1                    # Build for current platform
#   .\publish-local.ps1 -Platform win      # Windows x64 (Skia)
#   .\publish-local.ps1 -Platform linux    # Linux x64
#   .\publish-local.ps1 -Platform osx      # macOS x64
#   .\publish-local.ps1 -Platform osx-arm  # macOS ARM64
#   .\publish-local.ps1 -Platform winappsdk # WinAppSdk (native Windows)
#   .\publish-local.ps1 -Platform all      # All platforms
#   .\publish-local.ps1 -Run               # Build and run immediately
#############################################################################

param(
    [ValidateSet("win", "linux", "osx", "osx-arm", "winappsdk", "all")]
    [string]$Platform = "win",
    
    [switch]$Run,
    [switch]$Debug,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$ProjectDir = "$PSScriptRoot\PixlPunkt.Uno\PixlPunkt.Uno"
$ProjectFile = "$ProjectDir\PixlPunkt.Uno.csproj"
$PublishBase = "$ProjectDir\bin\publish"

$Configuration = if ($Debug) { "Debug" } else { "Release" }

Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "?         PixlPunkt Local Build & Publish Script                ?" -ForegroundColor Cyan
Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host ""

# Clean if requested
if ($Clean) {
    Write-Host "?? Cleaning publish directory..." -ForegroundColor Yellow
    if (Test-Path $PublishBase) {
        Remove-Item -Path $PublishBase -Recurse -Force
    }
    Write-Host "? Clean complete" -ForegroundColor Green
    Write-Host ""
}

function Publish-Platform {
    param(
        [string]$Name,
        [string]$RuntimeId,
        [string]$Framework,
        [string]$OutputDir,
        [hashtable]$ExtraProps = @{}
    )
    
    Write-Host "???????????????????????????????????????????????????????????????" -ForegroundColor Gray
    Write-Host "?? Building: $Name" -ForegroundColor Cyan
    Write-Host "   Runtime: $RuntimeId" -ForegroundColor Gray
    Write-Host "   Framework: $Framework" -ForegroundColor Gray
    Write-Host "   Output: $OutputDir" -ForegroundColor Gray
    Write-Host ""
    
    $props = @(
        "dotnet", "publish", $ProjectFile,
        "--configuration", $Configuration,
        "--framework", $Framework,
        "--runtime", $RuntimeId,
        "--self-contained", "true",
        "--output", $OutputDir,
        "-p:PublishReadyToRun=true",
        "-p:PublishSingleFile=false"
    )
    
    foreach ($key in $ExtraProps.Keys) {
        $props += "-p:$key=$($ExtraProps[$key])"
    }
    
    $startTime = Get-Date
    
    try {
        & $props[0] $props[1..($props.Length-1)]
        
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed with exit code $LASTEXITCODE"
        }
        
        $elapsed = (Get-Date) - $startTime
        Write-Host ""
        Write-Host "? $Name built successfully in $($elapsed.TotalSeconds.ToString('F1'))s" -ForegroundColor Green
        Write-Host "  Output: $OutputDir" -ForegroundColor Gray
        
        return $OutputDir
    }
    catch {
        Write-Host "? $Name build failed: $_" -ForegroundColor Red
        return $null
    }
}

$built = @()

# Windows Skia Desktop
if ($Platform -eq "win" -or $Platform -eq "all") {
    $out = Publish-Platform `
        -Name "Windows x64 (Skia Desktop)" `
        -RuntimeId "win-x64" `
        -Framework "net10.0-desktop" `
        -OutputDir "$PublishBase\win-x64" `
        -ExtraProps @{ "SkiaOnly" = "true" }
    
    if ($out) { $built += @{ Name = "Windows Skia"; Path = $out; Exe = "PixlPunkt.Uno.exe" } }
}

# WinAppSdk (Native Windows)
if ($Platform -eq "winappsdk" -or $Platform -eq "all") {
    $out = Publish-Platform `
        -Name "Windows x64 (WinAppSdk)" `
        -RuntimeId "win-x64" `
        -Framework "net10.0-windows10.0.26100" `
        -OutputDir "$PublishBase\winappsdk-x64" `
        -ExtraProps @{ "WinAppSdkOnly" = "true"; "WindowsPackageType" = "None" }
    
    if ($out) { $built += @{ Name = "WinAppSdk"; Path = $out; Exe = "PixlPunkt.Uno.exe" } }
}

# Linux x64
if ($Platform -eq "linux" -or $Platform -eq "all") {
    $out = Publish-Platform `
        -Name "Linux x64" `
        -RuntimeId "linux-x64" `
        -Framework "net10.0-desktop" `
        -OutputDir "$PublishBase\linux-x64" `
        -ExtraProps @{ "SkiaOnly" = "true" }
    
    if ($out) { $built += @{ Name = "Linux"; Path = $out; Exe = "PixlPunkt.Uno" } }
}

# macOS x64 (Intel)
if ($Platform -eq "osx" -or $Platform -eq "all") {
    $out = Publish-Platform `
        -Name "macOS x64 (Intel)" `
        -RuntimeId "osx-x64" `
        -Framework "net10.0-desktop" `
        -OutputDir "$PublishBase\osx-x64" `
        -ExtraProps @{ "SkiaOnly" = "true" }
    
    if ($out) { $built += @{ Name = "macOS Intel"; Path = $out; Exe = "PixlPunkt.Uno" } }
}

# macOS ARM64 (Apple Silicon)
if ($Platform -eq "osx-arm" -or $Platform -eq "all") {
    $out = Publish-Platform `
        -Name "macOS ARM64 (Apple Silicon)" `
        -RuntimeId "osx-arm64" `
        -Framework "net10.0-desktop" `
        -OutputDir "$PublishBase\osx-arm64" `
        -ExtraProps @{ "SkiaOnly" = "true" }
    
    if ($out) { $built += @{ Name = "macOS ARM"; Path = $out; Exe = "PixlPunkt.Uno" } }
}

Write-Host ""
Write-Host "???????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "                        BUILD SUMMARY" -ForegroundColor Cyan
Write-Host "???????????????????????????????????????????????????????????????" -ForegroundColor Cyan

if ($built.Count -eq 0) {
    Write-Host "No builds completed successfully." -ForegroundColor Red
    exit 1
}

foreach ($b in $built) {
    Write-Host "? $($b.Name): $($b.Path)" -ForegroundColor Green
}

Write-Host ""
Write-Host "Publish outputs are in: $PublishBase" -ForegroundColor Gray

# Run if requested
if ($Run -and $built.Count -gt 0) {
    $toRun = $built[0]
    $exePath = Join-Path $toRun.Path $toRun.Exe
    
    Write-Host ""
    Write-Host "?? Launching $($toRun.Name)..." -ForegroundColor Cyan
    
    if (Test-Path $exePath) {
        Start-Process -FilePath $exePath
    }
    else {
        Write-Host "? Executable not found: $exePath" -ForegroundColor Red
    }
}

Write-Host ""
