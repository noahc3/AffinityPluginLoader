#!/bin/bash
# Build d2d1.dll for all supported targets

set -e

echo "======================================"
echo "D2D1.DLL - Build All Targets"
echo "======================================"
echo ""

# Function to build a target
build_target() {
    local target=$1
    echo "Building for: $target"
    echo "--------------------------------------"
    make TARGET=$target
    echo ""
}

# Check for required tools
check_tools() {
    echo "Checking for required tools..."
    local missing_tools=()

    for tool in x86_64-w64-mingw32-gcc i686-w64-mingw32-gcc widl; do
        if ! command -v $tool &> /dev/null; then
            missing_tools+=($tool)
        fi
    done

    if [ ${#missing_tools[@]} -ne 0 ]; then
        echo "ERROR: Missing required tools:"
        for tool in "${missing_tools[@]}"; do
            echo "  - $tool"
        done
        echo ""
        echo "Please install:"
        echo "  - mingw-w64 toolchain (for cross-compilation)"
        echo "  - wine-devel or wine-tools (for widl)"
        exit 1
    fi

    echo "All required tools found!"
    echo ""
}

# Check tools first
check_tools

# Clean all build artifacts first
echo "Cleaning all build artifacts..."
make clean
echo ""

# Build all targets
build_target "x86_64-windows"
build_target "i386-windows"

# Try to build Unix PE if winegcc is available
if command -v winegcc &> /dev/null; then
    echo "Building for: x86_64-unix"
    echo "--------------------------------------"
    if make TARGET=x86_64-unix; then
        echo ""
    else
        echo "Warning: x86_64-unix build failed (this is optional)"
        echo ""
    fi
else
    echo "Skipping x86_64-unix: winegcc not found"
    echo "Install wine-devel to build Unix PE format"
    echo ""
fi

# Build SysWoW64
build_target "syswow64"

echo "======================================"
echo "Build Summary"
echo "======================================"
echo "Built DLLs:"
find build -name "*.dll*" -type f
echo ""
echo "All builds complete!"
