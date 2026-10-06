---
name: rogue-bench-npc-turn
description: Measure complete NPC map-turn time and its instrumented phases in Rogue Survivor. Use when estimating crowded-map turns or checking NPC/FOV optimizations.
---

# NPC turn benchmark

Run `sh tests/scenario.sh --bench-npc-turn` from the repository root. The seeded
fixture and timing probes are in `benchmarks/NpcTurnBenchmarks*.cs`. Record the
median total for eight map turns, actor count, action count, FOV, sensing,
decision, routing, zone lookup, and execution. Subphase times can overlap; do
not add them into a total. The fixture covers one 100×100 surface, not a whole
city. Use `rogue-bench-npc-calls` to inspect method chains, then remeasure here
for real elapsed time.
