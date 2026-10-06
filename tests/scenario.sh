#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
printf 'Building scenario image\n' >&2
docker build --progress=plain --target scenarios -t rogue-survivor-scenarios . >&2
printf 'Running scenario: %s\n' "$*" >&2
exec docker run --rm rogue-survivor-scenarios "$@"
