using System.Collections.Generic;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityCatalogScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-catalog", () => TownScenarioFactory.Arena(4525,
            "...", "...", "..."), world =>
        {
            PersonalityRegistry catalog = PersonalitySystem.Registry;
            HashSet<string> awardableTraits = new HashSet<string>();
            int startingTraits = 0;
            foreach (TraitDefinition trait in catalog.StartingTraits)
            {
                startingTraits++;
                Check.Equal(false, trait.Advanced, "advanced trait is absent from starting pool");
            }
            Check.Equal(50, startingTraits, "starting pool contains fifty traits");
            foreach (MemoryDefinition memory in catalog.AllMemories)
            {
                Check.Equal(true, memory.Triggers.Length > 0,
                    "each memory has at least one significant-event trigger");
                Check.Equal(true, memory.Outcomes.Length > 0,
                    "each memory has at least one resolution outcome");
                foreach (MemoryTrigger trigger in memory.Triggers)
                {
                    bool indexed = false;
                    foreach (MemoryDefinition routed in catalog.ForEvent(trigger.EventKind))
                        if (routed == memory) indexed = true;
                    Check.Equal(true, indexed, "every trigger is reachable through event routing");
                }
                foreach (MemoryOutcome outcome in memory.Outcomes)
                {
                    Check.Equal(true, outcome.TraitId != null || outcome.SkillId.HasValue,
                        "memory outcome awards a trait or skill");
                    if (outcome.TraitId != null)
                    {
                        Check.Equal(true, catalog.Trait(outcome.TraitId) != null,
                            "memory outcome refers to a registered trait");
                        awardableTraits.Add(outcome.TraitId);
                    }
                    if (outcome.SkillId.HasValue)
                        Check.Equal(true, outcome.SkillId.Value >= Skills.IDs._FIRST_LIVING &&
                            outcome.SkillId.Value <= Skills.IDs._LAST_LIVING,
                            "living NPC memory awards a living skill");
                }
            }
            foreach (TraitDefinition trait in catalog.AllTraits)
            {
                Check.Equal(true, trait.Effects.Length > 0, "every trait has a behavior effect");
                if (!trait.Advanced) continue;
                Check.Equal(true, trait.EarnedOnly ? trait.RequiresTrait == null :
                    trait.RequiresTrait != null && catalog.Trait(trait.RequiresTrait) != null,
                    "advanced trait has a prerequisite or is explicitly earned only");
                Check.Equal(true, awardableTraits.Contains(trait.Id),
                    "advanced trait can be awarded by a memory");
            }
        });
    }
}
