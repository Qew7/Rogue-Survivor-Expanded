# NPC traits and memories

Intelligent living actors created through `ActorModel` receive three distinct starting traits
and one or two unresolved memories when the new-game preset enables NPC traits
and memories. The actor owns a `PersonalityState`: trait instances, unresolved
memory instances, and a bounded journal of significant events it experienced or
witnessed. Personal memories also live in lasting relationship records after
resolution. No definition object or callback is serialized with the actor.

## Relationships

An NPC keeps separate private records for people, leader groups, and factions.
People and groups use stable actor identities; a leader's name is only a display
label, so namesakes do not merge. A group is the current leader and followers.
Changing leaders changes which group record applies, while old memories remain
attached to their original group. Factions use existing numeric faction IDs.

Memories involving another person are attributed to that person. Examples
include help, attacks, murder, theft, abandonment, joining a group, and deaths
of companions. Loss of the observer's own base is attributed to its group.
Unattributed raids and starvation have no personal target. A memory is linked
to the relevant relationship records as soon as it starts; when it resolves,
those records retain the memory, its resolution turn, and its outcome. Records
store identities and names, not Actor references, so a dead person is still
remembered and save graphs do not keep them alive.

Each event changes the feeling toward the person. The same experience has a
smaller group effect and, for non-civilian factions, a smaller faction effect.
The three feelings add for a current target, clamped to -100..100. Dislike can
stop trade offers and recruitment; positive feeling can make a marginal trade
acceptable. A follower's feeling toward its leader also affects trust growth.
Relationships only affect behavior while the preset option is enabled. NPC
inspection shows traits but never reveals private pending or resolved memories.

## Extending the catalog

Definitions live in `WRogue/Gameplay/Personality/PersonalityContent.cs` and are
registered in `PersonalityRegistry`. A `TraitDefinition` has a stable string ID,
display name, starting/advanced flag, optional required trait, optional item
model parameter, and one or more `TraitEffect`s. Standard effects change common
AI decision axes: item value, courage, group trust, law enforcement, trade,
exploration, compassion, and supply value. Conflicting starting traits can be
registered as a pair. A new trait that needs a new action should add its action
at the relevant AI or rule boundary and query `PersonalitySystem.HasTrait` there.
Group trust uses both the leader's traits and the follower's desire for company;
a solitary follower can lose trust over time while a sociable one gains it faster.

A `MemoryDefinition` owns its `MemoryTrigger`s and ordered `MemoryOutcome`s.
Its relationship roles select the event's subject, other participant, or group
leader, and its feeling values specify personal and collective reactions.
Triggers filter `SignificantEvent`s by kind and observer relationship. Outcomes
may inspect current traits and the actor's witnessed-event journal. Definitions
can declare evidence event kinds; a pending memory retains the latest observed
turn for each declared kind even after older journal entries are evicted. Use
this for events that must affect an outcome days later. A successful
outcome awards one eligible trait or one level of an existing living skill. The
last outcomes should usually provide related skill fallbacks so a capped skill
does not silently consume a memory. Resolution occurs on the
actor's map when its local day advances. Starting memories use the same
definitions and have a randomly rolled two-to-six-day deadline.

For a new trigger of an existing event kind, register another `MemoryTrigger`.
For a new kind of significant event, publish a `SignificantEvent` from the
actual game action through `RogueGame.ReportPersonalityEvent`. The event handler
checks the map's line of sight, records only witnesses, and routes the event to
matching triggers. Events can be about death, violence, leadership, aid, raids,
starvation, zombification, lost bases, or theft from a base. A theft of food or
weapons from an assigned storage room also reports lost supplies. A bounded
journal prevents save growth from repeated encounters. Relationship records
retain attributed memories without journal eviction, so saves may grow in long
games with many distinct encounters.
Death observations retain whether the subject was a leader or follower at the
time; later zombification can still trigger a companion memory after the game
removes the group relationship. A pending memory retains that relationship
even if the bounded event journal evicts the original death observation.
Events and memories keep actor identities separately from display names, so
namesakes do not merge or inherit each other's relationships.

The player starts without NPC starting traits or memories. Direct and witnessed
events build the player's own relationship records; resolving these memories
keeps their history but does not grant NPC trait or skill outcomes. Press
`Shift+I` (rebindable as Relationships) to view personal, leader-group, and
faction feelings. Groups are named after their leader. The screen displays
only the player's records and qualitative feelings, never another actor's
private memories or opinions. The list persists in the saved player actor.

The first catalog contains 50 starting and 20 advanced traits. Advanced traits
are available only through memory resolution and require an existing trait.
`likes_items` and `dislikes_items` each take an item model ID; pistol, shotgun,
magazine, or any other defined item model uses the same trait definition.

## Verification

Add a named scenario in its own file under `tests/scenarios/cases/npc/` for each
new behavior or trigger. Test a real action or AI choice and a boundary case.
Run `sh tests/scenario.sh <name>`, `docker build --target test .`, and for
menu or rendering changes `bash tests/e2e.sh`.
`npc/personality-catalog` audits all registered traits, memory triggers and
outcomes, so run it whenever the catalog changes.
