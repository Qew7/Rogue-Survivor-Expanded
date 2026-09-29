# Save format

Game worlds use **format 5**. Old world saves, including format 4 and unmarked
BinaryFormatter files, are intentionally unsupported; start a new game.
No running container or existing world save is deleted by the upgrade.
Version-4 binary settings (options, keybindings, hints, scores and preset
collections) remain readable, so changing the world format does not reset them.
Unmarked BinaryFormatter settings are no longer accepted.

## Envelope and archive

Saves record the Rogue Survivor Expanded version (`0.2.0` at
this release). Saves from `0.1.0`, `0.1.1`, and the current release series are accepted;
other versions are rejected before loading their mod list or object graph. The
error shows the saved and running versions. Older
formats have no game version and remain readable for migration.

Every write uses a temporary file, flushes it, and atomically replaces the
destination, retaining the previous file as `<name>.bak`. Failed writes remove
the temporary file. Normal game loading can recover from the backup; Read
Records reads exactly the selected file and never substitutes its backup.

The envelope contains, in order:

1. `RSE1` magic and one byte `5`.
2. The game version string and ordered mod name/version manifest.
3. A Boolean indicating whether the root is a Session.
4. For a Session: the personality-enabled Boolean, save turn (Int32),
   compressed archive length (Int64), and the GZip archive graph.
5. The GZip world/root graph.

Non-session settings omit step 4. The game-version compatibility policy remains
independent of the envelope version; an accepted game-version string does not
make an old world format readable. Incompatible game versions are rejected
before applying mods, and a backup does not hide that error.

The resident archive is stored **once**, in its own length-bounded section.
The world graph writes null for the Session's `m_ResidentRecords` field without
mutating the live Session. Full loading restores that field from the archive.
Existing archive data is preserved even if personalities are disabled.
Normal saves refresh living NPC snapshots before serialization; the diagnostic
`SaveSnapshot` entry point preserves existing snapshots without accessing
model catalogs.

Read Records reads only the archive section and checks its own GZip integrity.
It requires personalities enabled and does not load maps, activate a Session,
apply mods, change options, or start simulation. It can therefore read records
even when mod assets are unavailable. It does not validate the unused world
section; full game loading validates both sections and checks that the turn
and personality setting match their envelope.

The mod manifest remains outside both graphs. Full loading restores available
mods in saved priority order, with existing fallback/error behavior for missing
resources or model definitions. The menu mod profile remains separate.

## Compact graph

Each section has its own tables and reference graph:

- Object IDs are contiguous, starting at 1; node ID 0 ends the graph. Shared
  mutable objects and cycles retain identity.
- Type/schema IDs and string IDs use a negative ID followed by their definition
  on first use, and a positive ID on subsequent uses.
- Each type name and its ordered field names is written once. Field names still
  support optional fields: absent fields retain defaults and unknown fields are
  ignored. Type resolution and member metadata are cached per operation.
- Values have null, reference, literal or value-structure tags. Primitive and
  enum encodings preserve their previous values; strings share immutable text.
- Collections serialize their elements, not internal capacity or indexes.
- Tile nodes retain their object ID and write model ID, **all** flags and the
  decoration-list reference as three Int32 values. Decoration reference 0 means
  null. Empty lists, ordered contents and shared lists remain distinct as needed.
  Equal tile values do not merge mutable Tile instances.

A 64 KiB buffer sits before GZip. The writer and reader both cap graphs at
8,000,000 objects and string values, type tables at 4,096 types, and collection sizes at
100,000,000 elements. Invalid IDs, counts, type/value kinds, duplicate schema
field names and trailing graph data are rejected. The type allowlist still
permits serializable game types and a small set of framework values/collections.
The settings-only version-4 reader caps its graph at 20,000 objects and rejects
a Session root immediately.

## Persistent gameplay state

Numeric model/content IDs and string personality IDs remain stable. Tile,
actor and item model definitions and personality callbacks are rebuilt from
content; the save retains their IDs and instance state.

The graph retains map ownership and exits, local/world clocks and RNG state,
actor/item positions, corpse references, trap and base ownership, item dropper
attribution, NPC orders, selected presets/options, and connected base sections.
Auxiliary map indexes are not serialized and are rebuilt for gameplay.

Personality state retains traits, pending memories, bounded observations,
evidence turns, persistent actor identities, and person/group/faction
relationships. The same memory can be referenced from a pending queue and
several relationships; it is stored once and remains in those histories after
resolution, with its outcome and turn. Player relationships retain their own
experienced/witnessed events and do not expose another NPC's private memory
during gameplay.

Personality state also retains bounded NPC intentions and pending spoken
reactions, per-template cooldowns and the intention sequence. An intention stores
its stable definition ID, target identity/name snapshot, last known map/position
and attitude, cause/story IDs, initial priority, start/deadline/finish turns,
status, retry delay/count, announcement state and outcome. Reactions store target
IDs, text, kind, cause/story IDs and expiry. These fields contain no Actor
references. Last known positions share graph Map objects; terminal intentions
clear that reference. See [npc-intentions.md](npc-intentions.md) for limits.

Each intention can retain a generated `NpcPlan`: desired-state flags, bound
action IDs, participant IDs and shared Map locations, condition/effect flags,
costs, step cursor, knowledge revision, trait fingerprint, retry turn and the
last actual causal event ID. Failed bindings retain their expiry and location.
Terminal intentions release steps and failures to clear Map references. A
loaded plan is revalidated against current observations and rebuilt when needed;
predicted effects are never restored as actor/world facts. Search nodes and
execution callbacks are transient. Existing intention enum values remain
unchanged; `ObtainFood` is appended. These optional fields use format 5 without
a new world envelope.

Generated intentions also store `NpcGeneratedGoal`: value kind, permanent
subject ID, current/desired values, normalized deficit, importance, confidence,
utility, evaluation turn and desired result flags. Their cooldown keys combine
value and subject, bounded to 64 entries per personality; separate subjects can
retain separate goals under the same execution ID. These keys and remaining
cooldowns survive loading. Earlier intentions without this optional payload
retain their original execution/acceptance rules. `RestoreHealth` and medicine
plan actions are appended without changing existing enum values.

Knowledge retains bounded facts with original event IDs/time, source IDs,
confidence, retelling hops, named participant IDs and remembered positions;
known people, supplies, shelter and exits; remembered ownership risk, a change
revision; and conversation/query deduplication
and next planning/speaking turns. Pending location replies contain copied
knowledge snapshots, so a reply after loading reports the same observation.
The original event is not recreated as a witnessed event for a listener.
Known people retain perceived hostility, food-need magnitude/time/confidence,
its cause/story, danger and unaddressed-wrongdoing evidence, and social cause and
reciprocity eligibility turn. Danger and wrongdoing have independent evidence
time/confidence, so seeing a person does not strengthen an uncertain accusation.
The cause of an addressed incident is retained to prevent a retelling from
reopening the same desired response.
New location reports preserve existing conditions; weaker contemporaneous
reports cannot overwrite stronger evidence. Medicine places retain the same
shared Map references, units, ownership risk and original observation age as
food places. These are remembered snapshots, not references to unseen actors.

Person relationships retain trust, fear, attachment, grievance and debt.
Actors in a group share one serialized `SocialGroup`: permanent identity,
leader/member IDs, founder faction, plan sequence/cooldown and current plan.
Nested followers and a successor retain that shared instance. Actor group
sequences distinguish a newly founded group after a split. Assigned goals save
group identity, destination, collector progress, coordinator identity/last known
place and original observation turn, allowing continuation between pickup,
gift and return report.

Session retains the story director's bounded story/role history, source maps,
cause IDs, stages, deadlines, role target IDs, actor/resource reservations,
cooldowns and per-map proposal schedule. Maps in knowledge, exits, goals, plans and reservations are
references to the same loaded graph objects. The per-map schedule is a list of
saved rows with a nonserialized reference-keyed lookup rebuilt on demand; it
does not depend on Map's mutable hash code or a serialized dictionary comparer.
Terminal goals, plans and stories release location references. Starting a new
Session clears its director and event sequence. No knowledge or director entry
retains Actor references.

Session retains the significant-event ID sequence. Observations retain event,
cause and story IDs; a personality's processed-event cutoff survives journal
eviction and memory resolution. Intent delivery has its own cutoff. Replaying
the same identified event after loading does not create another memory, reward
or intention. The new fields use format 5's existing graph field encoding;
absent optional fields initialize to their defaults.

Resident records retain NPC identity/name, spawn/death turns, faction and
leader-group snapshots, last inventory and traits, cumulative item acquisitions,
and the snapshot turn. Entries retain ordered text, event kind, direct/witnessed
status, participant IDs and whether resolution actually granted a trait.
Deduplication keys are preserved. Histories contain no Actor references and
survive actor/corpse removal; entries are not evicted.
Entries also retain event/cause/story IDs. Private intention starts and terminal
outcomes are typed chronicle entries with deduplication by intention sequence
and outcome. They are available to the archive-only reader and excluded from
physical-event counts and the interesting-life score. Private missing-contact
inferences, generated `goal_plan` action lists and story-stage entries use the
same typed archive path and **Intentions and outcomes** filter, and also do not
inflate those counts.
Resident snapshots retain stable group identity alongside the current leader
label. Searching a story tag links its independent participants without loading
the world.

Inventory's `TotalReceived` counts item **units** successfully added by AddAll
and AddAsMuchAsPossible, including generation, pickups, gifts and trades.
Partial additions count only the transferred quantity; failed additions add
nothing. Consumption/removal does not subtract. Picking up the same item again
counts another acquisition. This is not a count of distinct physical items.

An explicitly missing archive can still be rebuilt from available actors,
corpses and personality records and marked partial. This does not add support
for old world envelopes or invent discarded events.

## Verification

When changing saved fields, test save/load state, alias identity and boundaries.
Renaming fields/types needs an explicit mapping or a new format policy; preserve
numeric and string content IDs. Run the named scenarios, then
`docker build --target test .` and `bash tests/e2e.sh`.

Relevant scenarios: `storage/compact-save`, `npc/records-reader-save`,
`npc/records-lifetime-items`, `npc/records-query`, `world/records-browser`,
`npc/intent-persistence`, `npc/intent-boundaries`, `npc/story-persistence`,
`npc/story-director`, `factions/social-group-succession`, and existing
personality/relationship/base persistence cases. The E2E test
generates a world, writes/loads format 5, then uses search, filters, sorting and
the interesting-NPC selector through the real VNC UI.

See [performance.md](performance.md) for measurements and
[save-structure.md](save-structure.md) for the original structural audit.
