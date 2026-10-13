# Performance measurements

This document describes the current implementation, repeatable measurements,
and approaches that were measured but not retained. The full benchmark catalog
and project skills are in [benchmarks/README.md](../benchmarks/README.md).

## Running and reading benchmarks

Run these commands from the repository root:

```sh
sh tests/scenario.sh --bench-npc-turn
sh tests/scenario.sh --bench-radio-rolling
sh tests/scenario.sh --bench-rest-large
sh tests/scenario.sh --bench-rest-phases
sh tests/scenario.sh --bench-rest-parallel-timing
sh tests/scenario.sh --bench-rest-parallel-production
sh tests/scenario.sh storage/save-budget
```

The focused NPC-turn fixture advances eight real turns on one seeded 100×100
surface. The large-rest fixture advances a generated 5×5 city with full NPC
actions and rumor exchange. The parallel timing fixture restores the same
saved 5×5 start for each worker count and measures 30 waits; the production
comparison checks world state after every wait. Setup and rendering are
outside these headless timers. Results from different fixtures must not be
compared as if they were the same workload.

Use `PROFILE_KIND=rest_large sh benchmarks/bench-npc-calls.sh` for a Mono
method-call report and SVG flamegraph of the full-city wait path;
`PROFILE_KIND=radio` profiles radio. The report's inclusive times overlap,
and method instrumentation can make the game tens of times slower. Use it to
find call paths and call counts, then use an unprofiled benchmark to measure
speed. Compare before and after on the same machine, runtime, fixture, and
Docker configuration.

## Current NPC and rest simulation

Full-density profiles identify NPC sight/FOV and civilian goal evaluation as
the main costs. The district-selection loop takes little self time in the
profile. The 5×5 phase fixture puts about 61,000 decisions through 30 waits;
intent preparation dominates decision time, with knowledge perception and
exploration smaller. These are diagnostic rankings, not additive wall-clock
shares. Run `--bench-rest-phases` again before choosing the next optimization.

The retained game-side changes keep the same NPC actions and rumor delivery:

- FOV uses squared-radius checks, precomputed ray geometry, and local
  visible-cell state. Transparency is checked on each call, so doors and
  moving actors take effect immediately. There is no cross-call FOV cache.
- Map objects and actor positions use transient tile-position arrays; zone
  lookup uses a row index. Map auxiliary structures are rebuilt after load.
- Goal evaluation reuses actor-only trait influences within one evaluation,
  visits traits once for all six influences, avoids sorting candidates when
  none can start a goal, and skips constructing zero-deficit offers only when
  no active goal needs them. Existing goals can still finish normally.
- Pending-goal checks return immediately when there is no active generated
  goal. Item, trade, perception, and exploration paths avoid redundant short
  scans and allocations where the same action choice is preserved.

Long sleep and waits budget older district-turn debt across remaining player
turns, simulating the oldest local clocks first. With the default preset
option, unfinished debt remains when rest ends and advances on later rests or
district entry; the separate "finish after rest" option drains it. A single
wait receives its normal district-turn budget. The game displays simulated
district turns and an estimated completion time, and limits sleep redraws to
four per real second. Budgeting spreads work across rest; it does not omit NPC
actions or rumor generation.

### Parallel district simulation

The `Parallel distant simulation while resting` preset option uses up to
four workers by default. Each turn, distant districts are split by checkerboard
color. Same-color districts with distinct entry cells can run together.
Districts competing for the **same** destination entry, sharing active
story/group state, or touching the player district remain ordered. Each
successful border crossing places the leader in the destination map during
its source district's turn. Followers select adjacent cells outside all
interdistrict entry points, so neighboring entries need no district-wide
serialization. Brief destination locking still protects concurrent arrivals.
NPCs, rumors, stories, and radio advance under the same game rules.

The deterministic `--bench-rest-parallel-production` check replayed one saved
5×5 start for 30 waits with 1, 2, 4, and 8 workers. Actor state, ordered
rumors, stories, radio programs, resident records, and district random
streams matched after every wait. The named
`world/parallel-rest-crossings` and
`world/parallel-rest-exact-arrivals` scenarios cover contested and nearby
entries, followers, rumor delivery, and save/load.

On the fixed-seed 5×5 timing fixture with 70 civilians per district, the
four-worker median was **2.37 seconds for 30 waits**, across three samples.
Before followers were kept off other entry cells, neighboring entries had to
be ordered and the median was **2.69 seconds** on the same fixture. The
approximately 12% difference is a local Docker/Mono result, not a guaranteed
gain on every city or machine. Timing excludes world construction and state
digests. More workers did not materially improve this fixture beyond four.

## Radio

The four stations share one program per station and game hour. A nearby
listener processes that program once in the hour even with multiple
receivers. Program selection favors familiar stories without excluding
unknown rumors; listener delivery avoids copying facts already known.
Candidate ranking stops when remaining facts cannot beat the current
headline. This work is measured with `--bench-radio`,
`--bench-radio-rolling`, and `PROFILE_KIND=radio`.
The rolling fixture adds rumors over 96 hours, so it exercises the cost of
continuing world news as well as a single fixed set of facts. Radio benchmark
times are not complete game-turn times.

## Automated save and load budget

`storage/save-budget` runs in the full Docker test target and can also be run
by name. Every measured save or load must take at most **10 seconds**, and
each compressed file must be at most **50,000,000 bytes**. These are regression
limits for its fixed workload, not limits on a mature player world. The day-12
fixture contains 416 maps, 1,124,864 tiles, 6,240 actors, and 66,560
historical events. It measures a cold first save, two atomic replacements,
two fresh-process full loads, and an archive-only load. Memory use is reported
but has no pass/fail limit.

In the most recent full Docker test for this implementation, the fixture
produced a **9.76 MB** file. Complete saves took **5.27–5.39 seconds**,
fresh-process loads **6.43–6.50 seconds**, and archive-only loading
**1.22 seconds**. The operation and file budgets passed. OS file caches are
not flushed, and simulation-worker waiting and UI redraws are excluded.

For a copied player save, use the `rogue-bench-save` and
`rogue-bench-save-budget` skills. The diagnostic reads the copy and writes
new files only in its temporary directory; it does not restart the game.
Large worlds can exceed the synthetic fixture's limits. One copied mature
save measured **68.76 MB** after compression, **60.39 seconds** for one cold
load, and **36.77 seconds** per complete save under a one-CPU Docker limit.
Those are historical measurements of that copy, not the size or load time of
the currently running container's save.

### Copied day-12 world, original format-4 investigation

The old format serialized repeated type and field names for every tile.
A copied 418-map world produced a 1.10 GB raw graph, took about 33 seconds to
save, and took about 102 seconds to load under one CPU. Removing the resident
archive diagnostically reduced save time only slightly; most of the cost was
the world graph. This was the reason for changing the codec. Production no
longer loads format-4 world saves; see
[save-structure.md](save-structure.md) for the audit.

### Implemented compact graph and independent records section

Current format 5 uses per-operation type/schema and string tables, compact
Tile nodes, shared-reference IDs, a 64 KiB buffer before GZip, and an
independent resident-archive section. Read Records can load that section
without the world. The reader restores tile model IDs and flags directly;
archives reconstruct numeric event deduplication keys rather than storing
duplicate strings. Saves preserve identity, history, clocks, and all map
content. [save-format.md](save-format.md) defines the format and compatibility
rules.

On the same copied day-12 graph, compact serialization took about
**7.8 seconds** instead of **32.6**, and compressed graph size fell from
**25.6 MB** to **13.5 MB**. These isolated codec timings exclude atomic
replacement, records refresh, and UI work; use the complete budget above
for current end-to-end fixture times.

## Archive memory during long rests

The permanent resident chronicle grows as play and simulation produce events.
Equal immutable entry text is shared within each archive; the transient pool
is rebuilt after load. No entry is removed. In a 400-wait, 5×5 fixture,
retained managed memory at wait 400 fell from **325.6 MiB** to
**231.6 MiB**, and RSS from **513.2 MiB** to **368.0 MiB**. The last
100 waits still took about 23 seconds, so text sharing reduces memory
duplication but does not explain all late-run time. Use
`--bench-rest-memory 400` and `--bench-rest-memory-gc 400` to repeat the
workload.

## Measured approaches not retained

- **Cross-turn FOV caching:** only 57 of about 1,250 FOV calls in the rest
  fixture repeated within one map turn. The optimistic saving was below
  0.3 ms per four waits, before accounting for door, light, weather, and
  map-object invalidation.
- **Recursive shadowcasting:** changed visibility at corners and ran
  slower on the open arena, so it did not preserve behavior or speed.
- **Thread-local FOV buffers and alternate sparse-resource sensing:** the
  full 5×5 wait time did not improve consistently; the resource variant
  slowed dense maps. The current simpler sensor was retained.
- **A ground-inventory tile array:** its repeated full-city timing was
  effectively flat, so its update cost and extra per-map memory were not
  justified.
- **Replacing goal or perception short scans with extra state:** several
  variants were flat in full-city timings; one reused a personality context
  that was wrong for custom content catalogs. They were reverted.
- **Forced periodic full GC:** did not stop chronicle growth because the
  entries remained reachable. Sharing duplicate text reduced retained
  memory without discarding history.
- **Fastest GZip compression:** did not meaningfully improve the measured
  old-format write. Compact encoding and buffering addressed the actual
  cost.
- **Parallel district groups without ownership of shared state:** the
  same saved start diverged in actors and rumors even at one worker because
  the district turn order changed. The current scheduler uses district
  random streams, synchronized global writes, immediate border arrivals,
  and per-wait state comparison.
