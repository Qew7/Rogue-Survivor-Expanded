#!/bin/sh
set -eu

repo=$(git rev-parse --show-toplevel)
cd "$repo"
tree=$(git write-tree)
scratch=$(mktemp -d "${TMPDIR:-/tmp}/rogue-rest-commit.XXXXXX")
trap 'rm -rf "$scratch"' EXIT HUP INT TERM
mkdir "$scratch/source"
git archive "$tree" | tar -x -C "$scratch/source"

echo 'Measuring staged 5x5 rest simulation (30 waits; 1, 2, 4, 8 workers)...' >&2
if ! (cd "$scratch/source" && sh tests/scenario.sh --bench-rest-parallel-timing) >"$scratch/output" 2>&1; then
    tail -n 80 "$scratch/output" >&2
    exit 1
fi

median() {
    sed -n "s/^REST TIMING exact arrivals, $1 workers:.*median \\([0-9][0-9.]*\\) ms$/\\1/p" "$scratch/output"
}
one=$(median 1)
two=$(median 2)
four=$(median 4)
eight=$(median 8)
if [ -z "$one" ] || [ -z "$two" ] || [ -z "$four" ] || [ -z "$eight" ]; then
    tail -n 80 "$scratch/output" >&2
    echo 'Could not read all four rest timing medians' >&2
    exit 1
fi

history=benchmarks/rest-timing-history.tsv
if [ ! -e "$history" ]; then
    printf 'measured_at_utc\tstaged_tree\thost\tdocker_cpus\tworkers_1_ms\tworkers_2_ms\tworkers_4_ms\tworkers_8_ms\n' >"$history"
fi
printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\n' \
    "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$tree" "$(uname -sm | tr ' ' '_')" \
    "$(docker info --format '{{.NCPU}}')" "$one" "$two" "$four" "$eight" >>"$history"
git add -- "$history"
printf 'Rest timing: 1=%s ms, 2=%s ms, 4=%s ms, 8=%s ms\n' \
    "$one" "$two" "$four" "$eight" >&2
