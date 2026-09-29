# Performance measurements

Run the headless benchmarks with:

```sh
sh tests/scenario.sh --bench
sh tests/scenario.sh --bench-ai
```

The runner warms each case, runs five timed samples, and prints the median.
`--bench-ai` runs just the AI and generation cases for quicker iteration.
The test UI omits actual graphics driver work, so the minimap case measures
game-side traversal and dispatch.
These general benchmarks are diagnostic. The save/load budget below is an
automated pass/fail gate. Compare measurements on the same
machine, Docker configuration, and runtime.

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
