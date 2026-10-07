#!/bin/sh
set -eu

cd "$(dirname "$0")/.."
case ${PROFILE_KIND:-npc} in
  npc) profile_kind=npc; profile_runner=--profile-npc-turn; profile_root='NpcTurnBenchmarks:RunTurns (';
    profile_title='NPC turn: managed call flamegraph'; profile_detail='8 turns · 1800 actions · width = attributed self time; hover a block for its call path' ;;
  radio) profile_kind=radio; profile_runner=--profile-radio; profile_root='RadioBenchmarks:RunBroadcasts (';
    profile_title='Radio: managed call flamegraph'; profile_detail='80 broadcasts · 40 hourly slots · 9 maps · 216 sources · 3456 facts · 20 listeners' ;;
  *) echo 'PROFILE_KIND must be npc or radio' >&2; exit 2 ;;
esac
profile_dir=${1:-$(mktemp -d "${TMPDIR:-/tmp}/rogue-$profile_kind-calls.XXXXXX")}
mkdir -p "$profile_dir"
profile_dir=$(cd "$profile_dir" && pwd -P)
rm -f "$profile_dir/$profile_kind.mlpd" "$profile_dir/run.txt" "$profile_dir/calls.txt" \
  "$profile_dir/summary.txt" "$profile_dir/report-warnings.txt" \
  "$profile_dir/flamegraph.svg" "$profile_dir/flamegraph.folded"

printf 'Building profiler image\n' >&2
docker build --progress=plain --target profile -t rogue-survivor-profile . >&2
printf 'Profiling %s\n' "$profile_kind" >&2
docker run --rm --mount "type=bind,source=$profile_dir,target=/profile" \
  --entrypoint sh rogue-survivor-profile -c \
  'mono --profile=log:calls,noalloc,calldepth=40,output=/profile/"$1".mlpd /src/tests/UnitTests.exe "$2" > /profile/run.txt' \
  sh "$profile_kind" "$profile_runner"

test -s "$profile_dir/$profile_kind.mlpd"
window=$(awk '/^PROFILE_WINDOW / { printf "%.3f-%.3f", $2 - 0.1, $3 + 0.1 }' "$profile_dir/run.txt")
test -n "$window"
printf 'Generating call report and flamegraph\n' >&2
docker run --rm --mount "type=bind,source=$profile_dir,target=/profile" \
  --entrypoint sh rogue-survivor-profile -ec \
  'mprof-report --reports=call --traces --maxframes=64 --time="$1" /profile/"$2".mlpd > /profile/calls.txt 2> /profile/report-warnings.txt
   ruby /src/benchmarks/npc_flamegraph.rb /profile/calls.txt /profile/flamegraph.svg /profile/flamegraph.folded "$3" "$4" "$5"' \
  sh "$window" "$profile_kind" "$profile_root" "$profile_title" "$profile_detail"
awk '/^[[:space:]]*[0-9]+[[:space:]]+[0-9]+[[:space:]]+[0-9]+[[:space:]]/ { print }' \
  "$profile_dir/calls.txt" > "$profile_dir/summary.txt"
grep -Fq "$profile_root" "$profile_dir/summary.txt"

grep '^PROFILE_' "$profile_dir/run.txt"
printf 'Total(ms) Self(ms)      Calls Method name\n'
head -30 "$profile_dir/summary.txt"
printf 'Full call stacks: %s/calls.txt\n' "$profile_dir"
printf 'Flat method summary: %s/summary.txt\n' "$profile_dir"
printf 'Flamegraph: %s/flamegraph.svg\n' "$profile_dir"
printf 'Folded stacks: %s/flamegraph.folded\n' "$profile_dir"
if [ "${PROFILE_KEEP_RAW:-0}" = 1 ]; then
  printf 'Raw Mono profile: %s/%s.mlpd\n' "$profile_dir" "$profile_kind"
else
  rm "$profile_dir/$profile_kind.mlpd"
fi
