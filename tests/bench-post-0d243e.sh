#!/usr/bin/env bash
set -euo pipefail

repo=$(cd "$(dirname "$0")/.." && pwd)
baseline=0d243e6820d6b04c02df279450fb68324712fb40
current=$(git -C "$repo" rev-parse HEAD)
runs=${1:-6}
if ! [[ $runs =~ ^[1-9][0-9]*$ ]]; then
    printf 'Run count must be a positive integer.\n' >&2
    exit 2
fi

baseline_dir=$(mktemp -d "${TMPDIR:-/tmp}/rogue-post-bench.XXXXXX")
trap 'rm -rf "$baseline_dir"' EXIT
git -C "$repo" archive "$baseline" | LC_ALL=C tar -x -C "$baseline_dir"
cp "$repo/tests/Program.cs" "$baseline_dir/tests/Program.cs"
cp "$repo/tests/benchmarks/PostBaselineBenchmarks.cs" "$baseline_dir/tests/benchmarks/"

docker build --target scenarios --quiet -t rogue-post-bench-baseline "$baseline_dir"
docker build --target scenarios --quiet -t rogue-post-bench-current "$repo"
run_bench()
{
    local label=$1 image=$2 revision=$3 run=$4
    printf '%s %s/%s (%s)\n' "$label" "$run" "$runs" "$revision"
    docker run --rm "$image" --bench-post-0d243e
}
for ((run = 1; run <= runs; run++)); do
    if ((run % 2)); then
        run_bench BASELINE rogue-post-bench-baseline "$baseline" "$run"
        run_bench CURRENT rogue-post-bench-current "$current" "$run"
    else
        run_bench CURRENT rogue-post-bench-current "$current" "$run"
        run_bench BASELINE rogue-post-bench-baseline "$baseline" "$run"
    fi
done
