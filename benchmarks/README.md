# Headless benchmarks

Run commands from the repository root. Benchmark sources and profiling scripts
live here; `Dockerfile` compiles the C# files into the scenario runner image.

| Measurement | Command | Project skill |
| --- | --- | --- |
| General game operations | `sh tests/scenario.sh --bench` | `rogue-bench-general` |
| AI, FOV, and generation | `sh tests/scenario.sh --bench-ai` | `rogue-bench-ai` |
| Full NPC map turns | `sh tests/scenario.sh --bench-npc-turn` | `rogue-bench-npc-turn` |
| NPC method calls and flamegraph | `sh benchmarks/bench-npc-calls.sh` | `rogue-bench-npc-calls` |
| Radio broadcasts | `sh tests/scenario.sh --bench-radio` | 4 stations, 9 maps, 80 broadcasts across 40 hourly slots |
| Rolling radio broadcasts | `sh tests/scenario.sh --bench-radio-rolling` | 12 new rumors per hour for 96 hours; includes listeners and story rendering |
| Radio news candidate strategies | `sh tests/scenario.sh --bench-radio-news` | Isolated candidate scans for full scan, incremental index, and previous-day snapshot; use rolling broadcasts for total cost |
| Radio method calls and flamegraph | `PROFILE_KIND=radio sh benchmarks/bench-npc-calls.sh` | Mono call profile |
| Distant district background completion | `sh tests/scenario.sh --bench-rest-simulation` | 9 generated districts, 4 waits, median of 5 fresh runs; waits for the worker after each turn |
| Large-city background completion | `sh tests/scenario.sh --bench-rest-large` | 5×5 generated districts, high NPC density, 4 waits, median of 3 fresh runs; waits for the worker after each turn |
| Large-city rest phases | `sh tests/scenario.sh --bench-rest-phases` | 5×5 districts, 30 waits, three alternating uninstrumented and phase-timed runs |
| Long-rest memory | `sh tests/scenario.sh --bench-rest-memory 400` and `--bench-rest-memory-gc 400` | Reports each 100 waits: interval time, managed memory, RSS, GC, NPC facts/events, and archive entries; the final interval also separates distinct events from witness entries, rumor audience and repeat counts, and repeated social interactions; the GC variant measures retained memory |
| Parallel background equivalence | `sh tests/scenario.sh --bench-rest-parallel-production` | Replays one saved 5×5 start for 30 waits with 1/2/4/8 workers; waits for background catch-up and checks identities, ordered rumors and goals, district RNG, stories, radio programs and archive state after every wait |
| Parallel background timing | `sh tests/scenario.sh --bench-rest-parallel-timing` | Three 30-wait samples per worker count, waiting for background catch-up after each turn; excludes state digests from timing |
| Rest optimization costs | `sh tests/scenario.sh --bench-rest-optimizations` | Actor lookup/movement and empty/sparse/dense sensing |
| Synthetic sleep yield overhead | `sh tests/scenario.sh --bench-sleep-yield` | Ten sleeping map turns with and without a dummy worker; production pauses its worker during the live turn |
| Rest simulation flamegraph | `PROFILE_KIND=rest sh benchmarks/bench-npc-calls.sh` | Mono call profile of waiting for background completion across 9 districts |
| One-district rest flamegraph | `PROFILE_KIND=rest_one sh benchmarks/bench-npc-calls.sh` | Four waits in one district, without distant simulation |
| Large-city rest flamegraph | `PROFILE_KIND=rest_large sh benchmarks/bench-npc-calls.sh` | Mono call profile of waiting for background completion across 5×5 districts |
| Comparison with `0d243e6` | `bash benchmarks/bench-post-0d243e.sh` | `rogue-bench-post-baseline` |
| NPC route, hunger, and sleep | `sh tests/scenario.sh --bench-npc-safety` | `rogue-bench-npc-safety` |
| XPD base and supply routing | `sh tests/scenario.sh --bench-xpd` | `rogue-bench-xpd` |
| Copied save diagnostics | `--bench-save /profile/save.dat` in a mounted scenario image | `rogue-bench-save` |
| Copied save core timings | `--bench-save-core /profile/save.dat` in a mounted scenario image | Cold load, three complete saves and archive-only writes |
| Copied save contents | `--bench-save-stats /profile/save.dat` in a mounted scenario image | Cold load, maps, actors, archived entries and event categories |
| Copied save/load budget | `--check-save-budget /profile/save.dat` in a mounted scenario image | `rogue-bench-save-budget` |

The repository pre-commit hook measures the parallel rest fixture from the
staged files and appends all four medians to
[rest-timing-history.tsv](rest-timing-history.tsv). Install it once per clone
with `git config core.hooksPath .githooks`. Each row belongs to the commit that
added it (`git blame benchmarks/rest-timing-history.tsv` shows that commit);
`staged_tree` identifies the exact source snapshot measured. The hook requires
Docker and stops the commit if the benchmark fails. Compare rows on the same
host and Docker CPU allocation.

The project skills in `.agents/skills/` give the complete commands and how to
interpret each result. Current implementation notes, reference measurements,
and rejected experiments are in
[`docs/performance.md`](../docs/performance.md).
