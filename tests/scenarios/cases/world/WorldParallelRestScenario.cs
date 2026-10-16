using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class WorldParallelRestScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/parallel-rest-crossings", () => TownScenarioFactory.Arena(7813,
            ".....", ".....", "....."), world =>
        {
            GamePreset preset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            preset.ParallelDistantSimulationDuringRest = true;
            Session.Get.GamePreset = preset;
            Actor player = NpcIntentSupport.Player(world, 2, 1);
            World city = new World(3);
            city[0, 0] = world.Map.District;
            for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++)
            {
                if (x == 0 && y == 0) continue;
                var district = new District(new Point(x, y), DistrictKind.RESIDENTIAL);
                var map = new Map(7820 + x * 3 + y, "parallel district", 5, 3);
                var sewers = new Map(7920 + x * 3 + y, "sewers", 5, 3);
                for (int row = 0; row < 3; row++) for (int col = 0; col < 5; col++)
                {
                    map.SetTileModelAt(col, row, world.Game.GameTiles.FLOOR_ASPHALT);
                    sewers.SetTileModelAt(col, row, world.Game.GameTiles.FLOOR_ASPHALT);
                }
                district.EntryMap = map;
                district.SewersMap = sewers;
                city[x, y] = district;
            }
            Session.Get.World = city;
            Map left = city[0, 2].EntryMap, right = city[2, 2].EntryMap;
            Map destination = city[1, 2].EntryMap;
            Point arrival = new Point(2, 1);
            left.SetExitAt(new Point(5, 1), new Exit(destination, arrival) { IsAnAIExit = true });
            right.SetExitAt(new Point(-1, 1), new Exit(destination, arrival) { IsAnAIExit = true });
            MethodInfo wavePlanner = typeof(RogueGame).GetMethod("RestSimulationWaves",
                BindingFlags.Static | BindingFlags.NonPublic);
            var waves = (List<List<int>>)wavePlanner.Invoke(null, new object[] {
                new List<District> { left.District, right.District }, Session.Get.NpcDirector, world.Map.District });
            Check.Equal(2, waves.Count, "sources sharing an arrival district execute in ordered waves");
            Check.Equal(0, waves[0][0], "the first source resolves its crossing first");
            Check.Equal(1, waves[1][0], "the second source sees the first arrival");
            Actor first = NpcIntentSupport.Actor(world, "first traveler", 3, 1);
            Actor second = NpcIntentSupport.Actor(world, "second traveler", 4, 1);
            world.Map.RemoveActor(first); left.PlaceActorAt(first, new Point(4, 1));
            world.Map.RemoveActor(second); right.PlaceActorAt(second, new Point(0, 1));
            first.ActionPoints = second.ActionPoints = Rules.BASE_ACTION_COST;
            FieldInfo worker = typeof(RogueGame).GetField("s_ParallelRestWorker",
                BindingFlags.Static | BindingFlags.NonPublic);
            Action<Actor, Point> leave = (actor, exit) =>
            {
                worker.SetValue(null, true);
                try { world.Game.DoLeaveMap(actor, exit, false); }
                finally { worker.SetValue(null, false); }
            };
            Parallel.Invoke(() => leave(first, new Point(5, 1)),
                () => leave(second, new Point(-1, 1)));
            Check.Equal(1, destination.Actors.Count(), "only one simultaneous traveler can occupy the arrival cell");
            Check.Equal(true, first.Location.Map == destination || second.Location.Map == destination,
                "one traveler arrives in the neighboring district");
            Check.Equal(true, left.HasActor(first) || right.HasActor(second),
                "the blocked traveler remains in the source district");
            Check.Equal(0, first.ActionPoints, "first crossing consumes its action on the source turn");
            Check.Equal(0, second.ActionPoints, "blocked crossing also consumes its action");
            Actor blocked = first.Location.Map == destination ? second : first;
            Map blockedMap = blocked.Location.Map;
            Point playerExit = blockedMap == left ? new Point(5, 2) : new Point(-1, 2);
            blockedMap.SetExitAt(playerExit, new Exit(world.Map, new Point(0, 2)) { IsAnAIExit = true });
            blocked.ActionPoints = Rules.BASE_ACTION_COST;
            lock (world.Map.District)
                Check.Equal(true, Task.Run(() => leave(blocked, playerExit)).Wait(TimeSpan.FromSeconds(2)),
                    "arrival in the locked player district must not deadlock the resting turn");
            Check.Equal(world.Map, blocked.Location.Map, "traveler arrives in the player district");
            first.Controller = second.Controller = null;
            world.Game.ParallelRestWorkers = 2;
            Type flags = typeof(RogueGame).GetNestedType("SimFlags", BindingFlags.NonPublic);
            object active = Enum.Parse(flags, "NOT_SIMULATING");
            player.ActionPoints = Rules.BASE_ACTION_COST;
            world.Game.DoWait(player);
            Check.Call(world.Game, "AdvancePlay", new[] { typeof(District), flags }, city[0, 0], active);
            Check.Call(world.Game, "StartSimThread");
            Check.Equal(true, SpinWait.SpinUntil(() => city[2, 2].EntryMap.LocalTime.TurnCounter == 1, 5000),
                "background districts reach the live turn");
            Check.Call(world.Game, "StopSimThread", false);
            for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++)
                Check.Equal(1, city[x, y].EntryMap.LocalTime.TurnCounter,
                    "each district advances once during parallel rest");
            int beforeHour = WorldTime.TURNS_PER_HOUR - 1;
            Session.Get.WorldTime.TurnCounter = beforeHour;
            for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++)
                city[x, y].EntryMap.LocalTime.TurnCounter = beforeHour;
            player.ActionPoints = Rules.BASE_ACTION_COST;
            world.Game.DoWait(player);
            Check.Call(world.Game, "AdvancePlay", new[] { typeof(District), flags }, city[0, 0], active);
            Check.Call(world.Game, "StartSimThread");
            Check.Equal(true, SpinWait.SpinUntil(() => city[2, 2].EntryMap.LocalTime.TurnCounter ==
                Session.Get.WorldTime.TurnCounter, 5000), "background hour completes");
            Check.Call(world.Game, "StopSimThread", false);
            for (int station = 0; station < 4; station++)
            {
                long key = (1L << 2) | (uint)station;
                Check.Equal(true, Session.Get.RadioProgramHistory.ContainsKey(key),
                    "hourly radio program is prepared before parallel district turns");
                Check.Equal(true, Object.ReferenceEquals(Session.Get.RadioProgramHistory[key],
                    Check.Call(world.Game, "GetRadioProgram", station, 1)),
                    "every receiver obtains the same station program for the hour");
            }
            string path = Path.Combine(Path.GetTempPath(), "parallel-rest-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.Load<Session>(path);
                Check.Equal(true, loaded.GamePreset.ParallelDistantSimulationDuringRest,
                    "parallel rest setting survives save and load");
                Check.Equal(city[1, 2].SimulationDiceRoller(Session.Get.Seed).Roll(0, 1000000),
                    loaded.World[1, 2].SimulationDiceRoller(loaded.Seed).Roll(0, 1000000),
                    "district random stream continues after load");
                Check.Equal(Session.Get.IdentityDiceRoller.Roll(0, 1000000),
                    loaded.IdentityDiceRoller.Roll(0, 1000000),
                    "identity random stream continues after load");
                Check.Equal(Session.Get.NextPersonalityEventId(), loaded.NextPersonalityEventId(),
                    "event ID allocation continues without reuse after load");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
