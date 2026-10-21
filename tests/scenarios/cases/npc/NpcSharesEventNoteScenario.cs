using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

static class NpcSharesEventNoteScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/shares-event-note", () => TownScenarioFactory.Arena(4878,
            "....#...", "....#...", "....#..."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1, "sociable", "kind");
            Actor thief = NpcIntentSupport.Actor(world, "thief", 3, 1);
            Actor friend = NpcIntentSupport.Actor(world, "friend", 6, 1);
            owner.Personality.Opinion(friend.PersonalityIdentity, friend.UnmodifiedName)
                .AdjustSocial(trust: 50);
            NpcIntentSupport.Player(world, 7, 2);
            var cache = new StorageContainer("drawer", "closed", "open", 40, 3);
            cache.IsMaterialTransparent = true;
            world.Map.PlaceMapObjectAt(cache, new Point(2, 1));
            world.Try(new ActionUseStorageContainer(owner, world.Game, cache, true));
            world.Map.PlaceActorAt(owner, cache.Location.Position);
            world.Try(new ActionDropItem(owner, world.Game, NpcIntentSupport.Food(world, owner, 1)));
            world.Map.PlaceActorAt(owner, new Point(1, 1));
            world.Try(new ActionGetFromContainer(thief, world.Game, cache.Location.Position));
            NpcFact theft = owner.Personality.Knowledge.Facts.Find(f => f.Kind == "cache_theft");
            Check.Equal(true, theft != null, "author witnessed a real theft");
            Check.Equal(false, friend.Personality.Knowledge.Facts.Exists(f => f.EventId == theft.EventId),
                "friend across a wall does not know the event");
            ItemNote note = new ItemNote(world.Game.GameItems.NOTE);
            owner.Inventory.AddAll(note);
            world.Try(new ActionWriteNote(owner, world.Game, note, NoteTopic.Warning, null, true, theft));
            world.Map.PlaceActorAt(thief, new Point(7, 0));
            world.Map.PlaceActorAt(friend, new Point(1, 0));
            for (int turn = 0; turn < 15 && !friend.Inventory.Contains(note); turn++)
            {
                owner.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(owner);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(true, friend.Inventory.Contains(note),
                "author chooses to hand an event note to a trusted person");
            Check.Equal(false, friend.Personality.Knowledge.Facts.Exists(f => f.EventId == theft.EventId),
                "receiving paper alone does not grant its claim");
            world.Try(new ActionUseItem(friend, world.Game, note));
            Check.Equal(NpcKnowledgeSource.Told,
                friend.Personality.Knowledge.Facts.Find(f => f.EventId == theft.EventId).Source,
                "reading the handed note conveys a report");
        });
    }
}
