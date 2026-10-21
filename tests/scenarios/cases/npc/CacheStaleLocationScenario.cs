using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

static class CacheStaleLocationScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/cache-stale-location", () => TownScenarioFactory.Arena(4751,
            "..........", "..........", ".........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor seeker = NpcIntentSupport.Actor(world, "seeker", 1, 1, "curious");
            Actor author = NpcIntentSupport.Actor(world, "author", 9, 1);
            NpcIntentSupport.Player(world, 9, 2);
            StorageContainer cache = new StorageContainer("fridge", "closed", "open", 80, 2);
            world.Map.PlaceMapObjectAt(cache, new Point(2, 1));
            cache.Contents.AddAll(new ItemFood(world.Game.GameItems.CANNED_FOOD));
            ItemNote note = new ItemNote(world.Game.GameItems.NOTE);
            note.Write(author, NoteTopic.CacheSupplies, cache.Location, cache.Id, "food", 1, false);
            world.Map.PlaceMapObjectAt(cache, new Point(7, 1));
            seeker.Inventory.AddAll(note);
            Check.Equal(true, world.Try(new ActionUseItem(seeker, world.Game, note)),
                "seeker reads a note about the old position");
            Check.Equal(1, seeker.Personality.Knowledge.Caches[0].Units,
                "report initially promises supplies");
            for (int i = 0; i < 12 && seeker.Personality.Knowledge.Caches[0].Units > 0; i++)
            {
                seeker.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(seeker);
                world.Map.LocalTime.TurnCounter++;
            }
            NpcKnownCache known = seeker.Personality.Knowledge.Caches[0];
            Check.Equal(0, known.Units, "failed search clears the old promise");
            Check.Equal(NpcKnowledgeSource.Inferred, known.Source,
                "missing furniture is an inference, not sight of an empty fridge");
            Check.Equal(new Point(2, 1), known.Place.Position,
                "NPC does not learn the moved furniture's new location");
            Check.Equal(new Point(7, 1), cache.Location.Position,
                "the physical fridge and supplies remain elsewhere");
        });
    }
}
