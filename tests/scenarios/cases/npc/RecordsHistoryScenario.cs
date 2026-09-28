using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class RecordsHistoryScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/records-history", () => TownScenarioFactory.Arena(4560,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.UniqueActors.TheSewersThing = new UniqueActor();
            Actor resident = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "Alex", true, false, 0);
            resident.Personality = new PersonalityState();
            resident.Personality.AddTrait(new TraitInstance("generous"));
            resident.Personality.AddTrait(new TraitInstance("likes_items", world.Game.GameItems.CANNED_FOOD.ID));
            Actor helper = new Actor(world.Game.GameActors.FemaleCivilian,
                world.Game.GameFactions.TheCivilians, "Alex", true, false, 0);
            helper.Personality = new PersonalityState();
            Actor player = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "player", true, false, 0);
            player.Controller = new PlayerController();
            world.Place(resident, 1, 1);
            world.Place(helper, 2, 1);
            world.Place(player, 5, 1);
            world.SetPlayer(player);
            resident.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            ItemFood food = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            helper.Inventory.AddAll(food);
            world.Game.DoGiveItemTo(helper, resident, food);
            RecordsSave snapshot = new RecordsSave("test", Session.Get);
            List<ResidentRecord> records = RecordsReader.Residents(snapshot);
            Check.Equal(2, records.Count, "both namesakes have separate resident records");
            ResidentRecord personal = records.Find(r => r.Identity == resident.PersonalityIdentity);
            Check.Equal(true, personal != null, "resident can be selected by persistent identity");
            string initial = String.Join(" ", new List<string>(RecordsReader.Lines(snapshot, personal)).ToArray());
            Check.Equal(true, initial.Contains("Starting trait: Generous"), "initial traits are recorded");
            Check.Equal(true, initial.Contains("Likes " + world.Game.GameItems.CANNED_FOOD.PluralName),
                "item preference records readable item names");
            Check.Equal(true, initial.Contains("helped Alex"), "real gift is recorded as a life event");
            Check.Equal(true, initial.Contains("Memory: Received help"), "new memory is recorded");
            int count = personal.Entries.Count;
            PersonalitySystem.Report(world.Game, new SignificantEvent("helped", resident, helper,
                world.Map, resident.Location.Position, world.Map.LocalTime.TurnCounter));
            Check.Equal(count, personal.Entries.Count, "duplicate observation does not duplicate history");
            for (int turn = 1; turn <= 40; turn++)
                PersonalitySystem.Report(world.Game, new SignificantEvent("raid", null, null,
                    world.Map, resident.Location.Position, turn));
            Check.Equal(32, resident.Personality.Events.Count, "AI event journal remains bounded");
            Check.Equal(true, personal.Entries.Count > 40, "reader history retains events beyond journal eviction");
            world.Map.LocalTime.TurnCounter = resident.Personality.Memories[0].ResolveTurn;
            Session.Get.WorldTime.TurnCounter = world.Map.LocalTime.TurnCounter;
            PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(true, resident.Personality.HasTrait("selfless"), "real resolution grants advanced trait");
            world.Game.KillActor(null, resident, "scenario", false);
            Check.Equal(false, world.Map.HasActor(resident), "dead resident leaves live actor collection");
            Check.Equal(true, personal.DeathTurn >= 0, "death is recorded without keeping a corpse");
            snapshot = new RecordsSave("test", Session.Get);
            string history = String.Join(" ", new List<string>(RecordsReader.Lines(snapshot, personal)).ToArray());
            Check.Equal(true, history.Contains("gained trait Selfless"), "resolution outcome stays in history");
            Check.Equal(true, history.Contains("Alex died"), "dead resident remains readable");
            Check.Equal(true, history.Contains("helped Alex"), "early life event survives long journal and death");
        });
    }
}
