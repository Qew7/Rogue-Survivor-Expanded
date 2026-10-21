using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;

static class BaseTheftWarningScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/base-theft-warning", () => TownScenarioFactory.Arena(4753,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor thief = NpcIntentSupport.Actor(world, "thief", 2, 1);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 3, 1, "honest", "patient");
            owner.Personality.Opinion(thief.PersonalityIdentity, thief.UnmodifiedName)
                .AdjustSocial(trust: -20, grievance: 50);
            NpcIntentSupport.Player(world, 6, 2);
            world.Map.AddXpdBase(new XpdBase(owner, new[] { new Point(2, 1) }));
            ItemFood food = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(food, new Point(2, 1));
            Check.Equal(true, world.Try(new ActionTakeItem(thief, world.Game, new Point(2, 1), food)),
                "the same physical theft succeeds");
            Check.Equal(true, NpcIntentSupport.HasEvent(owner, "base_theft"),
                "owner witnesses the crime");
            Check.Equal(false, world.Game.Rules.AreEnemies(thief, owner),
                "theft alone does not start combat");
            for (int i = 0; i < 8 && !NpcIntentSupport.HasEvent(owner, "confronted"); i++)
            {
                owner.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(owner);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(true, NpcIntentSupport.HasEvent(owner, "confronted"),
                "lawful patient owner chooses a spoken warning");
            Check.Equal(false, world.Game.Rules.AreEnemies(thief, owner),
                "warning remains a social response until violence actually occurs");
        });
    }
}
