using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityRegistryScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-registry", () => TownScenarioFactory.Arena(4524,
            "...", "...", "..."), world =>
        {
            PersonalityRegistry catalog = new PersonalityRegistry();
            catalog.Register(new TraitDefinition("collector", "Collector", false, false, null,
                new TraitEffect(DecisionKind.Item, 15)));
            catalog.Register(new TraitDefinition("renegade", "Renegade", false, false, null,
                new TraitEffect(DecisionKind.Law, -15)));
            catalog.Register(new TraitDefinition("specialist", "Specialist", true, false, "collector",
                new TraitEffect(DecisionKind.Item, 30)));
            catalog.Conflict("collector", "renegade");
            catalog.Register(new MemoryDefinition("lost_collection", "Lost collection", 2, 4,
                new[] { new MemoryTrigger("theft", (candidate, eventData) => candidate == eventData.Other) },
                new MemoryOutcome((candidate, memory) => candidate.Personality.HasTrait("collector"),
                    "specialist", null)), false);

            Actor actor = SkillScenario.Actor(world);
            actor.Personality = new PersonalityState();
            Check.Equal(false, catalog.Trait("specialist").Eligible(actor),
                "advanced extension requires its base trait");
            actor.Personality.AddTrait(new TraitInstance("collector"));
            Check.Equal(true, catalog.Trait("specialist").Eligible(actor),
                "registered prerequisite unlocks advanced extension");
            Check.Equal(false, catalog.Trait("renegade").Eligible(actor),
                "registered conflict excludes incompatible extension");
            int indexed = 0;
            foreach (MemoryDefinition definition in catalog.ForEvent("theft"))
            {
                Check.Equal("lost_collection", definition.Id, "new trigger is indexed by event kind");
                indexed++;
            }
            Check.Equal(1, indexed, "new memory is routed without a system switch");
            Check.Equal(0, Count(catalog.StartingMemories),
                "event-only extension is excluded from starting pool");
            Check.Equal(true, Rejects(() => catalog.Register(new TraitDefinition(
                "collector", "Duplicate", false, false, null))),
                "duplicate trait IDs are rejected");
            Check.Equal(true, Rejects(() => catalog.Register(new MemoryDefinition(
                "lost_collection", "Duplicate", 2, 4, new MemoryTrigger[0]), false)),
                "duplicate memory IDs are rejected");
            Check.Equal(true, Rejects(() => catalog.Register(new MemoryDefinition(
                "invalid_days", "Invalid", 0, 1, new MemoryTrigger[0]), false)),
                "memory extension needs a positive deadline");
        });
    }

    static int Count(System.Collections.Generic.IEnumerable<MemoryDefinition> definitions)
    {
        int count = 0;
        foreach (MemoryDefinition definition in definitions) count++;
        return count;
    }

    static bool Rejects(Action registration)
    {
        try { registration(); }
        catch (ArgumentException) { return true; }
        return false;
    }
}
