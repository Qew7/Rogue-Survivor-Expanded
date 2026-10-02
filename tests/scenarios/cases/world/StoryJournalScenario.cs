using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class StoryJournalScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/story-journal", () => TownScenarioFactory.Arena(4941,
            ".....", ".....", "....."), world =>
        {
            Session session = Session.Get;
            session.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "player", true, false, 0);
            player.Controller = new PlayerController();
            player.IsBotPlayer = true;
            world.Place(player, 1, 1);
            world.SetPlayer(player);
            Actor absent = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "absent", true, false, 0);
            absent.IsDead = true;
            foreach (PropertyInfo property in typeof(UniqueActors).GetProperties())
                property.SetValue(session.UniqueActors, new UniqueActor { TheActor = absent }, null);
            session.UniqueItems.TheSubwayWorkerBadge = new UniqueItem {
                TheItem = new ItemFood(world.Game.GameItems.CANNED_FOOD) };
            Actor prisoner = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "the prisoner", true, false, 0);
            world.Place(prisoner, 2, 1);
            session.UniqueActors.PoliceStationPrisoner = new UniqueActor { TheActor = prisoner, IsSpawned = true };
            session.UniqueMaps.PoliceStation_JailsLevel.TheMap = world.Map;
            Map facility = new Map(4941, "CHAR facility", 3, 3);
            world.Map.District.AddUniqueMap(facility);
            session.UniqueMaps.CHARUndergroundFacility.TheMap = facility;
            typeof(RogueGame).GetField("m_PlayerFOV", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(world.Game, new HashSet<Point> { prisoner.Location.Position });
            world.Map.GetTileAt(2, 1).IsInView = true;
            Check.Call(world.Game, "CheckSpecialPlayerEventsAfterAction", new[] { typeof(Actor) }, player);
            Check.Equal(ScriptStage.STAGE_1, session.ScriptStage_PoliceStationPrisoner,
                "seeing the prisoner starts the offer");
            Check.Equal(true, player.Personality.HeardJournal[0].Text.Contains("generator"),
                "offer records the actionable request");
            Check.Call(world.Game, "CheckSpecialPlayerEventsAfterAction", new[] { typeof(Actor) }, player);
            Check.Equal(ScriptStage.STAGE_2, session.ScriptStage_PoliceStationPrisoner,
                "release triggers the reveal");
            Check.Equal(true, player.Personality.HeardJournal[1].Text.Contains("iron door"),
                "release records the facility route");
            int count = player.Personality.HeardJournal.Count;
            Check.Call(world.Game, "CheckSpecialPlayerEventsAfterAction", new[] { typeof(Actor) }, player);
            Check.Equal(count, player.Personality.HeardJournal.Count, "story notes do not repeat");
            string path = Path.Combine(Path.GetTempPath(), "story-journal-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, session);
                Session loaded = BinarySaveStore.LoadExact<Session>(path);
                Actor saved = NpcIntentSupport.Find(loaded.World[0, 0].EntryMap, player.PersonalityIdentity);
                Check.Equal(count, saved.Personality.HeardJournal.Count, "story notes survive loading");
                Check.Equal(player.Personality.HeardJournal[1].Text, saved.Personality.HeardJournal[1].Text,
                    "the route survives loading");
            }
            finally { if (File.Exists(path)) File.Delete(path); if (File.Exists(path + ".bak")) File.Delete(path + ".bak"); }
        });
    }
}
