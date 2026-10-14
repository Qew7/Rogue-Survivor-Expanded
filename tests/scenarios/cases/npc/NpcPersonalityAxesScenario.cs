using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class NpcPersonalityAxesScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-axes", () => TownScenarioFactory.Arena(8294,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor npc = NpcIntentSupport.Actor(world, "npc", 1, 1);
            npc.Personality = new PersonalityState();
            npc.Faction = world.Game.GameFactions.TheCivilians;
            npc.Personality.AddTrait(new TraitInstance("kind"));
            npc.Personality.AddTrait(new TraitInstance("frugal"));
            npc.Personality.AddTrait(new TraitInstance("patient"));

            Check.Equal(25, PersonalitySystem.Bias(npc, NpcPersonalityAxis.Compassion),
                "the old decision tendency still reads the same trait effects");
            Check.Equal(19, PersonalitySystem.Disposition(npc, NpcPersonalityAxis.HumanLife, world.Game.NpcContent),
                "existing compassion and law effects retain their per-effect rounding");
            Check.Equal(10, PersonalitySystem.Disposition(npc, NpcPersonalityAxis.CareCircle, world.Game.NpcContent),
                "group and compassion still project onto care circle");
            Check.Equal(3, PersonalitySystem.Disposition(npc, NpcPersonalityAxis.MessageTrust, world.Game.NpcContent),
                "group and law still project onto message trust");
            Check.Equal(16, PersonalitySystem.Disposition(npc, NpcPersonalityAxis.Scarcity, world.Game.NpcContent),
                "supplies and trade still project onto scarcity");
            Check.Equal(35, PersonalitySystem.Disposition(npc, NpcPersonalityAxis.Patience, world.Game.NpcContent),
                "patience uses the same trait-effect path as other axes");

            npc.Personality.ChangeExperience(NpcPersonalityAxis.HumanLife, 7);
            npc.Personality.ChangeExperience(NpcPersonalityAxis.Patience, -12);
            Check.Equal(26, PersonalitySystem.Disposition(npc, NpcPersonalityAxis.HumanLife, world.Game.NpcContent),
                "event experience adds to current trait effects");
            Check.Equal(23, PersonalitySystem.Disposition(npc, NpcPersonalityAxis.Patience, world.Game.NpcContent),
                "patience experience uses its original save slot");
            Check.Equal(8, PersonalitySystem.Disposition(npc, NpcPersonalityAxis.HumanLife,
                world.Game.NpcContent, "kind"), "excluding one trait still explains its exact contribution");

            bool rejected = false;
            try { npc.Personality.ChangeExperience(NpcPersonalityAxis.Courage, 1); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Check.Equal(true, rejected, "decision-only axes cannot corrupt saved experience slots");
            Check.Equal(7, npc.Personality.ExperienceValue(NpcPersonalityAxis.HumanLife),
                "a rejected experience change leaves the saved slots intact");
            rejected = false;
            try { new MemoryDefinition("invalid", "Invalid", 2, 2, new MemoryTrigger[0])
                .ChangeDisposition(NpcPersonalityAxis.Courage, 1); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Check.Equal(true, rejected, "memory content rejects a decision-only experience axis");
        });
    }
}
