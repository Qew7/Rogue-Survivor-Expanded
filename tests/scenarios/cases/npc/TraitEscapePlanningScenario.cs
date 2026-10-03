using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.AI;

static class TraitEscapePlanningScenario
{
    static ScenarioWorld World(int seed, string trait, bool withExit, bool personalities, out Actor civilian)
    {
        ScenarioWorld world = TownScenarioFactory.Arena(seed,
            "############.", "####.#######.", "####.#######.", "...........#.",
            "############.", "############.", "############.");
        Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
        Session.Get.GamePreset.NpcPersonalitiesEnabled = personalities;
        NpcIntentSupport.Player(world, 12, 6);
        civilian = trait == null ? NpcIntentSupport.Actor(world, "civilian", 5, 3) :
            NpcIntentSupport.Actor(world, "civilian", 5, 3, trait);
        civilian.HitPoints = world.Game.Rules.ActorMaxHPs(civilian) / 2;
        Actor zombie = new Actor(world.Game.GameActors.Zombie, world.Game.GameFactions.TheUndeads,
            "zombie", false, false, 0);
        world.Place(zombie, 7, 3);
        if (withExit)
        {
            Map refuge = new Map(seed + 100000, "refuge", 3, 3);
            for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++)
                refuge.SetTileModelAt(x, y, world.Game.GameTiles.FLOOR_ASPHALT);
            world.Map.District.AddUniqueMap(refuge);
            world.Map.SetExitAt(new Point(4, 1), new Exit(refuge, new Point(1, 1)) { IsAnAIExit = true });
        }
        return world;
    }

    public static void Register()
    {
        ScenarioRunner.Add("npc/trait-escape-planning", () =>
        {
            Actor ignored;
            return World(7201, "organized", true, true, out ignored);
        }, world =>
        {
            Actor organized = world.Map.GetActorAt(5, 3);
            Check.Equal(true, world.NpcTurn(organized), "organized NPC executes a legal escape action");
            Check.Equal(new Point(4, 2), organized.Location.Position,
                "organized NPC turns toward the visible refuge rather than the dead end");
            for (int turn = 1; turn <= 8 && organized.Location.Map == world.Map; turn++)
            { organized.ActionPoints = Rules.BASE_ACTION_COST;
                Check.Equal(true, world.NpcTurn(organized), "organized NPC continues the escape plan"); }
            Check.Equal(false, organized.Location.Map == world.Map, "organized NPC reaches the refuge");

            Actor ordinary;
            ScenarioWorld noTrait = World(7000, null, true, true, out ordinary);
            for (int turn = 0; turn <= 8 && ordinary.Location.Map == noTrait.Map; turn++)
            { ordinary.ActionPoints = Rules.BASE_ACTION_COST;
                Check.Equal(true, noTrait.NpcTurn(ordinary), "ordinary NPC acts legally"); }
            Check.Equal(true, ordinary.Location.Map == noTrait.Map,
                "ordinary NPC does not reliably finish the visible detour");

            Actor noExit;
            ScenarioWorld blocked = World(7201, "organized", false, true, out noExit);
            Check.Equal(true, blocked.NpcTurn(noExit), "organized NPC acts without a refuge");
            Check.Equal(true, noExit.Location.Map == blocked.Map,
                "a missing exit cannot be used by the plan");

            Actor disabled;
            ScenarioWorld noPersonality = World(7000, "organized", true, false, out disabled);
            for (int turn = 0; turn <= 8 && disabled.Location.Map == noPersonality.Map; turn++)
            { disabled.ActionPoints = Rules.BASE_ACTION_COST;
                Check.Equal(true, noPersonality.NpcTurn(disabled), "NPC acts with personalities disabled"); }
            Check.Equal(true, disabled.Location.Map == noPersonality.Map,
                "disabled personalities do not grant the organized escape route");
        });
    }
}
