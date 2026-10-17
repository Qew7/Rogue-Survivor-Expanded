using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay;

static class RadioDropForecastScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/radio-drop-forecast", () => TownScenarioFactory.Arena(4638,
            "....................", "....................", "....................", "...................."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = NpcIntentSupport.Player(world, 1, 1);
            player.FoodPoints = 0;
            player.Sanity -= 5;
            int sanity = player.Sanity;
            ItemRadio radio = new ItemRadio((ItemTrackerModel)world.Game.GameItems[GameItems.IDs.RADIO_MILITARY]);
            player.Inventory.AddAll(radio);
            int day = 4 * WorldTime.TURNS_PER_DAY + WorldTime.TURNS_PER_DAY / 4;
            world.Map.LocalTime.TurnCounter = day;
            Session.Get.WorldTime.TurnCounter = day;
            Session.Get.RadioDropDistrict = new Point(0, 0);
            Session.Get.RadioDropTurn = day + WorldTime.TURNS_PER_DAY;
            Check.Equal(false, (bool)Check.Call(world.Game, "CheckForEvent_ArmySupplies", world.Map),
                "announced flight cannot arrive early");
            Check.Equal(true, world.Try(new ActionUseItem(player, world.Game, radio)), "player hears military forecast");
            Check.Equal(true, player.Personality.HeardJournal.Last().Text.Contains("district"),
                "forecast names district but no exact drop tile");
            Check.Equal(sanity + 1, player.Sanity, "hungry listener gains hope once");
            int heard = player.Personality.HeardJournal.Count;
            world.Try(new ActionUseItem(player, world.Game, radio));
            world.Try(new ActionUseItem(player, world.Game, radio));
            Check.Equal(heard, player.Personality.HeardJournal.Count, "same forecast is not repeated");
            string path = Path.Combine(Path.GetTempPath(), "radio-drop-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session saved = BinarySaveStore.Load<Session>(path);
                Check.Equal(Session.Get.RadioDropTurn, saved.RadioDropTurn, "flight date survives saving");
                Check.Equal(Session.Get.RadioDropDistrict, saved.RadioDropDistrict, "announced district survives saving");
                Actor savedPlayer = NpcIntentSupport.Find(saved.World[0, 0].EntryMap, player.PersonalityIdentity);
                Check.Equal(Session.Get.RadioDropTurn, savedPlayer.Personality.LastRadioDropTurn,
                    "listener remembers the forecast after loading");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
            Session.Get.RadioDropDistrict = new Point(1, 0);
            world.Map.LocalTime.TurnCounter = Session.Get.RadioDropTurn;
            Session.Get.WorldTime.TurnCounter = Session.Get.RadioDropTurn;
            Check.Equal(false, (bool)Check.Call(world.Game, "CheckForEvent_ArmySupplies", world.Map),
                "flight belongs to its announced district");
            Session.Get.RadioDropDistrict = new Point(0, 0);
            Check.Equal(true, (bool)Check.Call(world.Game, "CheckForEvent_ArmySupplies", world.Map),
                "flight arrives in announced district on schedule");
            ((ScenarioUI)world.Game.UI).QueueWaitKey(System.Windows.Forms.Keys.Enter);
            Check.Call(world.Game, "FireEvent_ArmySupplies", world.Map);
            Check.Equal(0, Session.Get.RadioDropTurn, "completed flight clears the schedule");
            Check.Equal(true, Enumerable.Range(0, world.Map.Width).Any(x =>
                Enumerable.Range(0, world.Map.Height).Any(y => world.Map.GetItemsAt(x, y) != null)),
                "real supplies are dropped");

            Map remoteMap = new Map(4639, "remote drop", 20, 4);
            for (int y = 0; y < remoteMap.Height; y++)
                for (int x = 0; x < remoteMap.Width; x++)
                    remoteMap.SetTileModelAt(x, y, world.Game.GameTiles.FLOOR_ASPHALT);
            District remote = new District(new Point(1, 0), DistrictKind.RESIDENTIAL);
            remote.EntryMap = remoteMap;
            World city = new World(2);
            city[0, 0] = world.Map.District;
            city[1, 0] = remote;
            Session.Get.World = city;
            int[,,] raids = new int[(int)RaidType._COUNT, 2, 2];
            for (int type = 0; type < (int)RaidType._COUNT; type++)
                for (int x = 0; x < 2; x++)
                    for (int y = 0; y < 2; y++) raids[type, x, y] = -1;
            typeof(Session).GetField("m_Event_Raids", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(Session.Get, raids);
            remoteMap.LocalTime.TurnCounter = Session.Get.WorldTime.TurnCounter;
            Session.Get.RadioDropDistrict = remote.WorldPosition;
            Session.Get.RadioDropTurn = Session.Get.WorldTime.TurnCounter;
            Check.Equal(false, (bool)Check.Call(world.Game, "CheckForEvent_ArmySupplies", remoteMap),
                "simulated destination cannot fire the same scheduled flight");
            player.FoodPoints = 10000;
            Type flags = typeof(RogueGame).GetNestedType("SimFlags", BindingFlags.NonPublic);
            Check.Call(world.Game, "AdvancePlay", new[] { typeof(District), flags },
                world.Map.District, Enum.Parse(flags, "NOT_SIMULATING"));
            Check.Equal(0, Session.Get.RadioDropTurn, "current district dispatches the remote flight once");
            int remoteStacks = Enumerable.Range(0, remoteMap.Width).Sum(x =>
                Enumerable.Range(0, remoteMap.Height).Count(y => remoteMap.GetItemsAt(x, y) != null));
            Check.Equal(true, remoteStacks > 0, "remote district receives actual supplies");
            Check.Call(world.Game, "AdvancePlay", new[] { typeof(District), flags },
                world.Map.District, Enum.Parse(flags, "NOT_SIMULATING"));
            Check.Equal(remoteStacks, Enumerable.Range(0, remoteMap.Width).Sum(x =>
                Enumerable.Range(0, remoteMap.Height).Count(y => remoteMap.GetItemsAt(x, y) != null)),
                "later turns cannot repeat the same flight");
        });
    }
}
