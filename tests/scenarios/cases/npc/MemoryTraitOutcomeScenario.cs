using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class MemoryTraitOutcomeScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/memory-trait-outcome", () => TownScenarioFactory.Arena(4823,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor survivor = NpcIntentSupport.Actor(world, "survivor", 1, 1);
            Actor aggressor = NpcIntentSupport.Actor(world, "aggressor", 2, 1);
            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", survivor, aggressor,
                world.Map, survivor.Location.Position, 0));
            MemoryInstance memory = null;
            foreach (MemoryInstance candidate in survivor.Personality.Memories)
                if (candidate.Id == "survived_attack") memory = candidate;
            Check.Equal(true, memory != null, "attack creates a pending memory");
            world.Map.LocalTime.TurnCounter = memory.ResolveTurn;
            PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(true, survivor.Personality.HasTrait("battle_scarred"),
                "attack memory can grant an earned trait without an unrelated starting prerequisite");
            Check.Equal("trait:battle_scarred", memory.OutcomeId, "trait is the actual resolution outcome");
            bool recorded = false;
            foreach (ResidentEntry entry in Session.Get.ResidentRecords.Register(survivor).Entries)
                if (entry.Text.Contains("gained trait Battle scarred")) recorded = true;
            Check.Equal(true, recorded, "Read Records shows the granted trait");
        });
    }
}
