#!/bin/bash
# Master build script for AffinityPluginLoader
# Builds both .NET assemblies and native AffinityBootstrap.dll

set -e

echo "========================================"
echo "Building AffinityPluginLoader"
echo "========================================"
echo

# Build .NET projects
echo "[1/2] Building .NET projects..."
dotnet build -c Release
echo

# Build AffinityBootstrap
echo "[2/2] Building AffinityBootstrap..."
cd AffinityBootstrap
bash build.sh
cd ..

echo
echo "========================================"
echo "Build completed successfully!"
echo "========================================"
