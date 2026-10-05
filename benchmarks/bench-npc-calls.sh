#!/bin/sh
set -eu

cd "$(dirname "$0")/.."
profile_dir=${1:-$(mktemp -d "${TMPDIR:-/tmp}/rogue-npc-calls.XXXXXX")}
mkdir -p "$profile_dir"
profile_dir=$(cd "$profile_dir" && pwd -P)
rm -f "$profile_dir/npc.mlpd" "$profile_dir/run.txt" "$profile_dir/calls.txt" \
  "$profile_dir/summary.txt" "$profile_dir/report-warnings.txt" \
  "$profile_dir/flamegraph.svg" "$profile_dir/flamegraph.folded"

docker build --quiet --target profile -t rogue-survivor-profile . >&2
docker run --rm --mount "type=bind,source=$profile_dir,target=/profile" \
  --entrypoint sh rogue-survivor-profile -c \
  'mono --profile=log:calls,noalloc,calldepth=40,output=/profile/npc.mlpd /src/tests/UnitTests.exe --profile-npc-turn > /profile/run.txt'

test -s "$profile_dir/npc.mlpd"
window=$(awk '/^PROFILE_WINDOW / { printf "%.3f-%.3f", $2 - 0.1, $3 + 0.1 }' "$profile_dir/run.txt")
test -n "$window"
docker run --rm --mount "type=bind,source=$profile_dir,target=/profile" \
  --entrypoint sh rogue-survivor-profile -ec \
  'mprof-report --reports=call --traces --maxframes=64 --time="$1" /profile/npc.mlpd > /profile/calls.txt 2> /profile/report-warnings.txt
   python3 /src/benchmarks/npc_flamegraph.py /profile/calls.txt /profile/flamegraph.svg /profile/flamegraph.folded' \
  sh "$window"
awk '/^[[:space:]]*[0-9]+[[:space:]]+[0-9]+[[:space:]]+[0-9]+[[:space:]]/ { print }' \
  "$profile_dir/calls.txt" > "$profile_dir/summary.txt"
grep -q '1 NpcTurnBenchmarks:RunTurns ' "$profile_dir/summary.txt"

grep '^PROFILE_' "$profile_dir/run.txt"
printf 'Total(ms) Self(ms)      Calls Method name\n'
head -30 "$profile_dir/summary.txt"
printf 'Full call stacks: %s/calls.txt\n' "$profile_dir"
printf 'Flat method summary: %s/summary.txt\n' "$profile_dir"
printf 'Flamegraph: %s/flamegraph.svg\n' "$profile_dir"
printf 'Folded stacks: %s/flamegraph.folded\n' "$profile_dir"
if [ "${PROFILE_KEEP_RAW:-0}" = 1 ]; then
  printf 'Raw Mono profile: %s/npc.mlpd\n' "$profile_dir"
else
  rm "$profile_dir/npc.mlpd"
fi
