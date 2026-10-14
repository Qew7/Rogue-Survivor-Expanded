using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class NpcPatienceRestraintScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/patience-restraint", () => TownScenarioFactory.Arena(8194,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 8, 2);
            Actor warner = NpcIntentSupport.Actor(world, "warner", 1, 1, "lawful");
            Actor patient = NpcIntentSupport.Actor(world, "patient", 2, 1, "patient", "kind");
            Actor witness = NpcIntentSupport.Actor(world, "witness", 4, 1);
            warner.Personality.Knowledge.See(patient, 0);
            NpcKnownPerson belief = warner.Personality.Knowledge.Person(patient.PersonalityIdentity);
            belief.Violation = belief.ViolationConfidence = 100;
            int basePatience = PersonalitySystem.Disposition(patient, NpcPersonalityAxis.Patience, world.Game.NpcContent);
            Check.Equal(true, basePatience >= 30, "the patient trait raises the actual scale");
            NpcIntentSupport.Turn(world, warner);
            NpcIntentSupport.Turn(world, patient);
            Check.Equal(true, NpcIntentSupport.HasEvent(warner, "boundary_accepted"),
                "the patient recipient accepts the warning instead of attacking");
            Check.Equal(false, world.Game.Rules.AreEnemies(patient, warner),
                "acceptance leaves the people at peace");
            Check.Equal(true, HasMemory(patient, "boundary_conceded") &&
                HasMemory(warner, "boundary_respected") && HasMemory(witness, "boundary_respect_witnessed"),
                "participants and a witness remember the restraint");
            Check.Equal(true, witness.Personality.Knowledge.Facts.Exists(f => f.Kind == "boundary_accepted"),
                "a witnessed peaceful resolution can be retold");
            int justice = NpcValues.Importance(world.Game.NpcContent, warner, NpcGoalValue.Justice, patient.PersonalityIdentity);
            warner.Personality.ChangeExperience(NpcPersonalityAxis.Patience, 40);
            Check.Equal(true, NpcValues.Importance(world.Game.NpcContent, warner, NpcGoalValue.Justice,
                patient.PersonalityIdentity) < justice,
                "more patience makes a new confrontation less urgent");
            int repair = world.Game.NpcContent.Value("RepairTrust").Importance(
                new NpcMotivation(world.Game.NpcContent, warner, patient.PersonalityIdentity));
            warner.Personality.ChangeExperience(NpcPersonalityAxis.Patience, -60);
            Check.Equal(true, world.Game.NpcContent.Value("RepairTrust").Importance(
                new NpcMotivation(world.Game.NpcContent, warner, patient.PersonalityIdentity)) < repair,
                "the same scale also affects attempts to repair a relationship");
        });
    }
    static bool HasMemory(Actor actor, string id)
    { foreach (MemoryInstance memory in actor.Personality.Memories) if (memory.Id == id) return true; return false; }
}
