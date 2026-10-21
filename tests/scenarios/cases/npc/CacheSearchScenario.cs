using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

static class CacheSearchScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/searches-reported-cache", () => TownScenarioFactory.Arena(4749,
            "..........", "..........", ".........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor seeker = NpcIntentSupport.Actor(world, "seeker", 1, 1, "curious");
            Actor author = NpcIntentSupport.Actor(world, "author", 9, 1);
            NpcIntentSupport.Player(world, 9, 2);
            StorageContainer cache = new StorageContainer("drawer", "closed", "open", 40, 2);
            world.Map.PlaceMapObjectAt(cache, new Point(2, 1));
            cache.Contents.AddAll(new ItemFood(world.Game.GameItems.CANNED_FOOD));
            ItemNote trueNote = new ItemNote(world.Game.GameItems.NOTE);
            trueNote.Write(author, NoteTopic.CacheSupplies, cache.Location, cache.Id, "food", 1, true);
            seeker.Inventory.AddAll(trueNote);
            Check.Equal(true, world.Try(new ActionUseItem(seeker, world.Game, trueNote)),
                "seeker reads the physical clue");
            Check.Equal(NpcKnowledgeSource.Told, seeker.Personality.Knowledge.Caches[0].Source,
                "before visiting, the clue is only a report");
            RunUntil(world, seeker, () => seeker.Personality.Knowledge.Caches[0].Source == NpcKnowledgeSource.Witness);
            Check.Equal(true, cache.IsOpen, "NPC opened the reported cache");
            Check.Equal(NpcKnowledgeSource.Witness, seeker.Personality.Knowledge.Caches[0].Source,
                "NPC observed the contents after opening the cache");
            Check.Equal(1, seeker.Personality.Knowledge.Caches[0].Units,
                "inspection found the actual remaining supply");

        });
    }

    static void RunUntil(ScenarioWorld world, Actor actor, Func<bool> done)
    {
        for (int i = 0; i < 12 && !done(); i++)
        {
            actor.ActionPoints = Rules.BASE_ACTION_COST;
            world.NpcTurn(actor);
            world.Map.LocalTime.TurnCounter++;
        }
    }
}
