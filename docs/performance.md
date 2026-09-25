# Repeatable gameplay microbenchmarks

Run the headless benchmarks with:

```sh
sh tests/scenario.sh --bench
```

The runner warms each case, runs five timed samples, and prints the median.
It tests a 40×40 minimap with visited tiles and player tags enabled, a 25×25
mouse route, a ten-candidate AI choice, zone lookup, scent insertion, NPC
reachability, one blast among 800 ground stacks, and presave traversal.
The test UI omits actual graphics
driver work, so the minimap case measures game-side traversal and dispatch.
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
variation between runs. The named scenarios `world/minimap-rendering`,
`movement/mouse-route`, and `npc/choice-selection` check behavior separately
from these timing measurements.

Additional local Docker/Mono measurements (median of five, same benchmark
setup before and after each change):

| Case | Calls | Before | After |
| --- | ---: | ---: | ---: |
| NPC route on 40×40 map | 1,000 | 19.43 ms | 12.71 ms |
| Insert scents on 1,600 tiles | 1 | 3.33 ms | 1.12 ms |
| One blast with 800 ground stacks | 20 | 169.26 ms | 4.23 ms |

The blast improvement comes from indexing each ground inventory's position,
while the existing explosion order remains unchanged. An idle district worker
made 10 callbacks in 100 ms before the change and one afterward; notification
now wakes it when the player district advances. A 40×40 map's presave traversal
took 4.39 ms for 100 calls. Zone lookup took roughly 165 ms for 100,000 calls;
an attempted change did not improve that measurement and was reverted.
Graphics resources are now released at frame clear and canvas shutdown; this
is a lifetime fix, with no claimed frame-rate improvement.

Second optimization pass, using the same five-sample median method:

| Case | Calls | Before | After |
| --- | ---: | ---: | ---: |
| Grayscale image 128×128 | 50 | 127.71 ms | 57.56 ms |
| Draw 20 overlays | 100,000 | 26.30 ms | 18.07 ms |
| Search 40×40 region for exits | 10,000 | 210.14 ms | 0.27 ms |
| Field of view on 40×40 map | 1,000 | 17.10 ms | 15.02 ms |
| Corpse membership among 300 corpses | 100,000 | 53.75 ms | 6.56 ms |

The grayscale pixel tests compare the optimized result with the original
formula, including transparent pixels. Overlay snapshots are rebuilt when the
collection changes. The exit check chooses between stored exits and region tiles
based on which side is smaller; a 1×1 query with 100 exits improved from
235.32 ms to 3.24 ms for 100,000 calls after this adjustment.
Field-of-view rays share one callback per computation. Corpse membership uses
an auxiliary index rebuilt after loading a map.

Measured candidates left unchanged: resolving a missing asset through five mods
cost 135.50 ms for 10,000 calls, parsing the equivalent of all 131 bundled CSV
rows cost 11.75 ms for 100 passes, and matching 100 mod stamps to 100 available
mods cost 119.52 ms for 1,000 calls. These operations run mainly during startup
or save loading. Inventory stacks have small capacity; an index there would add
maintenance cost. Grayscale variants are still generated during startup to
avoid introducing first-use pauses during play.
