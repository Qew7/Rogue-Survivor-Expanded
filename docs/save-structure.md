# Save structure investigation

This investigates a **copy** of the day-12 version-4 save profiled in
[performance.md](performance.md#copied-day-12-world-original-format-4-investigation).
The input is 25,601,550 bytes. The live game was left running; its container
was not restarted. These findings describe this snapshot, not every world.
The audit below describes the original format 4. Its findings led to the
implemented format-5 changes described later in this document.

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

| Repeated representation | Occurrences | Unique entries | Format-4 raw bytes |
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
**115 MB raw** under a simple accounting model. This was a preliminary encoding
estimate, excluding later string pooling and specialized Tile nodes. It predicted neither compressed file size nor
elapsed time. The implemented codec now measures 90,058,454 raw bytes for
this same graph.

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

## Implemented format-5 changes

- Type names and ordered field schemas are written once per operation and then
  referenced by integer IDs. Reflection members and validated types are cached.
- Immutable string values use a shared table. Exact historical text and
  deduplication keys are preserved.
- Tiles keep their object IDs and decoration-list references, while model ID
  and all flag bits use fixed integer payloads. No equal mutable tiles or maps
  are merged; no terrain palette or RLE has been introduced.
- The reader uses cached field schemas and deferred reference fixups, avoiding
  per-tile field dictionaries and field-value objects for undecorated tiles.
- Both reader and writer enforce an 8,000,000-object limit, replacing the old
  reader-only 2,000,000 limit. The audited graph used 90.2% of the old limit.
  Limits for schemas, strings and collections are documented in
  [save-format.md](save-format.md).
- A 64 KiB buffer batches writes before compression. Atomic replacement and
  backup recovery remain in the actual file writer.
- Resident history is compressed in an independent section. The world graph
  stores a null archive field; full loading attaches the separately loaded
  archive. Read Records loads only that section, with no session activation.

The same copied graph measured 7.79 s serialization (three-sample median),
13,510,655 compressed bytes and 10.81 s loading with the compact codec. Reading
its archive alone took 0.46 s. A serialize/load/serialize SHA-256 digest matched,
including the serialized fields and shared-reference IDs. The diagnostic used
an old assembly only to import the old copy; current production rejects old
world saves. Settings files from format 4 remain readable. See
[performance.md](performance.md#implemented-compact-graph-and-independent-records-section)
for the measurements and their scope. Peak RSS has not been measured.

Resident history text, deduplication keys and private relationship memories
serve different purposes. Keep all of them. History compaction should preserve
the exact historical text and event identity; reconstructing text from current
names or content definitions can change old records after a rename or mod change.
Recreating maps from their seeds is also unsuitable for lossless saving unless
every generation dependency and subsequent mutation is recorded.

Verification covers shared references/cycles, independent equal tiles,
decoration null/empty/order boundaries and flags, exits, actor/item locations,
RNG and clocks, pending/resolved memories, relationship histories, resident
entries and deduplication, and atomic backup recovery. New scenarios are
`storage/compact-save`, `npc/records-lifetime-items`, `npc/records-query` and
`world/records-browser`. Old world compatibility is intentionally not provided.

## Reproduce the audit

```sh
docker build --target scenarios -t rogue-survivor-save-structure .
docker run --rm --cpus=1 --memory=3g \
  --mount type=bind,source=/private/tmp/rogue-save-profile,target=/profile,readonly \
  rogue-survivor-save-structure --audit-save /profile/current-save.dat
```

Use a copied **format-5** save named `current-save.dat`; the historical
format-4 input requires the original diagnostic image.

`--audit-save` reads serialized fields and collections without activating the
session, loading mods, reconstructing map indexes or running presave cleanup.
It does not write any saves. Byte accounting reports a **legacy-format estimate**, followed by the actual
compact raw size; the per-type estimates are not format-5 byte totals. The unit
test checks that compact output is smaller, a cycle/shared-map/shared-tile
roundtrip, distinct equal tiles, and null/empty/ordered decoration boundaries. Full test and portable
build validation: `docker build --target test .`.
