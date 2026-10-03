using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class ResourcePermissionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/resource-permission", () => TownScenarioFactory.Arena(4692,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 8, 2);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 2, 1, "kind", "loyal");
            Actor taker = NpcIntentSupport.Actor(world, "taker", 1, 1, "lawful", "sociable");
            Point at = new Point(1, 2);
            world.Map.AddXpdBase(new XpdBase(owner, new[] { at }));
            var food = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(food, at);
            Location place = new Location(world.Map, at);
            PersonalitySystem.Report(world.Game, new SignificantEvent("resource_contested", taker, owner,
                world.Map, taker.Location.Position, 0) { ResourcePlace = place, Resource = "food" });
            NpcIntentSupport.Turn(world, owner);
            Check.Equal(true, NpcIntentSupport.HasEvent(taker, "resource_yielded"), "owner actually says yes");
            Check.Equal(1, taker.Personality.Permissions.Count, "permission is tied to the base place and resource");
            world.Game.DoTakeItem(taker, at, food);
            Check.Equal(true, NpcIntentSupport.HasEvent(owner, "permission_used"), "allowed pickup is observed");
            Check.Equal(false, NpcIntentSupport.HasEvent(owner, "base_theft"), "allowed pickup is not theft");
            Check.Equal(0, taker.Personality.Permissions.Count, "permission is consumed by one pickup");
            var second = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(second, at);
            world.Game.DoTakeItem(taker, at, second);
            Check.Equal(true, NpcIntentSupport.HasEvent(owner, "base_theft"), "second pickup without permission is theft");
        });
    }
}
