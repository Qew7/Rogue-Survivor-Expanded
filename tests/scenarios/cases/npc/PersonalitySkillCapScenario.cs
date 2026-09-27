using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalitySkillCapScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-skill-cap", () => TownScenarioFactory.Arena(4534,
            "...", "...", "..."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor witness = SkillScenario.Actor(world);
            witness.Personality = new PersonalityState();
            witness.Personality.AddMemory(new MemoryInstance("witnessed_murder", 0,
                2 * WorldTime.TURNS_PER_DAY, "victim"));
            for (int i = 0; i < Skills.MaxSkillLevel(Skills.IDs.STRONG_PSYCHE); i++)
                world.Game.SkillUpgrade(witness, Skills.IDs.STRONG_PSYCHE);
            world.Map.LocalTime.TurnCounter = witness.Personality.Memories[0].ResolveTurn;

            PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(Skills.MaxSkillLevel(Skills.IDs.STRONG_PSYCHE),
                witness.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.STRONG_PSYCHE),
                "primary skill reward never exceeds its cap");
            Check.Equal(1, witness.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.UNSUSPICIOUS),
                "memory grants its related alternative when primary skill is capped");
            Check.Equal(0, witness.Personality.Memories.Count,
                "memory completes after awarding alternative skill");
        });
    }
}
