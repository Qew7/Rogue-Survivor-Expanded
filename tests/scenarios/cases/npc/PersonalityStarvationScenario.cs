using System;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityStarvationScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-starvation", () => TownScenarioFactory.Arena(4526,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = SkillScenario.Actor(world);
            world.SetPlayer(player);
            Actor survivor = SkillScenario.Actor(world);
            survivor.Personality = new PersonalityState();
            survivor.Personality.AddTrait(new TraitInstance("pragmatic"));
            survivor.FoodPoints = 1;
            Type flags = typeof(RogueGame).GetNestedType("SimFlags", BindingFlags.NonPublic);

            Check.Call(world.Game, "NextMapTurn", new[] { typeof(Map), flags },
                world.Map, Enum.Parse(flags, "NOT_SIMULATING"));
            Check.Equal(0, survivor.FoodPoints, "real survival turn exhausts food");
            Check.Equal(1, survivor.Personality.Memories.Count,
                "crossing into starvation starts a significant memory");
            Check.Equal("starvation", survivor.Personality.Memories[0].Id,
                "starvation uses its memory definition");
            Check.Equal(true, survivor.Personality.Events[0].Direct,
                "survivor directly experiences starvation");

            Check.Call(world.Game, "NextMapTurn", new[] { typeof(Map), flags },
                world.Map, Enum.Parse(flags, "NOT_SIMULATING"));
            Check.Equal(1, survivor.Personality.Events.Count,
                "continuing to starve does not flood the event journal");
            int due = survivor.Personality.Memories[0].ResolveTurn;
            world.Map.LocalTime.TurnCounter = due;
            PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(true, survivor.Personality.HasTrait("cannibal"),
                "pragmatic survivor resolves starvation into cannibal trait");
        });
    }
}
