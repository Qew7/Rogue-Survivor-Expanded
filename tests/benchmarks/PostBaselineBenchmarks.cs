using System;
using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.Personality;

// Keep this fixture compatible with commit 0d243e6 so both revisions run identical code.
static class PostBaselineBenchmarks
{
    static volatile int s_Sink;

    public static void Run()
    {
        Knowledge();
        PlanningAndWandering();
        BaseTraps();
        Records();
        Console.WriteLine("BENCH sink: {0}", s_Sink);
    }

    static void Knowledge()
    {
        var knowledge = new NpcKnowledge();
        for (int i = 1; i <= 48; i++)
            knowledge.Facts.Add(new NpcFact { EventId = i, Kind = "raid", Confidence = 90 });
        var duplicate = new NpcFact { EventId = 48, Kind = "raid", Confidence = 40 };
        PerformanceBenchmarks.Measure("knowledge duplicate fact, 48 stored", 100000,
            () => s_Sink = knowledge.Learn(duplicate) ? 1 : 0);

        Guid first = Guid.Empty, last = Guid.Empty;
        for (int i = 0; i < 32; i++)
        {
            last = Guid.NewGuid();
            if (i == 0) first = last;
            knowledge.People.Add(new NpcKnownPerson { Id = last });
        }
        PerformanceBenchmarks.Measure("knowledge first person, 32 stored", 100000,
            () => s_Sink = knowledge.Person(first) == null ? 0 : 1);
        PerformanceBenchmarks.Measure("knowledge last person, 32 stored", 100000,
            () => s_Sink = knowledge.Person(last) == null ? 0 : 1);
    }

    static void PlanningAndWandering()
    {
        string[] rows = new string[20];
        for (int i = 0; i < rows.Length; i++) rows[i] = new string('.', 20);
        ScenarioWorld world = TownScenarioFactory.Arena(7351, rows);
        Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
        Actor target = NpcIntentSupport.Player(world, 12, 10);
        Actor owner = NpcIntentSupport.Actor(world, "planner", 10, 10, "loyal");
        var known = new NpcKnownPerson { Id = target.PersonalityIdentity,
            Name = target.UnmodifiedName, Place = target.Location };
        NpcIntent goal = NpcStorySystem.StartKnown(world.Game.NpcContent, owner, known,
            world.Game.NpcContent.Capability("seek_companion"));
        if (goal == null) throw new InvalidOperationException("Planner benchmark goal was not created");
        PerformanceBenchmarks.Measure("planner domain build", 1000,
            () => s_Sink = new NpcPlanDomain(world.Game, owner, goal, new List<Actor> { target }).Actions.Count);
        var domain = new NpcPlanDomain(world.Game, owner, goal, new List<Actor> { target });
        Location place = owner.Location;
        if (domain.At(place) == 0) throw new InvalidOperationException("Planner benchmark place was not stored");
        PerformanceBenchmarks.Measure("planner repeated place lookup", 100000,
            () => s_Sink = (int)domain.At(place));

        var ai = new PostBaselineProbeAI();
        owner.Controller = ai;
        if (ai.Wander(world.Game) == null) throw new InvalidOperationException("Wander benchmark has no move");
        PerformanceBenchmarks.Measure("AI wander choice, open 20x20", 10000,
            () => s_Sink = ai.Wander(world.Game) == null ? 0 : 1);
        Point destination = new Point(15, 10);
        if (ai.MoveToward(world.Game, destination) == null)
            throw new InvalidOperationException("Move-toward benchmark has no move");
        PerformanceBenchmarks.Measure("AI move toward, no traps", 10000,
            () => s_Sink = ai.MoveToward(world.Game, destination) == null ? 0 : 1);
    }

    static void BaseTraps()
    {
        string[] rows = new string[20];
        for (int i = 0; i < rows.Length; i++) rows[i] = new string('.', 20);
        ScenarioWorld world = TownScenarioFactory.Arena(7352, rows);
        Session.Get.GameMode = GameMode.GM_XPD;
        Actor guard = NpcIntentSupport.Actor(world, "guard", 5, 5);
        var ai = new PostBaselineProbeAI();
        guard.Controller = ai;
        var cells = new List<Point>();
        for (int y = 5; y < 15; y++)
            for (int x = 5; x < 15; x++) cells.Add(new Point(x, y));
        world.Map.AddXpdBase(new XpdBase(guard, cells));
        guard.Inventory.AddAll(new ItemTrap(world.Game.GameItems.BARBED_WIRE));
        if (ai.DefendBase(world.Game) == null) throw new InvalidOperationException("Base trap benchmark has no action");
        PerformanceBenchmarks.Measure("base trap scan, 100 cells", 1000,
            () => s_Sink = ai.DefendBase(world.Game) == null ? 0 : 1);
    }

    static void Records()
    {
        ScenarioWorld world = TownScenarioFactory.Arena(7353, "...");
        var records = new ResidentRecords();
        for (int person = 0; person < 16; person++)
        {
            var actor = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "resident " + person, false, false, 0);
            ResidentRecord record = records.Register(actor);
            for (int entry = 0; entry < 32; entry++)
                record.Add("note:" + entry, entry + 1, "Resident visited the shelter and shared supplies.");
        }
        var save = new RecordsSave("benchmark", 100, records);
        if (RecordsReader.Lines(save, null).Count < 512)
            throw new InvalidOperationException("Records benchmark did not render the fixture");
        PerformanceBenchmarks.Measure("records timeline, 16x32 entries", 50,
            () => s_Sink = RecordsReader.Lines(save, null).Count);
        PerformanceBenchmarks.Measure("records absent search, 16x32 entries", 200,
            () => s_Sink = RecordsReader.Lines(save, null, null, "absent-query", RecordsEventFilter.All).Count);
    }
}

sealed class PostBaselineProbeAI : CivilianAI
{
    public ActorAction Wander(RogueGame game) { return BehaviorWander(game, null); }
    public ActorAction MoveToward(RogueGame game, Point goal)
    { return BehaviorIntelligentBumpToward(game, goal, false, false); }
    public ActorAction DefendBase(RogueGame game) { return DefendXpdBaseWithTrap(game); }
}
