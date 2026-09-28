using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityRepeatedKillOverflowScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-repeated-kill-overflow", () => TownScenarioFactory.Arena(4538,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.UniqueActors.TheSewersThing = new UniqueActor();
            Actor player = SkillScenario.Actor(world);
            world.SetPlayer(player);
            Actor killer = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "killer", false, false, 0);
            killer.Personality = new PersonalityState();
            killer.Personality.AddTrait(new TraitInstance("cruel"));
            Actor first = new Actor(world.Game.GameActors.FemaleCivilian,
                world.Game.GameFactions.TheCivilians, "first", false, false, 0);
            Actor second = new Actor(world.Game.GameActors.FemaleCivilian,
                world.Game.GameFactions.TheCivilians, "second", false, false, 0);
            world.Place(killer, 1, 1);
            world.Place(first, 2, 1);
            world.Place(second, 2, 2);

            world.Game.KillActor(killer, first, "scenario", false);
            world.Map.LocalTime.TurnCounter = 1;
            world.Game.KillActor(killer, second, "scenario", false);
            for (int turn = 2; turn <= 41; turn++)
                PersonalitySystem.Report(world.Game, new SignificantEvent("raid", null, null,
                    world.Map, killer.Location.Position, turn));
            Check.Equal(32, killer.Personality.Events.Count,
                "later observations evict both killings from the journal");
            Check.Equal("raid", killer.Personality.Events[0].Kind,
                "only later raid events remain in the journal");

            int due = 0;
            foreach (MemoryInstance memory in killer.Personality.Memories)
                if (memory.Id == "killed_person") due = System.Math.Max(due, memory.ResolveTurn);
            world.Map.LocalTime.TurnCounter = due;
            PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(true, killer.Personality.HasTrait("maniac"),
                "the first killing remembers the later killing after journal eviction");
        });
    }
}
