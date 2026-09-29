# NPC traits and memories

Intelligent living actors created through `ActorModel` receive three distinct starting traits
and one or two unresolved memories when the new-game preset enables NPC traits
and memories. The actor owns a `PersonalityState`: trait instances, unresolved
memory instances, and a bounded journal of significant events it experienced or
witnessed. Personal memories also live in lasting relationship records after
resolution. No definition object or callback is serialized with the actor.

## Relationships

An NPC keeps separate private records for people, groups, and factions.
People use stable actor identities and groups use permanent group identities;
a leader's name is only a display label, so namesakes do not merge. Followers,
including nested followers, share their top-level group. Succession preserves
its identity and histories; a newly founded group after a split has a new
identity. Factions use existing numeric faction IDs.

Memories involving another person are attributed to that person. Examples
include help, attacks, murder, theft, abandonment, joining a group, and deaths
of companions. Loss of the observer's own base is attributed to its group.
The founder's identity initializes the separate group namespace; a later
leader does not replace its key. The base-loss memory changes
no feeling score and remains in this history after resolution.
Unattributed raids and starvation have no personal target. A memory is linked
to the relevant relationship records as soon as it starts; when it resolves,
those records retain the memory, its resolution turn, and its outcome. Records
store identities and names, not Actor references, so a dead person is still
remembered and save graphs do not keep them alive.

Each event changes the feeling toward the person. The same experience has a
smaller group effect and, for non-civilian factions, a smaller faction effect.
The three feelings and acquired faction-specific trait biases add for a current
target, clamped to -100..100. Dislike can
stop trade offers and recruitment; positive feeling can make a marginal trade
acceptable. A follower's feeling toward its leader also affects trust growth.
Accumulated `TrustInLeader` remains the authority for trusting the current
leader: while it meets the existing threshold, the follower accepts that
leader's trade offers and does not exclude them from autonomous trade solely
because of negative attitude. Memories can reduce trust on subsequent turns;
once trust falls below the threshold, normal attitude-based refusals apply.
Relationships only affect behavior while the preset option is enabled. NPC
inspection shows traits but never reveals private pending or resolved memories.

The original AI systems keep their existing roles: `MemorizedSensor` tracks
recent perceptions, `ExplorationData` tracks visited places, and aggression/
self-defense records determine combat hostility. Personality observations track
experienced or witnessed consequences. `OrderableAI.OnRaid` stores a heard
arrival signal for reporting even without line of sight; hearing it alone does
not create a witnessed personality memory. `Scoring` keeps the player's game
history, while `ResidentRecords` keeps individual NPC histories for Read Records.

## Trait-driven intentions

Civilian, gang, soldier and CHAR guard controllers can turn perceived aid,
hunger, food requests and violence by their leader into persistent personal
intentions. Current traits, relationships and leader trust determine motivation;
existing survival and combat priorities interrupt ordinary social actions.
NPCs can thank a helper, repay with actual food, ask for or decline aid, and
voluntarily leave an unsafe leader. Private intentions and memories remain
hidden during gameplay. Read Records retains their starts, outcomes and linked
physical events, including an **Intentions and outcomes** category.

NPCs also keep bounded knowledge with sources, confidence and remembered places.
Real conversations can pass reports or answer a searcher's question; direct
sight takes precedence over weaker reports of the same or older observation.
Traits select avoiding or warning a reported aggressor, searching for a missing
companion, accepting a supply assignment, or following a shelter proposal.
Supply missions use real pickups, gifts and return reports. Shared episodes
bind separate participant goals, with resource reservations and saved pacing.
Eligible followers can preserve their group under a successor after an NPC
leader's death.

Goals now describe desired results. A bounded planner composes available actions
at runtime, using the NPC's traits, relationships and local knowledge to compare
methods. Acquiring food can involve a known pickup, a request or a real offered
exchange; a helper can ask another person before assisting its recipient.
Refusals and changed supplies trigger replanning. Predicted help never creates
food, and witnessing another participant's successful delivery can remove a
redundant step. Plans stay private during gameplay and are retained in Read
Records alongside their actual outcomes.

`NpcGoalGenerator` creates those desired results by evaluating deficits in the
NPC's current needs and remembered conditions. Nutrition, recovery, care,
reciprocity, safety, justice, belonging and autonomy use trait-driven importance
and evidence confidence. It accepts no event kind: observations first update
beliefs, and changed state then motivates a goal. Existing debt or injury can
produce a goal without a new significant event. New urgent needs can replace a
weaker generated goal at the decision boundary. Private starts record the
current/desired values and utility explanation in Read Records.

An actual food exchange creates the acquired `traded_for_food` memory, attributed
to the trading partner. Its resolution can grant CHARISMATIC skill. An offer or
an invalid transaction alone creates no memory of successful negotiation.

Person records also retain trust, fear, attachment, grievance and debt at
0..100. Aid and known violence change these values; they affect reports and
method motivation. They complement the existing feeling score and explicit
leader trust. Knowledge, goals and these opinions remain private during play.

See [npc-intentions.md](npc-intentions.md) for implemented behavior, limits and
extension points, including faction preferences and director admission rules.

## Extending the catalog

Definitions live in `WRogue/Gameplay/Personality/PersonalityContent.cs` and
`PersonalityWorldContent.cs`; acquired intention/conversation memories are also
registered through `NpcIntentContent.cs` and `NpcStoryContent.cs`. They are
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

The catalog contains 50 starting and 64 advanced traits. Advanced traits
are available only through memory resolution and require an existing trait.
`likes_items` and `dislikes_items` each take an item model ID; pistol, shotgun,
magazine, or any other defined item model uses the same trait definition.

Promises, resource disputes, acquired `reliable` and `disillusioned` traits,
specific possession and home attachments, and causal continuations are described
in [npc-social-stories.md](npc-social-stories.md). Reports affect reputation with
confidence discounting. An overdue promise is a private assessment until someone
actually tells it to others.

## Experiences with unique characters, factions, and world events

There are 42 additional acquired traits, paired with 42 source-specific memories:
nine unique encounters, eighteen faction experiences, and fifteen world/story
events. None of these memories or traits is randomly assigned at character
creation. Each memory has a two-to-six-day deadline. Its first outcome offers
the special trait when the NPC has the required starting trait; otherwise it
offers a related skill, then a fallback skill. Existing traits and skill caps
still apply. `friend_<faction>` and `wary_<faction>` conflict with each other.
Player memories preserve history without awarding these NPC outcomes.

Unique encounters happen when an awake intelligent living observer sees the
actual actor registered in `Session.UniqueActors`. A matching name or actor
model is insufficient. A wall, distance, sleep, or an unspawned/removed actor
prevents the encounter. The first encounter is remembered once per observer
and unique identity, including after resolution and save/load. It changes
feelings toward the person and their group/faction where applicable.

| Unique | Memory ID | Special trait | Required trait |
| --- | --- | --- | --- |
| Big Bear | `met_big_bear` | `bear_resolve` | `brave` |
| Famu Fataru | `met_famu_fataru` | `blade_discipline` | `disciplined` |
| Santaman | `met_santaman` | `holiday_spirit` | `generous` |
| Roguedjack | `met_roguedjack` | `rogue_ingenuity` | `curious` |
| Duckman | `met_duckman` | `duck_camaraderie` | `sociable` |
| Hans von Hanz | `met_hans_von_hanz` | `hans_drill` | `disciplined` |
| The Prisoner Who Should Not Be | `met_prisoner` | `prisoner_secrets` | `suspicious` |
| Jason Myers | `met_jason_myers` | `masked_survivor` | `cautious` |
| The Sewers Thing | `met_sewers_thing` | `sewer_dread` | `fearful` |

Actual help from CHAR, Army, Bikers, Gangstas, Police, BlackOps, Psychopaths,
or Survivors can create `aid_<faction>` and develop `friend_<faction>` in a
trusting NPC. An attack by one of these factions, Undeads, or Ferals, or a
witnessed murder committed by them, creates `violence_<faction>` and can develop
`wary_<faction>` in a suspicious NPC. These experiences supplement the existing
personal help/violence memories; their additional feeling changes apply to the
faction without repeating the personal penalty or reward. Friendship adds +15
to attitude toward other faction members; distrust adds -20. Both also affect
normal AI trade, group, or supply preferences. Civilian individual encounters
retain their generic memories; refugee arrivals supply their collective memory.
Animals and undead have no aid outcome and do not gain personality traits.

World handlers report events at their actual arrival/drop/discovery positions.
Only observers who see those positions remember them. Failed spawns create no
arrival memory. Peaceful arrivals have their own events instead of the generic
`raid` event, which remains registered for old memories. Named raids attribute
their memories to the actual leader and their group; members of the raiding
faction do not acquire fear of their own raid.

| World/story event and memory ID | Special trait | Required trait |
| --- | --- | --- |
| `zombie_invasion` | `night_watch` | `vigilant` |
| `sewers_invasion` | `underground_caution` | `cautious` |
| `refugees_arrival` | `refugee_solidarity` | `kind` |
| `national_guard_arrival` | `army_confidence` | `trusting` |
| `army_supplies` | `relief_organizer` | `organized` |
| `bikers_raid` | `roadside_vigilance` | `vigilant` |
| `hells_souls_raid` | `hells_souls_defiance` | `brave` |
| `free_angels_raid` | `free_angels_watchfulness` | `vigilant` |
| `gangstas_raid` | `streetwise` | `pragmatic` |
| `craps_raid` | `craps_grudge` | `vindictive` |
| `floods_raid` | `floods_caution` | `cautious` |
| `blackops_raid` | `blackops_distrust` | `suspicious` |
| `survivors_arrival` | `convoy_hope` | `sociable` |
| `char_discovered` | `char_whistleblower` | `skeptic` |
| `prisoner_transformed` | `betrayal_scar` | `suspicious` |

Special traits affect existing AI decisions: courage, supplies, exploration,
compassion, trade, group trust, and law enforcement. World experiences also bias
attitude toward their source faction. For example, an Army assault can develop
`wary_army` and make an NPC refuse trade with a previously unfamiliar soldier;
meeting Santaman can develop generosity and willingness to trade. Resolved
memories stay in personal/group/faction histories, and the saved-world chronicle
records the encounter, memory, and eventual trait or skill outcome.

## Reading saved records

`Read Records`, directly below `Load Game` in the main menu, opens a saved-world
chronicle. Choose a `.dat` or `.sav` file in the user `Saves` directory, including
explicit `.bak` backups, with NPC personalities enabled. The game still has one
active save slot; copies kept in that directory can be browsed independently.
Then choose `All residents` or an individual NPC. Namesakes have distinct identity
tags, and dead NPCs remain available even after their bodies disappear.

The chronicle records intelligent living NPCs from their arrival: starting traits
and memories, significant events they experience or witness, memory resolution
and its trait/skill outcome, and death. It is a chronological history of the
personality system's significant events, not a log of every movement or action.
Unlike the short AI observation journal, this archive does not evict early events.
It therefore adds to save size as a world grows older.

This main-menu reader reveals saved NPC records outside gameplay. Format-5
saves carry a separate compressed archive, so reading does not load the world,
resume simulation, or change the active session, mods or options. NPC memories
remain hidden from gameplay inspection. Old world formats are unsupported.

### Search, filters and ordering

In the resident browser:

- **S** searches a name (case insensitive); **F** opens combined filters.
- Filter by current/last faction or group leader, alive/dead status, minimum
  item acquisitions, memories, events, unique participants appearing in events, human kills,
  resolved memories and gained traits; specify a minimum/maximum lifespan.
- **O** sorts by name or any of those metrics, including lifespan and interest
  score. Numeric sorts default to most/longest first; **V** reverses the order.
- **I**, or the “Most interesting NPC” row, opens the highest-scoring resident
  **among the current matches**. Empty results are explained. Ties use name and
  persistent identity, so the same save/query selects the same resident.
- **R** resets the browser; Enter opens one resident or the merged history of
  all matching residents.

Item acquisitions count successful incoming units over the entire life,
including starting inventory, partial pickups, gifts and trades. Spending or
losing items does not subtract; repeated pickups count again. Last inventory
units/stacks are shown separately. Memory counts include creation history,
even after resolution or removal from the AI's pending queue. Life duration
is elapsed turns divided by 720, ending at death for dead residents. Unknown
death dates display “?” and do not match a numeric lifespan range.

The interest score gives capped weights to different event kinds (8, cap 12),
direct experiences (2, cap 40), created memories (5, cap 20), resolutions (6,
cap 12), gained traits (8, cap 10), unique participants appearing in events (3, cap 15),
world experiences (4, cap 15) and help given (4, cap 12). It does not reward
item farming or age by themselves. The resident's page shows its metrics and
the formula so the selection is explainable.

In a timeline, **S** searches event text and **F** selects memories/traits,
combat, help, encounters/groups, world events, life/survival or intentions and
outcomes. Both restrictions
combine; **R** clears them. Up/Down and PgUp/PgDn scroll, Home/End jump, Escape
returns. Changing a timeline filter does not change the resident's life score.
Text prompts support Backspace, Ctrl+A to clear and Ctrl+V to paste names.


## Verification

Add a named scenario in its own file under `tests/scenarios/cases/npc/` for each
new behavior or trigger. Test a real action or AI choice and a boundary case.
Run `sh tests/scenario.sh <name>`, `docker build --target test .`, and for
menu or rendering changes `bash tests/e2e.sh`.
`npc/personality-catalog` audits all registered traits, memory triggers and
outcomes, so run it whenever the catalog changes.
