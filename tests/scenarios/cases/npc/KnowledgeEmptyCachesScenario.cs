using System;
using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.AI;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class KnowledgeEmptyCachesScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/knowledge-empty-caches", () => TownScenarioFactory.Arena(4821,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 8, 2);
            Actor observer = NpcIntentSupport.Actor(world, "observer", 4, 1);
            var bat = new ItemMeleeWeapon(world.Game.GameItems.BASEBALLBAT);
            observer.Personality.AddTrait(new TraitInstance("likes_items", bat.Model.ID));
            observer.Personality.Attach(new NpcAttachment { Kind = "item", ItemId = bat.StoryIdentity, ModelId = bat.Model.ID, Weight = 35 });
            Point cache = new Point(2, 1);
            var food = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(bat, cache); world.Map.DropItemAt(food, cache);
            NpcKnowledgeSystem.Perceive(world.Game, observer, new List<Percept> {
                new Percept(world.Map.GetItemsAt(cache), 0, new Location(world.Map, cache)) });
            string model = "item:" + bat.Model.ID, specific = "item:" + bat.StoryIdentity.ToString("N");
            Check.Equal(3, observer.Personality.Knowledge.Places.Count, "real cache retains food, preferred model and specific item");

            var empty = new List<Percept>();
            for (int x = 0; x < 9; x++) for (int y = 0; y < 3; y++)
            {
                Point place = new Point(x, y);
                if (place == cache || place == observer.Location.Position || place == new Point(8, 2)) continue;
                world.Map.DropItemAt(new ItemMeleeWeapon(world.Game.GameItems.CROWBAR), place);
                empty.Add(new Percept(world.Map.GetItemsAt(place), 0, new Location(world.Map, place)));
            }
            Check.Equal(true, empty.Count > 16, "fixture exceeds the bounded place list");
            NpcKnowledgeSystem.Perceive(world.Game, observer, empty);
            Check.Equal(3, observer.Personality.Knowledge.Places.Count, "unrelated inventories cannot evict useful caches");
            Check.Equal(true, observer.Personality.Knowledge.Places.Exists(p => p.Kind == "food" && p.Units > 0) &&
                observer.Personality.Knowledge.Places.Exists(p => p.Kind == model && p.Units > 0) &&
                observer.Personality.Knowledge.Places.Exists(p => p.Kind == specific && p.Units > 0),
                "all three kinds of real knowledge survive empty observations");

            world.Map.RemoveItemAt(bat, cache); world.Map.RemoveItemAt(food, cache);
            world.Map.DropItemAt(new ItemMeleeWeapon(world.Game.GameItems.CROWBAR), cache);
            NpcKnowledgeSystem.Perceive(world.Game, observer, new List<Percept> {
                new Percept(world.Map.GetItemsAt(cache), 0, new Location(world.Map, cache)) });
            Check.Equal(true, observer.Personality.Knowledge.Places.Exists(p => p.Kind == "food" && p.Place.Position == cache && p.Units == 0) &&
                observer.Personality.Knowledge.Places.Exists(p => p.Kind == model && p.Place.Position == cache && p.Units == 0) &&
                observer.Personality.Knowledge.Places.Exists(p => p.Kind == specific && p.Place.Position == cache && p.Units == 0),
                "an actually emptied known cache is updated rather than kept stale");
        });
    }
}
