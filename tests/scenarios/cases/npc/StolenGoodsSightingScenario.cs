using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.AI;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class StolenGoodsSightingScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/stolen-goods-sighting", () => TownScenarioFactory.Arena(5944,
            "........", "........", "........"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 4, 1);
            Actor thief = NpcIntentSupport.Actor(world, "thief", 2, 1);
            world.Map.AddXpdBase(new XpdBase(owner, new[] { new Point(2, 1), new Point(3, 1), new Point(4, 1) }));
            ItemFood loot = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(loot, new Point(2, 1));
            world.Game.DoTakeItem(thief, new Point(2, 1), loot);
            Actor stranger = NpcIntentSupport.Actor(world, "stranger", 7, 1);
            world.Game.DoDropItem(thief, loot);
            Location place = new Location(world.Map, new Point(2, 1));
            Percept sight = new Percept(world.Map.GetItemsAt(place.Position), world.Map.LocalTime.TurnCounter, place);
            NpcKnowledgeSystem.Perceive(world.Game, owner, new[] { sight });
            NpcFact found = owner.Personality.Knowledge.Facts.Find(f => f.Kind == "stolen_goods_found");
            Check.Equal(true, found != null && found.ItemId == loot.StoryIdentity && found.Place == place,
                "seeing abandoned stolen goods produces a located discovery without picking them up");
            NpcKnowledgeSystem.Perceive(world.Game, owner, new[] { sight });
            Check.Equal(1, owner.Personality.Knowledge.Facts.FindAll(f => f.Kind == "stolen_goods_found").Count,
                "seeing the same item again does not create another rumor");
            NpcKnowledgeSystem.Perceive(world.Game, stranger, new[] { sight });
            Check.Equal(false, stranger.Personality.Knowledge.Facts.Exists(f => f.Kind == "stolen_goods_found"),
                "a stranger cannot identify a victim from an unfamiliar item");
            world.Game.DoTakeItem(owner, place.Position, loot);
            Check.Equal(1, owner.Personality.Knowledge.Facts.FindAll(f => f.Kind == "stolen_goods_found").Count,
                "picking up an already reported item does not duplicate its discovery");
            world.Game.DoDropItem(owner, loot);
            Location ownPlace = owner.Location;
            NpcKnowledgeSystem.Perceive(world.Game, owner, new[] {
                new Percept(world.Map.GetItemsAt(ownPlace.Position), world.Map.LocalTime.TurnCounter, ownPlace) });
            Check.Equal(1, owner.Personality.Knowledge.Facts.FindAll(f => f.Kind == "stolen_goods_found").Count,
                "dropping one's own recovered item does not invent another find");
        });
    }
}
