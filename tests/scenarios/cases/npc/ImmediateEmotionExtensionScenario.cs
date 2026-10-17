using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class ImmediateEmotionExtensionScenario
{
    sealed class EmotionContent : INpcContentModule
    {
        public string Id { get { return "scenario.emotion"; } }
        public void Register(NpcCatalogBuilder catalog)
        {
            catalog.Event(new NpcEventDefinition("found_hope", NpcRecordCategory.Life,
                describe: e => e.Subject + " found hope.", required: NpcEventFields.Subject)
                { ImmediateSanity = (observer, source, active) => observer == source.Subject ? 3 : 0,
                  RadioSanity = (listener, fact, active) => listener.PersonalityIdentity == fact.SubjectId ? 2 : 0 });
            catalog.Event(new NpcEventDefinition("ordinary_news", NpcRecordCategory.Life,
                describe: e => e.Subject + " heard ordinary news.", required: NpcEventFields.Subject));
        }
    }

    public static void Register()
    {
        ScenarioRunner.Add("npc/immediate-emotion-extension", () => TownScenarioFactory.Arena(8371,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Game.NpcContent = PersonalityContent.Create(new EmotionContent()).Content;
            Actor subject = NpcIntentSupport.Actor(world, "hopeful", 1, 1);
            Actor witness = NpcIntentSupport.Actor(world, "witness", 2, 1);
            subject.Sanity = witness.Sanity = 10;
            var source = new SignificantEvent("found_hope", subject, null, world.Map,
                subject.Location.Position, world.Map.LocalTime.TurnCounter);
            PersonalitySystem.Report(world.Game, source);
            Check.Equal(13, subject.Sanity, "custom event gives its subject the registered reaction");
            Check.Equal(10, witness.Sanity, "registered reaction respects its own audience rule");
            PersonalitySystem.Report(world.Game, source);
            Check.Equal(13, subject.Sanity, "replayed event cannot apply emotion twice");
            PersonalitySystem.Report(world.Game, new SignificantEvent("ordinary_news", subject, null,
                world.Map, subject.Location.Position, world.Map.LocalTime.TurnCounter));
            Check.Equal(13, subject.Sanity, "event without a reaction leaves sanity unchanged");
            NpcFact hope = new NpcFact { Kind = "found_hope", SubjectId = subject.PersonalityIdentity,
                SubjectName = subject.UnmodifiedName };
            Check.Call(world.Game, "ApplyRadioSanity", subject, hope);
            Check.Equal(15, subject.Sanity, "custom radio reaction uses the event's registration");
            Check.Call(world.Game, "ApplyRadioSanity", witness, hope);
            Check.Equal(10, witness.Sanity, "custom radio reaction keeps its listener boundary");
            Check.Call(world.Game, "ApplyRadioSanity", subject, new NpcFact { Kind = "ordinary_news" });
            Check.Equal(15, subject.Sanity, "news without a radio reaction leaves sanity unchanged");

            subject.Personality.Opinion(witness.PersonalityIdentity, witness.UnmodifiedName).Attachment = 30;
            NpcFact theft = new NpcFact { Kind = "stolen_goods_found", OtherId = witness.PersonalityIdentity,
                OtherName = witness.UnmodifiedName };
            Check.Call(world.Game, "ApplyRadioSanity", subject, theft);
            Check.Equal(true, subject.Sanity < 15, "registered theft news still troubles a close contact");
            Actor timid = NpcIntentSupport.Actor(world, "timid listener", 3, 1, "timid");
            timid.Sanity = 10;
            Check.Call(world.Game, "ApplyRadioSanity", timid, new NpcFact { Kind = "psychopaths_arrival" });
            Check.Equal(9, timid.Sanity, "registered world news still troubles a timid listener");
        });
    }
}
