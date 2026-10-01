using System;
using System.Collections.Generic;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class RecordsQueryScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/records-query", () => TownScenarioFactory.Arena(4582, ".......", "......."), world =>
        {
            Session session = Session.Get; session.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.UniqueActors.TheSewersThing = new UniqueActor();
            Actor player = new Actor(world.Game.GameActors.MaleCivilian, world.Game.GameFactions.TheCivilians, "player", true, false, 0);
            player.Controller = new PlayerController(); world.Place(player, 4, 0); world.SetPlayer(player);
            Actor rich = new Actor(world.Game.GameActors.MaleCivilian, world.Game.GameFactions.TheCivilians, "Alex", true, false, 0);
            Actor quiet = new Actor(world.Game.GameActors.MaleCivilian, world.Game.GameFactions.TheCivilians, "Alex", true, false, 0);
            Actor soldier = new Actor(world.Game.GameActors.MaleCivilian, world.Game.GameFactions.TheArmy, "Bob", true, false, 720);
            foreach (Actor actor in new[] { rich, quiet, soldier }) actor.Personality = new PersonalityState();
            world.Place(rich, 1, 1); world.Place(quiet, 2, 1); world.Place(soldier, 3, 1);
            quiet.AddFollower(rich);
            ItemFood food = new ItemFood(world.Game.GameItems.CANNED_FOOD); food.Quantity = 3; rich.Inventory.AddAll(food);
            rich.FoodPoints = session.GamePreset.HungerPoints - 1;
            PersonalitySystem.Report(world.Game, new SignificantEvent("helped", rich, quiet, world.Map, rich.Location.Position, 1));
            PersonalitySystem.Report(world.Game, new SignificantEvent("starvation", rich, null, world.Map, rich.Location.Position, 2));
            ResidentRecord r = session.ResidentRecords.Register(rich), q = session.ResidentRecords.Register(quiet);
            for (int i = 3; i < 83; i++) session.ResidentRecords.Observe(quiet, new ObservedEvent("raid", i, null, null, false));
            // Memory creation/resolution history counts independently from the bounded pending queue.
            r.Add("memory:fixture", 3, "Memory: special.");
            r.Add("resolved:fixture", 4, "Resolved memory: special; gained trait Selfless.", null, true);
            foreach (string kind in new[] { "met_unique", "base_loss", "joined_group", "attack", "army_supplies" })
                session.ResidentRecords.Observe(rich, new ObservedEvent(kind, 5, soldier.Name, quiet.Name, true,
                    false, soldier.PersonalityIdentity, quiet.PersonalityIdentity));
            world.Map.LocalTime.TurnCounter = 1440; world.Game.KillActor(null, soldier, "scenario", false);
            session.WorldTime.TurnCounter = 2160;
            session.ResidentRecords.Refresh(session);
            RecordsSave save = new RecordsSave("test", session);
            RecordsQuery query = new RecordsQuery { Name = "aLeX", MinItems = 3, MinMemories = 1, MinDays = 3, MaxDays = 3 };
            Check.Equal(1, query.Select(save).Count, "case-insensitive name and numeric filters combine");
            Check.Same(r, query.Select(save)[0].Resident, "resolved and pending memory history selects correct namesake");
            query.ClearFilters(); query.Life = RecordsLife.Dead; query.Faction = "army";
            Check.Equal(1, query.Select(save).Count, "dead faction member remains searchable after removal");
            Check.Equal(1d, query.Select(save)[0].Days, "lifespan ends at death rather than save time");
            query.ClearFilters(); query.Group = "alex";
            Check.Equal(2, query.Select(save).Count, "group lookup uses current or last leader name");
            query.ClearFilters(); query.Sort = RecordsSort.Items;
            Check.Same(r, query.Select(save)[0].Resident, "lifetime item sort uses quantity");
            long richItems = r.ItemsReceived, quietItems = q.ItemsReceived;
            r.ItemsReceived = 9007199254740992L; q.ItemsReceived = r.ItemsReceived + 1;
            Check.Same(q, query.Select(save)[0].Resident, "large lifetime counters sort without floating-point rounding");
            r.ItemsReceived = richItems; q.ItemsReceived = quietItems;
            query.Sort = RecordsSort.Events;
            Check.Same(q, query.Select(save)[0].Resident, "event sort can select repeated observer events");
            query.Sort = RecordsSort.Name;
            List<RecordsProfile> sorted = query.Select(save);
            Check.Equal(true, sorted[0].Resident.Identity.CompareTo(sorted[1].Resident.Identity) < 0, "namesakes have deterministic identity tie break");
            Check.Same(r, query.MostInteresting(save).Resident, "diverse personal life beats repeated raid observations");
            query.Name = "missing";
            Check.Equal(0, query.Select(save).Count, "no-result query is valid");
            Check.Equal(null, query.MostInteresting(save), "no result does not fall back to unrelated NPC");
            query.ClearFilters(); query.MinResolved = 1; query.MinTraitChanges = 1;
            Check.Equal(1, query.Select(save).Count, "resolution and development filters");
            Check.Equal(false, query.SetFilter(6, "NaN"), "invalid numeric input rejected");
            Check.Equal(false, query.SetFilter(4, "-1"), "negative acquisitions rejected");
            Check.Equal(true, query.SetFilter(6, "2.5"), "fractional days accepted");
            Check.Equal(false, query.SetFilter(7, "2"), "reversed day range rejected without changing value");
            Check.Equal(Double.MaxValue, query.MaxDays, "invalid range leaves previous filter");
            string filtered = String.Join(" ", new List<string>(RecordsReader.Lines(save, null, query, "SPECIAL", RecordsEventFilter.Memories)).ToArray());
            Check.Equal(true, filtered.Contains("Memory: special"), "event text query ignores case and finds memory");
            Check.Equal(false, filtered.Contains("faced starvation"), "event text and category exclude unrelated entries");
            string path = Path.Combine(Path.GetTempPath(), "query-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, session);
                RecordsSave loaded = RecordsReader.Load(path);
                Check.Equal(r.Identity, query.MostInteresting(loaded).Resident.Identity, "same winner after archive-only load");
                Check.Equal(query.MostInteresting(save).Score, query.MostInteresting(loaded).Score, "score survives save/load");
                int count = loaded.Records.Residents.Count;
                byte[] damaged = File.ReadAllBytes(path); damaged[damaged.Length - 1] ^= 127; File.WriteAllBytes(path, damaged);
                Check.Equal(count, RecordsReader.Load(path).Records.Residents.Count, "archive reader does not deserialize world section");
                Check.Throws<Exception>(() => BinarySaveStore.LoadExact<Session>(path), "full load still validates world checksum");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        });
    }
}
