using System.Drawing;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class FalseTheftAccusationNoEnemyScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/false-theft-accusation-no-enemy", () => TownScenarioFactory.Arena(9161,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor thief = NpcIntentSupport.Actor(world, "liar", 1, 1, "deceptive");
            Actor owner = NpcIntentSupport.Actor(world, "owner", 3, 1);
            NpcIntentSupport.Player(world, 4, 2);
            Point at = new Point(2, 1);
            world.Map.AddXpdBase(new XpdBase(owner, new[] { at }));
            ItemFood food = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(food, at);
            Check.Equal(true, world.Try(new ActionTakeItem(thief, world.Game, at, food)),
                "deceptive NPC can commit a real theft");
            Check.Equal(false, thief.Personality.Knowledge.Facts.Any(f => f.Kind == "theft_accusation"),
                "without a known enemy the thief invents no culprit");
        });
    }
}
