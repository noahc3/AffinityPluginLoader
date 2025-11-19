#!/bin/bash
# Package release script for AffinityPluginLoader
# Creates release archives for distribution

set -e

SKIP_BUILD=false
CONFIGURATION="Release"

# Parse arguments
for arg in "$@"; do
    case $arg in
        --skip-build)
            SKIP_BUILD=true
            ;;
        --debug)
            CONFIGURATION="Debug"
            ;;
        *)
            echo "Unknown argument: $arg"
            echo "Usage: $0 [--skip-build] [--debug]"
            exit 1
            ;;
    esac
done

echo "========================================"
echo "AffinityPluginLoader Release Packaging"
echo "========================================"
echo "Configuration: $CONFIGURATION"
echo

# Function to parse version from .csproj file
get_project_version() {
    local csproj_path=$1
    local version=$(grep -oP '<Version>\K[^<]+' "$csproj_path" | head -n 1)

    if [ -z "$version" ]; then
        echo "Error: Could not find Version in $csproj_path" >&2
        exit 1
    fi

    echo "$version"
}

# Build everything if not skipping
if [ "$SKIP_BUILD" = false ]; then
    echo "[1/4] Building all projects..."
    if [ "$CONFIGURATION" = "Debug" ]; then
        bash build.sh Debug
    else
        bash build.sh
    fi
    echo
else
    echo "[1/4] Skipping build (using existing binaries)..."
    echo
fi

# Parse versions
APL_VERSION=$(get_project_version "AffinityPluginLoader/AffinityPluginLoader.csproj")
WINEFIX_VERSION=$(get_project_version "WineFix/WineFix.csproj")

echo "AffinityPluginLoader version: $APL_VERSION"
echo "WineFix version: $WINEFIX_VERSION"
echo

# Create output directory
OUTPUT_DIR="releases"
if [ -d "$OUTPUT_DIR" ]; then
    rm -rf "$OUTPUT_DIR"
fi
mkdir -p "$OUTPUT_DIR"

# Package AffinityPluginLoader
echo "[2/4] Packaging affinitypluginloader-v$APL_VERSION.zip..."
APL_TEMP="$OUTPUT_DIR/apl_temp"
mkdir -p "$APL_TEMP"

# Copy files for AffinityPluginLoader package
cp "AffinityPluginLoader/bin/x64/$CONFIGURATION/net48/win-x64/0Harmony.dll" "$APL_TEMP/"
cp "AffinityBootstrap/build/AffinityBootstrap.dll" "$APL_TEMP/"
cp "AffinityHook/bin/x64/$CONFIGURATION/net48/win-x64/AffinityHook.exe" "$APL_TEMP/"
cp "AffinityPluginLoader/bin/x64/$CONFIGURATION/net48/win-x64/AffinityPluginLoader.dll" "$APL_TEMP/"
cp "README.md" "$APL_TEMP/"
cp "AffinityPluginLoader/LICENSE" "$APL_TEMP/"

# Create zip
(cd "$APL_TEMP" && zip -q -r "../affinitypluginloader-v$APL_VERSION.zip" *)
rm -rf "$APL_TEMP"
echo "Created: releases/affinitypluginloader-v$APL_VERSION.zip"
echo

# Package WineFix
echo "[3/4] Packaging winefix-v$WINEFIX_VERSION.zip..."
WINEFIX_TEMP="$OUTPUT_DIR/winefix_temp"
mkdir -p "$WINEFIX_TEMP/plugins"

# Copy files for WineFix package
cp "README.md" "$WINEFIX_TEMP/"
cp "WineFix/LICENSE" "$WINEFIX_TEMP/"
cp "WineFix/bin/x64/$CONFIGURATION/net48/win-x64/WineFix.dll" "$WINEFIX_TEMP/plugins/"

# Copy d2d1.dll (Wine native) if it exists
if [ -f "WineFix/lib/d2d1/build/x86_64-unix/d2d1.dll.so" ]; then
    cp "WineFix/lib/d2d1/build/x86_64-unix/d2d1.dll.so" "$WINEFIX_TEMP/d2d1.dll"
    echo "Included d2d1.dll (Wine native)"
else
    echo "Warning: d2d1.dll.so not found. Skipping d2d1.dll in WineFix package."
fi

# Create zip
(cd "$WINEFIX_TEMP" && zip -q -r "../winefix-v$WINEFIX_VERSION.zip" *)
rm -rf "$WINEFIX_TEMP"
echo "Created: releases/winefix-v$WINEFIX_VERSION.zip"
echo

# Package combined archive (tar.xz)
echo "[4/4] Packaging affinitypluginloader-plus-winefix.tar.xz..."
COMBINED_TEMP="$OUTPUT_DIR/combined_temp"
mkdir -p "$COMBINED_TEMP/plugins"

# Copy files for combined package
cp "AffinityPluginLoader/bin/x64/$CONFIGURATION/net48/win-x64/0Harmony.dll" "$COMBINED_TEMP/"
cp "AffinityBootstrap/build/AffinityBootstrap.dll" "$COMBINED_TEMP/"
cp "AffinityHook/bin/x64/$CONFIGURATION/net48/win-x64/AffinityHook.exe" "$COMBINED_TEMP/"
cp "AffinityPluginLoader/bin/x64/$CONFIGURATION/net48/win-x64/AffinityPluginLoader.dll" "$COMBINED_TEMP/"
cp "WineFix/bin/x64/$CONFIGURATION/net48/win-x64/WineFix.dll" "$COMBINED_TEMP/plugins/"

# Copy d2d1.dll (Wine native) if it exists
if [ -f "WineFix/lib/d2d1/build/x86_64-unix/d2d1.dll.so" ]; then
    cp "WineFix/lib/d2d1/build/x86_64-unix/d2d1.dll.so" "$COMBINED_TEMP/d2d1.dll"
    echo "Included d2d1.dll (Wine native) in combined package"
else
    echo "Warning: d2d1.dll.so not found. Skipping d2d1.dll in combined package."
fi

# Create tar.xz
tar -C "$COMBINED_TEMP" -cJf "$OUTPUT_DIR/affinitypluginloader-plus-winefix.tar.xz" .
rm -rf "$COMBINED_TEMP"
echo "Created: releases/affinitypluginloader-plus-winefix.tar.xz"
echo

echo "========================================"
echo "Release packaging completed!"
echo "========================================"
echo "Output directory: releases/"
