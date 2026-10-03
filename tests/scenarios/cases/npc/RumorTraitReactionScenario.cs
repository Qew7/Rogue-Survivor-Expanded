using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class RumorTraitReactionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/rumor-trait-reaction", () => TownScenarioFactory.Arena(4910,
            "...#.........", "...#.........", "...#........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor witness = NpcIntentSupport.Actor(world, "witness", 0, 1);
            Actor victim = NpcIntentSupport.Actor(world, "victim", 1, 1);
            Actor attacker = NpcIntentSupport.Actor(world, "attacker", 2, 1);
            attacker.Faction = world.Game.GameFactions.TheBikers;
            Actor lawful = NpcIntentSupport.Actor(world, "lawful", 6, 0, "lawful", "kind");
            Actor rebel = NpcIntentSupport.Actor(world, "rebel", 7, 0, "rebellious", "cruel");
            Actor timid = NpcIntentSupport.Actor(world, "timid", 8, 0, "timid");
            Actor brave = NpcIntentSupport.Actor(world, "brave", 9, 0, "brave");
            Actor player = NpcIntentSupport.Player(world, 10, 0);
            player.Personality.AddTrait(new TraitInstance("lawful"));
            witness.Personality.Opinion(attacker.PersonalityIdentity, attacker.UnmodifiedName);

            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", victim, attacker,
                world.Map, victim.Location.Position, 0));
            NpcFact attack = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "attack");
            Check.Equal("attacker", attack.ReportOther, "acquainted witness names the attacker");
            foreach (Actor listener in new[] { lawful, rebel, timid, brave, player })
                Check.Equal(true, NpcKnowledgeSystem.Hear(world.Game, listener, witness, attack), "listener hears a new attack report");
            Check.Equal(true, lawful.Personality.Person(attacker.PersonalityIdentity).Feeling <
                rebel.Personality.Person(attacker.PersonalityIdentity).Feeling, "lawful and kind listener condemns attack more strongly");
            Check.Equal(true, timid.Personality.Person(attacker.PersonalityIdentity).Fear >
                brave.Personality.Person(attacker.PersonalityIdentity).Fear, "timid listener fears attacker more");
            Check.Equal(true, player.Personality.Person(attacker.PersonalityIdentity).Feeling < 0,
                "player also reacts to a named attack report");
            int feeling = lawful.Personality.Person(attacker.PersonalityIdentity).Feeling;
            Check.Equal(false, NpcKnowledgeSystem.Hear(world.Game, lawful, witness, attack), "duplicate report is ignored");
            Check.Equal(feeling, lawful.Personality.Person(attacker.PersonalityIdentity).Feeling,
                "duplicate report does not change relationships again");

            Actor unknownWitness = NpcIntentSupport.Actor(world, "unknown witness", 0, 0);
            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", victim, attacker,
                world.Map, victim.Location.Position, 0));
            NpcFact anonymous = unknownWitness.Personality.Knowledge.Facts.Find(f => f.Kind == "attack");
            Check.Equal("a civilian", anonymous.ReportSubject, "unfamiliar victim is described by faction too");
            Check.Equal("a biker", anonymous.ReportOther, "unfamiliar attacker is reported by faction");
            Actor factionListener = NpcIntentSupport.Actor(world, "faction listener", 11, 0, "lawful");
            Check.Equal(true, NpcKnowledgeSystem.Hear(world.Game, factionListener, unknownWitness, anonymous),
                "listener hears faction report");
            Check.Equal(true, factionListener.Personality.Faction(attacker.Faction.ID).Feeling < 0,
                "faction report changes the faction impression");
            Check.Equal(attacker.Faction.Name, factionListener.Personality.Faction(attacker.Faction.ID).Name,
                "faction relationship uses the faction's proper name");
            Check.Equal(null, factionListener.Personality.Person(attacker.PersonalityIdentity),
                "faction report does not blame a named stranger");
            Check.Equal(null, factionListener.Personality.Knowledge.Person(attacker.PersonalityIdentity),
                "faction report does not create a known person from the hidden actor ID");
            Check.Equal(null, factionListener.Personality.Knowledge.Person(victim.PersonalityIdentity),
                "faction report does not identify the unnamed victim either");
            Actor priorAcquaintance = NpcIntentSupport.Actor(world, "prior acquaintance", 12, 0);
            NpcKnownPerson knownAttacker = priorAcquaintance.Personality.Knowledge.See(attacker, -1);
            Check.Equal(true, NpcKnowledgeSystem.Hear(world.Game, priorAcquaintance, unknownWitness, anonymous),
                "listener with prior knowledge also hears the anonymous report");
            Check.Equal(0, knownAttacker.Danger, "anonymous report does not identify a previously known attacker");
            Check.Equal(0, knownAttacker.Violation, "anonymous report cannot assign a personal violation");

            Actor helper = NpcIntentSupport.Actor(world, "helper", 2, 2);
            witness.Personality.Opinion(helper.PersonalityIdentity, helper.UnmodifiedName);
            PersonalitySystem.Report(world.Game, new SignificantEvent("shared_food", helper, victim,
                world.Map, helper.Location.Position, 0));
            NpcFact aid = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "shared_food");
            Check.Equal(true, NpcKnowledgeSystem.Hear(world.Game, lawful, witness, aid), "kind listener hears aid");
            Check.Equal(true, NpcKnowledgeSystem.Hear(world.Game, rebel, witness, aid), "cruel listener hears aid");
            Check.Equal(true, lawful.Personality.Person(helper.PersonalityIdentity).Feeling >
                rebel.Personality.Person(helper.PersonalityIdentity).Feeling, "compassion changes response to aid");
        });
    }
}
