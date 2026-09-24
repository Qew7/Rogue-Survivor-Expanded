using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;

static class ItemPickupScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("items/pickup", () => TownScenarioFactory.Arena(4304,
            ".....", ".....", "....."), world =>
        {
            Actor actor = SkillScenario.Actor(world);
            Point at = actor.Location.Position;
            ItemRangedWeapon pistol = new ItemRangedWeapon(world.Game.GameItems.PISTOL);
            world.Map.DropItemAt(pistol, at);
            Check.Equal(true, world.Map.GetItemsAt(at).Contains(pistol), "pistol starts on ground");
            Check.Equal(true, world.Try(new ActionTakeItem(actor, world.Game, at, pistol)), "pickup succeeds");
            Check.Equal(true, actor.Inventory.Contains(pistol), "pistol enters inventory");
            Inventory remaining = world.Map.GetItemsAt(at);
            Check.Equal(true, remaining == null || !remaining.Contains(pistol),
                "ground no longer holds pistol");
        });
    }
}
