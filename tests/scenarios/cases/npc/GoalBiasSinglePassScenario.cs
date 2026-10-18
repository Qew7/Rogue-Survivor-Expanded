using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class GoalBiasSinglePassScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/goal-bias-single-pass", () => TownScenarioFactory.Arena(8137,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 2, 1);
            PersonalityRegistry registry = world.Game.NpcContent.Personalities;
            Compare(owner, registry);
            foreach (TraitDefinition definition in registry.AllTraits)
            {
                owner.Personality.AddTrait(new TraitInstance(definition.Id));
                Compare(owner, registry);
            }
            Session.Get.GamePreset.NpcPersonalitiesEnabled = false;
            Compare(owner, registry);
        });
    }

    static void Compare(Actor owner, PersonalityRegistry registry)
    {
        int group, compassion, trade, courage, law, supplies;
        PersonalitySystem.GoalBiases(owner, registry, out group, out compassion, out trade,
            out courage, out law, out supplies);
        Check.Equal(PersonalitySystem.Bias(owner, NpcPersonalityAxis.Group, registry: registry), group, "group bias matches");
        Check.Equal(PersonalitySystem.Bias(owner, NpcPersonalityAxis.Compassion, registry: registry), compassion, "compassion bias matches");
        Check.Equal(PersonalitySystem.Bias(owner, NpcPersonalityAxis.Trade, registry: registry), trade, "trade bias matches");
        Check.Equal(PersonalitySystem.Bias(owner, NpcPersonalityAxis.Courage, registry: registry), courage, "courage bias matches");
        Check.Equal(PersonalitySystem.Bias(owner, NpcPersonalityAxis.Law, registry: registry), law, "law bias matches");
        Check.Equal(PersonalitySystem.Bias(owner, NpcPersonalityAxis.Supplies, registry: registry), supplies, "supplies bias matches");
    }
}
