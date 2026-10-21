#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
docker build --progress=plain --target build -t rogue-social-export . >&2
if [ "$#" -eq 0 ]; then
    docker run --rm --entrypoint mono -v "$PWD/docs:/out" rogue-social-export \
        /src/WRogue/bin/Release/RogueSurvivor.exe --export-social-graph /out/social-graph-data.js
elif [ "$#" -eq 2 ]; then
    input_dir=$(cd "$(dirname "$1")" && pwd)
    input_name=$(basename "$1")
    output_dir=$(cd "$(dirname "$2")" && pwd)
    output_name=$(basename "$2")
    docker run --rm --entrypoint mono -v "$input_dir:/in:ro" -v "$output_dir:/out" \
        rogue-social-export /src/WRogue/bin/Release/RogueSurvivor.exe \
        --export-social-graph "/in/$input_name" "/out/$output_name"
else
    echo "Usage: sh tools/social-graph-export.sh [save.dat output.json]" >&2
    exit 2
fi
