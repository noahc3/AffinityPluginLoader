#!/bin/bash
# Build and deploy AffinityPluginLoader to the Affinity install directory.
# Usage:
#   ./deploy.sh                        # Build in Docker and deploy
#   ./deploy.sh --skip-build           # Deploy existing build
#   ./deploy.sh --set-affinity-path /path/to/affinity  # Save install path
set -e

ROOTDIR=$(dirname "$(readlink -f "$0")")
PATH_FILE="$ROOTDIR/.AFFINITY_PATH"
SKIP_BUILD=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --set-affinity-path)
            echo "$2" > "$PATH_FILE"
            echo "Affinity path saved: $2"
            exit 0
            ;;
        --skip-build)
            SKIP_BUILD=true
            shift
            ;;
        *)
            echo "Usage: $0 [--skip-build] [--set-affinity-path <path>]"
            exit 1
            ;;
    esac
done

if [ ! -f "$PATH_FILE" ]; then
    echo "Error: Affinity install path not set."
    echo "Run: $0 --set-affinity-path /path/to/affinity"
    exit 1
fi

AFFINITY_PATH=$(cat "$PATH_FILE")

if [ ! -d "$AFFINITY_PATH" ]; then
    echo "Error: Affinity path does not exist: $AFFINITY_PATH"
    exit 1
fi

if [ "$SKIP_BUILD" = false ]; then
    bash "$ROOTDIR/package-release.sh" --docker --debug
fi

ARCHIVE="$ROOTDIR/releases/affinitypluginloader-plus-winefix.tar.xz"

if [ ! -f "$ARCHIVE" ]; then
    echo "Error: Archive not found: $ARCHIVE"
    exit 1
fi

echo "Deploying to: $AFFINITY_PATH"
tar -xJf "$ARCHIVE" -C "$AFFINITY_PATH"
echo "Done."
