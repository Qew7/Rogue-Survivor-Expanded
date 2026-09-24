# Fast gameplay scenarios

Run a named scenario without opening the game window:

```sh
sh tests/scenario.sh --list
sh tests/scenario.sh movement/wall
sh tests/scenario.sh --all
```

The first command builds a Docker image with the game and scenario runner.
Later runs reuse Docker's build cache. A failed scenario exits nonzero and
prints its name, random seed, exception, and final map. Regular
`docker build --target test .` also runs every scenario alongside unit tests.

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
