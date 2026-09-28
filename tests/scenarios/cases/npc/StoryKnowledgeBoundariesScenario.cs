using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;

static class StoryKnowledgeBoundariesScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/story-knowledge-boundaries", () => TownScenarioFactory.Arena(4628, ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 6, 2);
            Actor speaker = NpcIntentSupport.Actor(world, "speaker", 1, 1, "sociable");
            Actor listener = NpcIntentSupport.Actor(world, "listener", 2, 1, "suspicious", "timid");
            Actor target = NpcIntentSupport.Actor(world, "target", 0, 1);
            var fact = new NpcFact { EventId = 100, Kind = "death", SubjectId = target.PersonalityIdentity, SubjectName = target.UnmodifiedName,
                Place = target.Location, Confidence = 90, Source = NpcKnowledgeSource.Witness, SourceId = speaker.PersonalityIdentity };
            speaker.Personality.Knowledge.Learn(fact);
            listener.Personality.Knowledge.See(target, 0);
            Check.Equal(true, world.Try(new ActionNpcTell(speaker, world.Game, listener, fact)), "real conversation delivers uncertain news");
            Check.Equal(false, listener.Personality.Knowledge.Person(target.PersonalityIdentity).Dead, "hearsay cannot overwrite stronger contemporaneous direct sight");
            Check.Equal(NpcKnowledgeSource.Witness, listener.Personality.Knowledge.Person(target.PersonalityIdentity).Source, "contradiction preserves the stronger source");
            fact.Hops = 3;
            Actor another = NpcIntentSupport.Actor(world, "another", 3, 1);
            Check.Equal(false, new ActionNpcTell(speaker, world.Game, another, fact).IsLegal(), "propagation has a hop limit");
            fact.Hops = 0; fact.Confidence = 30;
            Check.Equal(false, new ActionNpcTell(speaker, world.Game, another, fact).IsLegal(), "low confidence cannot be retold");
            fact.Confidence = 90; another.IsSleeping = true;
            Check.Equal(false, new ActionNpcTell(speaker, world.Game, another, fact).IsLegal(), "sleeping listener cannot receive an unsolicited report");
            for (int i = 0; i < 80; i++) speaker.Personality.Knowledge.Learn(new NpcFact { EventId = i + 200, Kind = "attack", Place = target.Location, EventTurn = i });
            Check.Equal(48, speaker.Personality.Knowledge.Facts.Count, "facts do not grow without bound");
            speaker.Personality.Knowledge.Expire(2 * WorldTime.TURNS_PER_DAY + 81);
            Check.Equal(0, speaker.Personality.Knowledge.Facts.Count, "stale facts expire by observation time");
            var disabled = GamePreset.BuiltIn(GameMode.GM_STANDARD); disabled.NpcPersonalitiesEnabled = false; Session.Get.GamePreset = disabled;
            Check.Equal(false, new ActionNpcTell(speaker, world.Game, listener, fact).IsLegal(), "disabled preset rejects selected social action");
            NpcIntentSupport.Turn(world, another);
            Check.Equal(false, another.Personality.HasKnowledge, "disabled controller does not accumulate new knowledge");
        });
    }
}
