using System;
using System.Reflection;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI.Sensors;

static class RestOptimizationBenchmarks
{
    public static void SleepYield()
    {
        ScenarioWorld world = TownScenarioFactory.Arena(7982,
            ".....", ".....", ".....");
        Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
        Actor player = NpcIntentSupport.Player(world, 2, 1);
        player.SleepPoints = 0;
        world.Game.DoStartSleeping(player);
        if (!player.IsSleeping) throw new InvalidOperationException("player did not sleep");
        FieldInfo worker = typeof(RogueGame).GetField("m_SimWorker",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (worker.GetValue(world.Game) != null)
            throw new InvalidOperationException("benchmark unexpectedly started a worker");
        Type flags = typeof(RogueGame).GetNestedType("SimFlags", BindingFlags.NonPublic);
        MethodInfo next = typeof(RogueGame).GetMethod("NextMapTurn",
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            new[] { typeof(Map), flags }, null);
        object active = Enum.Parse(flags, "NOT_SIMULATING");
        PerformanceBenchmarks.Measure("ten sleeping map turns, no worker", 1, () =>
        {
            for (int i = 0; i < 10; i++)
            {
                player.SleepPoints = 0;
                next.Invoke(world.Game, new[] { (object)world.Map, active });
                if (!player.IsSleeping) throw new InvalidOperationException("player woke during benchmark");
            }
        });
        DistrictSimulationWorker activeWorker = new DistrictSimulationWorker(() => { });
        activeWorker.Start();
        worker.SetValue(world.Game, activeWorker);
        try
        {
            PerformanceBenchmarks.Measure("ten sleeping map turns, worker present", 1, () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    player.SleepPoints = 0;
                    next.Invoke(world.Game, new[] { (object)world.Map, active });
                }
            });
        }
        finally
        {
            activeWorker.Stop();
            worker.SetValue(world.Game, null);
        }
    }

    public static void Run()
    {
        string[] rows = new string[40];
        for (int i = 0; i < rows.Length; i++) rows[i] = new string('.', 40);
        ScenarioWorld world = TownScenarioFactory.Arena(7980, rows);
        world.Map.Lighting = Lighting.LIT;
        Actor observer = SkillScenario.Actor(world);
        world.Place(observer, 20, 20);
        for (int i = 0; i < 30; i++)
            world.Place(SkillScenario.Actor(world), 2 + i % 10, 2 + i / 10);

        PerformanceBenchmarks.Measure("actor lookup occupied", 100000,
            () => world.Map.GetActorAt(20, 20));
        PerformanceBenchmarks.Measure("actor lookup empty", 100000,
            () => world.Map.GetActorAt(39, 39));
        PerformanceBenchmarks.Measure("actor move, 31 actors", 10000, () =>
        {
            world.Map.PlaceActorAt(observer, new Point(21, 20));
            world.Map.PlaceActorAt(observer, new Point(20, 20));
        });

        LOSSensor sensor = new LOSSensor(LOSSensor.SensingFilter.ITEMS |
            LOSSensor.SensingFilter.CORPSES);
        PerformanceBenchmarks.Measure("sense empty items and corpses", 1000,
            () => sensor.Sense(world.Game, observer));

        world.Map.DropItemAt(new ItemFood(world.Game.GameItems.GROCERIES), new Point(22, 20));
        Actor dead1 = SkillScenario.Actor(world);
        Actor dead2 = SkillScenario.Actor(world);
        world.Map.AddCorpseAt(new Corpse(dead1, 5, 5, 0, 0, 1), new Point(23, 20));
        world.Map.AddCorpseAt(new Corpse(dead2, 5, 5, 0, 0, 1), new Point(23, 20));
        PerformanceBenchmarks.Measure("sense sparse items and stacked corpses", 1000,
            () => sensor.Sense(world.Game, observer));

        world.Map.DropItemAt(new ItemFood(world.Game.GameItems.GROCERIES), new Point(24, 20));
        world.Map.AddCorpseAt(new Corpse(SkillScenario.Actor(world), 5, 5, 0, 0, 1), new Point(24, 21));
        PerformanceBenchmarks.Measure("sense two items and three corpses", 1000,
            () => sensor.Sense(world.Game, observer));

        for (int i = 0; i < 64; i++)
        {
            Point position = new Point(12 + i % 8, 12 + i / 8);
            world.Map.DropItemAt(new ItemFood(world.Game.GameItems.GROCERIES), position);
            world.Map.AddCorpseAt(new Corpse(SkillScenario.Actor(world), 5, 5, 0, 0, 1), position);
        }
        PerformanceBenchmarks.Measure("sense dense items and corpses", 1000,
            () => sensor.Sense(world.Game, observer));

        Map cachedMap = observer.Location.Map;
        Point cachedPosition = observer.Location.Position;
        int cachedRange = world.Game.Rules.ActorFOV(observer, cachedMap.LocalTime,
            world.Game.Session.World.Weather);
        int cachedTurn = cachedMap.LocalTime.TurnCounter;
        PerformanceBenchmarks.Measure("FOV cache key validation", 100000, () =>
        {
            Map currentMap = observer.Location.Map;
            bool hit = currentMap == cachedMap && observer.Location.Position == cachedPosition &&
                currentMap.LocalTime.TurnCounter == cachedTurn &&
                world.Game.Rules.ActorFOV(observer, currentMap.LocalTime,
                    world.Game.Session.World.Weather) == cachedRange;
            if (!hit) throw new InvalidOperationException("stable FOV key missed");
        });
    }
}
