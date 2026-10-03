using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;

static class ParticipantRumorWordingScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/participant-rumor-wording", () => TownScenarioFactory.Arena(4911,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor actor = NpcIntentSupport.Actor(world, "actor", 2, 1);
            actor.Personality.AddTrait(new TraitInstance("generous"));
            Actor recipient = NpcIntentSupport.Actor(world, "recipient", 1, 1);
            Actor witness = NpcIntentSupport.Actor(world, "witness", 1, 2);
            Actor sleeper = NpcIntentSupport.Actor(world, "sleeper", 0, 1);
            sleeper.IsSleeping = true;
            Actor player = NpcIntentSupport.Player(world, 4, 1);
            PersonalitySystem.Report(world.Game, new SignificantEvent("shared_food", actor, recipient,
                world.Map, actor.Location.Position, 0));

            NpcFact participantFact = actor.Personality.Knowledge.Facts.Find(f => f.Kind == "shared_food");
            NpcFact witnessFact = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "shared_food");
            Check.Equal(NpcKnowledgeSource.Participant, participantFact.Source,
                "the actor retains knowledge as a participant");
            Check.Equal(NpcKnowledgeSource.Witness, witnessFact.Source,
                "a bystander retains knowledge as a witness");
            Check.Equal(false, sleeper.Personality.Knowledge.Facts.Exists(f => f.Kind == "shared_food"),
                "sleeping bystander does not witness the action");

            Actor listener = NpcIntentSupport.Actor(world, "listener", 3, 1);
            Check.Equal(true, world.Try(new ActionNpcTell(actor, world.Game, listener, participantFact)),
                "participant tells the event");
            Check.Equal(true, Heard(player, "I was involved when", participantFact.EventId),
                "participant does not claim to be a witness of their own action");
            Actor second = NpcIntentSupport.Actor(world, "second listener", 0, 2);
            Check.Equal(true, world.Try(new ActionNpcTell(witness, world.Game, second, witnessFact)),
                "witness tells the same event");
            Check.Equal(true, Heard(player, "I saw that", witnessFact.EventId),
                "actual witness still speaks as a witness");
        });
    }

    static bool Heard(Actor player, string phrase, long eventId)
    {
        foreach (HeardJournalEntry entry in player.Personality.HeardJournal)
            if (entry.CauseId == eventId && entry.Text.Contains(phrase)) return true;
        return false;
    }
}
