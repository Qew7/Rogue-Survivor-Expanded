# Completed optimizations

This is a quick inventory of changes to runtime work and code structure.
For benchmark commands, before/after timings, and measurement limits, see
[docs/performance.md](docs/performance.md).

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
| Exits | Used stored exits for small regions. |
| Field of view | Shared a ray callback across one computation and compared squared distances when checking its radius. |
| Corpses | Added an auxiliary membership index rebuilt after loading. |
| Canvas resources | Released temporary graphics resources at frame clear and canvas shutdown. |
| District worker | Waited for turn notifications instead of repeatedly polling while idle. |
| Save graph | Added shared type/schema/string tables, compact Tile nodes, cached reflection metadata and buffered compression in format 5. |
| Read Records | Stored the archive once in an independent section and cached resident metrics and timeline lines for browsing. |

The canvas change fixes resource lifetime; it has no claimed frame-rate gain.
Behavior is covered by named scenarios and unit tests.
