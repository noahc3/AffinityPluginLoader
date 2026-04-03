#!/bin/bash
# Build AffinityPluginLoader inside a Docker container.
# Usage: ./docker-build.sh [build.sh args...]
# Example: ./docker-build.sh Debug
set -e

ROOTDIR=$(dirname "$(readlink -f "$0")")
IMAGE_NAME="apl-builder"

# Build the Docker image if it doesn't exist (or pass --rebuild to force)
if [ "$1" = "--rebuild" ]; then
    shift
    docker build -t "$IMAGE_NAME" "$ROOTDIR/docker"
elif ! docker image inspect "$IMAGE_NAME" &>/dev/null; then
    echo "Building Docker image '$IMAGE_NAME' (first run)..."
    docker build -t "$IMAGE_NAME" "$ROOTDIR/docker"
fi

docker run --rm \
    -v "$ROOTDIR:/src" \
    -w /src \
    --user "$(id -u):$(id -g)" \
    "$IMAGE_NAME" \
    bash build.sh "$@"
