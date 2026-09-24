using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;

static class ItemEquipScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("items/equip", () => TownScenarioFactory.Arena(4307,
            ".....", ".....", "....."), world =>
        {
            Actor actor = SkillScenario.Actor(world);
            ItemRangedWeapon gun = new ItemRangedWeapon(world.Game.GameItems.PISTOL);
            Check.Equal(true, actor.Inventory.AddAll(gun), "pistol fits inventory");
            Check.Equal(true, world.Try(new ActionEquipItem(actor, world.Game, gun)), "equip action legal");
            Check.Same(gun, actor.GetEquippedRangedWeapon(), "pistol equipped in right hand");
            Check.Equal(true, actor.CurrentRangedAttack.Range > 0, "ranged attack becomes available");
        });
    }
}
