# Save format

`BinarySaveStore` writes every binary save atomically and keeps the previous
file as `<name>.bak`. It recognizes four payloads:

| Header | Payload | Reader |
| --- | --- | --- |
| None | Original BinaryFormatter stream | Legacy migration only |
| `RSE1` + byte `1` | BinaryFormatter stream | Legacy migration only |
| `RSE1` + byte `2` | GZip compressed object graph | Previous writer and current reader |
| `RSE1` + byte `3` | Ordered mod names and versions, then GZip graph | Current writer and reader |

The mod list is outside the graph so a failed load can still report which mod
and version the save needs. Older saves have no recorded mod list. Loading a
save applies its available mods in saved priority order; unavailable mods fall
back to original resource files and are omitted from the next save. The menu's
selected mods are stored separately in `mod-profile.json` under the user data
directory and restored when gameplay ends. If an absent mod supplied a model ID
that has no definition in the active game data, loading stops and reports the
missing mod and required version.

The version 2 graph assigns IDs to reference objects. Nodes contain their
type, instance fields, arrays or collection elements. Field names are read by
name, so missing fields keep defaults and unknown fields are ignored. Shared
references and cycles retain their identity. The reader accepts serializable
game types and a restricted set of framework values and collections.

When changing a saved class, add a migration test to `tests/unit/` and run both
`docker build --target test .` and `bash tests/e2e.sh`. The end-to-end test
creates a real world, saves it, loads it, and reaches the game screen again.
Renaming a private field loses its old value unless the reader is taught to
map the old name. Renaming or removing a type needs an explicit type alias in
`ObjectGraphStore`.

The legacy `BinaryFormatter` reader remains solely for existing local saves.
After loading one, saving again writes version 2. Do not load legacy files
obtained from untrusted sources.
