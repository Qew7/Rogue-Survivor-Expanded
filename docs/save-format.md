# Save format

`BinarySaveStore` writes every binary save atomically and keeps the previous
file as `<name>.bak`. It recognizes five payloads:

| Header | Payload | Reader |
| --- | --- | --- |
| None | Original BinaryFormatter stream | Legacy migration only |
| `RSE1` + byte `1` | BinaryFormatter stream | Legacy migration only |
| `RSE1` + byte `2` | GZip compressed object graph | Previous writer and current reader |
| `RSE1` + byte `3` | Ordered mod names and versions, then GZip graph | Previous writer and current reader |
| `RSE1` + byte `4` | Expanded game version, ordered mod names and versions, then GZip graph | Current writer and reader |

Version 4 saves record the Rogue Survivor Expanded version (`0.2.0` at
this release). Saves from `0.1.0`, `0.1.1`, and the current release series are accepted;
other versions are rejected before loading their mod list or object graph. The
error shows the saved and running versions. Older
formats have no game version and remain readable for migration.

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

Armed traps placed by base owners can retain a reference to their `XpdBase`.
This lets the base leader and their followers cross those traps safely after a save and load.
Older saves have no trap base reference and continue using the trap owner's
existing group safety rule.

Older saves may contain bases assigned to an entire faction without a group
leader. Such claims are released when the map is loaded; group-owned claims
remain intact.

Ground items may store the actor who last dropped them. This keeps an actor's
own item from being treated as stolen when picked up on a foreign base, even
after loading. Older saves have no dropper reference and retain the previous
ground-item behavior.

NPC supply orders store whether a leader sent the follower specifically for
food. This keeps an active food trip focused on food after loading. Orders
from older saves retain their previous food-and-weapon behavior.

When changing a saved class, add a migration test to `tests/unit/` and run both
`docker build --target test .` and `bash tests/e2e.sh`. The end-to-end test
creates a real world, saves it, loads it, and reaches the game screen again.
Renaming a private field loses its old value unless the reader is taught to
map the old name. Renaming or removing a type needs an explicit type alias in
`ObjectGraphStore`.

The legacy `BinaryFormatter` reader remains solely for existing local saves.
After loading one, saving again writes version 4. Do not load legacy files
obtained from untrusted sources.

Game sessions now contain an optional `GamePreset` field. Saves written before
presets were introduced reconstruct the matching Standard, Corpses & Infection,
Vintage, or Expanded rules from the old mode ID. The session also stores the
selected gameplay options, so loading a game restores its rules. User-defined
presets are kept separately in the user config directory as `game-presets.dat`; deleting that file
does not change existing game saves.

New games with NPC personalities enabled save each intelligent living actor's
trait instances, unresolved memories and a bounded journal of witnessed
significant events in the actor graph. The definitions and their callbacks are
registered from game code and are not serialized. The selected preset stores
whether this system is enabled. Compatibility with saves from before NPC
personalities was introduced is outside the current feature scope.
Journal entries also retain whether the observer was related to the event's
subject at the time, so a later zombification can affect former companions.
Pending memories retain the same relationship flag, allowing their outcomes
to use it even when the bounded event journal evicts the original event.
They also retain the latest turn for each evidence kind declared by the memory
definition, so later events can still affect resolution after journal eviction.
Actors lazily receive a persistent personality identity when involved in an
event. Memories and journal entries keep those identities alongside names so
different actors with the same name remain distinct across saves.
The personality state also stores private person, leader-group, and faction
relationship records. A record keeps a feeling score and references to its
attributed memory instances, including resolved instances with their resolution
turn and outcome. Person and group records use persistent actor identities;
faction records use existing numeric faction IDs. Names are display snapshots,
not lookup keys. These fields are optional when reading older personality saves;
an absent relationship tree starts empty.
The player's optional personality state stores only relationships formed from
events they directly experience or witness. It has no generated starting traits
or memories. Player memories resolve without NPC trait or skill rewards, while
their attributed relationship history remains in the saved actor graph.

Each claimed base section may reference the original section through its
optional `m_Root` field. Sections on connected maps then remain one base after
loading. Older saves have no such field; each existing claim remains its own
section until the player claims a connected level.
