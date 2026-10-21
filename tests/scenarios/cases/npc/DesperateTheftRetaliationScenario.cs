using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;

static class DesperateTheftRetaliationScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/desperate-theft-retaliation", () => TownScenarioFactory.Arena(4770,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor thief = NpcIntentSupport.Actor(world, "thief", 2, 1);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 3, 1,
                "brave", "hotheaded", "hoarder");
            owner.Personality.ChangeExperience(NpcPersonalityAxis.Scarcity, 80);
            NpcIntentSupport.Player(world, 6, 2);
            world.Map.AddXpdBase(new XpdBase(owner, new[] { new Point(2, 1) }));
            ItemFood food = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(food, new Point(2, 1));
            Check.Equal(true, world.Try(new ActionTakeItem(thief, world.Game, new Point(2, 1), food)),
                "thief takes a watched food supply");
            Check.Equal(false, world.Game.Rules.AreEnemies(thief, owner),
                "physical theft alone does not start combat");
            NpcKnownPerson known = owner.Personality.Knowledge.Person(thief.PersonalityIdentity);
            Check.Equal(true, known != null && known.Danger > 0,
                "desperate confident victim recognizes an immediate conflict");
            for (int i = 0; i < 8 && !NpcIntentSupport.HasEvent(owner, "retaliated"); i++)
            {
                owner.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(owner);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(true, NpcIntentSupport.HasEvent(owner, "retaliated"),
                "victim chooses a real attack through the retaliation goal");
            Check.Equal(true, world.Game.Rules.AreEnemies(thief, owner),
                "hostility begins after the chosen attack");
        });
    }
}
