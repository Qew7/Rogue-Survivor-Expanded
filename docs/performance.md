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
