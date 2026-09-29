using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;

static class ReportProseScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/report-prose", () => TownScenarioFactory.Arena(4823,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 6, 2);
            Actor speaker = NpcIntentSupport.Actor(world, "speaker", 1, 1);
            Actor listener = NpcIntentSupport.Actor(world, "listener", 2, 1);
            Actor helper = NpcIntentSupport.Actor(world, "helper", 3, 1);
            var offer = new NpcFact { Kind = "food_offered", EventId = 500, EventTurn = 0, LearnedTurn = 0,
                Confidence = 90, Source = NpcKnowledgeSource.Witness, SourceId = speaker.PersonalityIdentity,
                SubjectId = helper.PersonalityIdentity, SubjectName = helper.UnmodifiedName,
                OtherId = listener.PersonalityIdentity, OtherName = listener.UnmodifiedName, Place = helper.Location };
            speaker.Personality.Knowledge.Learn(offer);
            string neutral = NpcRecordDescriptions.Report(world.Game.NpcContent, offer);
            Check.Equal(true, neutral.Contains("food offered") && neutral.Contains("helper") && !neutral.Contains("violence"),
                "nonviolent report without custom prose stays neutral");
            Check.Equal(true, world.Try(new ActionNpcTell(speaker, world.Game, listener, offer)), "neutral fact is actually told and learned");
            Check.Equal(true, listener.Personality.Knowledge.Facts.Exists(f => f.EventId == offer.EventId), "listener retains the real report");
            var attack = new NpcFact { Kind = "attack", OtherId = helper.PersonalityIdentity, OtherName = helper.UnmodifiedName,
                SubjectId = listener.PersonalityIdentity, SubjectName = listener.UnmodifiedName };
            Check.Equal(true, NpcRecordDescriptions.Report(world.Game.NpcContent, attack).Contains("violence against"),
                "actual attack keeps the violence wording");
        });
    }
}
