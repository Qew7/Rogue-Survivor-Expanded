using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityBiasScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-bias", () => TownScenarioFactory.Arena(4520,
            "...", "...", "..."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor actor = SkillScenario.Actor(world);
            actor.Personality = new PersonalityState();
            ItemEntertainment magazine = new ItemEntertainment(world.Game.GameItems.MAGAZINE);
            ItemEntertainment book = new ItemEntertainment(world.Game.GameItems.BOOK);
            actor.Personality.AddTrait(new TraitInstance("likes_items", magazine.Model.ID));
            Check.Equal(35, PersonalitySystem.Bias(actor, DecisionKind.Item, magazine),
                "generic preference applies to its configured model");
            Check.Equal(0, PersonalitySystem.Bias(actor, DecisionKind.Item, book),
                "preference does not affect other models in the same item class");
            Check.Equal(0, PersonalitySystem.Bias(actor, DecisionKind.Item),
                "item preference has no effect without an item");

            int ordinaryTrust = world.Game.Rules.ActorTrustIncrease(actor);
            actor.Personality.AddTrait(new TraitInstance("kind"));
            actor.Personality.AddTrait(new TraitInstance("sociable"));
            Check.Equal(true, world.Game.Rules.ActorTrustIncrease(actor) > ordinaryTrust,
                "compassion and group preference improve the real trust rule");
            actor.Personality = new PersonalityState();
            actor.Personality.AddTrait(new TraitInstance("cruel"));
            actor.Personality.AddTrait(new TraitInstance("solitary"));
            Check.Equal(true, world.Game.Rules.ActorTrustIncrease(actor) < ordinaryTrust,
                "opposing traits reduce the real trust rule");
            Check.Equal(true, PersonalitySystem.Registry.Trait("maniac").Eligible(actor),
                "advanced trait becomes eligible after prerequisite");
            Check.Equal(false, PersonalitySystem.Registry.Trait("kind").Eligible(actor),
                "opposed compassion trait is ineligible");
            Check.Equal(false, PersonalitySystem.Registry.Trait("sociable").Eligible(actor),
                "opposed group trait is ineligible");
            actor.Personality = new PersonalityState();
            actor.Personality.AddTrait(new TraitInstance("kind"));
            Check.Equal(false, PersonalitySystem.Registry.Trait("kind").Eligible(actor),
                "owned trait cannot be awarded twice");
            Check.Equal(false, PersonalitySystem.Registry.Trait("cruel").Eligible(actor),
                "conflicting traits cannot be rolled together");
        });
    }
}
