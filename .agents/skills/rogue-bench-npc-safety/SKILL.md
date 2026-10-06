---
name: rogue-bench-npc-safety
description: Run fixed-seed NPC route, hunger, and sleep behavior experiments in Rogue Survivor. Use when measuring safety tradeoffs in civilian AI rather than raw throughput.
---

# NPC safety experiment

Run `sh tests/scenario.sh --bench-npc-safety` from the repository root. The
fixtures are in `benchmarks/NpcSafetyBenchmarks*.cs`. Compare routes, escapes,
food collection, sleep, and survival by named case and seed. Treat these as
behavior outcomes, not milliseconds per NPC turn. The interpretation and
earlier runs are in `docs/npc-safety-experiment.md`.
