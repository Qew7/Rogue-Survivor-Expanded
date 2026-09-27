using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class PersonalityDescriptionBoundsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-description-bounds", () => TownScenarioFactory.Arena(4539,
            "...", "...", "..."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = SkillScenario.Actor(world);
            world.SetPlayer(player);
            Actor actor = SkillScenario.Actor(world);
            actor.Personality = new PersonalityState();
            actor.Personality.AddTrait(new TraitInstance("likes_items", int.MaxValue));
            actor.Personality.AddTrait(new TraitInstance("dislikes_items",
                world.Game.GameItems.MAGAZINE.ID));

            string[] description = (string[])Check.Call(world.Game, "DescribeActor",
                new[] { typeof(Actor) }, actor);
            string text = String.Join(" ", description);
            Check.Equal(true, text.Contains("Likes"),
                "invalid item preference still appears by trait name");
            Check.Equal(true, text.Contains(world.Game.GameItems.MAGAZINE.PluralName),
                "valid item preference still shows its item name");
        });
    }
}
