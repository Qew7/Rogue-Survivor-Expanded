using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;

static class MedicineUseScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("items/medicine", () => TownScenarioFactory.Arena(4316,
            ".....", ".....", "....."), world =>
        {
            Actor actor = SkillScenario.Actor(world);
            actor.HitPoints -= 10;
            int before = actor.HitPoints;
            ItemMedicine bandage = new ItemMedicine(world.Game.GameItems.BANDAGE);
            actor.Inventory.AddAll(bandage);
            Check.Equal(true, world.Try(new ActionUseItem(actor, world.Game, bandage)),
                "bandage use legal");
            Check.Equal(true, actor.HitPoints > before, "bandage restores health");
            Check.Equal(false, actor.Inventory.Contains(bandage), "bandage consumed");
        });
    }
}
