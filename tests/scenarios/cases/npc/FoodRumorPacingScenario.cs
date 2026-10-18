using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.AI;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class FoodRumorPacingScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/food-rumor-pacing", () => TownScenarioFactory.Arena(8204,
            ".........", ".........", "........."), world =>
        {
            Session original = Session.Get;
            original.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            var city = new World(1);
            city[0, 0] = world.Map.District;
            original.World = city;
            Actor player = NpcIntentSupport.Player(world, 2, 1);
            Actor speaker = NpcIntentSupport.Actor(world, "speaker", 1, 1);
            Point cache = new Point(3, 1);
            world.Map.DropItemAt(new ItemFood(world.Game.GameItems.CANNED_FOOD), cache);
            PerceiveCache(world, speaker, cache);
            NpcFact first = speaker.Personality.Knowledge.Facts.Single(f => f.Kind == "food_cache");
            Check.Equal(true, new ActionPlayerTalk(player, world.Game, speaker, null).IsLegal(),
                "the player can ask the eyewitness for news");
            new ActionPlayerTalk(player, world.Game, speaker, null).Perform();
            Check.Equal(true, player.Personality.Knowledge.Facts.Any(f => f.EventId == first.EventId),
                "real conversation tells the player about the food");

            world.Map.LocalTime.TurnCounter = 1;
            world.Map.DropItemAt(new ItemFood(world.Game.GameItems.CANNED_FOOD), cache);
            PerceiveCache(world, speaker, cache);
            NpcFact changed = speaker.Personality.Knowledge.Facts.Last(f => f.Kind == "food_cache");
            Check.Equal(true, changed.EventId != first.EventId, "a changed supply produces a new fact ID");
            Check.Equal(false, NpcConversation.EligibleListener(world.Game, speaker, player, changed),
                "the same find is not repeated to the player on the next turn");
            Point nearbyCache = new Point(4, 1);
            world.Map.DropItemAt(new ItemFood(world.Game.GameItems.CANNED_FOOD), nearbyCache);
            PerceiveCache(world, speaker, nearbyCache);
            NpcFact nearby = speaker.Personality.Knowledge.Facts.Last(f => f.Kind == "food_cache");
            Check.Equal(false, NpcConversation.EligibleListener(world.Game, speaker, player, nearby),
                "another tile with identical location wording does not repeat the same rumor");
            Check.Equal(false, new ActionNpcTell(speaker, world.Game, player, changed).IsLegal(),
                "the autonomous tell action follows the same limit");
            Actor other = NpcIntentSupport.Actor(world, "other", 0, 1);
            Check.Equal(true, new ActionNpcTell(speaker, world.Game, other, changed).IsLegal(),
                "the find can still be told to a different actor");

            Guid speakerId = speaker.PersonalityIdentity, playerId = player.PersonalityIdentity;
            string path = Path.Combine(Path.GetTempPath(), "food-rumor-pacing-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, original);
                Session loaded = BinarySaveStore.Load<Session>(path);
                Map map = loaded.World[0, 0].EntryMap;
                map.ReconstructAuxiliaryFields();
                Actor savedSpeaker = NpcIntentSupport.Find(map, speakerId);
                Actor savedPlayer = NpcIntentSupport.Find(map, playerId);
                NpcFact savedChanged = savedSpeaker.Personality.Knowledge.Facts.Single(f => f.EventId == changed.EventId);
                map.LocalTime.TurnCounter = WorldTime.TURNS_PER_DAY - 1;
                Check.Equal(false, NpcConversation.EligibleListener(world.Game, savedSpeaker, savedPlayer, savedChanged),
                    "the per-recipient limit survives save and load until the day ends");
                map.LocalTime.TurnCounter = WorldTime.TURNS_PER_DAY;
                Check.Equal(true, NpcConversation.EligibleListener(world.Game, savedSpeaker, savedPlayer, savedChanged),
                    "a fresh changed report can be told after 720 turns");
                map.LocalTime.TurnCounter = WorldTime.TURNS_PER_DAY + 1;
                NpcFact savedFirst = savedSpeaker.Personality.Knowledge.Facts.Single(f => f.EventId == first.EventId);
                Check.Equal(false, NpcConversation.EligibleFact(world.Game, savedSpeaker, savedFirst,
                    map.LocalTime.TurnCounter), "the original food report is stale after one day");
                Check.Equal(false, NpcKnowledgeSystem.Hear(world.Game, savedPlayer, savedSpeaker, savedFirst),
                    "stale food cannot be learned through another delivery path");
                savedSpeaker.Personality.Knowledge.Expire(++map.LocalTime.TurnCounter);
                Check.Equal(false, savedSpeaker.Personality.Knowledge.Facts.Contains(savedFirst),
                    "the old food fact is removed");
                Check.Equal(false, savedSpeaker.Personality.Knowledge.Places.Any(p => p.Kind == "food"),
                    "the old known food place is removed");
            }
            finally
            {
                Session.Restore(original);
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }

    static void PerceiveCache(ScenarioWorld world, Actor speaker, Point cache)
    {
        int turn = world.Map.LocalTime.TurnCounter;
        NpcKnowledgeSystem.Perceive(world.Game, speaker, new List<Percept> {
            new Percept(world.Map.GetItemsAt(cache), turn, new Location(world.Map, cache)) });
    }
}
