using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay;

static class RadioHourlyBarkScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/radio-hourly-bark", () => TownScenarioFactory.Arena(4986,
            "....................", "....................", "...................."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = NpcIntentSupport.Player(world, 1, 1);
            int turn = 25 * WorldTime.TURNS_PER_HOUR;
            world.Map.LocalTime.TurnCounter = Session.Get.WorldTime.TurnCounter = turn;
            Session.Get.RadioDropDistrict = new Point(0, 0);
            Session.Get.RadioDropTurn = turn + WorldTime.TURNS_PER_DAY;
            RadioReceiver receiver = new RadioReceiver(GameImages.OBJ_RADIO);
            receiver.TuneNext(); receiver.TuneNext();
            world.Map.PlaceMapObjectAt(receiver, new Point(2, 1));
            MessageManager messages = (MessageManager)typeof(RogueGame).GetField("m_MessageManager",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(world.Game);
            Type flags = typeof(RogueGame).GetNestedType("SimFlags", BindingFlags.NonPublic);
            object active = Enum.Parse(flags, "NOT_SIMULATING");

            Advance(world, flags, active);
            Check.Equal(1, CountStation(messages), "on map receiver speaks once at the hour");
            Check.Equal(true, Session.Get.RadioPrograms[1].Text.Contains("supply flight"),
                "hourly line retains the scheduled news");
            Check.Equal(1, player.Personality.HeardJournal.Count, "hourly program enters the journal once");
            for (int i = 1; i < WorldTime.TURNS_PER_HOUR; i++) Advance(world, flags, active);
            Check.Equal(1, CountStation(messages), "on receiver does not speak between hours");
            Advance(world, flags, active);
            Check.Equal(2, CountStation(messages), "same station speaks once at the next hour");
            Check.Equal(2, player.Personality.HeardJournal.Count, "next hour has one new program");

            receiver.TuneNext(); receiver.TuneNext(); receiver.TuneNext();
            Check.Equal(false, receiver.IsOn, "map receiver is off");
            for (int i = 0; i < WorldTime.TURNS_PER_HOUR - 1; i++) Advance(world, flags, active);
            Check.Equal(2, CountStation(messages), "off receiver stays quiet through the next hour");

            ItemRadio portable = new ItemRadio((ItemTrackerModel)world.Game.GameItems[GameItems.IDs.RADIO_MILITARY]);
            player.Inventory.AddAll(portable);
            portable.Batteries = 1;
            portable.IsOn = true;
            Advance(world, flags, active);
            Check.Equal(3, CountStation(messages), "on portable receiver speaks at the next hour");
            Check.Equal(false, portable.IsOn, "empty portable receiver switches off");
            for (int i = 1; i < WorldTime.TURNS_PER_HOUR; i++) Advance(world, flags, active);
            Advance(world, flags, active);
            Check.Equal(3, CountStation(messages), "empty portable receiver stays quiet");
        });
    }

    static void Advance(ScenarioWorld world, Type flags, object active)
    {
        Check.Call(world.Game, "NextMapTurn", new[] { typeof(Map), flags }, world.Map, active);
        Session.Get.WorldTime.TurnCounter = world.Map.LocalTime.TurnCounter;
    }

    static int CountStation(MessageManager messages)
    { return messages.History.Count(message => message.Text.Contains("Military Dispatch: ")); }
}
