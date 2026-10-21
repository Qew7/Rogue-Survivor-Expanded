using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;

static class ConfidentTheftRetaliationScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/confident-theft-retaliation", () => TownScenarioFactory.Arena(4783,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor thief = NpcIntentSupport.Actor(world, "thief", 2, 1);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 3, 1, "hardened");
            NpcIntentSupport.Player(world, 6, 2);
            world.Map.AddXpdBase(new XpdBase(owner, new[] { new Point(2, 1) }));
            ItemFood food = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(food, new Point(2, 1));
            Check.Equal(true, world.Try(new ActionTakeItem(thief, world.Game, new Point(2, 1), food)),
                "thief takes food in sight of a confident owner");
            Check.Equal(false, world.Game.Rules.AreEnemies(thief, owner),
                "theft itself does not create combat hostility");
            for (int i = 0; i < 8 && !NpcIntentSupport.HasEvent(owner, "retaliated"); i++)
            {
                owner.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(owner);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(true, NpcIntentSupport.HasEvent(owner, "retaliated"),
                "confident owner chooses an actual attack after witnessing theft");
        });
    }
}
