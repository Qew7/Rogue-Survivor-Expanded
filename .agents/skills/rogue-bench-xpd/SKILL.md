---
name: rogue-bench-xpd
description: Measure XPD base preview and sparse supply-route lookup on fixed-size maps. Use when optimizing shelter/base planning or supply searches in Rogue Survivor.
---

# XPD base benchmark

Run `sh tests/scenario.sh --bench-xpd` from the repository root. The cases in
`benchmarks/XpdBaseBenchmarks.cs` measure base preview and sparse supply lookup
on 40×40 and 100×100 maps. Report median milliseconds per batch and map size;
compare the same fixture under the same Docker and hardware settings.
