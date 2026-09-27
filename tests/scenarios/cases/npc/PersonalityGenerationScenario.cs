using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityGenerationScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-generation", () => TownScenarioFactory.Create(4511, false), world =>
        {
            GamePreset enabled = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.GamePreset = enabled;
            Check.Equal(70, PersonalitySystem.Registry.TraitCount, "all trait definitions registered");
            Check.Equal(20, PersonalitySystem.Registry.AdvancedTraitCount, "advanced pool registered");
            Actor first = world.Game.GameActors.MaleCivilian.CreateNumberedName(
                world.Game.GameFactions.TheCivilians, 0);
            Check.Equal(3, first.Personality.Traits.Count, "three starting traits");
            Check.Equal(true, first.Personality.Memories.Count >= 1 && first.Personality.Memories.Count <= 2,
                "one or two starting memories");
            foreach (TraitInstance trait in first.Personality.Traits)
            {
                Check.Equal(false, PersonalitySystem.Registry.Trait(trait.Id).Advanced,
                    "advanced traits are not rolled at spawn");
            }
            for (int i = 0; i < 80; i++)
            {
                Actor sample = world.Game.GameActors.MaleCivilian.CreateNumberedName(
                    world.Game.GameFactions.TheCivilians, 0);
                Check.Equal(false, sample.Personality.HasTrait("kind") && sample.Personality.HasTrait("cruel"),
                    "opposed starting traits do not coexist");
                Check.Equal(false, sample.Personality.HasTrait("brave") && sample.Personality.HasTrait("timid"),
                    "opposed courage traits do not coexist");
            }

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
