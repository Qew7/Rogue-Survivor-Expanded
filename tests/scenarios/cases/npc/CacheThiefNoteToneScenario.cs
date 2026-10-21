using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay.Personality;

static class CacheThiefNoteToneScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/cache-thief-note-tone", () => TownScenarioFactory.Arena(4877,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1);
            Actor thief = NpcIntentSupport.Actor(world, "thief", 3, 1, "cruel");
            NpcIntentSupport.Player(world, 6, 2);
            var cache = new StorageContainer("drawer", "closed", "open", 40, 3);
            world.Map.PlaceMapObjectAt(cache, new Point(2, 1));
            world.Try(new ActionUseStorageContainer(owner, world.Game, cache, true));
            world.Map.PlaceActorAt(owner, cache.Location.Position);
            world.Try(new ActionDropItem(owner, world.Game, NpcIntentSupport.Food(world, owner, 1)));
            world.Map.PlaceActorAt(owner, new Point(1, 1));
            Check.Equal(true, world.Try(new ActionGetFromContainer(thief, world.Game, cache.Location.Position)),
                "thief really takes a claimed item");
            NpcFact theft = thief.Personality.Knowledge.Facts.Find(f => f.Kind == "cache_theft");
            Check.Equal(true, theft != null && theft.Source == NpcKnowledgeSource.Participant,
                "theft is a firsthand fact for its actor");
            Check.Equal(NoteTopic.Taunt, NpcNoteTemplates.TopicFor(thief, theft),
                "cruel trait selects a taunt");
            ItemNote note = new ItemNote(world.Game.GameItems.NOTE);
            thief.Inventory.AddAll(note);
            Check.Equal(true, world.Try(new ActionWriteNote(thief, world.Game, note,
                NoteTopic.Taunt, null, false, theft)), "thief writes on physical paper");
            Check.Equal(true, note.Text.Contains("I took 1 food") &&
                note.Text.Contains("You did not stop me"), "taunt includes the real theft details");
            Check.Equal(false, world.Try(new ActionWriteNote(thief, world.Game, note,
                NoteTopic.Threat, null, false, theft)), "written paper cannot be reused");
        });
    }
}
