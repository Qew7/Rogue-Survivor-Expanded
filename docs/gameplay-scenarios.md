# Fast gameplay scenarios

Run a named scenario without opening the game window:

```sh
sh tests/scenario.sh --list
sh tests/scenario.sh movement/wall
sh tests/scenario.sh --all
sh tests/scenario.sh --bench-npc-safety
```

The first command builds a Docker image with the game and scenario runner.
Later runs reuse Docker's build cache. A failed scenario exits nonzero and
prints its name, random seed, exception, and final map. Regular
`docker build --target test .` also runs every scenario alongside unit tests.
`--bench-npc-safety` runs the route, hunger and sleep experiments in one process
and prints one summary after all fixtures finish. See [npc-safety-experiment.md](npc-safety-experiment.md).

`storage/save-budget` measures a large fixed world in isolated child processes
and enforces a 10-second limit per save/load and a 50,000,000-byte limit per file.
It prints RAM measurements and verifies restored state. This case takes tens of
seconds; see [performance.md](performance.md#automated-save-and-load-budget).

NPC module contracts are exercised by `npc/content-module`,
`npc/content-module-persistence`, `npc/content-catalog-validation`, `npc/content-catalog-rebind` and
`npc/private-content-event`. They cover a complete extension selected by real
AI, extended planning facts, saved execution, archive-only event filtering,
invalid registration/payloads and private audiences. See
[npc-content-modules.md](npc-content-modules.md) for the extension API.
`npc/records-causes` checks archive-only causal explanations and a missing
cause, while `npc/knowledge-empty-caches`, `npc/resource-item-need`,
`npc/report-prose` and `factions/social-group-nested-succession` cover the
related review boundaries with real perception, reactions, speech and death.
`npc/records-live-cache` checks archive refresh during a live session;
`npc/operator-archive-text` checks registered plan wording.
`npc/ask-location-unknown-place`, `npc/protection-missing-faction`,
`npc/planner-barter-place-limit` and `npc/group-plan-participant-exit` cover
unknown map data, bounded planner locations and shared-goal lifecycle.
`npc/player-conversation`, `npc/player-talk-input`, `npc/overheard-conversation` and
`npc/overheard-rumor` cover the talk command, Y/N consequences, audio range,
walls, rumor learning and archive-only heard-speech search. The first also
checks persistent promises; `world/talk-keybinding-migration` checks old keys.
`npc/rumor-third-listener` checks that a hearer cannot be addressed the same
rumor later, while `npc/perception-current-sensor` checks fresh and stale
observations. `npc/service-open-agreements` checks the active-service guard and
restored history.
`npc/all-rumor-identities` checks the name/faction rule against every registered
reportable event. `npc/supply-loss-rumor`, `npc/aid-trade-rumor`,
`npc/barter-rumor`, `npc/base-loss-rumor`, `npc/raid-rumor` and
`npc/raid-rumor-ai` exercise the corresponding real actions or event handlers.
`npc/supply-loss-rumor-save`
checks the new supply payload across save and load.
`npc/resource-permission`, `npc/conflicting-testimony`,
`npc/resource-permission-stack`, `npc/relayed-false-testimony`,
`npc/group-supply-rule`, `npc/group-supply-trade`, `npc/contested-succession`,
`npc/lone-succession-split`, `npc/goal-fallback-text`, `npc/service-agreement-capacity`,
`npc/shelter-care-exchange`, `npc/shelter-care-deadline` and
`npc/shelter-care-player-reply`
exercise new social consequences through pickup, speech, trade, death,
travel, treatment and deadlines. `npc/social-dynamics-save` checks their
persistent state and archive text.
`npc/courage-assessment`, `npc/courage-retreat` and `npc/courage-rumor-save`
cover situational resolve, actual flight, witnesses, memory resolution, rumor
consequences and save/load.

Add each new scenario in its own file under the matching topic directory in
`tests/scenarios/cases/`. Living and undead skills have separate directories.
Give its static class a name ending in `Scenario` and a public `Register()`
method using `ScenarioRunner.Add(name, createWorld, run)`. The runner finds
these classes automatically. The `createWorld` callback
constructs a fresh `ScenarioWorld` with a fixed seed and rows of `.` (floor)
and `#` (wall). `Place` adds a lightweight actor or an actor created from a
real game model; `SetTile` changes terrain; `Bump`
and `NpcTurn` execute real game actions. Use `Check.Equal` and `Check.Same`
to assert both the actor's state and the map's state after each action.

```csharp
static class MovementNewCaseScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("movement/new-case", () => new ScenarioWorld(104,
            "...", ".#.", "..."), world =>
        {
            Actor actor = world.Place("player", 0, 1);
            Check.Equal(false, world.Bump(actor, Direction.E), "wall blocks movement");
            Check.Equal(new Point(0, 1), actor.Location.Position, "position unchanged");
        });
    }
}
```

`NpcTurn` calls the actor's assigned `ActorController.GetAction`, checks the
action's legality, and performs it. The `npc/obstacle` example uses a small
test controller so the expected choice is exact. `npc/civilian-decision`
generates a town, runs a production civilian AI for one action, and checks
that it spent action points and remained on the map. `generation/residential`
checks the actual town generator's output at a fixed seed. For broader
playthroughs, `bash tests/e2e.sh` drives the actual game window through VNC.

The small map fixture runs without UI setup. For procedural generation and
production AI, use the headless `ScenarioUI` and load the game data as shown
in `tests/scenarios/TownScenarioFactory.cs`. Keep the seed fixed so failures reproduce.
Scenarios run individual actions; the VNC end-to-end test covers the running
game and full district turns.

There is one named scenario per file. The current suite includes a primary
rule effect for every living and undead skill, saved progression, and scenarios for combat,
factions, followers, doors, items, survival, weather, generation, map exits,
and map saving. `SkillScenario.AssertCoverage` fails if a skill is added without a
named scenario. These checks cover the stated outcomes; they do not yet
simulate every AI choice, item model, quest, or multi-turn
world event.
