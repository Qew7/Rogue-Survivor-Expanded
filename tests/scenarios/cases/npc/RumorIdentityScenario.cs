using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;

static class RumorIdentityScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/rumor-identity", () => TownScenarioFactory.Arena(4901,
            "...#.........", "...#.........", "...#........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor witness = NpcIntentSupport.Actor(world, "witness", 0, 1);
            Actor victim = NpcIntentSupport.Actor(world, "Peter Steel", 1, 1);
            Actor attacker = NpcIntentSupport.Actor(world, "Vasily", 2, 1);
            attacker.Faction = world.Game.GameFactions.TheBikers;
            Actor listener = NpcIntentSupport.Actor(world, "listener", 6, 1);
            Actor next = NpcIntentSupport.Actor(world, "next", 8, 1);
            Actor player = NpcIntentSupport.Player(world, 7, 1);
            witness.Personality.Opinion(victim.PersonalityIdentity, victim.UnmodifiedName);

            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", victim, attacker,
                world.Map, victim.Location.Position, 0));
            NpcFact fact = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "attack");
            Check.Equal("Peter Steel", fact.ReportSubject, "known victim is named");
            Check.Equal("a biker", fact.ReportOther, "unknown attacker is described by faction");
            Check.Equal(attacker.PersonalityIdentity, fact.OtherId, "the real attacker identity is retained for NPC decisions");
            Check.Equal(false, listener.Personality.HasKnowledge, "wall prevents eyewitness knowledge");

            witness.Personality.Opinion(attacker.PersonalityIdentity, attacker.UnmodifiedName).Name = attacker.UnmodifiedName;
            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", victim, attacker,
                world.Map, victim.Location.Position, 0));
            NpcFact named = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "attack" && f.EventId != fact.EventId);
            Check.Equal("Vasily", named.ReportOther, "known attacker is named in a later witnessed event");
            Check.Equal("a biker", fact.ReportOther, "an earlier report does not gain a name retroactively");

            world.Place(witness, 5, 0); world.Place(listener, 6, 0);
            world.Place(next, 8, 0); world.Place(player, 7, 0);
            var tell = new ActionNpcTell(witness, world.Game, listener, fact);
            Check.Equal(true, tell.IsLegal(), "witness can speak to listener");
            tell.Perform();
            NpcFact heard = listener.Personality.Knowledge.Facts.Find(f => f.EventId == fact.EventId);
            Check.Equal("a biker", heard.ReportOther, "listener keeps the witness's description");
            Check.Equal(NpcKnowledgeSource.Told, heard.Source, "listener has hearsay");
            Check.Equal(true, Heard(player, "a biker attacked Peter Steel"),
                "player hears explicit attacker and victim wording");

            listener.Personality.Opinion(attacker.PersonalityIdentity, attacker.UnmodifiedName);
            var retell = new ActionNpcTell(listener, world.Game, next, heard);
            Check.Equal(true, retell.IsLegal(), "listener can retell the report");
            retell.Perform();
            NpcFact twice = next.Personality.Knowledge.Facts.Find(f => f.EventId == fact.EventId);
            Check.Equal("a biker", twice.ReportOther, "later acquaintance does not change the quoted identity");
            Check.Equal(true, Heard(player, "I was told that a biker attacked Peter Steel"),
                "the player hears the same description on retelling");
            Check.Equal(false, NpcIntentSupport.HasEvent(next, "attack"), "retelling does not invent a witnessed attack");
        });
    }

    static bool Heard(Actor actor, string phrase)
    {
        foreach (HeardJournalEntry entry in actor.Personality.HeardJournal)
            if (entry.Text.Contains(phrase)) return true;
        return false;
    }
}
