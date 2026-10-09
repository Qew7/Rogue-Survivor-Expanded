using System;
using System.Drawing;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class WorldSleepPacingScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/sleep-paces-distant-districts", () => TownScenarioFactory.Arena(4994,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = NpcIntentSupport.Player(world, 2, 1);
            World city = new World(2);
            city[0, 0] = world.Map.District;
            District distantDistrict = new District(new Point(1, 1), DistrictKind.RESIDENTIAL);
            Map distant = new Map(4995, "distant", 5, 3);
            Map sewers = new Map(4996, "sewers", 5, 3);
            for (int y = 0; y < 3; y++) for (int x = 0; x < 5; x++)
            {
                distant.SetTileModelAt(x, y, world.Game.GameTiles.FLOOR_ASPHALT);
                sewers.SetTileModelAt(x, y, world.Game.GameTiles.FLOOR_ASPHALT);
            }
            distantDistrict.EntryMap = distant;
            distantDistrict.SewersMap = sewers;
            city[1, 1] = distantDistrict;
            Session.Get.World = city;
            world.Map.LocalTime.TurnCounter = Session.Get.WorldTime.TurnCounter = 100;
            player.SleepPoints = Math.Max(0, world.Game.Rules.ActorMaxSleep(player) - 50);
            player.ActionPoints = Rules.BASE_ACTION_COST;
            world.Game.DoStartSleeping(player);
            Type flags = typeof(RogueGame).GetNestedType("SimFlags", BindingFlags.NonPublic);
            object active = Enum.Parse(flags, "NOT_SIMULATING");
            Check.Call(world.Game, "AdvancePlay", new[] { typeof(District), flags }, world.Map.District, active);
            Check.Equal(true, player.IsSleeping, "player remains asleep after first turn");
            Check.Equal(true, distant.LocalTime.TurnCounter > 0 &&
                distant.LocalTime.TurnCounter < Session.Get.WorldTime.TurnCounter,
                "old district turns are distributed across sleep");
            for (int i = 0; player.IsSleeping && i < 200; i++)
                Check.Call(world.Game, "AdvancePlay", new[] { typeof(District), flags }, world.Map.District, active);
            Check.Equal(false, player.IsSleeping, "sleep eventually ends");
            Check.Equal(Session.Get.WorldTime.TurnCounter, distant.LocalTime.TurnCounter,
                "all deferred turns finish before the player wakes");
        });
    }
}
