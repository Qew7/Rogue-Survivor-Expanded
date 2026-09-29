using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcSocialContent
    {
        public static void Register(PersonalityRegistry registry)
        {
            registry.Register(new TraitDefinition("reliable", "Reliable", true, false, "honest",
                new TraitEffect(DecisionKind.Law, 12), new TraitEffect(DecisionKind.Compassion, 8)));
            registry.Register(new TraitDefinition("disillusioned", "Disillusioned", true, false, "trusting",
                new TraitEffect(DecisionKind.Group, -12), new TraitEffect(DecisionKind.Trade, -8)));
            Memory(registry, "resource_rivalry", "Disputed scarce supplies", "resource_contested", -4, "mistrustful");
            Memory(registry, "resource_concession", "Someone yielded scarce supplies", "resource_yielded", 6, "protector");
            Memory(registry, "resource_taken", "Someone took disputed supplies", "contested_taken", -12, "mistrustful");
            Memory(registry, "promise_received", "Received a promise of assistance", "food_promised", 2, null);
            Memory(registry, "medicine_promise_received", "Received a promise of medicine", "medicine_promised", 2, null);
            Memory(registry, "promise_kept", "Someone kept their promise", "promise_kept", 12, null);
            registry.Register(new MemoryDefinition("kept_my_word", "Fulfilled my promise", 2, 5,
                new[] { new MemoryTrigger("promise_kept", (a, e) => a == e.Subject) },
                new MemoryOutcome(null, "reliable", null), new MemoryOutcome(null, null, Skills.IDs.CHARISMATIC)).Relate(MemoryRelationRole.Other, 2), false);
            Memory(registry, "promise_broken", "A promise was not fulfilled by its deadline", "promise_broken", -15, "disillusioned");
            Memory(registry, "boundary_accepted", "Someone accepted a boundary", "boundary_accepted", 4, null);
            Memory(registry, "boundary_defied", "Someone rejected a boundary", "boundary_defied", -8, "mistrustful");
            Memory(registry, "medical_aid", "Received medicine or treatment", "shared_medicine", 8, "protector");
            Memory(registry, "medical_treatment", "Someone treated my wounds", "treated_person", 8, "protector");
            Memory(registry, "restitution", "Someone replaced lost supplies", "restitution_given", 10, null);
            Memory(registry, "restitution_refused", "A demand for compensation was refused", "restitution_refused", -8, "mistrustful");
            Memory(registry, "promise_released", "Released from a promise", "promise_released", 3, null);
        }
        static void Memory(PersonalityRegistry registry, string id, string name, string kind, int feeling, string trait)
        {
            registry.Register(new MemoryDefinition(id, name, 2, 5,
                new[] { new MemoryTrigger(kind, (a, e) => a == e.Other) },
                new MemoryOutcome(null, trait, trait == null ? (Skills.IDs?)Skills.IDs.CHARISMATIC : null),
                new MemoryOutcome(null, null, Skills.IDs.STRONG_PSYCHE))
                .Relate(MemoryRelationRole.Subject, feeling, MemoryRelationRole.Subject), false);
        }
    }
}
