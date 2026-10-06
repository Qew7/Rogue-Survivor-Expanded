---
name: rogue-bench-save-budget
description: Check Rogue Survivor save/load time, memory, and file-size budgets on its large synthetic fixture or an existing copied save. Use when validating persistence performance.
---

# Save/load budget

Run `sh tests/scenario.sh storage/save-budget` for the synthetic large-world
gate. It checks complete saves and fresh-process loads against 10-second and
50 MB limits. For a real copied format-5 save, run:

```sh
docker build --target scenarios -t rogue-survivor-save-budget .
docker run --rm \
  --mount type=bind,source=/absolute/path/to/copied-save-dir,target=/profile,readonly \
  rogue-survivor-save-budget --check-save-budget /profile/save.dat
```

Replace both example paths with the actual copied save. Sources are
`benchmarks/SaveBudget*.cs`. Report elapsed save/load time, peak RSS, file
size, and pass/fail status. See `docs/performance.md` for fixture scope.
