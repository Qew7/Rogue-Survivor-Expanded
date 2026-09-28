# Save structure investigation

This investigates a **copy** of the day-12 version-4 save profiled in
[performance.md](performance.md#copied-day-12-world-save-investigation).
The input is 25,601,550 bytes. The live game was left running; its container
was not restarted. These findings describe this snapshot, not every world.
Production serialization and the save version remain unchanged.

## Maps and shared references

The city is 7×7 districts. Auditing both the district lists and every object
reachable through serialized fields gives:

| Map role | Unique objects |
| --- | ---: |
| Surface | 49 |
| Sewers | 49 |
| Subway | 7 |
| Residential and shop basements | 305 |
| Special levels | 8 |
| **Total** | **418** |

The eight special levels come from the hospital's five floors, the police
station's offices and jails, and the CHAR underground facility. Basements
are separate maps generated for individual buildings, so the city overview
shows substantially fewer locations than the save contains.

There are 418 district-list entries and 418 unique map references: **zero
duplicate entries and zero maps outside those lists**. Each map's district
back-reference and every non-null Entry/Sewers/Subway reference agree with
the lists. Equal shop-basement names occur in different buildings; names
are labels rather than map identity.

`ObjectGraphStore` already assigns IDs with reference equality. A map reached
through `District.Maps`, `District.EntryMap`, an exit, an actor's location, or
the session's unique-map fields is written once. Actors, inventories and
memory instances also share IDs wherever their references are shared. Cycles
survive loading. Auxiliary map indexes are `[NonSerialized]` and are rebuilt.
Tiles, actors and items keep model IDs rather than complete model definitions.

Eight groups containing 18 maps have equal tile layouts, including dimensions,
all tile flags and ordered decorations (SHA-256 diagnostic fingerprints).
This comparison excludes actors, objects, items, exits, seeds, time and other
state. It therefore does **not** establish duplicate whole maps. Equal tile
data could be encoded once with references to a disk template, while loading
independent mutable maps and tiles.

## Where the bytes go

The audit matches the previous writer measurement exactly: **1,104,833,938
uncompressed bytes**. The graph has 1,804,161 distinct reference objects and
2,192,769 reference occurrences. References already cost a tag and a 32-bit ID.

| Repeated representation | Occurrences | Unique entries | Current raw bytes |
| --- | ---: | ---: | ---: |
| Assembly-qualified type names | 9,263,724 | 155 types | 917,219,523 |
| Field counts and field names | 2,569,954 groups | Schemas total 14,858 bytes | 110,053,233 |
| String value payloads | 676,835 | 80,108 values | 18,071,693 |

Type names and field schemas occupy **93.0% of the raw stream**. Unlike
reference objects, primitive values repeat a complete type name each time.
Every tile repeats its field names as well. GZip makes these names small on
disk, but the writer still constructs and compresses the entire raw stream.
The 25.6 MB file size consequently hides much of the save work.

The 155 unique type-name strings total 21,504 bytes. Storing these and the
field schemas once, replacing type names with 32-bit IDs, would give roughly
**115 MB raw** under a simple accounting model. This is an encoding estimate,
not a measured new format: framing, validation and migration details remain
to be designed. It predicts neither compressed file size nor elapsed time.

Unique string payloads total 4,869,644 bytes. A string table with 32-bit IDs
would save roughly 10.5 MB more raw, after accounting for IDs and the table.
Names, content IDs, decoration IDs and repeated history text can share string
values without discarding any entries or changing their contents.

The largest node bodies are tiles (389,934,344 raw bytes), map objects
(173,151,434), exploration `HashSet<Point>` collections (70,366,803), actors
(53,003,074), and AI percepts (41,195,153). These totals include each node's
inline fields and values, excluding separately referenced nodes; they must
not be added to the metadata totals above, which classify the same bytes.
Exploration and percepts belong to individual NPCs and cannot be discarded
without investigating their gameplay effects.

## Tile encoding opportunities

All 1,107,446 cells contain distinct mutable Tile objects. Their serialized
state is just a model ID, flags and a decoration-list reference. The snapshot
has **334 distinct tile value patterns**, 109 non-null decoration-list value
patterns, and 28,338 cells with non-null decoration lists.

A tile palette and 32-bit cell indexes would require about 4,439,512 bytes
for tile values; adding run-length encoding gives about 2,183,136 bytes for
271,676 runs. These are **payload-only estimates**, excluding graph IDs,
reference aliasing, map framing and compression. They are not complete save
sizes or a drop-in replacement for the existing graph.

A safer initial specialization is a compact Tile node that keeps its existing
object ID and decoration-list reference, writing model and flags as integers.
It preserves external references to a tile and shared decoration-list identity.
A subsequent palette/block encoding must still restore each distinct tile as
a distinct object, including references from outside the tile array. Otherwise
changing or exploring one cell could change another equal cell.

Keep all flag bits, including current visibility and visited state. Preserve
decoration order, contents, null versus empty lists, coordinate order, map
dimensions and shared references. Equal layouts alone do not justify removing
a map's actors, objects, exits, local time or seed.

## Recommended sequence

1. **Compatible writer/reader improvements:** buffer before GZip (already
   measured at about 27% less compression-path time), cache serializable
   member metadata during each operation, and cache resolved/validated types
   during each read. These retain the existing wire format and allowlist.
   Verify through the actual atomic file writer and reader.
2. **New version with type and field tables:** retain graph object IDs and
   schema field names for migration, but write each name once. Preserve the
   version-4 reader and all existing legacy readers. This targets the largest
   measured structural overhead before introducing domain-specific codecs.
3. **String table and compact tile nodes:** deduplicate immutable values and
   specialize common tile fields while keeping graph identity. Measure actual
   compressed size, write/load time and peak memory before adopting blocks/RLE.
4. **Reader allocation work:** avoid a separate field-name dictionary and
   StoredValue object for every tile field. Cached schemas and deferred
   reference fixups can reduce intermediate allocations while preserving
   forward references and cycles. A full collection after this diagnostic load
   retained about 172 MB; the earlier uncollected reading was 2.21 GB. Neither
   reading measures peak RSS, so peak memory needs separate instrumentation.

The current reader caps the graph at 2,000,000 nodes. This snapshot already
uses 90.2% of that limit, leaving 195,839 nodes. The writer does not enforce the
same limit. Growing histories and other world objects therefore warrant a
separate save-readability regression check. A type table alone does not reduce
node count; a tile-block design or a reviewed limit change must address this.

Resident history text, deduplication keys and private relationship memories
serve different purposes. Keep all of them. History compaction should preserve
the exact historical text and event identity; reconstructing text from current
names or content definitions can change old records after a rename or mod change.
Recreating maps from their seeds is also unsuitable for lossless saving unless
every generation dependency and subsequent mutation is recorded.

Before a format migration, test old-version loading, shared references/cycles,
tile mutation independence, decorations and flags, exits, actor/item locations,
RNG and clocks, pending and resolved memories, relationship histories, resident
entries and deduplication, and atomic backup recovery. A repeated save/load
should preserve a normalized state digest and deterministic next-turn results.

## Reproduce the audit

```sh
docker build --target scenarios -t rogue-survivor-save-structure .
docker run --rm --cpus=1 --memory=3g \
  --mount type=bind,source=/private/tmp/rogue-save-profile,target=/profile,readonly \
  rogue-survivor-save-structure --audit-save /profile/input.dat
```

`--audit-save` reads serialized fields and collections without activating the
session, loading mods, reconstructing map indexes or running presave cleanup.
It does not write any saves. The unit test checks byte accounting against the
actual graph writer, a cycle/shared-map/shared-tile roundtrip, distinct equal
tiles, and null/empty/ordered decoration boundaries. Full test and portable
build validation: `docker build --target test .`.
