---
name: rogue-bench-npc-calls
description: Generate the Mono NPC-turn method call report, text flamegraph stacks, and SVG flamegraph. Use when asking which methods dominate an NPC turn or requesting a flamegraph.
---

# NPC method calls

Run `sh benchmarks/bench-npc-calls.sh` from the repository root, optionally
passing an output directory. The script prints the busiest methods and writes
`summary.txt`, `calls.txt`, `flamegraph.folded`, and `flamegraph.svg` there. Set
`PROFILE_KEEP_RAW=1` only when the raw `npc.mlpd` is needed. Read inclusive
`Total(ms)` and exclusive `Self(ms)` separately; flamegraph widths attribute
self time to callers by call count. Mono call instrumentation adds heavy
overhead, so use `rogue-bench-npc-turn` for actual turn duration. Give the user
the path to `flamegraph.svg` and the leading method chains.
