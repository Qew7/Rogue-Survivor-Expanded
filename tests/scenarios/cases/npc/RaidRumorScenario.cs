using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;

static class RaidRumorScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/raid-rumor", () => TownScenarioFactory.Arena(4908,
            "...#.........", "...#.........", "...#........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor witness = NpcIntentSupport.Actor(world, "witness", 0, 1);
            Actor leader = NpcIntentSupport.Actor(world, "Vasily", 1, 1);
            leader.Faction = world.Game.GameFactions.TheBikers;
            Actor listener = NpcIntentSupport.Actor(world, "listener", 5, 1);
            Actor player = NpcIntentSupport.Player(world, 6, 1);
            Check.Call(world.Game, "NotifyOrderablesAI",
                new[] { typeof(Map), typeof(RaidType), typeof(Point), typeof(Actor) },
                world.Map, RaidType.BIKERS, leader.Location.Position, leader);
            NpcFact namedRaid = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "bikers_raid");
            Check.Equal(true, namedRaid != null, "real raid handler retains its specific event");
            Check.Equal("a biker", namedRaid.ReportSubject, "unfamiliar raid leader is described by faction");
            Check.Equal(true, NpcRecordDescriptions.Report(world.Game.NpcContent, namedRaid)
                .Contains("there was a biker raid involving a biker"),
                "specific raid report uses natural wording and the original actor description");
            Check.Equal(false, listener.Personality.Knowledge.Facts.Exists(f => f.EventId == namedRaid.EventId),
                "wall blocks the original raid observation");

            // The catalog's generic fallback also remains reportable when no leader is known.
            PersonalitySystem.Report(world.Game, new SignificantEvent("raid", null, null,
                world.Map, new Point(1, 1), world.Map.LocalTime.TurnCounter));
            NpcFact generic = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "raid");
            Check.Equal(true, generic != null && generic.SubjectId == Guid.Empty,
                "generic raid can be retained without inventing a leader");
            Check.Equal("there was a raid", NpcRecordDescriptions.Report(world.Game.NpcContent, generic),
                "generic raid has clear report wording");

            world.Place(witness, 4, 0); world.Place(listener, 5, 0); world.Place(player, 6, 0);
            var tell = new ActionNpcTell(witness, world.Game, listener, generic);
            Check.Equal(true, tell.IsLegal(), "subjectless raid can be reported"); tell.Perform();
            Check.Equal(NpcKnowledgeSource.Told, listener.Personality.Knowledge.Facts.Find(f => f.EventId == generic.EventId).Source,
                "listener learns the generic raid as hearsay");
            Check.Equal(true, player.Personality.HeardJournal[0].Text.Contains("there was a raid"),
                "player hears the generic raid report");
        });
    }
}
