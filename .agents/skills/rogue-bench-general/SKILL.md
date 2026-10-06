---
name: rogue-bench-general
description: Run the general Rogue Survivor headless performance benchmark for graphics-side work, FOV, maps, AI, and generation. Use when comparing broad game-operation timings.
---

# General benchmark

From the repository root, run `sh tests/scenario.sh --bench`. The sources are
in `benchmarks/PerformanceBenchmarks.cs`, `GraphicsAndWorldBenchmarks.cs`, and
`AIAndGenerationBenchmarks.cs`. Compare medians on the same machine and Docker
configuration. These are diagnostic microbenchmarks; the test UI does not
measure graphics driver time. See `docs/performance.md` for prior results.
