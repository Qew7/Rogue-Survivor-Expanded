# Completed optimizations

These changes were measured with Docker/Mono microbenchmarks. See
[docs/performance.md](docs/performance.md) for the workloads, timings, and
measurement limits. Named scenarios and unit tests check behavior separately.

| Area | Change |
| --- | --- |
| Input and turns | Split turn phases and simplified input handling so each phase is easier to change and test. |
| Minimap | Reduced repeated game-side work while drawing visited tiles and tags. |
| Mouse navigation | Reduced allocations and repeated work during route search. |
| NPC choices and routes | Reduced repeated candidate processing and route search overhead. |
| Scents | Indexed scent lookup and updates by position. |
| Ground inventories | Indexed stack positions, avoiding a map-wide search for each explosion hit. |
| Grayscale images | Processed opaque pixels in a bitmap buffer while preserving the original alpha behavior. |
| Overlays | Rebuilt drawing snapshots only when overlays change. |
| Exits and zones | Used stored exits for small regions; retained the existing zone lookup after a trial showed no gain. |
| Field of view | Shared a ray callback across one computation. |
| Corpses | Added an auxiliary membership index rebuilt after loading. |
| Canvas resources | Released temporary graphics resources at frame clear and canvas shutdown. |
| District worker | Waited for turn notifications instead of repeatedly polling while idle. |

No performance gain is claimed for the canvas lifetime fix. Asset resolution,
CSV parsing, mod matching, and inventory indexing were measured but left
unchanged because their costs or tradeoffs did not justify the change.
