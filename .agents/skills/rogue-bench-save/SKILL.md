---
name: rogue-bench-save
description: Diagnose Rogue Survivor save/load serialization cost using a copied save file. Use when investigating save latency, compression, or archive size.
---

# Copied-save diagnostics

First copy the save to a temporary directory; do not benchmark the live file.
From the repository root, build and run with that directory mounted read-only:

```sh
docker build --target scenarios -t rogue-survivor-save-profile .
docker run --rm --cpus=1 --memory=3g \
  --mount type=bind,source=/absolute/path/to/copied-save-dir,target=/profile,readonly \
  rogue-survivor-save-profile --bench-save /profile/save.dat
```

Replace both example paths with the actual copied save. The code is in
`benchmarks/SavePerformanceBenchmarks.cs`. Report cold load separately from
three-sample medians and note the CPU/memory limits. See `docs/performance.md`.
