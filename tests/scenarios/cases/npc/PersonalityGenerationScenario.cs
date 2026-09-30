using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityGenerationScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-generation", () => TownScenarioFactory.Create(4511, false), world =>
        {
            GamePreset enabled = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.GamePreset = enabled;
            Check.Equal(116, PersonalitySystem.Registry.TraitCount, "all trait definitions registered");
            Check.Equal(66, PersonalitySystem.Registry.AdvancedTraitCount, "advanced pool registered");
            Actor first = world.Game.GameActors.MaleCivilian.CreateNumberedName(
                world.Game.GameFactions.TheCivilians, 0);
            Check.Equal(3, first.Personality.Traits.Count, "three starting traits");
            Check.Equal(true, first.Personality.Memories.Count >= 1 && first.Personality.Memories.Count <= 2,
                "one or two starting memories");
            HashSet<string> traitIds = new HashSet<string>();
            foreach (TraitInstance trait in first.Personality.Traits)
            {
                Check.Equal(false, PersonalitySystem.Registry.Trait(trait.Id).Advanced,
                    "advanced traits are not rolled at spawn");
                Check.Equal(true, traitIds.Add(trait.Id), "starting traits are distinct");
            }
            HashSet<string> memoryIds = new HashSet<string>();
            foreach (MemoryInstance memory in first.Personality.Memories)
            {
                Check.Equal(true, memoryIds.Add(memory.Id), "starting memories are distinct");
                Check.Equal(true, memory.ResolveTurn - memory.StartTurn >= 2 * WorldTime.TURNS_PER_DAY &&
                    memory.ResolveTurn - memory.StartTurn <= 6 * WorldTime.TURNS_PER_DAY,
                    "starting memory resolves in two to six days");
            }
            for (int i = 0; i < 80; i++)
            {
                Actor sample = world.Game.GameActors.MaleCivilian.CreateNumberedName(
                    world.Game.GameFactions.TheCivilians, 0);
                Check.Equal(false, sample.Personality.HasTrait("kind") && sample.Personality.HasTrait("cruel"),
                    "opposed starting traits do not coexist");
                Check.Equal(false, sample.Personality.HasTrait("brave") && sample.Personality.HasTrait("timid"),
                    "opposed courage traits do not coexist");
                foreach (TraitInstance trait in sample.Personality.Traits)
                    if (PersonalitySystem.Registry.Trait(trait.Id).ItemParameter)
                        Check.Equal(true, trait.ItemModelId >= 0 &&
                            trait.ItemModelId < (int)GameItems.IDs.UNIQUE_SUBWAY_BADGE &&
                            Models.Items[trait.ItemModelId] != null,
                            "generated item preference targets available ordinary loot");
            }

            Actor undead = world.Game.GameActors.Zombie.CreateNumberedName(
                world.Game.GameFactions.TheUndeads, 0);
            Check.Equal(null, undead.Personality, "undead do not receive personality");

            enabled.NpcPersonalitiesEnabled = false;
            Session.Get.GamePreset = enabled;
            Actor disabled = world.Game.GameActors.FemaleCivilian.CreateNumberedName(
                world.Game.GameFactions.TheCivilians, 0);
            Check.Equal(null, disabled.Personality, "disabled preset creates no personality");
            Check.Equal(0, PersonalitySystem.Bias(first, DecisionKind.Group),
                "disabled preset suppresses effects on existing actors");
        });
    }
}
