using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class RestDeferredDebtScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/rest-leaves-distant-debt", () => TownScenarioFactory.Arena(4998,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = NpcIntentSupport.Player(world, 2, 1);
            World city = new World(4);
            city[0, 0] = world.Map.District;
            var remote = new District(new Point(3, 3), DistrictKind.RESIDENTIAL);
            Map distant = new Map(4999, "distant", 5, 3);
            Map sewers = new Map(5099, "sewers", 5, 3);
            for (int y = 0; y < 3; y++) for (int x = 0; x < 5; x++)
            {
                distant.SetTileModelAt(x, y, world.Game.GameTiles.FLOOR_ASPHALT);
                sewers.SetTileModelAt(x, y, world.Game.GameTiles.FLOOR_ASPHALT);
            }
            remote.EntryMap = distant;
            remote.SewersMap = sewers;
            city[3, 3] = remote;
            Session.Get.World = city;
            world.Map.LocalTime.TurnCounter = Session.Get.WorldTime.TurnCounter = 10;

            Type flags = typeof(RogueGame).GetNestedType("SimFlags", BindingFlags.NonPublic);
            object active = Enum.Parse(flags, "NOT_SIMULATING");
            player.ActionPoints = Rules.BASE_ACTION_COST;
            world.Game.DoWait(player);
            Check.Call(world.Game, "AdvancePlay", new[] { typeof(District), flags }, world.Map.District, active);
            Check.Equal(11, Session.Get.WorldTime.TurnCounter, "player turn advances normally");
            Check.Equal(0, distant.LocalTime.TurnCounter, "far debt remains after the live turn");
            Check.Equal(0, distant.LocalTime.TurnCounter, "returning control does not block on far debt");

            string path = Path.Combine(Path.GetTempPath(), "rest-debt-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.Load<Session>(path);
                Check.Equal(0, loaded.World[3, 3].EntryMap.LocalTime.TurnCounter,
                    "unfinished district clock survives save and load");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }

            Check.Call(world.Game, "StartSimThread");
            Check.Equal(true, SpinWait.SpinUntil(() => distant.LocalTime.TurnCounter == 11, 5000),
                "background worker eventually clears the saved debt");
            Check.Call(world.Game, "StopSimThread", false);
        });
    }
}
