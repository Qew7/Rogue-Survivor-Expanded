using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay.Personality;

static class NpcSharesCacheNoteScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/shares-cache-note", () => TownScenarioFactory.Arena(4773,
            "........", "........", "........"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor petr = NpcIntentSupport.Actor(world, "Petr", 2, 1, "kind");
            Actor andrey = NpcIntentSupport.Actor(world, "Andrey", 1, 1);
            NpcIntentSupport.Player(world, 7, 2);
            petr.AddFollower(andrey);
            petr.Personality.Knowledge.See(andrey, 0);
            StorageContainer cache = new StorageContainer("drawer", "closed", "open", 40, 2);
            world.Map.PlaceMapObjectAt(cache, new Point(3, 1));
            cache.SetState(StorageContainer.OPEN);
            cache.Contents.AddAll(new ItemFood(world.Game.GameItems.CANNED_FOOD));
            ItemNote note = new ItemNote(world.Game.GameItems.NOTE);
            petr.Inventory.AddAll(note);
            Check.Equal(true, world.Try(new ActionWriteNote(petr, world.Game, note,
                NoteTopic.CacheSupplies, cache)), "Petr writes a physical cache note");
            Check.Equal(true, world.Try(new ActionUseStorageContainer(petr, world.Game, cache, false)),
                "Petr closes the cache before sharing its description");
            Check.Equal(0, andrey.Personality.Knowledge.Caches.Count,
                "Andrey does not learn the paper's contents from a distance");
            for (int i = 0; i < 16 && !andrey.Inventory.Contains(note); i++)
            {
                petr.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(petr);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(true, andrey.Inventory.Contains(note),
                "Petr gives the note through a real item transfer");
            Check.Equal(true, NpcIntentSupport.HasEvent(andrey, "cache_note_shared"),
                "the observed handoff has its own event");
            Check.Equal(false, andrey.Personality.Knowledge.HasReadNote(note.Id),
                "receiving paper is not the same as reading it");
            for (int i = 0; i < 8 && !andrey.Personality.Knowledge.HasReadNote(note.Id); i++)
            {
                andrey.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(andrey);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(true, andrey.Personality.Knowledge.HasReadNote(note.Id),
                "recipient chooses to read the paper");
            Check.Equal(NpcKnowledgeSource.Told, andrey.Personality.Knowledge.Caches[0].Source,
                "the note becomes a report only after reading");
        });
    }
}
