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
| Comparison with `0d243e6` | `bash benchmarks/bench-post-0d243e.sh` | `rogue-bench-post-baseline` |
| NPC route, hunger, and sleep | `sh tests/scenario.sh --bench-npc-safety` | `rogue-bench-npc-safety` |
| XPD base and supply routing | `sh tests/scenario.sh --bench-xpd` | `rogue-bench-xpd` |
| Copied save diagnostics | `--bench-save /profile/save.dat` in a mounted scenario image | `rogue-bench-save` |
| Copied save/load budget | `--check-save-budget /profile/save.dat` in a mounted scenario image | `rogue-bench-save-budget` |

The project skills in `.agents/skills/` give the complete commands and how to
interpret each result. Historical measurements and caveats are in
[`docs/performance.md`](../docs/performance.md).
