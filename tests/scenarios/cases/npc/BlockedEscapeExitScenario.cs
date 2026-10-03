using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.MapObjects;

static class BlockedEscapeExitScenario
{
    static ScenarioWorld World(int seed, bool alternate, out Actor civilian, out Map refuge)
    {
        ScenarioWorld world = TownScenarioFactory.Arena(seed,
            ".......", ".......", ".......", ".......", ".......");
        Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
        NpcIntentSupport.Player(world, 0, 0);
        civilian = NpcIntentSupport.Actor(world, "civilian", 3, 2, "organized", "timid");
        civilian.HitPoints = 1;
        civilian.StaminaPoints = 1;
        Actor zombie = new Actor(world.Game.GameActors.Zombie, world.Game.GameFactions.TheUndeads,
            "zombie", false, false, 0);
        world.Place(zombie, 4, 2);
        Map blocked = new Map(seed + 1, "blocked refuge", 3, 3);
        refuge = new Map(seed + 2, "open refuge", 3, 3);
        for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++)
        {
            blocked.SetTileModelAt(x, y, world.Game.GameTiles.FLOOR_ASPHALT);
            refuge.SetTileModelAt(x, y, world.Game.GameTiles.FLOOR_ASPHALT);
        }
        world.Map.District.AddUniqueMap(blocked);
        world.Map.District.AddUniqueMap(refuge);
        blocked.PlaceMapObjectAt(new Fortification("wall", "wall", 40), new Point(1, 1));
        world.Map.SetExitAt(new Point(3, 2), new Exit(blocked, new Point(1, 1)) { IsAnAIExit = true });
        if (alternate)
            world.Map.SetExitAt(new Point(2, 2), new Exit(refuge, new Point(1, 1)) { IsAnAIExit = true });
        return world;
    }

    public static void Register()
    {
        ScenarioRunner.Add("npc/blocked-escape-exit", () =>
        {
            Actor ignored; Map refuge;
            return World(7421, true, out ignored, out refuge);
        }, world =>
        {
            Actor civilian = world.Map.GetActorAt(3, 2);
            Check.Equal(true, world.NpcTurn(civilian), "civilian chooses a legal escape step");
            Check.Equal(new Point(2, 2), civilian.Location.Position,
                "civilian passes the blocked exit for a usable refuge");
            civilian.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(true, world.NpcTurn(civilian), "civilian uses the alternate exit");
            Check.Equal("open refuge", civilian.Location.Map.Name, "civilian reaches the usable refuge");

            Actor trapped; Map unused;
            ScenarioWorld noAlternate = World(7422, false, out trapped, out unused);
            Check.Equal(true, noAlternate.NpcTurn(trapped), "civilian acts when the only exit is blocked");
            Check.Equal(false, trapped.Location.Position == new Point(3, 2),
                "civilian does not spend the turn repeatedly trying the blocked exit");
            Check.Equal(true, trapped.Location.Map == noAlternate.Map,
                "the blocked exit cannot move the civilian to the destination map");
        });
    }
}
