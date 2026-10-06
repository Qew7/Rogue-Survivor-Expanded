---
name: rogue-bench-ai
description: Run the focused AI, FOV, route-check, and generation microbenchmarks in Rogue Survivor. Use when changing NPC sensing, visibility, navigation, or actor placement.
---

# AI and generation benchmark

Run `sh tests/scenario.sh --bench-ai` from the repository root. Inspect cases
in `benchmarks/AIAndGenerationBenchmarks.cs` and
`benchmarks/CachePotentialBenchmarks.cs`. Report the median and fixture size for
each relevant case. Action selection is measured without performing the action;
use `rogue-bench-npc-turn` for elapsed NPC turn time. Compare under the same
Docker and hardware conditions.
