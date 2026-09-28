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
Timings are diagnostic, not CI pass/fail thresholds. Compare runs on the same
machine, Docker configuration, and runtime.

## Save diagnostics

`--bench-save` profiles an existing **copied** save with three samples per case.
It reports world/chronicle counts, presave traversal, the current atomic file
writer, serialization without compression, direct GZip, buffered GZip at
Optimal/Fastest levels, the archive alone, and the graph without its archive.
The last case isolates cost; it is not a proposal to discard history.
The input file is never overwritten. Generated saves are written to and removed
from an independent temporary directory inside the diagnostic container.

For a copied save at `/private/tmp/rogue-save-profile/input.dat`:

```sh
docker build --target scenarios -t rogue-survivor-save-profile .
docker run --rm --cpus=1 --memory=3g \
  --mount type=bind,source=/private/tmp/rogue-save-profile,target=/profile,readonly \
  rogue-survivor-save-profile --bench-save /profile/input.dat
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

### Copied day-12 world, save investigation

Measured on a copy of a running game's version-4 save, with Docker/Mono limited
to one CPU and 3 GiB RAM. The live game was left running. Medians of three
samples, fixed case order:

| Case | Median | Output size |
| --- | ---: | ---: |
| Presave world traversal | 6.29 ms | — |
| Current atomic file save | 33.17 s | 25,601,529 bytes |
| Graph serialization to a counting sink | 17.39 s | 1,104,833,938 bytes |
| Graph + direct default/Optimal GZip | 32.56 s | 25,601,514 bytes |
| Graph + 64 KiB buffer before Optimal GZip | 23.75 s | 25,601,514 bytes |
| Graph + 64 KiB buffer before Fastest GZip | 23.67 s | 25,601,514 bytes |
| Resident archive alone, buffered Optimal GZip | 1.06 s | 2,650,017 bytes |
| Graph without archive, direct Optimal GZip | 31.23 s | 22,945,915 bytes |

This world contains 418 maps and 1,107,446 tiles. Maps retain 6,071 actors;
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

The strongest measured compatible candidate is a buffer before GZip: about
27% less time in the direct-GZip comparison, with the same output size.
Fastest gave no meaningful further benefit or size change on this runtime.
Follow-up work should verify buffered saves through the actual file writer and
reader, then investigate cached serialization metadata or a compact type/field
table and tile encoding. The latter changes require format compatibility work.
No production save code was changed by this investigation.

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
