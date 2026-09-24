using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;

static class ItemDropScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("items/drop", () => TownScenarioFactory.Arena(4308,
            ".....", ".....", "....."), world =>
        {
            Actor actor = SkillScenario.Actor(world);
            ItemRangedWeapon gun = new ItemRangedWeapon(world.Game.GameItems.PISTOL);
            actor.Inventory.AddAll(gun);
            Check.Equal(true, world.Try(new ActionDropItem(actor, world.Game, gun)), "drop action legal");
            Check.Equal(false, actor.Inventory.Contains(gun), "pistol leaves inventory");
            Check.Equal(true, world.Map.GetItemsAt(actor.Location.Position).Contains(gun),
                "pistol appears on ground");
        });
    }
}
