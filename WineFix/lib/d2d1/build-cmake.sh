#!/bin/bash
# Build d2d1.dll using CMake

set -e

TARGET_ARCH="${1:-x86_64}"
BUILD_TYPE="${2:-Release}"

echo "======================================"
echo "D2D1.DLL - CMake Build"
echo "======================================"
echo "Architecture: $TARGET_ARCH"
echo "Build Type: $BUILD_TYPE"
echo ""

# Create build directory
BUILD_DIR="build-cmake-${TARGET_ARCH}"
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

# Configure with CMake
echo "Configuring..."
cmake .. \
    -DCMAKE_BUILD_TYPE=$BUILD_TYPE \
    -DTARGET_ARCH=$TARGET_ARCH

# Build
echo ""
echo "Building..."
cmake --build . -- -j$(nproc)

echo ""
echo "======================================"
echo "Build complete!"
echo "Output: $BUILD_DIR/d2d1.dll"
echo "======================================"
