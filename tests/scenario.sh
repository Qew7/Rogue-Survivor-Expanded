#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
docker build --target scenarios -t rogue-survivor-scenarios . >&2
exec docker run --rm rogue-survivor-scenarios "$@"
