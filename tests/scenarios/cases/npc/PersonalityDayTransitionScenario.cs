using System;
using System.Drawing;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class PersonalityDayTransitionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-day-transition", () => TownScenarioFactory.Arena(4523,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = SkillScenario.Actor(world);
            world.SetPlayer(player);
            Actor witness = SkillScenario.Actor(world);
            witness.Personality = new PersonalityState();
            witness.Personality.AddTrait(new TraitInstance("lawful"));
            witness.Personality.AddMemory(new MemoryInstance("witnessed_murder", 0,
                2 * WorldTime.TURNS_PER_DAY, "victim"));
            MemoryInstance memory = witness.Personality.Memories[0];
            world.Map.PlaceActorAt(witness, new Point(2, 1));
            world.Map.LocalTime.TurnCounter = 2 * WorldTime.TURNS_PER_DAY - 1;

            Type flags = typeof(RogueGame).GetNestedType("SimFlags", BindingFlags.NonPublic);
            Check.Equal(false, witness.Personality.HasTrait("zealot"),
                "advanced trait is absent before deadline");
            Check.Call(world.Game, "NextMapTurn", new[] { typeof(Map), flags },
                world.Map, Enum.Parse(flags, "LODETAIL_TURN"));
            Check.Equal(2 * WorldTime.TURNS_PER_DAY, world.Map.LocalTime.TurnCounter,
                "real map turn crosses the day boundary");
            string outcome = memory.OutcomeId;
            Check.Equal(true, outcome != null && outcome != "none",
                "day transition resolves due memory into a lasting outcome");
            Check.Equal(0, witness.Personality.Memories.Count,
                "resolved memory leaves pending list");
            int traitsAfterResolution = witness.Personality.Traits.Count;
            Check.Call(world.Game, "NextMapTurn", new[] { typeof(Map), flags },
                world.Map, Enum.Parse(flags, "LODETAIL_TURN"));
            Check.Equal(traitsAfterResolution, witness.Personality.Traits.Count,
                "later turns do not repeat the memory outcome");
        });
    }
}
