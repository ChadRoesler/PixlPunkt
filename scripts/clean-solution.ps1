# clean-solution.ps1
# Cleans all bin and obj folders from the solution



param(
    [string]$SolutionRoot = "..\"
)

$ErrorActionPreference = "Stop"
Write-Host "Cleaning solution..." -ForegroundColor Cyan

# Get the solution root directory (where this script is located)
if (-not $SolutionRoot) {
    $SolutionRoot = Get-Location
}

Write-Host "Solution root: $solutionRoot" -ForegroundColor Gray

# Find and remove all bin and obj folders
$foldersToRemove = @("bin", "obj")

foreach ($folderName in $foldersToRemove) {
    Write-Host "`nSearching for '$folderName' folders..." -ForegroundColor Yellow
    
    $folders = Get-ChildItem -Path $solutionRoot -Directory -Recurse -Filter $folderName -ErrorAction SilentlyContinue
    
    if ($folders) {
        foreach ($folder in $folders) {
            Write-Host "  Removing: $($folder.FullName)" -ForegroundColor DarkGray
            try {
                Remove-Item -Path $folder.FullName -Recurse -Force -ErrorAction Stop
                Write-Host "    Removed" -ForegroundColor Green
            }
            catch {
                Write-Host "    Failed: $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    }
    else {
        Write-Host "  No '$folderName' folders found." -ForegroundColor DarkGray
    }
}

# Also clean common temporary/cache folders
$additionalFolders = @(
    ".vs",
    "packages",
    "TestResults",
    "_ReSharper*",
    "*.user"
)

Write-Host "`nCleaning additional folders/files..." -ForegroundColor Yellow

foreach ($pattern in $additionalFolders) {
    $items = Get-ChildItem -Path $solutionRoot -Directory -Recurse -Filter $pattern -ErrorAction SilentlyContinue
    foreach ($item in $items) {
        Write-Host "  Removing: $($item.FullName)" -ForegroundColor DarkGray
        try {
            Remove-Item -Path $item.FullName -Recurse -Force -ErrorAction Stop
            Write-Host "    ✓ Removed" -ForegroundColor Green
        }
        catch {
            Write-Host "    ✗ Failed: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

Write-Host "\n Clean complete!" -ForegroundColor Green