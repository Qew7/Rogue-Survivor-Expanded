using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityOutcomeScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-outcome", () => TownScenarioFactory.Arena(4518,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.UniqueActors.TheSewersThing = new UniqueActor();
            Actor player = SkillScenario.Actor(world);
            world.SetPlayer(player);
            Actor repeatKiller = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "repeat killer", false, false, 0);
            repeatKiller.Personality = new PersonalityState();
            repeatKiller.Personality.AddTrait(new TraitInstance("cruel"));
            world.Place(repeatKiller, 1, 1);
            Actor first = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "first victim", false, false, 0);
            Actor second = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "second victim", false, false, 0);
            world.Place(first, 2, 1);
            world.Place(second, 2, 2);

            world.Game.KillActor(repeatKiller, first, "scenario", false);
            Check.Equal(1, repeatKiller.Personality.Memories.Count,
                "first killing starts one unresolved memory");
            Check.Equal(false, repeatKiller.Personality.HasTrait("maniac"),
                "advanced trait is not awarded immediately");
            world.Map.LocalTime.TurnCounter++;
            world.Game.KillActor(repeatKiller, second, "scenario", false);
            Check.Equal(2, repeatKiller.Personality.Memories.Count,
                "different victims create separate memories");
            int due = 0;
            foreach (MemoryInstance memory in repeatKiller.Personality.Memories)
                due = System.Math.Max(due, memory.ResolveTurn);
            PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(false, repeatKiller.Personality.HasTrait("maniac"),
                "memory cannot resolve before its deadline");

            Actor singleKiller = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "single killer", false, false, 0);
            singleKiller.Personality = new PersonalityState();
            singleKiller.Personality.AddTrait(new TraitInstance("cruel"));
            world.Place(singleKiller, 6, 1);
            Actor third = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "third victim", false, false, 0);
            world.Place(third, 7, 1);
            world.Game.KillActor(singleKiller, third, "scenario", false);
            due = System.Math.Max(due, singleKiller.Personality.Memories[0].ResolveTurn);
            world.Map.LocalTime.TurnCounter = due;
            PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(true, repeatKiller.Personality.HasTrait("maniac"),
                "second witnessed killing unlocks advanced trait for cruel killer");
            Check.Equal(false, singleKiller.Personality.HasTrait("maniac"),
                "one killing does not satisfy the event history condition");
            Check.Equal(1, singleKiller.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.MARTIAL_ARTS),
                "unsatisfied advanced outcome falls back to a skill");
        });
    }
}
