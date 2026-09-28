using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityWorldExperiencesScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-world-experiences", () => TownScenarioFactory.Arena(4572,
            "...#...", "...#...", "...#..."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            foreach (PersonalityWorldContent.Experience experience in PersonalityWorldContent.WorldEvents)
            {
                Actor witness = new Actor(world.Game.GameActors.MaleCivilian,
                    world.Game.GameFactions.TheCivilians, "witness", true, false, 0);
                witness.Personality = new PersonalityState();
                witness.Personality.AddTrait(new TraitInstance(experience.Requires));
                Actor hidden = new Actor(world.Game.GameActors.MaleCivilian,
                    world.Game.GameFactions.TheCivilians, "hidden", true, false, 0);
                hidden.Personality = new PersonalityState();
                Actor source = new Actor(world.Game.GameActors.MaleCivilian,
                    world.Game.GameFactions[experience.Faction], "source", true, false, 0);
                Actor prisoner = new Actor(world.Game.GameActors.MaleCivilian,
                    world.Game.GameFactions.TheCivilians, "former prisoner", true, false, 0);
                world.Place(witness, 1, 1);
                world.Place(source, 2, 1);
                world.Place(hidden, 5, 1);
                int before = PersonalitySystem.Bias(witness, experience.Axis);
                SignificantEvent lifeEvent = new SignificantEvent(experience.Kind, source,
                    experience.Kind == "prisoner_transformed" ? prisoner : null,
                    world.Map, source.Location.Position, world.Map.LocalTime.TurnCounter, false, false);
                PersonalitySystem.Report(world.Game, lifeEvent);
                Check.Equal(1, witness.Personality.Memories.Count, experience.Kind + ": distinct memory created");
                Check.Equal(experience.Id, witness.Personality.Memories[0].Id, "world event keeps its identity");
                Check.Equal(0, hidden.Personality.Memories.Count, "unseen world event creates no memory");
                Check.Equal(true, witness.Personality.Faction(experience.Faction) != null,
                    "even supplies and story discoveries retain faction attribution");
                world.Map.LocalTime.TurnCounter = witness.Personality.Memories[0].ResolveTurn;
                PersonalitySystem.ResolveDue(world.Game, world.Map);
                Check.Equal(true, witness.Personality.HasTrait(experience.Trait), "event awards its acquired trait");
                Check.Equal(before + experience.Amount, PersonalitySystem.Bias(witness, experience.Axis),
                    "special trait changes the real AI decision bias");
                Check.Equal(1, witness.Personality.Faction(experience.Faction).Memories.Count,
                    "faction retains the resolved world memory");
                world.Map.RemoveActor(witness);
                world.Map.RemoveActor(source);
                world.Map.RemoveActor(hidden);
            }
        });
    }
}
