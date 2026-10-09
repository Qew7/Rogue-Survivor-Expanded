using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class WorldRestSimulationScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/rest-simulates-distant-districts", () => TownScenarioFactory.Arena(4971,
            ".....", ".....", "....."), world =>
        {
            GamePreset preset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.GamePreset = preset;
            Actor player = NpcIntentSupport.Player(world, 2, 1);
            World city = new World(2);
            city[0, 0] = world.Map.District;
            for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++)
            {
                if (x == 0 && y == 0) continue;
                District district = new District(new Point(x, y), DistrictKind.RESIDENTIAL);
                Map map = new Map(4972 + x * 2 + y, "distant", 5, 3);
                for (int row = 0; row < 3; row++) for (int col = 0; col < 5; col++)
                    map.SetTileModelAt(col, row, world.Game.GameTiles.FLOOR_ASPHALT);
                district.EntryMap = map;
                city[x, y] = district;
            }
            Session.Get.World = city;
            Map distant = city[1, 1].EntryMap;
            Actor witness = NpcIntentSupport.Actor(world, "witness", 3, 1, "sociable");
            world.Map.RemoveActor(witness);
            distant.PlaceActorAt(witness, new Point(1, 1));
            PersonalitySystem.Report(world.Game, new SignificantEvent("building_explored", witness, null,
                distant, witness.Location.Position, 0, storyId: "remote-event"));
            NpcFact remoteFact = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "building_explored");
            Actor neighbor = NpcIntentSupport.Actor(world, "neighbor", 4, 1);
            world.Map.RemoveActor(neighbor);
            distant.PlaceActorAt(neighbor, new Point(2, 1));
            Check.Equal(false, neighbor.Personality.Knowledge.Facts.Exists(f => f.EventId == remoteFact.EventId),
                "neighbor did not witness the earlier event");
            Type flags = typeof(RogueGame).GetNestedType("SimFlags", BindingFlags.NonPublic);
            object active = Enum.Parse(flags, "NOT_SIMULATING");
            player.ActionPoints = Rules.BASE_ACTION_COST;
            world.Game.DoWait(player);
            Check.Call(world.Game, "AdvancePlay", new[] { typeof(District), flags }, world.Map.District, active);
            Check.Equal(1, distant.LocalTime.TurnCounter, "ordinary wait simulates a distant district");

            preset.DisableDistantSimulationDuringRest = true;
            Session.Get.GamePreset = preset;
            player.ActionPoints = Rules.BASE_ACTION_COST;
            world.Game.DoWait(player);
            Check.Call(world.Game, "AdvancePlay", new[] { typeof(District), flags }, world.Map.District, active);
            Check.Equal(1, distant.LocalTime.TurnCounter, "preset switch keeps distant district paused");

            preset.DisableDistantSimulationDuringRest = false;
            Session.Get.GamePreset = preset;
            player.SleepPoints = 0;
            player.ActionPoints = Rules.BASE_ACTION_COST;
            world.Game.DoStartSleeping(player);
            Check.Call(world.Game, "AdvancePlay", new[] { typeof(District), flags }, world.Map.District, active);
            Check.Equal(true, distant.LocalTime.TurnCounter < Session.Get.WorldTime.TurnCounter,
                "sleep spreads the older distant turns");
            world.Game.DoWakeUp(player);
            Check.Call(world.Game, "FinishRestSimulationIfNeeded");
            Check.Equal(Session.Get.WorldTime.TurnCounter, distant.LocalTime.TurnCounter,
                "interrupted sleep catches the distant district up before player input");
            Check.Equal(true, neighbor.Personality.Knowledge.Facts.Exists(f => f.EventId == remoteFact.EventId &&
                f.Source == NpcKnowledgeSource.Told), "simulated NPCs pass the remote event by word of mouth");
            string savePath = Path.Combine(Path.GetTempPath(), "rest-world-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(savePath, Session.Get);
                Session loaded = BinarySaveStore.Load<Session>(savePath);
                Map loadedDistant = loaded.World[1, 1].EntryMap;
                Check.Equal(distant.LocalTime.TurnCounter, loadedDistant.LocalTime.TurnCounter,
                    "distant local time survives save and load");
                Check.Equal(true, loadedDistant.Actors.Any(a => a.Name == "neighbor" &&
                    a.Personality.Knowledge.Facts.Exists(f => f.EventId == remoteFact.EventId &&
                        f.Source == NpcKnowledgeSource.Told)),
                    "distant NPC and learned rumor survive save and load");
            }
            finally
            {
                if (File.Exists(savePath)) File.Delete(savePath);
                if (File.Exists(savePath + ".bak")) File.Delete(savePath + ".bak");
            }
            Session.Get.WorldTime.TurnCounter = WorldTime.TURNS_PER_DAY;
            bool broadcast = false;
            for (int slot = 24; slot < 30; slot++)
            {
                RadioProgram program = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 0, slot);
                if (program.EventId == remoteFact.EventId) { broadcast = true; break; }
            }
            Check.Equal(true, broadcast, "a distant rumor retold during rest can reach the next day's radio");
        });
    }
}
