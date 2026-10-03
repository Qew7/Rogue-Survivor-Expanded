using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class ResourcePermissionStackScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/resource-permission-stack", () => TownScenarioFactory.Arena(4861,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 8, 2);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 2, 1, "kind", "loyal");
            Actor taker = NpcIntentSupport.Actor(world, "taker", 1, 1, "lawful", "sociable");
            Point at = new Point(1, 2);
            world.Map.AddXpdBase(new XpdBase(owner, new[] { at }));
            var food = new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 2 };
            world.Map.DropItemAt(food, at);
            PersonalitySystem.Report(world.Game, new SignificantEvent("resource_contested", taker, owner,
                world.Map, taker.Location.Position, 0) { ResourcePlace = new Location(world.Map, at), Resource = "food" });
            NpcIntentSupport.Turn(world, owner);
            Check.Equal(1, taker.Personality.Permissions[0].Units, "owner grants one food unit");

            world.Game.DoTakeItem(taker, at, food);
            Check.Equal(true, owner.Personality.Knowledge.Facts.Exists(f => f.Kind == "permission_used" && f.Units == 1),
                "one unit is attributed to the permission");
            Check.Equal(true, owner.Personality.Knowledge.Facts.Exists(f => f.Kind == "base_theft" && f.Units == 1),
                "the excess unit is reported as theft");
            Check.Equal(0, taker.Personality.Permissions.Count, "one-unit permission is exhausted");
        });
    }
}
