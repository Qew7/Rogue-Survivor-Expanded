---
name: rogue-bench-post-baseline
description: Compare current Rogue Survivor microbenchmarks against commit 0d243e6 in alternating Docker runs. Use when checking the impact of post-baseline knowledge, planning, AI, base, or archive changes.
---

# Post-baseline comparison

Run `bash benchmarks/bench-post-0d243e.sh` from the repository root. An
optional positive integer sets paired run count; default is six. The script
archives commit `0d243e6`, builds baseline and current images with the same
`PostBaselineBenchmarks.cs`, and alternates run order. Report medians for each
case and note that these compare whole revisions, so one changed method cannot
be credited from this benchmark alone. The script uses temporary directories
and does not modify the working tree.
