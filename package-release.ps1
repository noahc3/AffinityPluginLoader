# AffinityPluginLoader Release Packager
# Builds clean distribution packages from Release build output

param(
    [string]$Version = "0.1.0.1",
    [switch]$IncludeSymbols = $false
)

$ErrorActionPreference = "Stop"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host " AffinityPluginLoader Release Packager" -ForegroundColor Cyan
Write-Host " Version: $Version" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host

# Paths
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$affinityHookBin = Join-Path $scriptDir "AffinityHook\bin\x64\Release"
$pluginLoaderBin = Join-Path $scriptDir "AffinityPluginLoader\bin\x64\Release"
$wineFixBin = Join-Path $scriptDir "WineFix\bin\x64\Release"
$bootstrapBin = Join-Path $scriptDir "AffinityBootstrap\bin\x64\Release"

# Verify build outputs exist
Write-Host "Verifying build outputs..." -ForegroundColor Yellow
if (-not (Test-Path $affinityHookBin)) {
    Write-Error "AffinityHook Release build not found at: $affinityHookBin`nBuild the solution in x64 Release configuration first."
    exit 1
}

if (-not (Test-Path $pluginLoaderBin)) {
    Write-Error "AffinityPluginLoader Release build not found at: $pluginLoaderBin`nBuild the solution in x64 Release configuration first."
    exit 1
}

if (-not (Test-Path $bootstrapBin)) {
    Write-Error "AffinityBootstrap Release build not found at: $bootstrapBin`nBuild AffinityBootstrap first."
    exit 1
}

# ================================================
# Package 1: AffinityPluginLoader
# ================================================
Write-Host
Write-Host "================================================" -ForegroundColor Green
Write-Host " Building AffinityPluginLoader Package" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Green

$loaderOutDir = Join-Path $scriptDir "affinitypluginloader-v$Version-x64"
$loaderZipPath = Join-Path $scriptDir "affinitypluginloader-v$Version-x64.zip"

# Clean output directory
Write-Host "[1/4] Cleaning output directory..." -ForegroundColor Yellow
if (Test-Path $loaderOutDir) {
    Remove-Item $loaderOutDir -Recurse -Force
}
New-Item $loaderOutDir -ItemType Directory | Out-Null

# Copy core files
Write-Host "[2/4] Copying core files..." -ForegroundColor Yellow
Copy-Item (Join-Path $affinityHookBin "AffinityHook.exe") $loaderOutDir
Copy-Item (Join-Path $pluginLoaderBin "AffinityPluginLoader.dll") $loaderOutDir
Copy-Item (Join-Path $pluginLoaderBin "0Harmony.dll") $loaderOutDir
Copy-Item (Join-Path $bootstrapBin "AffinityBootstrap.dll") $loaderOutDir

# Optional: Copy debug symbols
if ($IncludeSymbols) {
    Write-Host "    Including debug symbols..." -ForegroundColor Gray
    Copy-Item (Join-Path $affinityHookBin "AffinityHook.pdb") $loaderOutDir -ErrorAction SilentlyContinue
    Copy-Item (Join-Path $pluginLoaderBin "AffinityPluginLoader.pdb") $loaderOutDir -ErrorAction SilentlyContinue
    Copy-Item (Join-Path $pluginLoaderBin "0Harmony.pdb") $loaderOutDir -ErrorAction SilentlyContinue
}

# Copy documentation
Write-Host "[3/4] Copying documentation..." -ForegroundColor Yellow
if (Test-Path (Join-Path $scriptDir "README.md")) {
    Copy-Item (Join-Path $scriptDir "README.md") $loaderOutDir
}
if (Test-Path (Join-Path $scriptDir "LICENSE")) {
    Copy-Item (Join-Path $scriptDir "LICENSE") $loaderOutDir
}

# Create ZIP archive
Write-Host "[4/4] Creating ZIP archive..." -ForegroundColor Yellow
if (Test-Path $loaderZipPath) {
    Remove-Item $loaderZipPath -Force
}
Compress-Archive -Path "$loaderOutDir\*" -DestinationPath $loaderZipPath -CompressionLevel Optimal

# Summary
Write-Host
Write-Host "AffinityPluginLoader Package Created!" -ForegroundColor Green
Write-Host "Location: $loaderZipPath" -ForegroundColor White
Write-Host
Write-Host "Package Contents:" -ForegroundColor Cyan
Get-ChildItem $loaderOutDir -File | ForEach-Object {
    $size = "{0:N2} KB" -f ($_.Length / 1KB)
    Write-Host "  $($_.Name)" -NoNewline -ForegroundColor Gray
    Write-Host " ($size)" -ForegroundColor DarkGray
}

$loaderSize = (Get-ChildItem $loaderOutDir -File | Measure-Object -Property Length -Sum).Sum
Write-Host
Write-Host "Total Size: " -NoNewline
Write-Host ("{0:N2} MB" -f ($loaderSize / 1MB)) -ForegroundColor Yellow

# ================================================
# Package 2: WineFix Plugin
# ================================================
Write-Host
Write-Host "================================================" -ForegroundColor Green
Write-Host " Building WineFix Plugin Package" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Green

if (-not (Test-Path $wineFixBin)) {
    Write-Host "WARNING: WineFix not found at $wineFixBin" -ForegroundColor Yellow
    Write-Host "Skipping WineFix package..." -ForegroundColor Yellow
} else {
    $wineFixOutDir = Join-Path $scriptDir "winefix-v$Version-x64"
    $wineFixZipPath = Join-Path $scriptDir "winefix-v$Version-x64.zip"

    # Clean output directory
    Write-Host "[1/3] Cleaning output directory..." -ForegroundColor Yellow
    if (Test-Path $wineFixOutDir) {
        Remove-Item $wineFixOutDir -Recurse -Force
    }
    New-Item $wineFixOutDir -ItemType Directory | Out-Null
    New-Item (Join-Path $wineFixOutDir "plugins") -ItemType Directory | Out-Null

    # Copy WineFix plugin
    Write-Host "[2/3] Copying WineFix plugin..." -ForegroundColor Yellow
    Copy-Item (Join-Path $wineFixBin "WineFix.dll") (Join-Path $wineFixOutDir "plugins\")
    
    if ($IncludeSymbols) {
        Write-Host "    Including debug symbols..." -ForegroundColor Gray
        Copy-Item (Join-Path $wineFixBin "WineFix.pdb") (Join-Path $wineFixOutDir "plugins\") -ErrorAction SilentlyContinue
    }

    # Create ZIP archive
    Write-Host "[3/3] Creating ZIP archive..." -ForegroundColor Yellow
    if (Test-Path $wineFixZipPath) {
        Remove-Item $wineFixZipPath -Force
    }
    Compress-Archive -Path "$wineFixOutDir\*" -DestinationPath $wineFixZipPath -CompressionLevel Optimal

    # Summary
    Write-Host
    Write-Host "WineFix Plugin Package Created!" -ForegroundColor Green
    Write-Host "Location: $wineFixZipPath" -ForegroundColor White
    Write-Host
    Write-Host "Package Contents:" -ForegroundColor Cyan
    Get-ChildItem $wineFixOutDir -Recurse -File | ForEach-Object {
        $relativePath = $_.FullName.Substring($wineFixOutDir.Length + 1)
        $size = "{0:N2} KB" -f ($_.Length / 1KB)
        Write-Host "  $relativePath" -NoNewline -ForegroundColor Gray
        Write-Host " ($size)" -ForegroundColor DarkGray
    }

    $wineFixSize = (Get-ChildItem $wineFixOutDir -Recurse -File | Measure-Object -Property Length -Sum).Sum
    Write-Host
    Write-Host "Total Size: " -NoNewline
    Write-Host ("{0:N2} MB" -f ($wineFixSize / 1MB)) -ForegroundColor Yellow
}

# ================================================
# Final Summary
# ================================================
Write-Host
Write-Host "================================================" -ForegroundColor Cyan
Write-Host " All Packages Created Successfully!" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host
Write-Host "Packages created:" -ForegroundColor White
Write-Host "  1. $loaderZipPath" -ForegroundColor Green
if (Test-Path $wineFixBin) {
    Write-Host "  2. $wineFixZipPath" -ForegroundColor Green
}
Write-Host