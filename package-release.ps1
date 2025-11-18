#!/usr/bin/env pwsh
# Package release script for AffinityPluginLoader
# Creates release archives for distribution

param(
    [switch]$SkipBuild,
    [switch]$Debug
)

$ErrorActionPreference = "Stop"

$Configuration = if ($Debug) { "Debug" } else { "Release" }

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "AffinityPluginLoader Release Packaging" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Green
Write-Host

# Function to parse version from .csproj file
function Get-ProjectVersion {
    param([string]$csprojPath)

    [xml]$csproj = Get-Content $csprojPath
    $version = $csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

    if (-not $version) {
        throw "Could not find Version in $csprojPath"
    }

    return $version
}

# Build everything if not skipping
if (-not $SkipBuild) {
    Write-Host "[1/4] Building all projects..." -ForegroundColor Yellow
    if ($Debug) {
        & .\build.bat Debug
    } else {
        & .\build.bat
    }
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed"
    }
    Write-Host
} else {
    Write-Host "[1/4] Skipping build (using existing binaries)..." -ForegroundColor Yellow
    Write-Host
}

# Parse versions
$apl_version = Get-ProjectVersion "AffinityPluginLoader\AffinityPluginLoader.csproj"
$winefix_version = Get-ProjectVersion "WineFix\WineFix.csproj"

Write-Host "AffinityPluginLoader version: $apl_version" -ForegroundColor Green
Write-Host "WineFix version: $winefix_version" -ForegroundColor Green
Write-Host

# Create output directory
$output_dir = "releases"
if (Test-Path $output_dir) {
    Remove-Item $output_dir -Recurse -Force
}
New-Item -ItemType Directory -Path $output_dir | Out-Null

# Package AffinityPluginLoader
Write-Host "[2/4] Packaging affinitypluginloader-v$apl_version.zip..." -ForegroundColor Yellow
$apl_temp = "releases\apl_temp"
New-Item -ItemType Directory -Path $apl_temp | Out-Null

# Copy files for AffinityPluginLoader package
Copy-Item "AffinityPluginLoader\bin\$Configuration\net48\win-x64\0Harmony.dll" $apl_temp
Copy-Item "AffinityBootstrap\build\AffinityBootstrap.dll" $apl_temp
Copy-Item "AffinityHook\bin\$Configuration\net48\win-x64\AffinityHook.exe" $apl_temp
Copy-Item "AffinityPluginLoader\bin\$Configuration\net48\win-x64\AffinityPluginLoader.dll" $apl_temp
Copy-Item "README.md" $apl_temp
Copy-Item "AffinityPluginLoader\LICENSE" $apl_temp

# Create zip
Compress-Archive -Path "$apl_temp\*" -DestinationPath "releases\affinitypluginloader-v$apl_version.zip" -Force
Remove-Item $apl_temp -Recurse -Force
Write-Host "Created: releases\affinitypluginloader-v$apl_version.zip" -ForegroundColor Green
Write-Host

# Package WineFix
Write-Host "[3/4] Packaging winefix-v$winefix_version.zip..." -ForegroundColor Yellow
$winefix_temp = "releases\winefix_temp"
New-Item -ItemType Directory -Path $winefix_temp | Out-Null
New-Item -ItemType Directory -Path "$winefix_temp\plugins" | Out-Null

# Copy files for WineFix package
Copy-Item "README.md" $winefix_temp
Copy-Item "WineFix\LICENSE" $winefix_temp
Copy-Item "WineFix\bin\$Configuration\net48\win-x64\WineFix.dll" "$winefix_temp\plugins\"

# Create zip
Compress-Archive -Path "$winefix_temp\*" -DestinationPath "releases\winefix-v$winefix_version.zip" -Force
Remove-Item $winefix_temp -Recurse -Force
Write-Host "Created: releases\winefix-v$winefix_version.zip" -ForegroundColor Green
Write-Host

# Package combined archive (tar.xz)
Write-Host "[4/4] Packaging affinitypluginloader-plus-winefix.tar.xz..." -ForegroundColor Yellow
$combined_temp = "releases\combined_temp"
New-Item -ItemType Directory -Path $combined_temp | Out-Null
New-Item -ItemType Directory -Path "$combined_temp\plugins" | Out-Null

# Copy files for combined package
Copy-Item "AffinityPluginLoader\bin\$Configuration\net48\win-x64\0Harmony.dll" $combined_temp
Copy-Item "AffinityBootstrap\build\AffinityBootstrap.dll" $combined_temp
Copy-Item "AffinityHook\bin\$Configuration\net48\win-x64\AffinityHook.exe" $combined_temp
Copy-Item "AffinityPluginLoader\bin\$Configuration\net48\win-x64\AffinityPluginLoader.dll" $combined_temp
Copy-Item "WineFix\bin\$Configuration\net48\win-x64\WineFix.dll" "$combined_temp\plugins\"

# Create tar.xz (requires tar command, available in Windows 10+)
$tar_path = "releases\affinitypluginloader-plus-winefix.tar"
$xz_path = "releases\affinitypluginloader-plus-winefix.tar.xz"

Push-Location $combined_temp
tar -cf "..\affinitypluginloader-plus-winefix.tar" *
Pop-Location

# Try to compress with xz if available, otherwise use 7z, otherwise just keep as tar
$compressed = $false
if (Get-Command xz -ErrorAction SilentlyContinue) {
    xz -z $tar_path
    $compressed = $true
    Write-Host "Created: releases\affinitypluginloader-plus-winefix.tar.xz (xz)" -ForegroundColor Green
} elseif (Get-Command 7z -ErrorAction SilentlyContinue) {
    7z a -txz $xz_path $tar_path | Out-Null
    Remove-Item $tar_path
    $compressed = $true
    Write-Host "Created: releases\affinitypluginloader-plus-winefix.tar.xz (7z)" -ForegroundColor Green
} else {
    Write-Host "Warning: xz and 7z not found, keeping as .tar file" -ForegroundColor Yellow
    Write-Host "Created: releases\affinitypluginloader-plus-winefix.tar" -ForegroundColor Green
}

Remove-Item $combined_temp -Recurse -Force
Write-Host

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Release packaging completed!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Output directory: releases\" -ForegroundColor Green
