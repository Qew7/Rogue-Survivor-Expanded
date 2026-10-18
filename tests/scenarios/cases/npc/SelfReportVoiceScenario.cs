using System;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;

static class SelfReportVoiceScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/self-report-voice", () => TownScenarioFactory.Arena(8827,
            "....#....", "....#....", "....#...."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor kind = NpcIntentSupport.Actor(world, "Vasily", 1, 1, "kind");
            Actor cruel = NpcIntentSupport.Actor(world, "Boris", 2, 2, "cruel");
            Actor recipient = NpcIntentSupport.Actor(world, "recipient", 2, 1);
            Actor victim = NpcIntentSupport.Actor(world, "victim", 3, 2);
            Actor listener = NpcIntentSupport.Actor(world, "listener", 7, 1);
            Actor player = NpcIntentSupport.Player(world, 8, 1);
            kind.Personality.Opinion(recipient.PersonalityIdentity, recipient.UnmodifiedName);
            cruel.Personality.Opinion(victim.PersonalityIdentity, victim.UnmodifiedName);
            victim.Personality.Opinion(cruel.PersonalityIdentity, cruel.UnmodifiedName);

            PersonalitySystem.Report(world.Game, new SignificantEvent("shared_food", kind, recipient,
                world.Map, kind.Location.Position, 10));
            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", victim, cruel,
                world.Map, victim.Location.Position, 12));
            Actor namesake = NpcIntentSupport.Actor(world, "Vasily", 1, 2);
            kind.Personality.Opinion(namesake.PersonalityIdentity, namesake.UnmodifiedName);
            PersonalitySystem.Report(world.Game, new SignificantEvent("shared_food", namesake, recipient,
                world.Map, namesake.Location.Position, 13));
            NpcFact aid = kind.Personality.Knowledge.Facts.Find(f => f.Kind == "shared_food");
            NpcFact attack = cruel.Personality.Knowledge.Facts.Find(f => f.Kind == "attack");
            NpcFact victimAccount = victim.Personality.Knowledge.Facts.Find(f => f.Kind == "attack");
            world.Map.LocalTime.TurnCounter = 13;
            Check.Equal(aid.EventId, NpcConversation.Rumor(world.Game, kind, listener).EventId,
                "kind narrator favors their helpful action over a slightly newer witnessed attack");
            Check.Equal(attack.EventId, NpcConversation.Rumor(world.Game, cruel, listener).EventId,
                "cruel narrator favors their own attack over a slightly newer witnessed aid");
            Check.Equal(true, NpcConversation.ReportSentence(world.Game, kind, aid).Contains("I shared food with recipient"),
                "helper tells their own action in first person");
            Check.Equal(true, NpcConversation.ReportSentence(world.Game, cruel, attack).Contains("I attacked victim"),
                "attacker tells their own action in first person");
            Check.Equal(true, NpcConversation.ReportSentence(world.Game, victim, victimAccount).Contains("Boris attacked me"),
                "victim describes their role without repeating their own name");
            Check.Equal(true, NpcConversation.ReportSentence(world.Game, kind, aid, true).Contains("Vasily shared food"),
                "radio keeps attributed third-person wording");
            NpcFact namesakeReport = kind.Personality.Knowledge.Facts.Find(f => f.Kind == "shared_food" && f.SubjectId == namesake.PersonalityIdentity);
            Check.Equal(true, NpcConversation.ReportSentence(world.Game, kind, namesakeReport).Contains("Vasily shared food"),
                "same name does not turn another actor's action into a confession");
            Actor sameNamedVictim = NpcIntentSupport.Actor(world, "Boris", 0, 1);
            sameNamedVictim.Personality.Opinion(cruel.PersonalityIdentity, cruel.UnmodifiedName);
            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", sameNamedVictim, cruel,
                world.Map, sameNamedVictim.Location.Position, 14));
            NpcFact namesakeVictimAccount = sameNamedVictim.Personality.Knowledge.Facts.Find(f => f.Kind == "attack");
            NpcFact latestAttack = cruel.Personality.Knowledge.Facts.Find(f => f.Kind == "attack" && f.SubjectId == sameNamedVictim.PersonalityIdentity);
            Check.Equal(false, NpcConversation.ReportSentence(world.Game, sameNamedVictim, namesakeVictimAccount).Contains("I attacked"),
                "a victim sharing the attacker's name does not claim to be the attacker");
            PersonalitySystem.Report(world.Game, new SignificantEvent("traded", recipient, kind,
                world.Map, recipient.Location.Position, 15));
            NpcFact trade = kind.Personality.Knowledge.Facts.Find(f => f.Kind == "traded");
            Check.Equal(true, NpcConversation.ReportSentence(world.Game, kind, trade).Contains("I traded with recipient"),
                "a participant named second in a symmetric trade still speaks in first person");

            world.Map.LocalTime.TurnCounter = 15;
            world.Place(kind, 6, 1);
            world.Place(cruel, 6, 2);
            Check.Equal(true, world.Try(new ActionNpcTell(kind, world.Game, listener, aid)),
                "helper actually tells the story to the listener");
            NpcIntentSupport.Turn(world, cruel);
            Check.Equal(true, listener.Personality.Knowledge.Facts.Any(f => f.EventId == aid.EventId && f.Source == NpcKnowledgeSource.Told),
                "first-person aid report transmits the original fact");
            Check.Equal(true, listener.Personality.Knowledge.Facts.Any(f => f.EventId == latestAttack.EventId && f.Source == NpcKnowledgeSource.Told),
                "cruel attacker chooses to tell the first-person attack without a social trait");
            Check.Equal(true, player.Personality.HeardJournal.Any(e => e.Text.Contains("I shared food with recipient")),
                "player hears the first-person aid report");

        });
    }
}
