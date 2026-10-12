# Performance measurements

Benchmark sources, scripts, and the list of project skills are in
[`benchmarks/`](../benchmarks/README.md).

Run the headless benchmarks with:

```sh
sh tests/scenario.sh --bench
sh tests/scenario.sh --bench-ai
sh tests/scenario.sh --bench-npc-turn
```

The runner warms each case, runs five timed samples, and prints the median.
`--bench-ai` runs just the AI and generation cases for quicker iteration.
The test UI omits actual graphics driver work, so the minimap case measures
game-side traversal and dispatch.
`--bench-npc-turn` runs eight real map turns on a seeded 100×100 surface with
the maximum 75 configured civilians (plus any building residents) and 200
shambling zombies, with fresh fixture runs for warmup and
five timed samples. It reports the median run's map upkeep, actor dispatch,
AI sensing, field-of-view computation, decision, intent preparation, route
checks, zone lookup, legality and action execution; the named subphases are inclusive and
some can overlap. The top action types rank
decision time by the action returned. World setup, rendering, other districts
and background simulation are outside the timed interval. These timings show
which phase dominates this fixture, not a full-city turn or an exact cost of
every NPC behavior.
On October 4, 2026, the same Docker/Mono fixture on `ea156b1` measured
141.0 ms per eight map turns, including 57.1 ms in 1,800 FOV computations.
With per-call transparency and visible-cell state in `LOS.ComputeFOVFor`, it
measured 121.3 ms per eight turns, including 35.2 ms in FOV (38% less FOV time,
14% less total time). Each figure is the median of five fresh runs after one
warmup; the before/after samples were run sequentially, so treat these as
diagnostic rather than a controlled paired comparison. The FOV scenario checks
the complete visible-cell set against the original algorithm across positions,
walls and door-state changes. There is no cross-call cache: moved actors and
newly opened doors are recomputed on the next observation.
A follow-up on the same fixture reused that per-call visible-cell state during
the wall-fix pass. The current-branch median changed from 116.7 to 109.0 ms
per eight turns; FOV changed from 34.3 to 27.9 ms (19% less FOV time). These
were separate five-sample runs, so the whole-turn difference is approximate.
These general benchmarks are diagnostic. The save/load budget below is an
automated pass/fail gate. Compare measurements on the same
machine, Docker configuration, and runtime.

## NPC method call profile

Run `sh benchmarks/bench-npc-calls.sh` to record the same seeded 100×100 fixture for
one eight-turn run. The script prints the busiest methods and creates
`summary.txt` (methods sorted by inclusive time), `calls.txt` (the same
times plus caller chains), `flamegraph.svg` (a visual call tree), and
`flamegraph.folded` (text stacks and attributed microseconds) in the temporary directory shown at the end. Pass an
output directory as the first argument to keep the text at a known path. Set
`PROFILE_KEEP_RAW=1` to retain the large `npc.mlpd` file for another
`mprof-report` query; otherwise the script removes it after generating text.

The `Total(ms)` column includes callees; `Self(ms)` excludes them. `Calls` and
caller chains come from Mono's method enter/leave events. The script selects
the measured turn loop by its process time and checks that `RunTurns` appears
once in the report. The window has a 0.1-second margin around the loop, so a
few setup/teardown calls can remain. Methods inlined by Mono and native runtime
work do not have separate managed call entries. When a method has several
callers, the report gives its combined time, while its chains show call counts
per caller rather than time per caller. The flamegraph divides each method's
self time among its caller chains in proportion to their call counts. Its
widths are estimates; hover over a block to see the chain and attributed time.

**Do not use the profiled milliseconds as game speed.** Method entry/exit
instrumentation makes millions of tiny calls much slower and can change their
relative costs. Use this report to find call paths and counts; use
`--bench-npc-turn` for the unprofiled elapsed time. See the [Mono log profiler
documentation](https://www.mono-project.com/docs/debug+profile/profile/profiler/)
for the meaning and overhead of its call report.

On October 5, 2026, four alternating runs of the same 100×100 fixture before
and after a zone row index gave medians of **16.4 → 2.7 ms** for 6,406 zone
lookups over eight map turns (84% less), and **112.3 → 96.3 ms** for the full
eight turns (14% less). Each run itself was the median of five fresh fixture
samples. The index narrows the zone scan by map row, preserves overlapping
zones and their order, and rebuilds after zone additions, removals, bounds
changes, or deserialization. It is transient and does not change the save
format. These numbers are for one surface; runtime on other map layouts and
resident counts may differ.

## Changes after `0d243e6`

Run the focused comparison with `bash benchmarks/bench-post-0d243e.sh`. The script
builds `0d243e6820d6b04c02df279450fb68324712fb40` and the current tree
with the same `PostBaselineBenchmarks` source, then runs each version six times
in alternating order. Each reported run is the median of five timed batches.
The fixtures exercise real knowledge, planning, AI, base-trap, and archive code;
setup and Docker builds are outside the timed batches. Readability-only edits
have no isolated timing case.

Local Docker/Mono measurements on October 3, 2026, comparing the baseline with
production commit `54ab6f722800c90f31ddcae53259d77ff8dfdd1b` (median of six
run medians; lower is faster):

- Duplicate fact among 48 stored: 15.97 → 13.21 ms per 100,000 calls (17% faster).
- First person among 32: 4.23 → 4.17 ms per 100,000 calls (effectively unchanged).
- Last person among 32: 33.80 → 45.72 ms per 100,000 calls (35% slower).
- Build a planning domain: 5.41 → 5.25 ms per 1,000 calls (3% faster).
- Revisit a known planning place: 6.13 → 3.88 ms per 100,000 calls (37% faster).
- Choose an AI wander move on an open 20×20 map: 30.17 → 27.12 ms per 10,000 calls (10% faster).
- Move toward a goal without traps: 26.43 → 20.78 ms per 10,000 calls (21% faster).
- Scan 100 base cells for trap placement: 229.18 → 235.69 ms per 1,000 calls (3% slower).
- Render a 16-resident, 512-entry record timeline: 22.61 → 25.12 ms per 50 calls (11% slower).
- Search that archive for absent text: 70.20 → 75.14 ms per 200 calls (7% slower).

The last-person lookup is a clear regression in this bounded-list fixture.
The archive cases also got slower for repeated reads of this synthetic history;
other archive sizes, search terms, and live-session updates may differ. These
numbers compare complete revisions, so they do not assign each difference to a
single commit or imply the same change in whole-game frame or turn time.

Follow-up fixes measured on the same Docker/Mono setup on October 3, 2026
(six paired runs, median of run medians, baseline → fixed tree):

- Last person among 32: 36.18 → 30.77 ms per 100,000 calls (15% faster).
- Scan 100 base cells for trap placement: 235.81 → 4.00 ms per 1,000 calls (98% faster). Once a boundary cell at distance zero is found, the remaining cells cannot improve it.
- Repeat the same 512-entry timeline: 23.21 → 0.09 ms per 50 calls. The archive reuses rendered lines and returns a separate list to callers.
- Repeat an absent-text search: 72.76 → 0.28 ms per 200 calls with the same search term.
- First timeline read from a fresh archive view: 23.71 → 22.37 ms per 50 calls (6% faster).
- Search with a different absent term on every call: 65.76 → 35.83 ms per 200 calls (46% faster).

The two repeat-read cases measure cache hits; they do not represent the first
opening of an archive. The first-read and changing-search cases exercise the
uncached path. Cache hits check resident names and the archive's partial-history
flag as well as the search and event filter. Live-session views do not cache
rendered lines.

## Automated save and load budget

The regular `docker build --target test .` and `--all` scenario run include
`storage/save-budget`. Run it independently with:

```sh
sh tests/scenario.sh storage/save-budget
```

Each measured operation must take **at most 10 seconds**, and the compressed
save must contain **at most 50,000,000 bytes** (50 decimal MB). All samples must
pass; a median cannot hide a slow sample. These are regression limits on the
specified workload and test machine, not a guarantee for arbitrarily large
worlds or slower hardware. Memory usage is reported without a RAM limit.

The deterministic day-12 fixture uses current production models and contains
416 distinct 52×52 maps (1,124,864 tiles), 6,240 civilian actors with inventories,
2,080 personality states, 6,240 traits, 4,160 pending memories, 66,560 historical
events, and commitments, disputes, attachments, knowledge and active plans.
Tile flags/decorations vary; maps share exit, knowledge, attachment and plan
references. This is a synthetic workload near the scale of the profiled day-12
world below, with its composition fixed to make regressions comparable.

The save worker measures three real `Session.Save` calls: the first write with
a cold serializer, then two atomic replacements. Timings include presave world
traversal, resident snapshot refresh, both compressed sections, file flushing
and replacement. Two additional fresh workers measure `Session.Load`, including
archive/world decoding and rebuilding every map's auxiliary indexes. A final
fresh worker measures the archive-only Read Records path. Loading verifies map
and actor counts, tile flags/independence, inventories, traits, memories,
obligations, active plan references, clocks, all historical events and exit
alias identity. Fixture construction, model loading, subprocess startup and
post-operation validation are outside the measured intervals. OS file caches
are not flushed; a fresh process isolates JIT/codec state, not cold disk access.
Simulation-worker waiting, mod asset switching and UI redraws are also outside
these headless operation timings.

`SAVE BUDGET` lines report operation time, file bytes, managed-memory baseline,
sampled peak and peak increase, retained heap change after collection, process
RSS baseline/peak/increase, and per-generation GC counts. A background thread
samples every 10 ms; brief spikes may be missed. Explicit collections run before
and after timing, never inside it; natural collections during the operation are
included. RAM sampling overhead is included in wall time. Child processes keep
prior scenarios and previously loaded worlds out of load-memory measurements.

The disk limit applies to each primary/backup file separately. The retained
primary-plus-backup total is printed; replacement also temporarily needs space
for the new file. The test removes its private temporary directory. A worker
watchdog fails stalled subprocesses after 60 seconds.

To enforce the same limits on an existing **copied format-5** save:

```sh
docker build --target scenarios -t rogue-survivor-save-budget .
docker run --rm \
  --mount type=bind,source=/private/tmp/rogue-save-profile,target=/profile,readonly \
  rogue-survivor-save-budget --check-save-budget /profile/current-save.dat
```

The input is copied to a private temporary directory and remains untouched.
This measures a fresh-process load, three full saves and two further
fresh-process loads. The built-in catalogs must provide the save's models;
mod asset restoration is outside this headless runner. Personality-disabled
saves can be checked too; the copied-save mode does not require Read Records.
The diagnostic `--bench-save` below remains available for detailed codec
comparisons without thresholds. Neither command restarts the running game.

Local Docker/Mono run of this fixture (unrestricted CPU, September 29, 2026):

| Operation | Time | Sampled peak process RSS |
| --- | ---: | ---: |
| First complete save | 6.03 s | 283.4 MB |
| Atomic replacements, two samples | 5.89–5.93 s | 283.6 MB |
| Complete load, two fresh processes | 7.18–7.37 s | 578.0–580.2 MB |
| Archive-only load | 1.23 s | 180.8 MB |

Each saved file was 9,918,302 bytes (9.92 MB); primary plus backup occupied
19,836,604 bytes. Peak managed-memory increases were approximately 146–149 MB
for saving and 527 MB for loading. Retained heap increases after collection were
2.7 MB for the first save, effectively zero for replacements and 115 MB for the
loaded world. These figures describe this workload, not a production maximum.

After the NPC module refactor, the same fixture passed all operation/file
limits in the complete 238-scenario test build (September 29, 2026):

| Operation | Time | Sampled peak process RSS |
| --- | ---: | ---: |
| First complete save | 6.05 s | 284.1 MB |
| Atomic replacements, two samples | 6.03–6.05 s | 284.1–284.2 MB |
| Complete load, two fresh processes | 7.42–7.52 s | 590.5–592.1 MB |
| Archive-only load | 1.32 s | 187.3 MB |

Each file contained 9,978,825 bytes (9.98 MB); primary plus backup occupied
19,957,650 bytes. Peak managed-memory increases were 146–149 MB for saving
and 537–538 MB for loading. Additional archived category metadata and optional
planning fields preserve the existing fixture's residents, history and maps;
the benchmark composition and thresholds were not reduced.

After adding composable NPC goals, long-term interests and collective tasks,
the same fixture passed the complete 254-scenario run (September 29, 2026):

| Operation | Time | Sampled peak process RSS |
| --- | ---: | ---: |
| First complete save | 6.45 s | 293.5 MB |
| Atomic replacements, two samples | 6.31–6.36 s | 293.7–293.8 MB |
| Complete load, two fresh processes | 7.96–8.12 s | 590.6–592.8 MB |
| Archive-only load | 1.53 s | 189.5 MB |

Each file contained 9,980,128 bytes (9.98 MB); primary plus backup occupied
19,960,256 bytes. All operations stayed below 10 seconds and each file below
50,000,000 bytes. The fixture still contains 416 maps, 6,240 actors and 66,560
historical events.

## Save diagnostics

`--bench-save` profiles an existing **copied** save with three samples per case.
It reports world/chronicle counts, presave traversal, the current atomic file
writer for the already captured snapshot, serialization without compression,
direct GZip, buffered GZip at Optimal/Fastest levels, the archive alone, and the graph without its archive.
The last case isolates cost; it is not a proposal to discard history.
The input file is never overwritten. Generated saves are written to and removed
from an independent temporary directory inside the diagnostic container.

For a copied **format-5** save at `/private/tmp/rogue-save-profile/current-save.dat`:

```sh
docker build --target scenarios -t rogue-survivor-save-profile .
docker run --rm --cpus=1 --memory=3g \
  --mount type=bind,source=/private/tmp/rogue-save-profile,target=/profile,readonly \
  rogue-survivor-save-profile --bench-save /profile/current-save.dat
```

This uses a separate image and container and does not restart a running game.
Large saves may take substantial time to load before the first measurement.
The CPU limit reduces interference with play, but its timings are not directly
comparable with an unrestricted game process. Cold load is reported separately;
save cases use the loaded graph with GC before each sample. Cases run in a fixed
order, so follow-up optimization comparisons should alternate variants to check
for order effects. File writing uses the temporary container filesystem, not
the live save volume; waiting for the simulation worker and UI redraws is outside
these headless measurements.

### Copied day-12 world, original format-4 investigation

Measured on a copy of a running game's version-4 save, with Docker/Mono limited
to one CPU and 3 GiB RAM. The live game was left running. Medians of three
samples, fixed case order:

| Case | Median | Output size |
| --- | ---: | ---: |
| Presave world traversal | 6.29 ms | — |
| Original atomic file save | 33.17 s | 25,601,529 bytes |
| Graph serialization to a counting sink | 17.39 s | 1,104,833,938 bytes |
| Graph + direct default/Optimal GZip | 32.56 s | 25,601,514 bytes |
| Graph + 64 KiB buffer before Optimal GZip | 23.75 s | 25,601,514 bytes |
| Graph + 64 KiB buffer before Fastest GZip | 23.67 s | 25,601,514 bytes |
| Resident archive alone, buffered Optimal GZip | 1.06 s | 2,650,017 bytes |
| Graph without archive, direct Optimal GZip | 31.23 s | 22,945,915 bytes |

This world contains 418 maps and 1,107,446 tiles. A subsequent
[structure audit](save-structure.md) confirmed that these are 418 distinct map
objects, with no additional maps reachable outside the district lists; 305
are building basements. Repeated type names and field schemas account for 93%
of the raw graph bytes. These historical counts were obtained with the original
format-4 diagnostic image. The current reader accepts only format-5 worlds; its audit reports the
old encoding estimate alongside the actual compact raw size.
Maps retain 6,071 actors;
their personality states contain 5,873 traits, 3,171 pending memories, 2,898
recent observations, 588 relationships, and 874 relationship-memory links.
The archive contains 4,150 residents and 65,805 entries: 2,887,059 text
characters plus 6,006,390 characters in persisted deduplication keys.
These actor counts exclude actors held only by corpses or other references.

The raw writer made 63,250,737 stream writes. A separate small probe serialized
10,000 undecorated tiles into 3,570,123 bytes, about 357 bytes per tile, even
though each tile carries only a model ID, flags, and a null decoration list.
The graph format repeats full type names and field names for these objects and
their primitive values. All of this is rebuilt before compression on each save.

In this snapshot, removing the archive diagnostically reduced direct-GZip time
by about 4% and compressed size by about 10%. It does not remove other personality
data and is not a before/after comparison with personalities disabled. The
archive grows without eviction, so its future cost can be larger. Its current
cost does not explain most of this world's save pause.

The original investigation measured a 27% reduction from buffering before
GZip, with the same output size. Fastest gave no meaningful further benefit or
size change on this runtime.

### Implemented compact graph and independent records section

Format 5 now uses per-operation type/schema and string tables, cached reflection
metadata, compact Tile nodes, and a 64 KiB buffer before GZip. Object IDs still
preserve shared references and cycles; distinct mutable tiles remain distinct.
Resident records are stored once in an independent compressed section so
Read Records can read the archive without loading the world.

A separate diagnostic imported the **same copied format-4 world** with the old
assembly, then serialized and loaded it with the new graph codec, under the same
Docker/Mono limits (one CPU, 3 GiB). This importer is diagnostic only; production
does not load old world formats.

| Case | Original codec | Compact codec |
| --- | ---: | ---: |
| Graph + GZip, three-sample write median | 32.56 s | 7.79 s |
| Compressed graph size | 25,601,514 bytes | 13,510,655 bytes |
| Raw graph size | 1,104,833,938 bytes | 90,058,454 bytes |
| Cold graph load, one sample | 101.78 s | 10.81 s |
| Independent resident archive load, one sample | — | 0.46 s |
| Independent resident archive compressed size | 2,650,017 bytes | 1,987,874 bytes |

The compact write samples were 8.05, 7.79 and 7.76 seconds. Loading the compact
graph and serializing it again produced the same SHA-256 raw-stream digest,
covering all fields and reference IDs. These measurements isolate the graph
codec: they exclude atomic file replacement, records snapshot refresh, the new
format-5 envelope, new metadata fields, simulation-worker waiting and UI work.
They are not measurements of the complete in-game pause. The earlier 99.69 s
cold load and this import's 101.78 s are separate runs of the old reader.

Named scenarios verify the actual format-5 envelope, archive stored once,
archive-only reads, counters, shared references, tile independence, flags,
decorations, clocks/RNG, resolved history and atomic backup recovery. The VNC
E2E also saves/loads a newly generated world and exercises record search,
filters, sorting and the interesting-NPC selector. No live container was
restarted for this work. See [save-format.md](save-format.md) for the wire layout.

Local Docker/Mono measurements during this refactor (milliseconds for the
specified number of calls):

| Case | Calls | Before | After |
| --- | ---: | ---: | ---: |
| Minimap 40×40 | 300 | 55.28 | 33.44 |
| Mouse route 25×25 | 1,000 | 253.30 | 54.65 |
| AI choose, ten candidates | 100,000 | 46.50 | 17.24 |

These are illustrative local results; container scheduling can cause large
variation between runs. Named scenarios check behavior separately from timing.

Additional local Docker/Mono measurements (median of five, same benchmark
setup before and after each change):

| Case | Calls | Before | After |
| --- | ---: | ---: | ---: |
| NPC route on 40×40 map | 1,000 | 19.43 ms | 12.71 ms |
| Insert scents on 1,600 tiles | 1 | 3.33 ms | 1.12 ms |
| One blast with 800 ground stacks | 20 | 169.26 ms | 4.23 ms |

An idle district worker
made 10 callbacks in 100 ms before the change and one afterward; notification
now wakes it when the player district advances. A 40×40 map's presave traversal
took 4.39 ms for 100 calls. Zone lookup took roughly 165 ms for 100,000 calls;
an attempted change did not improve that measurement and was reverted.

Second optimization pass, using the same five-sample median method:

| Case | Calls | Before | After |
| --- | ---: | ---: | ---: |
| Grayscale image 128×128 | 50 | 127.71 ms | 57.56 ms |
| Draw 20 overlays | 100,000 | 26.30 ms | 18.07 ms |
| Search 40×40 region for exits | 10,000 | 210.14 ms | 0.27 ms |
| Field of view on 40×40 map | 1,000 | 17.10 ms | 15.02 ms |
| Corpse membership among 300 corpses | 100,000 | 53.75 ms | 6.56 ms |

The exit check on a 1×1 query with 100 exits improved from
235.32 ms to 3.24 ms for 100,000 calls after this adjustment.

Measured candidates left unchanged: resolving a missing asset through five mods
cost 135.50 ms for 10,000 calls, parsing the equivalent of all 131 bundled CSV
rows cost 11.75 ms for 100 passes, and matching 100 mod stamps to 100 available
mods cost 119.52 ms for 1,000 calls. These operations run mainly during startup
or save loading. Inventory stacks have small capacity; an index there would add
maintenance cost. Grayscale variants are still generated during startup to
avoid introducing first-use pauses during play.

For a concise list of the changes behind these measurements, see
[optimizations.md](../optimizations.md).

## NPC turn profile

Local Docker/Mono run on October 4, 2026, using `--bench-npc-turn` on one
100×100 generated surface: 75 configured civilians plus six building residents,
200 shambling zombies, eight full map turns. The median of five fresh runs was
136.0 ms total, or 17.0 ms per map turn. It performed 813 civilian and 987
zombie actions. In that median run:

- Field-of-view computation used 27.3 ms for civilians and 28.3 ms for zombies:
  **55.6 ms combined, 40.9%** of the total.
- Civilian decision logic used 44.4 ms, including 16.2 ms preparing NPC
  intents and 1.5 ms in 924 reachability checks. Zombie decisions used 15.5 ms.
- Performing chosen actions used 4.1 ms for civilians and 1.6 ms for zombies.
  Map upkeep used 1.3 ms; action legality and actor dispatch were smaller.

These are inclusive measurements: field of view is part of sensing, and intent
preparation and route checks are part of decision time. The benchmark replaces
the generated controllers with timing subclasses, performs their real legal
actions and advances the real map clock. It does not advance basements or other
districts, draw the UI, or include fixture generation in the timed interval.
The fixture has only shambling zombies so their controller phase can be measured
consistently; different zombie mixes, longer runs and denser social interactions
may change the ranking. The timing hooks also add some measurement overhead.

## AI and generation profile

Local Docker/Mono measurements on a 40×40 map (median of five runs):

| Case | Calls | Time |
| --- | ---: | ---: |
| NPC sight with 30 nearby actors | 1,000 | 35.80 ms |
| Civilian action choice with 30 actors | 1,000 | 47.70 ms |
| Classify the visible percepts | 1,000 | 2.38 ms |
| Four reachability checks | 1,000 | 36.12 ms |
| Generate a residential surface | 1 | 1.83 ms |
| Generate a surface with 30 civilians | 1 | 1.94 ms |
| Place an actor on a 94% occupied map | 1 | about 0.01 ms |

These are targeted cases, not a profile of the entire city or a full game
turn. The action benchmark chooses actions repeatedly without performing them;
it isolates decision cost rather than simulation cost. Actor placement uses a
fixed seed and a newly prepared dense map for each timed sample.
The lightweight arena fixture does not support a repeatable full-world
generation benchmark yet; that needs a separate startup fixture.

The same 40×40, 31 actor fixture now compares service history checks with
eight closed agreements per actor. A 10,000-call sample checks every actor;
Docker/Mono medians on 2026-10-03 were 8.01 ms for scanning the lists and
2.16 ms for the transient open-agreement indicator. This isolates the guard
cost; it is not a measurement of a full city turn.

The measurements point first to field-of-view computation inside the sight
sensor, then to repeated reachability checks for multiple targets. Potential
next experiments are a map-revision-aware FOV cache and a shared traversal for
multiple reachability targets. Both need scenario tests for terrain changes,
doors, weather, actor movement, and action ordering before they can be used.

Three smaller changes were tried and reverted because their timing was flat
or worse: scanning zone names directly (167.52 to 168.79 ms per 100,000 calls),
removing a distance check from sight (29.41 to 30.46 ms per 1,000 calls on the
earlier fixture), and reusing a stored route distance (51.99 to 53.84 ms per
1,000 groups of four). Returning the original percept list instead of copying
it changed 47.70 to 46.70 ms per 1,000 civilian decisions, within local run
variation, and risked exposing a mutable alias. It was also reverted. The
generator and dense placement cases did not warrant a behavior-changing
algorithm on these workloads.

## FOV and route investigation

Three further runs on the same fixed 40×40 fixture gave these ranges (per
1,000 calls, median of five samples within each run):

| Case | Original | Squared-radius FOV |
| --- | ---: | ---: |
| Compute FOV | 24.11–24.55 ms | 20.73–21.12 ms |
| Civilian action choice | 45.93–46.80 ms | 42.50–44.38 ms |

The FOV loop now compares squared distances, avoiding a square root for each
candidate cell. `FovRadiusEquivalenceTests` checks the old and new predicates
for offsets from −128 to 128 and radii from 0 to 100. The new sight and door
scenarios cover changes to visible actors and door state.

For an unchanged map, scanning an already computed FOV took 6.33–6.39 ms per
1,000 calls; recomputing the FOV took 24.11–24.66 ms. Four unchanged route
lookups took 0.15–0.19 ms per 1,000 groups versus 35.10–35.66 ms for four
fresh reachability checks. These are best-case bounds, not expected game
speedups: the fixture keeps actors and terrain fixed, and cache invalidation
is excluded. No cache was added.

The door scenarios show that both sight and routes change within the same map
turn when a door opens or closes. `npc/route-finder-detour` also shows why a
normal shared BFS cannot replace the current goal-directed reachability check:
the current AI intentionally rejects paths that first move no closer to its
target. None of the 31 actors in this benchmark fixture had enough speed for
more than one ordinary action per map turn. A FOV cache therefore needs a
measured hit rate in actual play before its invalidation cost is justified.

## Precomputed FOV rays

On October 5, 2026, six alternating Docker/Mono runs of the crowded NPC-turn
fixture compared the previous Bresenham trace with precomputed ray geometry.
The median for eight map turns fell from 107.7 to 102.7 ms; FOV time within
those turns fell from 30.9 to 29.0 ms. The 40×40 open-arena FOV microbenchmark
changed from 8.24 to 8.01 ms per 1,000 calls. The improvement is modest and
run-to-run variation is visible. Transparency is still checked each call, so
moving objects and doors take effect immediately. The FOV equivalence scenario
compares the visible cells across door, lighting, and sleeping changes.

Recursive shadowcasting was also prototyped. It changed visibility at corners
and was slower on the open arena, so it was not kept.

## Radio broadcasts

On October 7, 2026, a Docker/Mono fixture with nine maps, 216 NPC sources,
3,456 tellable `shared_food` facts and 20 nearby listeners measured 80
broadcasts across 40 actual game hours. Five-run medians, repeated twice,
were 11.1–12.0 ms for Survivor Network, 1.5–1.6 ms for Military Dispatch,
11.3–11.6 ms for Local Calls and 4.1 ms for Gang Frequency before skipping
repeat listeners in the same hour. With that check, the same fixture measures
10.5 ms for Survivor Network and 10.4 ms for Local Calls. Military and
Gang have no matching facts in this fixture. With no nearby listener the
same city takes 0.2 ms for 80 broadcasts. These are radio delivery and
selection costs, not complete game turns or world generation.

Earlier fixed-slot runs fell from 44.6 to 19.1 ms for 80 survivor broadcasts
after avoiding world scans without listeners. An interim shared-program
fixture used 60-turn slots, which are two game hours, so its 6.6–6.9 ms
result is not directly comparable with the corrected hourly fixture. The
current implementation indexes the player's known event and story IDs before
ranking headlines, skips copying a rumor the listener already knows, and
checks `shared_food` directly on Local Calls before substring matching. A
listener processes each station program once per hour, even when several
receivers are nearby.

The archival [before](../benchmarks/profiles/radio-before.svg) and
[after](../benchmarks/profiles/radio-after.svg) flamegraphs show the fixed-slot
optimization. The [shared-program graph](../benchmarks/profiles/radio-shared.svg)
profiles the corrected 40-hour fixture with a live station host. Its inclusive
root time is 465 ms with Mono call instrumentation: 310 ms under
`BuildRadioProgram` for 40 generated programs, 107 ms under
`NpcKnowledgeSystem.Hear` for 5,200 listener deliveries, and 1 ms in shared
`NpcConversation.Chapter` assembly.
Profiler overhead means these values are not game frame times. The benchmark
is reproducible with `--bench-radio` and
`PROFILE_KIND=radio sh benchmarks/bench-npc-calls.sh`.

A further October 7 pass reused station-kind decisions and stopped ranking a
fact once even its maximum player-familiarity bonus could not beat the current
headline. On the same fixture, 80-broadcast medians changed from 9.9 to 7.3 ms
for Survivor Network, 10.4 to 6.5 ms for Local Calls, and 5.0 to 1.1 ms for
Gang Frequency. Military Dispatch stayed near 1.2–1.5 ms. The call profile
fell from 103,080 to 37,008 known-event lookups per 80 survivor broadcasts;
its instrumented root fell from 475 to 278 ms. The
[pruned-program flamegraph](../benchmarks/profiles/radio-pruned.svg) captures
the final profile. The fixture has one repeated fact kind, so the kind-cache
gain will vary with the mix of events in a city.

On October 8, retelling checks were changed to scan recent facts without a
`List.Find` delegate, and headline ranking reuses the known-story decision
for consecutive facts in the same story. Alternating two clean-checkout runs
with two optimized runs gave 7.1–8.4 versus 5.7–7.1 ms for Survivor Network
and 6.5 versus 5.4–5.8 ms for Local Calls per 80 broadcasts. The Mono call
profile fell from 319 to 239 ms inclusive at `RadioBenchmarks.RunBroadcasts`;
`NpcKnowledgeSystem.Hear` fell from 115 to 65 ms and `BuildRadioProgram` from
153 to 125 ms. The [updated flamegraph](../benchmarks/profiles/radio-recent.svg)
shows the remaining call costs. These are fixture timings, and the
instrumented profile is not a frame-time measurement.

## Rest simulation profile

`sh tests/scenario.sh --bench-rest-simulation` performs four real player waits
with distant simulation enabled. The fixed seed creates nine 40×40 districts
with surface and sewer maps, 150 living NPCs, 54 undead, and 12 border links.
The median of five fresh Docker/Mono runs was **43.6 ms for four waits**, or
**10.9 ms per wait** (samples: 41.3, 42.1, 43.6, 44.2, 45.4 ms). Fixture setup
is outside the timer; this is a 3×3 workload, not a full 5×5 city estimate.

`PROFILE_KIND=rest sh benchmarks/bench-npc-calls.sh` records the same wait path.
The [rest simulation flamegraph](../benchmarks/profiles/rest-simulation.svg) shows
1,252 AI actions. Under Mono call instrumentation, `LOSSensor.Sense` occupies
2,289 of the 4,196 ms root window, including 1,734 ms in `LOS.ComputeFOVFor`.
`NextMapTurn` occupies 86 ms; the district-selection loop has no measurable
self time in this report. FOV computation is the first target; the existing
map-revision and action-order concerns for an FOV cache still apply. Replacing
the district scan would have little effect on this fixture.

Map-object lookup occurs roughly 100,000 times in that wait path. Replacing its
transient `Dictionary<Point, MapObject>` with a flat array indexed by tile
coordinates avoids hashing on every FOV transparency check. In alternating
Docker runs against the pre-change image, the four-wait medians were **45.1 and
44.5 ms before**, versus **39.9 and 39.1 ms after** (about 12% faster). A
separate eight-turn NPC benchmark changed from 104.7 to 92.1 ms. The
[indexed-object flamegraph](../benchmarks/profiles/rest-simulation-indexed.svg)
records the new path: `LOSSensor.Sense` occupies 1,998 of 3,751 ms, including
1,457 ms in `LOS.ComputeFOVFor`. These profiled times are heavily inflated
and should only guide which code to benchmark next. The array is rebuilt after
load and uses one reference per map tile, including tiles without objects.

## Rest optimization experiments, October 9, 2026

The targeted 40×40 fixture contains 31 actors. Alternating Docker/Mono runs
against the frozen pre-change image gave these median ranges (five samples per
run):

| Operation | Before | After |
| --- | ---: | ---: |
| Occupied actor lookup, 100,000 calls | 4.72–5.17 ms | 0.28–0.32 ms |
| Empty actor lookup, 100,000 calls | 2.67–3.10 ms | 0.29–0.38 ms |
| 10,000 pairs of actor moves, 31 actors | 2.99–3.44 ms | 1.99–4.63 ms |
| Sense empty item/corpse map, 1,000 calls | 27.02–29.80 ms | 15.01–16.16 ms |
| Sense one item stack and two stacked corpses, 1,000 calls | 28.38–29.13 ms | 21.99–23.15 ms |
| Sense 65 item stacks and 66 corpses, 1,000 calls | 37.69–38.38 ms | 35.79–38.63 ms |

Actor positions now use a transient flat array. It is updated on every move,
spawn, removal, and cross-map transition and rebuilt after load. A 100×100 map
uses about 78 KiB for the array on a 64-bit runtime. Movement timings have one
high after-change outlier, so they do not establish a precise update-time
speedup; the lookup improvement is consistent. The empty-map sensing path
checks existing collection counts. The sparse path handles a single item stack
or a single corpse directly, preserving percept order without temporary sort
collections. Multiple resource positions retain the original FOV scan.

Alternating full 3×3 rest runs gave **38.8 and 39.8 ms before** versus **35.7
and 35.9 ms after** for four waits. The separate sleep probe measured ten real
map turns with no worker at **137.02 ms** under the old `SimThread` option check
and **0.22–0.29 ms** when checking for an actual worker. With a worker present,
the optimized path still yielded (**122.28 ms**). The forced pause was meant
to give the former nearby-district simulation thread CPU time; full-city rest
simulation does not start that worker.

The FOV cache experiment measured its optimistic hit ceiling in the same rest
fixture: 299–311 of roughly 1,250 FOV calls repeated an actor's map, position,
and range, but only 57 repeated within one map turn. A key check including
`ActorFOV` took **3.60–3.77 ms per 100,000 calls**. The 40×40 static FOV probe
took about **5.80 ms per 1,000 recomputations**. At that per-call cost, all 57
same-turn candidates together would save roughly 0.3 ms per four waits before
tracking doors and other visibility changes. Cross-turn reuse also needs reliable invalidation for
map-object state, fire, lighting, weather, and nearby carried lights. No FOV
cache was retained. The targeted benchmarks do not measure the rendering cost
of sleep or a long district catch-up transition.

## Large-city rest and progress, October 9, 2026

The 3×3 benchmark starts the player in a corner, so five districts are outside
the one-cell nearby radius. It is still too small to represent a crowded city.
`sh tests/scenario.sh --bench-rest-large` generates 5×5 districts with 1,780
living NPCs, 150 undead, and 40 border links; 21 districts are beyond that
radius. Four real waits with full simulation took **588.1 ms** median across
three fresh runs (559.9, 588.1, 592.5 ms). Fixture generation and rendering
are outside the timer; actual hour-long waits can be interrupted and become
slower as the world evolves. The benchmark retains full NPC action frequency.

During sleep and long waits, the game displays district-turn progress and an
elapsed-rate ETA once the city has simulated long enough to estimate one. Long
waits count down their remaining game turns, and sleep shows approximate turns
until rested. Both show a wall-clock ETA after at least one game turn. Sleep redrawing is limited
to four times per real second instead of once per map turn. The ETA is an
estimate because later turns can cost more. A save/load scenario checks that
simulated district time, a remote NPC, and a rumor it learned survive the round
trip.

Long sleep and hour-long waits now spread an existing distant-district backlog
over their remaining player turns. Each rest turn has a budget for the new
district turns plus a proportional share of the older backlog, with oldest
district turns simulated first. A single wait still catches up fully. Natural
completion, waking, or an interrupted long wait drains any remainder before
the next player command. Every district turn and NPC action still runs, but
arrivals and rumors from a previously paused district can reach the player
later during rest. Named scenarios cover sleep, long waits, interruption and
rumor transfer; the existing save/load scenario checks the final world state.
This spreads latency rather than reducing total simulation work.

## Full-density 5×5 call profile and experiments, October 9, 2026

`PROFILE_KIND=rest_large sh benchmarks/bench-npc-calls.sh` profiles the same
full-detail 5×5 fixture. The [large-city flamegraph](../benchmarks/profiles/rest-large.svg)
contains 8,938 NPC actions in four waits. Mono call instrumentation stretched
the measured interval to 41.8 seconds, compared with roughly 0.54–0.59 seconds
without instrumentation. Inclusive time in `LOS.ComputeFOVFor` was 13.8 seconds
(8,938 calls, about 33% of the profile interval); `LOSSensor.Sense` was 17.1
seconds including FOV. `PrepareNpcIntents` was 7.7 seconds and 249 rumor-sharing
actions occupied 3.6 seconds inclusive. These inclusive figures overlap and
cannot be added. They identify hot paths, not wall-clock speedup estimates.

Two behavior-preserving ideas were benchmarked and reverted:

- Reusing thread-local FOV cell and wall buffers preserved the equivalence
  scenario, but the 5×5 median was 551.1 ms before and 554.6 ms after; an
  isolated 1,000-call FOV check was 6.14 and 6.03 ms. There was no established
  end-to-end gain.
- For two ground item stacks and three corpses, scanning a small resource
  array in FOV order improved the targeted 1,000-call medians from 27.8–28.1
  to 26.3–26.6 ms, while the dense case slowed from about 35 to 38 ms.
  Alternating 5×5 run medians overlapped (before 539.6–551.7, after
  541.9–544.1 ms). The simpler original sensor was retained. A named
  sparse-resource scenario verifies item and corpse percept order.

All measurements retained full NPC action frequency and normal rumor
generation. `benchmarks/RestOptimizationBenchmarks.cs` now includes a
two-item, three-corpse probe for future experiments.

## Lossless full-city simulation tuning, October 10, 2026

The 5×5 call profile showed 36,467 goal offers in four waits. Each offer used
to recalculate six actor-only trait biases. Goal evaluation now calculates
those biases once per NPC evaluation pass; person-specific relationship and
faction values are still calculated for every offer. The context lasts only
for that pass, so the next NPC action sees any new trait or event. The
`npc/goal-bias-reuse` scenario compares every generated goal's importance and
utility with a fresh calculation across multiple people and after a trait
change. No NPC action, event, or rumor is deferred or omitted.

Four alternating 5×5 Docker runs, each the median of three fresh fixtures,
measured 528.8, 532.7, 557.9, 559.7 ms before and 490.3, 498.3, 519.7,
524.6 ms after for four waits with full simulation. The median of run medians
was **545.3 → 509.0 ms (6.7% faster)**. Load variation remains visible in
individual runs. The small change to FOV wall-neighbor coordinate arithmetic
was already present in both versions of this comparison; its separate 5×5
comparisons showed about a 2% gain, while the open-map FOV microbenchmark was
effectively unchanged. Two further FOV experiments, a custom `Point` hash
and different ray-cell arithmetic, were removed after failing to show a
consistent gain. The full Docker test target passed 366 scenarios.

## Full-density simulation follow-up, October 11, 2026

The 5×5 fixture still runs every NPC action and rumor exchange. A fresh Mono
call profile recorded 9,041 AI actions over four waits. Instrumentation stretched
the measured interval to 39.2 seconds, so its times are **not** wall-clock
performance: `LOS.ComputeFOVFor` used 10.5 seconds inclusive, `Rules.ActorFOV`
4.7 seconds inclusive, `PersonalitySystem.Report` 5.1 seconds inclusive, and
`ActionNpcTell.Perform` 3.8 seconds inclusive. These paths overlap. The
district-selection loop used 7 ms self time, and `GetNextActorToAct` 163 ms
inclusive. Replacing the scheduler or actor selection cannot remove most of
this workload. The profile is in the temporary directory reported by the
profiling script; rerun with `PROFILE_KIND=rest_large sh benchmarks/bench-npc-calls.sh`.

`ActorFOV` now checks the eight neighboring cells directly when looking for a
carried light. This avoids constructing a predicate and calling the generic
adjacency helper on a very frequent path. The named corner/diagonal scenario
checks the same light and battery behavior. Two unprofiled 5×5 run medians
before the change were **523.2 and 511.2 ms**, and two after were **497.4 and
502.5 ms**, each for four waits with three fresh fixtures per run. The midpoint
of those medians is about 3% lower, but the runs were not tightly paired and
the individual samples overlap. The full Docker test target passed 373 scenarios.
The focused 40×40 lit-map FOV case stayed at 5.97–5.98 ms per 1,000 calls;
it does not exercise the neighboring-light branch changed here.

A transient flat array for ground-inventory lookup was also tried because the
profile recorded 438,187 lookups in four waits. Its repeated full-city median
was **490.0 ms**, versus **491.1 ms** for the previous image in an adjacent run.
The negligible difference did not justify another per-map array and update
path, so this experiment was removed. The remaining high-cost paths are NPC
field of view, goals, and event observation; they need a behavior-equivalent
design and a full-city before/after measurement before replacing a library or
runtime component.

### Measured waiting phases and goal evaluation

The earlier Mono call profile identifies call paths, but its inclusive times
overlap and its 36-second run was slowed by profiler instrumentation. A new
`--bench-rest-phases` benchmark alternates three plain and three timed runs of
30 player waits in a fresh 5×5 city with full NPC simulation. Around 61,000
AI actions, 71,000 goal evaluations, 3,700 reported events, and 415 rumor
shares occur per run. In the first lightly instrumented pass, the plain median
was 4.55 seconds and the timed median 4.61 seconds. Of that timed interval,
action choice took 3.59 seconds, including 1.89 seconds of intent preparation
and 0.56 seconds of field of view. Action execution took 0.89 seconds; event
reporting across the sampled phases took 0.74 seconds. District scheduling and
actor dispatch outside those phases accounted for about 0.04 seconds. Nested
phase times must not be added together. The 0.2 ms radio-hour check in this fixture does not measure
an active receiver; use the radio benchmark for broadcasts.

More detailed timers found about 1.19 million checks for an already pending
goal. `NpcGoalContext.Pending` obtained an `AsReadOnly` wrapper for the same
intent list on every check. Reading the existing internal list directly
reduced the timed check from about 142 to 76 ms. Adjacent three-sample plain
medians were 4.40/4.42 seconds before and 4.29 seconds after; a repeat of the
old image measured 4.42 seconds. The change creates no index and does not
alter goal order or state. The same direct-list access in goal application and
trait-bias evaluation gave smaller gains: the final plain median was 4.22
seconds, versus 4.25/4.28 seconds for the preceding image. These small
differences are close to run-to-run variation. First-generation GC collections
in the timed runs fell from roughly 440 to 401. The detailed timers themselves
add around 0.6–0.7 seconds, so their times rank work but are not direct
wall-clock costs; the plain runs provide the before/after comparison.

The goal-source breakdown points to food aid as the largest source in this
fixture (about 0.5 seconds with detailed timers), followed by medical care
and interests. This is repeated real goal evaluation for known people, not a
district scheduler stall. Any further change to the number or timing of those
evaluations needs to preserve goal creation, NPC actions, and rumor delivery.
`PROFILE_KIND=rest_large sh benchmarks/bench-npc-calls.sh` regenerates the Mono
call flamegraph; the final snapshot is `benchmarks/profiles/rest-large.svg`.
The named `world/adjacent-light-fov` scenario and the full
Docker test target passed all 373 scenarios after these changes.

### Decision-path follow-up

The 30-wait phase fixture puts roughly 61,000 NPC decisions through a 5×5
city. Within decision making, preparing intentions dominates: its timed
samples used about 1.6–2.1 seconds, with goal evaluation accounting for most
of that work. Knowledge perception used about 0.3 seconds and exploration
about 0.16 seconds. These nested timings rank code paths; they are not
independent shares of the uninstrumented run.

Goal application formerly sorted every candidate by utility and then by its
constructed string key, even when all candidates were below the admission
threshold. A full-detail run saw roughly 54,000 such evaluations out of
72,000. Application still refreshes and finishes existing goals, but skips the
sort if no candidate is viable. If exactly one is viable, it sorts by utility
only; multiple viable candidates retain the original key tie-break. The
timed sort fell from about 763 to 34–49 ms in adjacent runs. Three-sample
plain medians moved from about 4.42 to 3.53 seconds for 30 waits. The
`npc/goal-sort-skip` scenario checks low-utility refresh, viable admission,
and tied viable order.

Food aid was the largest goal source, with roughly 0.48 million care offers
per 30 waits. A candidate now passes its already-known person to motivation
calculation and the enum-based offer keeps its resolved value and capability
definitions, avoiding repeated bounded-list and catalog lookups. The
`npc/goal-bias-reuse` scenario compares generated utility against a fresh
calculation, including multiple known people and a trait change. In an
alternating repeat with comparable offer counts, the timed care-offer path
used 405 ms before the catalog-lookup change and 347 ms after; the plain
run medians were 3.42 and 3.38 seconds. Another adjacent pair before and
after passing the known person measured 3.54 and 3.43 seconds. Whole-run
figures vary with fixture action counts and Docker load, so the path timing
is stronger evidence than either small total difference.

Perception probes split its approximately 0.34-second timed path into
observed people (~0.15 seconds for 166,000 observations), items (~0.04
seconds), expiry (~0.03 seconds), and surroundings (~0.06 seconds). Replacing
the two short list scans in `NpcKnowledge.See` with one scan showed no
end-to-end improvement and was reverted. Removing the read-only wrapper
around the sorted known-person list likewise showed no improvement and was
reverted. These are not the next high-yield targets. No new cache or index was
added. The complete Docker test target passed all 374 scenarios after the
retained changes.

### Decision-path recheck, October 11, 2026

A fresh 5×5, 30-wait run on `6fc1f04` measured **3.378 seconds** without
probes. In the timed run, intention preparation used 1.648 seconds, including
1.446 seconds in goal evaluation. Knowledge perception used 0.366 seconds
and exploration decisions 0.168 seconds. Within goal evaluation, food aid
was the largest source at 0.451 seconds, followed by medical care at 0.150
seconds and lasting interests at 0.132 seconds. These are nested, probed
times; they cannot be added or treated as uninstrumented wall time. The
corresponding Mono call profile and flamegraph are in
`/private/tmp/rogue-decision-calls/`; call instrumentation stretched four
waits to about 36 seconds and served to identify call paths and counts.

The goal context originally spent about 115 ms per 30 waits computing six
trait influences through six passes. `PersonalitySystem.GoalBiases` now visits
each trait once with the same effect predicates and per-axis clamping. The
timed trait portion fell to about 29 ms; adjacent plain medians changed from
3.388 to 3.268 seconds. The `npc/goal-bias-single-pass` scenario compares
all six values with the original `Bias` calculation after every registered
trait is added and with personalities disabled. `NpcMotivation` also became a
temporary value type: a paired repeat recorded 3.440 versus 3.349 seconds
and 332 versus 317 Gen0 collections, while the first adjacent pair had
almost identical elapsed time. Its whole-run gain is therefore uncertain,
but the allocation reduction is repeatable.

Exploration previously rejected visited neighbors and then searched the same
visited list to score the remaining neighbors. Their age must be zero, so the
second scan was removed. A paired run measured 168 versus 136 ms for
exploration and 3.266 versus 3.235 seconds for the plain full-city run.
`npc/explore-unvisited-age` preserves the seeded direction and checks the
all-neighbors-visited boundary.

Knowledge observation previously searched the bounded people list once by
identity and again to remove the same record before appending it. `See` now
keeps the first index, removes by index, and reads the actor identity once.
An adjacent pair measured 160 versus 157 ms for person perception and 3.312
versus 3.274 seconds for the plain full-city run. With identity hoisted, a
later timed run measured 148 ms for person perception. Whole-run differences
remain within run-to-run variation.
`npc/knowledge-see-order` checks record identity, recency, revision, and
capacity eviction.

Motivation evaluation for a known person previously looked up the same
relationship twice: once for attitude and once for attachment, fear, and
grievance. It now reuses the record returned by the attitude calculation.
A nearby repeat measured about 351 ms for 473 thousand food-care offers with
the duplicate lookup and 341 ms for 486 thousand offers without it. Plain
full-city medians were effectively unchanged (3.387 versus 3.385 seconds),
so no whole-run gain is claimed.

Reusing the context's supply influence in food-reserve interests reduced the
timed interest source, but failed `npc/content-module`: custom catalogs can
use a different personality registry, and the original global-registry
lookup is intentional there. It was reverted. Direct enum comparison in
pending-goal checks, storing candidates or perception contexts as value
types, passing the known-person record into medical perception, and computing
observed max HP once were also tried. None showed a reliable full-run
benefit; they were reverted. The retained changes add no cache or index and
do not skip actions, observations, or rumors.

The final plain 30-wait median was 3.322 seconds versus 3.378 seconds at
`6fc1f04`; the difference is small compared with variation between runs.
The complete Docker test target passed all 377 scenarios, layout checks, and
the flamegraph parser check on the final code.

### SelectAction follow-up, October 11, 2026

A new Mono call profile in `/private/tmp/rogue-select-calls/` attributed
13.7 seconds to `CivilianAI.SelectAction` over four profiled waits, but only
0.12 seconds was self time. Its largest children were intention preparation
(5.8 seconds), item collection (2.1 seconds), exploration (0.94 seconds),
and ranged attacks (0.87 seconds). These are instrumentation-inflated call
times, not expected player wait times. A plain 5×5, 30-wait baseline was
3.370 seconds; phase probes put intention preparation at about 1.6 seconds.

The item and trade branches now filter percepts once instead of building an
intermediate stack or non-enemy list and then filtering that list again. They
keep the same order, predicates, and action choice. In paired 30-wait runs,
the timed item branch fell from 50.8 to 47.1 ms and the trade branch from
182.9 to 166.6 ms. Exploration now tests and scores each candidate using
one adjacent location rather than computing it twice. Its paired timed
branch fell from 150.2 to 144.9 ms over almost identical call counts.

Plain whole-run medians varied by tens of milliseconds between adjacent
runs; these changes have no established end-to-end speedup beyond that
variation. No persistent cache, index, action pruning, or rumor pruning was
introduced. The existing `npc/explore-unvisited-age`,
`npc/stolen-stack-capacity`, and personality trade scenarios passed. The
complete Docker test target passed all 377 scenarios, layout checks, and
the flamegraph parser check.

### Goal offer construction, October 11, 2026

The 5×5 district, 30-wait profile put goal evaluation at 1,429 ms for about
71,000 evaluations. The food aid source alone took 454 ms. A temporary count
found 461,707 zero-deficit offers against 33,937 positive offers. Most of the
time in that source was spent constructing candidates that could not start a
goal. This was game-side C# work, independent of Docker or headless rendering.

During `NpcGoalGenerator.Refresh`, `NpcGoalOffers` now omits zero-deficit
candidates when the owner has no active generated goal to update. It still
counts omitted offers toward the 192-offer limit. Full `Evaluate` calls keep
their complete candidate list, and active goals still receive zero-deficit
offers so they can complete normally. This adds no persistent cache or index.

In a paired run of the same benchmark, food-care offer construction fell from
331 to 61 ms over about 475,000 calls; the food aid source fell from 454 to
186 ms and goal evaluation from 1,429 to 1,283 ms. Gen0 collections fell from
305 to 264. An initial pair of plain whole-run medians was 3,297 ms before
and 3,411 ms after. A subsequent alternating old/new/new/old check yielded
3,366 and 3,516 ms for the old code, versus 3,020 and 2,940 ms for the new
code. The latter check shows an approximately 13% reduction in whole-run
time, but the first pair shows that regenerated worlds and machine load make
the exact gain uncertain. `npc/goal-inert-offer` covers the offer limit, goal
completion, and positive needs. The full Docker test target passed all 378
scenarios, layout checks, and the flamegraph parser check.

### Follow-up candidates, October 12, 2026

Two more event-sight changes were measured on alternating 5×5, 30-wait
runs and reverted. Sharing one FOV calculation between an event's two
participants produced no stable gain (plain medians 3,132 and 3,169 ms before,
3,278 and 3,201 ms after). A strict per-actor FOV upper bound reduced timed
event reporting by roughly 5–7% per event but did not improve the full run
(plain medians 2,924 and 3,012 ms before, 3,046 and 3,030 ms after). Event
reporting includes goal refresh and so is not additive with goal evaluation.

Refreshing existing lasting interests in place instead of allocating a
temporary interest also failed to improve the source or Gen0 count. The
interest source took 128–133 ms before and 134 ms after; plain-run medians
overlapped. That change was reverted too. The remaining measured areas worth
investigating are goal sources (medical care, food aid and interests at roughly
130–190 ms each), knowledge perception (about 239 ms), and event reporting
(about 500–580 ms including nested goal work). These are instrumented phase
times, not directly additive parts of the plain 3-second wait benchmark.

### Pending goal checks, October 12, 2026

The updated large-rest Mono call profile in `/private/tmp/rogue-after-goals-calls/`
attributed 284 ms to `MedicalCareModule.Evaluate` over four waits, including
173 ms in repeated `NpcGoalContext.Pending` checks. `NpcInterestSystem` took
311 ms and knowledge perception 2,964 ms inclusive under this instrumented
profile. The latter includes 1,683 ms in registered perception handlers;
overall FOV construction was a larger 10,123 ms. These call-profiler numbers
are heavily inflated and should only rank call chains.

`NpcGoalContext` now records whether the owner has any active generated goal
once per evaluation. `Pending` returns immediately when there is none, and
`NpcGoalOffers` uses the same local fact for its zero-deficit shortcut. No
persistent cache or person index is added. In alternating 5×5, 30-wait runs,
full pending scans dropped from 1.17–1.21 million (74–76 ms timed) to about
11,000 (1 ms timed). The medical goal source fell from 156–158 to 55–61 ms;
goal evaluation fell from 1,166–1,185 to 918–984 ms. Plain medians were
3,067 and 3,209 ms before versus 3,050 and 2,867 ms after. The full-run
difference is smaller than the instrumented subphase gain and varies between
world generations. `npc/goal-inert-offer` now checks inactive, active, and
finished generated goals. The full Docker test target passed all 378 scenarios,
layout checks, and the flamegraph parser check.

### Experimental parallel district turns, October 12, 2026

`sh tests/scenario.sh --bench-rest-parallel` runs the real 5×5, 30-wait
fixture with one turn per district at each player turn. Its experimental
scheduler processes four nonadjacent district groups in sequence and runs
each group on 1, 2, or 4 workers. All 25 district clocks reached turn 30.
The latest three-sample uninstrumented medians were 2,874 ms for the existing
serial scheduler, 2,904 ms for the grouped scheduler on one worker, 2,094 ms
on two workers, and 1,732 ms on four workers. Thus four workers have about
40% lower elapsed time than the serial baseline in this fixture, including
group scheduling overhead. A first run gave 2,872, 2,073, and 1,816 ms for
one, two, and four workers, respectively.

This is only a throughput probe, not a production-safe mode. District turns
still share the game's RNG, resident records, story director, radio program
builder, and other mutable `RogueGame` state. NPCs can cross district borders
during a turn. The fresh fixed-seed fixture also produces random GUIDs, and
even its serial runs varied in NPC and rumor counts, so the coarse outcome
counters cannot establish equivalence. No parallel scheduling was enabled
in the game. A production version needs explicit ownership or synchronization
of global writes, a defined cross-district event order, and stress tests
around border crossings, radio hours, and story updates.

`sh tests/scenario.sh --bench-rest-parallel-snapshot` now replays the same
saved 5×5 start for two serial runs and grouped 1-, 2-, and 4-worker runs.
It compares sorted actor state, NPC facts, intents, observed events, and
stories, excluding random IDs and save serialization order. The two serial
runs matched the logical digests after 30 waits. Grouped scheduling diverged
even with one worker: 1,887 actors and 20,877 facts versus 1,894 actors and
21,194 facts in both serial runs. This isolates a turn-order change before
any concurrent writes. Two and four workers also diverged. The command
reports `PARALLEL EQUIVALENCE: FAIL` and exits nonzero when state differs.

## Current Docker save and tile loading, October 10, 2026

The running game's `save.dat` was 64,480,804 bytes (61.5 MiB) at turn 5,877;
its backup was 55,798,195 bytes. The current file contains a 19,547,440-byte
compressed resident archive and a 44,933,314-byte compressed world graph. The
two sections expand to 125,788,131 and 307,271,822 bytes respectively. Docker
log timestamps for the game's actual load span 56.04 seconds. A separate
archive-only load from a copy took 10.08 seconds and peaked at 1.14 GB RSS.
The live save exceeds the synthetic fixture's 50 MB file budget; that budget
was never intended as a limit for all worlds.

The graph reader previously used reflection and a temporary object array for
every tile. It now restores the saved model ID and flags through a direct Tile
constructor, preserving individual tile identity and the existing decoration
reference fixup. On the fixed 1,124,864-tile save-budget fixture, two full
fresh-process loads fell from 7.43–7.61 seconds to 6.68–6.72 seconds. A repeat
run of the old image after the new image took 7.45–7.49 seconds. The file size
was unchanged at 10,054,061 bytes; first-generation GC counts fell from 404
to 376. `storage/compact-save` verifies tile flags, decoration sharing, and
reference identity. The 64 MB live save has not been reloaded with this change,
so its exact post-change load time is not yet measured.

## Copied save and archive keys, October 12, 2026

The current game save at turn 6,903 was 71,053,256 bytes. Its compressed
resident archive occupied 21,784,373 bytes and the world graph 49,268,833
bytes; uncompressed sizes were 142,050,959 and 336,262,334 bytes. The archive
contained 9,522 residents and 749,940 entries. The old archive stored a
separate string deduplication key for every entry, including observed events
whose numeric ID is already part of the entry. New saves reconstruct those
event keys and keep only other string keys. Loading older saves still works.
The graph writer also reads tile fields directly, and the reader stores
deferred values in structs instead of allocating one object per value.

The same copied save was loaded and written before and after the change in
separate Docker/Mono containers, each limited to one CPU and 6 GiB RAM. Cold
load is one sample; complete save and archive-only write are medians of three.
The live game and its save were not modified.

| Operation | Before | After |
| --- | ---: | ---: |
| Cold load | 64.14 s | 60.39 s |
| Managed memory immediately after load, before collection | 3.57 GB | 2.78 GB |
| Complete save | 38.09 s | 36.77 s |
| Archive-only write | 9.22 s | 8.76 s |
| New save size | 71,053,235 bytes | 68,757,329 bytes |
| Archive section size | 21,784,373 bytes | 19,488,467 bytes |

The world section's compressed size did not change; all 2,295,906 saved bytes
came from the archive. The fixed synthetic save-budget fixture still passes:
three saves took 5.31–5.35 seconds, two fresh-process loads 6.46 seconds,
and its file measured 9,755,419 bytes. The copied production save remains
above the fixture's 50 MB file and 10-second operation budgets.

The preceding backup was saved at world turn 6,895 and measured 66,052,054
bytes. Its 25 surface districts lagged behind world time by 30,936 turns in
total. At turn 6,903 the lag was zero: waiting had caught up those districts.
Over the eight player turns, the archive gained 50,721 entries and 659
residents; the largest entry increases were 16,985 chats, 5,862 witnessed or
experienced deaths, 5,321 heard rumors, and 4,996 shared rumors. The 5.0 MB
file jump therefore reflects substantial simulated activity and retained
history. Compact keys reduce its storage cost, but future catch-up simulation
will still generate records.
