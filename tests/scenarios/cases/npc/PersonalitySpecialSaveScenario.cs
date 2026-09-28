using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalitySpecialSaveScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-special-save", () => TownScenarioFactory.Arena(4574,
            ".....", ".....", "....."), world =>
        {
            Session original = Session.Get;
            original.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor witness = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "witness", true, false, 0);
            witness.Personality = new PersonalityState();
            witness.Personality.AddTrait(new TraitInstance("generous"));
            witness.Personality.AddTrait(new TraitInstance("trusting"));
            Actor unprepared = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "unprepared", true, false, 0);
            unprepared.Personality = new PersonalityState();
            Actor santa = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "Santaman", true, false, 0);
            Actor soldier = new Actor(world.Game.GameActors.NationalGuard,
                world.Game.GameFactions.TheArmy, "helper", true, false, 0);
            world.Place(witness, 1, 1);
            world.Place(santa, 2, 1);
            world.Place(unprepared, 3, 1);
            world.Place(soldier, 2, 2);
            original.UniqueActors.Santaman = new UniqueActor { TheActor = santa, IsSpawned = true };
            PersonalitySystem.ObserveEncounters(world.Game, world.Map);
            PersonalitySystem.Report(world.Game, new SignificantEvent("helped", witness, soldier,
                world.Map, witness.Location.Position, world.Map.LocalTime.TurnCounter));
            Guid santaId = santa.PersonalityIdentity;
            string path = Path.Combine(Path.GetTempPath(), "special-memory-" + Guid.NewGuid().ToString("N"));
            FieldInfo active = typeof(RogueGame).GetField("m_Session", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                BinarySaveStore.Save(path, original);
                Session loaded = BinarySaveStore.LoadExact<Session>(path);
                Session.Restore(loaded);
                active.SetValue(world.Game, loaded);
                Map map = loaded.World[0, 0].EntryMap;
                map.ReconstructAuxiliaryFields();
                Actor saved = map.GetActorAt(1, 1);
                Actor fallback = map.GetActorAt(3, 1);
                Check.Equal("met_santaman", saved.Personality.Person(santaId).Memories[0].Id,
                    "saved encounter keeps unique source identity");
                Check.Same(map.GetActorAt(2, 1), loaded.UniqueActors.Santaman.TheActor,
                    "unique registry and map preserve shared identity after load");
                foreach (Actor actor in map.Actors)
                    if (actor.Personality != null)
                        foreach (MemoryInstance memory in actor.Personality.Memories)
                            map.LocalTime.TurnCounter = Math.Max(map.LocalTime.TurnCounter, memory.ResolveTurn);
                loaded.WorldTime.TurnCounter = map.LocalTime.TurnCounter;
                PersonalitySystem.ResolveDue(world.Game, map);
                Check.Equal(true, saved.Personality.HasTrait("holiday_spirit"), "loaded unique memory gains special trait");
                Check.Equal(true, saved.Personality.HasTrait("friend_army"), "loaded faction memory gains special trait");
                Check.Equal(false, fallback.Personality.HasTrait("holiday_spirit"), "missing prerequisite blocks special trait");
                Check.Equal("skill:CHARISMATIC", fallback.Personality.Person(santaId).Memories[0].OutcomeId,
                    "ineligible unique outcome improves a related skill instead");
                PersonalitySystem.ObserveEncounters(world.Game, map);
                Check.Equal(0, saved.Personality.Memories.Count, "loaded resolved encounter is not repeated");
                BinarySaveStore.Save(path, loaded);
                Session reloaded = BinarySaveStore.LoadExact<Session>(path);
                Map finalMap = reloaded.World[0, 0].EntryMap;
                finalMap.ReconstructAuxiliaryFields();
                Session.Restore(reloaded);
                Actor finalWitness = finalMap.GetActorAt(1, 1);
                Check.Equal(true, finalWitness.Personality.HasTrait("holiday_spirit"), "special trait survives second save");
                Actor stranger = new Actor(world.Game.GameActors.NationalGuard,
                    world.Game.GameFactions.TheArmy, "stranger", true, false, 0);
                Check.Equal(32, PersonalitySystem.Attitude(finalWitness, stranger),
                    "saved faction trait still changes attitude to another faction member");
                RecordsSave records = RecordsReader.Load(path);
                string history = String.Join(" ", new List<string>(RecordsReader.Lines(records, null)).ToArray());
                Check.Equal(true, history.Contains("Encountered Santaman"), "chronicle preserves named encounter");
                Check.Equal(true, history.Contains("gained trait Holiday spirit"), "chronicle preserves special outcome");
            }
            finally
            {
                Session.Restore(original);
                active.SetValue(world.Game, original);
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
