# Save format

Game worlds use **format 5**. Old world saves, including format 4 and unmarked
BinaryFormatter files, are intentionally unsupported; start a new game.
No running container or existing world save is deleted by the upgrade.
Version-4 binary settings (options, keybindings, hints, scores and preset
collections) remain readable, so changing the world format does not reset them.
Unmarked BinaryFormatter settings are no longer accepted.

## Envelope and archive

Saves record the Rogue Survivor Expanded version (`0.4.1` at the current release)
separately from the envelope format number. Saves from `0.1.0`, `0.1.1`, and
the current release series are accepted;
other versions are rejected before loading their mod list or object graph. The
error shows the saved and running versions. Older settings formats have no game
version and remain readable for migration.

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
The optional preset field `DisableDistantSimulationDuringRest` defaults to false
for existing presets and saves, so distant districts advance during sleep and
waiting unless the player disables it before a new game.
Recent hourly radio programs are retained in the session, so a district catching
up after loading hears the same broadcast already heard elsewhere. Older saves
without this optional history rebuild a program when first requested.

The graph retains map ownership and exits, local/world clocks and RNG state,
actor/item positions, corpse references, trap and base ownership, item dropper
attribution, NPC orders, selected presets/options, and connected base sections.
Auxiliary map indexes are not serialized and are rebuilt for gameplay.
`world/rest-simulates-distant-districts` saves after a distant rest turn and
reloads the district clock, NPC, and rumor learned from another NPC.
An XPD base optionally keeps up to 32 unseen theft losses or fatal raids with
their position, source event and story IDs. Theft losses retain resource and
quantity but no thief identity; a raid retains the killed member's actor link.
An owner or group member discovers one only after returning to the base and
seeing the affected cell or the member's corpse.
Stolen item instances retain the original victim group's stable identity, the
leader's identity and name snapshot, and the theft event and story IDs. Inventory
stacking keeps stolen goods from different thefts and clean goods separate.
The marker survives dropping, corpse loot, gifts, trades and save/load; a later
find or transfer can add a fact to the original theft story without storing a
long-lived actor reference on the item. Stolen-goods facts retain the claimant
group and item IDs as well, so members can recognize a retold loss after the
original leader is out of sight and a visible item does not create the same
discovery every turn. `items/stolen-goods` checks this state.

Radio receivers retain their tuning state, and portable receivers retain their
station, power state and batteries. The Session retains the survivor station
host's actor identity and the district and due turn of one scheduled military
drop. Its four current hourly programs retain their text, source and facts so
receivers remain synchronized after loading. Each listener retains the last
forecast and the last hourly slot per station they heard, preventing repeated
knowledge and sanity effects. The player's journal retains the broadcast text.
Radio noise targets are transient and are restored by the next
broadcast after loading.

Personality state retains traits, pending memories, bounded observations,
evidence turns, persistent actor identities, and person/group/faction
relationships. The same memory can be referenced from a pending queue and
several relationships; it is stored once and remains in those histories after
resolution, with its outcome and turn. Player relationships retain their own
experienced/witnessed events and do not expose another NPC's private memory
during gameplay.

NPC courage is recalculated from current conditions and has no new saved stat.
The `frightened_escape` memory, witnessed `fled_in_fear` fact and any reported
threat cause use the existing personality, knowledge and archive fields.
`npc/courage-rumor-save` checks all three across save and load.

Each intelligent living non-player resident can optionally retain one snapshot
from their first serious visible threat (assessed threat at least 20 or an
immediately mortal attack) and one from death. Both are in the resident archive,
not in the actor or corpse. They retain local and world turns; current and
maximum health, stamina, food, sleep and sanity; effective movement speed;
hunger, starvation, tiredness, sleepiness, exhaustion, disturbed and insane
flags; sleeping, running and activity; plus the visible enemy and its distance,
threat and resolve at first danger, or the actual death reason and killer at
death. Enemy and killer are copied as ID, name and model, without actor links.
Older archives load with both optional fields absent. A death snapshot reflects
state when `KillActor` runs, after any lethal damage was applied. The first
threat snapshot is retained even if later encounters are worse. The
`npc/resident-survival-snapshots` scenario checks capture, bounds, archive-only
reading and full save/load.

Social state keeps separate promise snapshots for each participant, including
resource, remaining units, deadline, outcome and the promisor's original group
and faction. Resource disputes and personal attachments also persist. Each of
these lists is allocated only when needed and capped at 16; active promises are
never evicted to accept a new obligation. Specific nonstacking possessions use
an item's lazily assigned persistent GUID, while model preferences retain a
model ID. Generated goal keys distinguish resources, obligations and specific
items; their supporting event IDs are retained in `Causes`.

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

Plans now optionally retain up to 32 named facts for effects that already
happened during this goal (for example, delivering an item before returning to
report). Inventory, health and location facts are always reconstructed from
the loaded world. A catalog change invalidates the binding; no predicted
effect becomes a world fact. New goals and operators use stable string IDs.

Modular content adds optional stable value/operator IDs, cached custom value
descriptions/identity suffixes and a deterministic catalog fingerprint. Existing
goal cooldown keys retain their spelling. The legacy enum values
and low-word condition/effect masks keep their encodings. Extended symbolic
planning masks use optional `NpcPlanningResult`/`NpcPlanningExtension` objects;
ordinary plans allocate neither. The 256-bit runtime state reserves separate
ranges for existing facts, local places/questions and named catalog facts.
Loading a plan against a changed catalog revision/layout invalidates its steps
and recomputes the desired state from registered content and current knowledge.
Callbacks and search nodes remain transient. Existing types keep their full
`Gameplay.Personality` names despite the source-directory move to `Gameplay/Npc`.

Stories optionally retain their completion-goal and delivery-report policies,
so the director does not need to dispatch on a content ID. Observed events and
resident entries retain optional category metadata; custom observations also
cache prose for archive recovery. Resident entries always retain their text.
The archive-only reader therefore displays and filters extension events without
restoring runtime modules. Absent optional metadata falls back to the built-in
event definitions. See [npc-content-modules.md](npc-content-modules.md).

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
The optional group supply rule and last contributing event ID survive save and
load. Each member optionally saves the group identity, rule value and event ID
of the rule they last witnessed or learned from a trusted report. A contested
succession keeps the original group object for loyal members;
departing members receive a new group identity and retain their individual
relationship history.

Personalities optionally save up to sixteen unit-limited permissions: grantor
identity, map location, resource, expiry turn, remaining unit count and the
spoken concession event ID. A used, expired or mismatched permission cannot
authorize another base unit. Up to eight separate service agreements store
provider/patient identities and names, visited shelter location, offer and
accepted-event causes, deadline, story ID and status. Both participants save
their own copy. Active offers and accepted agreements are retained until they
reach a terminal status; a new offer requires capacity on both sides. Goals for
the shared shelter trip retain their ordinary story,
cause and destination fields. No new object points directly to an actor.
`claimed_permission` and `false_testimony_exposed` are bounded knowledge facts
with the original theft event ID; retellings preserve source and confidence.

Each personality may optionally retain up to sixteen `NpcInterest` records:
stable definition ID, subject identity, last known place, importance/need,
evidence and expiry turns, and a causal event ID. Definitions and callbacks
are rebuilt from the catalog. Faction coordinators optionally save one current
`NpcGroupPlan`, proposal sequence and retry turn on their personality; this
does not change social-group membership. The participant's ordinary goal keeps
the coordinator and story IDs. Group and faction proposals use the same saved
plan shape and resume from real observations after loading.

Session retains the story director's bounded story/role history, source maps,
cause IDs, stages, deadlines, role target IDs, actor/resource reservations,
cooldowns and per-map proposal schedule. Maps in knowledge, exits, goals, plans and reservations are
references to the same loaded graph objects. The per-map schedule is a list of
saved rows with a nonserialized reference-keyed lookup rebuilt on demand; it
does not depend on Map's mutable hash code or a serialized dictionary comparer.
Terminal goals, plans and stories release location references. Starting a new
Session clears its director and event sequence. No knowledge or director entry
retains Actor references.

Story roles retain the assigned intention sequence so concurrent intentions
using the same execution method remain distinct. Episodes retain up to eight
parent story IDs, with cycles rejected when connecting episodes. Resident
entries retain supporting causal event IDs in `SupportingCauses` and readable
links between episodes; the archive-only reader needs no world load to read them.

Session retains the significant-event ID sequence. Observations retain event,
cause and story IDs; a personality's processed-event cutoff survives journal
eviction and memory resolution. Intent delivery has its own cutoff. Replaying
the same identified event after loading does not create another memory, reward
or intention. The new fields use format 5's existing graph field encoding;
absent optional fields initialize to their defaults.

Generated building zones retain an optional `BuildingKind` alongside their
existing name and bounds. `None` is the default for roads, rooms and older
zones. Rumor wording looks up the kind at the fact's saved map position; the
spoken location is then preserved as text in heard journals and resident
records. Archive entries still do not store separate building coordinates.

Resident records retain NPC identity/name, spawn/death turns, faction and
leader-group snapshots, last inventory and traits, cumulative item acquisitions,
and the snapshot turn. Entries retain ordered text, event kind, direct/witnessed
status, participant IDs and whether resolution actually granted a trait.
The archive optionally retains each district's kind by coordinate so
`Read Records` can color district labels like the world map without loading the
world graph. Older archives without this field leave district labels uncolored.
Deduplication keys are preserved. Histories contain no Actor references and
survive actor/corpse removal; entries are not evicted.
Entries also retain event/cause/story IDs. Private intention starts and terminal
outcomes are typed chronicle entries with deduplication by intention sequence
and outcome. They are available to the archive-only reader and excluded from
physical-event counts and the interesting-life score. Private missing-contact
inferences, generated `goal_plan` action lists and story-stage entries use the
same typed archive path and **Intentions and outcomes** filter, and also do not
inflate those counts.
New plan entries include known building or map destinations and their district.
Older plan entries contain only action names, so their destinations cannot be
reconstructed by the archive reader.
Heard speech uses existing resident entries with kinds `heard_rumor`,
`heard_request` and `heard_reply`. Only awake intelligent NPCs within audio range
gain an entry; seeing the speaker is not required. Text, event ID and any known
cause/story ID survive archive-only load, under the **Encounters** filter.
Retained `NpcFact` entries optionally store `SubjectReportName` and
`OtherReportName`, the observer's original spoken description of each participant.
They also optionally store `CauseId`, copied from the significant event and
preserved on retelling. A story uses it for causal wording only when the linked
earlier event is the immediately preceding visible report. Older saves default
to zero and retain neutral chronological transitions.
An acquaintance is named; an unfamiliar visible person is described by faction
membership. Retelling and save/load preserve these descriptions while stable
participant IDs remain available for causal records. Faction-only hearsay does
not identify a person for NPC targeting. Older saves without these
fields fall back to their existing participant names. Retained facts also store
optional `SubjectFactionId` and `OtherFactionId` snapshots.
These let a listener react to the named faction when the observer did not know
the actor's name; older saves have no faction snapshot. A retained supply-loss
fact also stores optional `Resource` and its existing `Units` count so a later
report can describe the actual contents and amount. Older facts default to an
unspecified supply loss.
Unanswered player requests reuse saved `NpcReaction` state and expire after 30
turns. Its optional `Overheard` field distinguishes requests made to someone
else from requests addressed to the player. Player promises use the existing
`NpcCommitment` state and deadline.
The player's `PersonalityState` also has an optional bounded `HeardJournalEntry`
list (up to 128 entries), storing turn, speech kind, identified or anonymous
speaker, text and cause ID. Heard speech is populated only for awake players
within hearing range. Significant scripted discoveries and conversations also
store short summaries in this list, including the prisoner's request and the
facility directions after release. It is separate from NPC `ResidentRecords`
and is displayed in game with J. Saves made before this field was added load
with an empty list.
Read Records builds a temporary index of archived event IDs and resolves a
record's `CauseId` and `SupportingCauses` into up to two readable antecedents.
Only prior, present archive entries are shown; missing links produce no text.
The explanation and its searchable words are derived at read time, so this
change adds no persistent fields or save-format version.
The viewer keeps prepared display text and cause explanations on its `RecordsSave`
instance and refreshes them when a live archive gains entries or advances a turn.
Service agreements retain their existing saved status and history. The quick
indicator for an offered or accepted agreement is transient and rebuilt on first
access after loading; it adds no saved field.
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

On map reconstruction, radios saved with the old handheld-item image ID switch
both their visible and remembered image IDs to the dedicated map-object tile.
Their station, on/off state and location remain unchanged.

## Verification

When changing saved fields, test save/load state, alias identity and boundaries.
Renaming fields/types needs an explicit mapping or a new format policy; preserve
numeric and string content IDs. Run the named scenarios, then
`docker build --target test .` and `bash tests/e2e.sh`.

Relevant scenarios: `storage/compact-save`, `npc/interest-save`,
`npc/faction-medicine`, `npc/records-reader-save`, `npc/records-causes`,
`npc/records-lifetime-items`, `npc/records-query`, `world/records-browser`,
`npc/intent-persistence`, `npc/intent-boundaries`, `npc/story-persistence`,
`npc/story-director`, `factions/social-group-succession`,
`factions/social-group-nested-succession`, and existing
personality/relationship/base persistence cases. The E2E test
generates a world, writes/loads format 5, then uses search, filters, sorting and
the interesting-NPC selector through the real VNC UI.

See [performance.md](performance.md) for measurements and
[save-structure.md](save-structure.md) for the original structural audit.
