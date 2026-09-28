using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityUniqueEncountersScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-unique-encounters", () => TownScenarioFactory.Arena(4570,
            "...#...", "...#...", "...#..."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            string[][] cases = {
                new[] { "BigBear", "met_big_bear", "brave", "bear_resolve" },
                new[] { "FamuFataru", "met_famu_fataru", "disciplined", "blade_discipline" },
                new[] { "Santaman", "met_santaman", "generous", "holiday_spirit" },
                new[] { "Roguedjack", "met_roguedjack", "curious", "rogue_ingenuity" },
                new[] { "Duckman", "met_duckman", "sociable", "duck_camaraderie" },
                new[] { "HansVonHanz", "met_hans_von_hanz", "disciplined", "hans_drill" },
                new[] { "PoliceStationPrisoner", "met_prisoner", "suspicious", "prisoner_secrets" },
                new[] { "JasonMyers", "met_jason_myers", "cautious", "masked_survivor" },
                new[] { "TheSewersThing", "met_sewers_thing", "fearful", "sewer_dread" }
            };
            foreach (string[] entry in cases)
            {
                world.Map.LocalTime.TurnCounter = WorldTime.TURNS_PER_DAY / 2;
                Actor witness = new Actor(world.Game.GameActors.MaleCivilian,
                    world.Game.GameFactions.TheCivilians, "witness", true, false, 0);
                witness.Personality = new PersonalityState();
                witness.Personality.AddTrait(new TraitInstance(entry[2]));
                Actor hidden = new Actor(world.Game.GameActors.MaleCivilian,
                    world.Game.GameFactions.TheCivilians, "hidden", true, false, 0);
                hidden.Personality = new PersonalityState();
                Actor sleeping = new Actor(world.Game.GameActors.MaleCivilian,
                    world.Game.GameFactions.TheCivilians, "sleeping", true, false, 0);
                sleeping.Personality = new PersonalityState();
                sleeping.IsSleeping = true;
                Actor unique = new Actor(entry[0] == "TheSewersThing" ? world.Game.GameActors.SewersThing :
                    entry[0] == "JasonMyers" ? world.Game.GameActors.JasonMyers : world.Game.GameActors.MaleCivilian,
                    entry[0] == "TheSewersThing" ? world.Game.GameFactions.TheUndeads :
                    entry[0] == "JasonMyers" ? world.Game.GameFactions.ThePsychopaths :
                    world.Game.GameFactions.TheCivilians, entry[0], true, false, 0);
                world.Place(witness, 1, 1);
                world.Place(unique, 2, 1);
                world.Place(hidden, 5, 1);
                world.Place(sleeping, 1, 2);
                PersonalitySystem.ObserveEncounters(world.Game, world.Map);
                Check.Equal(0, witness.Personality.Memories.Count,
                    entry[0] + ": a namesake is not treated as the registered unique actor");
                typeof(UniqueActors).GetProperty(entry[0]).SetValue(Session.Get.UniqueActors,
                    new UniqueActor { TheActor = unique, IsSpawned = true }, null);
                Type flags = typeof(RogueGame).GetNestedType("SimFlags", System.Reflection.BindingFlags.NonPublic);
                Check.Call(world.Game, "NextMapTurn", new[] { typeof(Map), flags },
                    world.Map, Enum.Parse(flags, "LODETAIL_TURN"));
                Check.Equal(1, witness.Personality.Memories.Count, entry[0] + ": visible unique creates memory");
                Check.Equal(entry[1], witness.Personality.Memories[0].Id, "unique has a distinct memory");
                Check.Equal(0, hidden.Personality.Memories.Count, "wall blocks the encounter");
                Check.Equal(0, sleeping.Personality.Memories.Count, "sleep blocks the encounter");
                Guid id = unique.PersonalityIdentity;
                Check.Equal(true, witness.Personality.Person(id) != null, "encounter belongs to this unique's identity");
                world.Map.LocalTime.TurnCounter = witness.Personality.Memories[0].ResolveTurn;
                PersonalitySystem.ResolveDue(world.Game, world.Map);
                Check.Equal(true, witness.Personality.HasTrait(entry[3]), "encounter awards its special trait");
                int events = witness.Personality.Events.Count;
                PersonalitySystem.ObserveEncounters(world.Game, world.Map);
                Check.Equal(0, witness.Personality.Memories.Count, "resolved first encounter does not repeat");
                Check.Equal(events, witness.Personality.Events.Count, "known unique does not spam observations");
                Check.Equal(1, witness.Personality.Person(id).Memories.Count, "resolved encounter remains in relationship");
                world.Map.RemoveActor(witness);
                world.Map.RemoveActor(hidden);
                world.Map.RemoveActor(sleeping);
                world.Map.RemoveActor(unique);
            }
        });
    }
}
