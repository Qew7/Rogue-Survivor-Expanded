# NPC traits and memories

Intelligent living actors created through `ActorModel` receive three distinct starting traits
and one or two unresolved memories when the new-game preset enables NPC traits
and memories. The actor owns a `PersonalityState`: trait instances, unresolved
memory instances, and a bounded journal of significant events it experienced or
witnessed. No definition object or callback is serialized with the actor.

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
Triggers filter `SignificantEvent`s by kind and observer relationship. Outcomes
may inspect current traits and the actor's witnessed-event journal. A successful
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
journal prevents save growth from repeated encounters.
Death observations retain whether the subject was a leader or follower at the
time; later zombification can still trigger a companion memory after the game
removes the group relationship. A pending memory retains that relationship
even if the bounded event journal evicts the original death observation.
Events and memories keep actor identities separately from display names, so
namesakes do not merge or inherit each other's relationships.

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
