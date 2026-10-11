using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class RestDeferredDebtScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/rest-leaves-distant-debt", () => TownScenarioFactory.Arena(4998,
            ".....", ".....", "....."), world =>
        {
            GamePreset preset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.GamePreset = preset;
            Actor player = NpcIntentSupport.Player(world, 2, 1);
            World city = new World(2);
            city[0, 0] = world.Map.District;
            District remote = new District(new Point(1, 1), DistrictKind.RESIDENTIAL);
            Map distant = new Map(4999, "distant", 5, 3);
            for (int y = 0; y < 3; y++) for (int x = 0; x < 5; x++)
                distant.SetTileModelAt(x, y, world.Game.GameTiles.FLOOR_ASPHALT);
            remote.EntryMap = distant;
            city[1, 1] = remote;
            Session.Get.World = city;
            world.Map.LocalTime.TurnCounter = Session.Get.WorldTime.TurnCounter = 10;

            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            Type flags = typeof(RogueGame).GetNestedType("SimFlags", BindingFlags.NonPublic);
            object active = Enum.Parse(flags, "NOT_SIMULATING");
            typeof(RogueGame).GetField("m_IsPlayerLongWait", fields).SetValue(world.Game, true);
            typeof(RogueGame).GetField("m_PlayerLongWaitEnd", fields).SetValue(world.Game, new WorldTime(15));
            Advance(world, player, flags, active);
            Check.Equal(true, distant.LocalTime.TurnCounter > 0 && distant.LocalTime.TurnCounter < 11,
                "distant turns progress during the wait without finishing the old debt");

            typeof(RogueGame).GetField("m_IsPlayerLongWaitForcedStop", fields).SetValue(world.Game, true);
            Advance(world, player, flags, active);
            int interruptedClock = distant.LocalTime.TurnCounter;
            Check.Equal(true, interruptedClock < Session.Get.WorldTime.TurnCounter,
                "interrupted wait leaves distant turns pending");
            typeof(RogueGame).GetField("m_IsPlayerLongWait", fields).SetValue(world.Game, false);
            Check.Call(world.Game, "FinishRestSimulationIfNeeded");
            Check.Equal(interruptedClock, distant.LocalTime.TurnCounter,
                "returning control does not simulate the remaining wait debt");

            player.SleepPoints = 0;
            player.ActionPoints = Rules.BASE_ACTION_COST;
            world.Game.DoStartSleeping(player);
            Check.Call(world.Game, "AdvancePlay", new[] { typeof(District), flags }, world.Map.District, active);
            int sleepingClock = distant.LocalTime.TurnCounter;
            Check.Equal(true, sleepingClock > interruptedClock && sleepingClock < Session.Get.WorldTime.TurnCounter,
                "sleep continues the distant simulation without a full catch-up");
            world.Game.DoWakeUp(player);
            Check.Call(world.Game, "FinishRestSimulationIfNeeded");
            Check.Equal(sleepingClock, distant.LocalTime.TurnCounter,
                "waking leaves the unfinished district turns for later");

            typeof(RogueGame).GetField("m_IsPlayerLongWaitForcedStop", fields).SetValue(world.Game, false);
            typeof(RogueGame).GetField("m_IsPlayerLongWait", fields).SetValue(world.Game, true);
            typeof(RogueGame).GetField("m_PlayerLongWaitEnd", fields).SetValue(world.Game,
                new WorldTime(Session.Get.WorldTime.TurnCounter + 1));
            Advance(world, player, flags, active);
            typeof(RogueGame).GetField("m_IsPlayerLongWait", fields).SetValue(world.Game, false);
            int naturalClock = distant.LocalTime.TurnCounter;
            Check.Call(world.Game, "FinishRestSimulationIfNeeded");
            Check.Equal(true, naturalClock > sleepingClock && naturalClock < Session.Get.WorldTime.TurnCounter,
                "a naturally ended wait also leaves the distant debt");
            Check.Equal(naturalClock, distant.LocalTime.TurnCounter,
                "natural wait completion does not force catch-up");

            string path = Path.Combine(Path.GetTempPath(), "rest-debt-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.Load<Session>(path);
                Check.Equal(false, loaded.GamePreset.FinishDistantSimulationAfterRest,
                    "new default persists in the game save");
                Check.Equal(naturalClock, loaded.World[1, 1].EntryMap.LocalTime.TurnCounter,
                    "unfinished district clock survives save and load");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
            typeof(RogueGame).GetField("m_IsPlayerLongWait", fields).SetValue(world.Game, true);
            typeof(RogueGame).GetField("m_PlayerLongWaitEnd", fields).SetValue(world.Game,
                new WorldTime(Session.Get.WorldTime.TurnCounter + 2));
            Advance(world, player, flags, active);
            Check.Equal(true, distant.LocalTime.TurnCounter > naturalClock,
                "the next wait resumes unfinished distant turns");
        });
    }

    static void Advance(ScenarioWorld world, Actor player, Type flags, object active)
    {
        player.ActionPoints = Rules.BASE_ACTION_COST;
        world.Game.DoWait(player);
        Check.Call(world.Game, "AdvancePlay", new[] { typeof(District), flags }, world.Map.District, active);
    }
}
