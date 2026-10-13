using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class NpcDispositionMemoryScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/disposition-memory", () => TownScenarioFactory.Arena(8162,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            bool gained = false, skilled = false, removed = false, shifted = false;
            for (int i = 0; i < 48; i++)
            {
                Actor survivor = NpcIntentSupport.Actor(world, "survivor " + i, 1, 1);
                survivor.Personality = new PersonalityState();
                survivor.Personality.AddTrait(new TraitInstance("pragmatic"));
                survivor.Personality.AddTrait(new TraitInstance("minimalist"));
                int start = world.Map.LocalTime.TurnCounter;
                PersonalitySystem.Report(world.Game, new SignificantEvent("starvation", survivor, null,
                    world.Map, survivor.Location.Position, start));
                MemoryInstance memory = survivor.Personality.Memories[0];
                Check.Equal(start + 2 * WorldTime.TURNS_PER_DAY, memory.ResolveTurn,
                    "new memories resolve exactly two days after the event");
                world.Map.LocalTime.TurnCounter = memory.ResolveTurn - 1;
                PersonalitySystem.ResolveDue(world.Game, world.Map);
                Check.Equal(null, memory.OutcomeId, "memory is still pending one turn before the deadline");
                world.Map.LocalTime.TurnCounter++;
                PersonalitySystem.ResolveDue(world.Game, world.Map);
                gained |= memory.OutcomeId.StartsWith("trait:");
                skilled |= memory.OutcomeId.StartsWith("skill:");
                removed |= memory.OutcomeId.StartsWith("removed_trait:");
                shifted |= memory.OutcomeId.StartsWith("disposition:");
                if (memory.OutcomeId.StartsWith("removed_trait:"))
                    Check.Equal(false, survivor.Personality.HasTrait("minimalist"), "removal affects the actual trait list");
                if (memory.OutcomeId.StartsWith("disposition:"))
                    Check.Equal(23, survivor.Personality.ExperienceValue(NpcDispositionAxis.Scarcity),
                        "resolution adds the memory's scarcity change to the observed starvation change");
                world.Map.RemoveActor(survivor);
            }
            Check.Equal(true, gained && skilled && removed && shifted,
                "fixed-seed resolutions reach all four eligible outcome categories");
        });
    }
}
