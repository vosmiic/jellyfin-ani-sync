#!/bin/bash
set -e
set -o pipefail
shopt -s nullglob

# CD to script directory
cd "$(dirname "$0")"
# Store absolute path
SCRIPT_PATH="$(pwd)"

DEST_PATH=$1

if [[ $DEST_PATH == "" ]]; then
    echo "Usage: $0 <path to Ani-Sync or plugins folder> [docker image]"
    exit 1
fi

DOCKER_IMAGE=$2

# If "Ani-Sync" not in path name, try to find it in the given folder
if [[ $DEST_PATH != *"Ani-Sync"* ]]; then
    echo "Searching for Ani-Sync folder in $DEST_PATH"
    cd "$DEST_PATH"
    VERSION_NUM="$(echo Ani-Sync* | sed 's/Ani-Sync_//g' | sed 's/ /\n/g' | sort -V | tac | head -n 1)"
    # If we didn't find anything, just pretend dest is the Ani-Sync folder
    if [[ $VERSION_NUM == "" ]]; then
        echo "No Ani-Sync folder found in $DEST_PATH, assuming this is the Ani-Sync folder"
    else
        DEST_PATH="$DEST_PATH/Ani-Sync_$VERSION_NUM"
        echo "Found Ani-Sync folder at $DEST_PATH"
    fi
fi

# If $DOCKER_IMAGE is not set, build it
if [[ $DOCKER_IMAGE == "" ]]; then
    echo "Building docker image"
    docker build -t jellyfin-ani-sync-build "$SCRIPT_PATH"
    DOCKER_IMAGE="jellyfin-ani-sync-build"
fi

docker run --rm -v "$DEST_PATH":/out $DOCKER_IMAGE

docker rmi $DOCKER_IMAGE