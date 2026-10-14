using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class NpcPatienceScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/patience-boundary-escalation", () => TownScenarioFactory.Arena(8193,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = NpcIntentSupport.Player(world, 8, 2);
            Actor warner = NpcIntentSupport.Actor(world, "warner", 1, 1, "lawful");
            Actor impatient = NpcIntentSupport.Actor(world, "impatient", 2, 1, "hotheaded", "cruel");
            Actor witness = NpcIntentSupport.Actor(world, "witness", 4, 1);
            warner.HitPoints = 100;
            warner.Personality.Knowledge.See(impatient, 0);
            NpcKnownPerson belief = warner.Personality.Knowledge.Person(impatient.PersonalityIdentity);
            belief.Violation = belief.ViolationConfidence = 100;
            int patience = PersonalitySystem.Disposition(impatient, NpcPersonalityAxis.Patience, world.Game.NpcContent);
            Check.Equal(true, patience <= -30, "hotheadedness lowers the actual patience scale");
            NpcIntentSupport.Turn(world, warner);
            Check.Equal(true, NpcIntentSupport.HasEvent(impatient, "confronted"),
                "an NPC gives the warning through the real action");
            NpcIntentSupport.Turn(world, impatient);
            Check.Equal(true, NpcIntentSupport.HasEvent(warner, "boundary_escalated"),
                "an impatient recipient acts on the warning with a real attack");
            Check.Equal(true, world.Game.Rules.AreEnemies(impatient, warner),
                "the attack changes actual combat hostility");
            Check.Equal(true, impatient.Personality.Memories.Count > 0 &&
                HasMemory(impatient, "boundary_lashed_out") && HasMemory(warner, "boundary_violence") &&
                HasMemory(witness, "boundary_violence"),
                "attacker, warned person and witness receive resolvable memories");
            NpcFact report = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "boundary_escalated");
            Check.Equal(true, report != null && report.CauseId > 0 &&
                NpcConversation.Chapter(world.Game, witness, player, report).Count > 0,
                "the witnessed attack is a reportable continuation of the warning");
            ObservedEvent blow = null;
            foreach (ObservedEvent observed in witness.Personality.Events)
                if (observed.Kind == "attack") blow = observed;
            Check.Equal(true, blow != null && blow.CauseId == report.CauseId && blow.StoryId == report.StoryId,
                "the actual melee attack keeps the warning's cause and story");
            MemoryInstance memory = FindMemory(witness, "boundary_violence");
            world.Map.LocalTime.TurnCounter = memory.ResolveTurn;
            PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(true, memory.OutcomeId != null,
                "the witness memory resolves two days later");
        });

    }

    static bool HasMemory(Actor actor, string id) { return FindMemory(actor, id) != null; }
    static MemoryInstance FindMemory(Actor actor, string id)
    { foreach (MemoryInstance memory in actor.Personality.Memories) if (memory.Id == id) return memory; return null; }
}
