using System.Drawing;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class PoliceMissingPropertyScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/police-missing-property", () => TownScenarioFactory.Arena(9153,
            "....#....", "....#....", "....#...."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Map.Lighting = Lighting.LIT;
            Actor victim = NpcIntentSupport.Actor(world, "victim", 1, 1);
            Actor thief = NpcIntentSupport.Actor(world, "thief", 3, 1, "honest", "lawful");
            victim.Personality.Opinion(thief.PersonalityIdentity, thief.UnmodifiedName);
            Actor cop = NpcIntentSupport.Actor(world, "cop", 6, 1, "lawful", "brave");
            cop.Faction = world.Game.GameFactions.ThePolice;
            NpcIntentSupport.Player(world, 8, 2);
            Point at = new Point(2, 1);
            world.Map.AddXpdBase(new XpdBase(victim, new[] { at }));
            ItemFood food = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(food, at);
            Check.Equal(true, world.Try(new ActionTakeItem(thief, world.Game, at, food)), "real theft succeeds");
            NpcFact theft = victim.Personality.Knowledge.Facts.Find(f => f.Kind == "base_theft");
            world.Place(victim, 5, 0); world.Place(cop, 6, 0); world.Place(thief, 7, 0);
            Check.Equal(true, world.Try(new ActionNpcTell(victim, world.Game, cop, theft)),
                "officer learns a named theft from its victim");
            for (int i = 0; i < 12 && !NpcIntentSupport.HasEvent(cop, "police_theft_questioned"); i++)
            { cop.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(cop); world.Map.LocalTime.TurnCounter++; }
            for (int i = 0; i < 8 && !NpcIntentSupport.HasEvent(cop, "police_theft_admitted"); i++)
            { thief.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(thief); world.Map.LocalTime.TurnCounter++; }
            for (int i = 0; i < 12 && !NpcIntentSupport.HasEvent(cop, "police_return_requested"); i++)
            { cop.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(cop); world.Map.LocalTime.TurnCounter++; }
            Check.Equal(true, NpcIntentSupport.HasEvent(cop, "police_return_requested"),
                "officer really asks for property after admission");
            Check.Equal(true, world.Try(new ActionDropItem(thief, world.Game, food)),
                "thief no longer carries the stolen item before replying");
            for (int i = 0; i < 8 && !NpcIntentSupport.HasEvent(cop, "police_goods_missing"); i++)
            { thief.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(thief); world.Map.LocalTime.TurnCounter++; }
            Check.Equal(true, NpcIntentSupport.HasEvent(cop, "police_goods_missing"),
                "suspect reports the missing property rather than promising a transfer");
            Check.Equal(false, cop.Inventory.Items.Any(item => item.TheftEventId == theft.EventId),
                "officer does not conjure replacement goods");
            Check.Equal(false, NpcIntentSupport.HasEvent(victim, "police_goods_delivered"),
                "victim receives no false restitution event");
        });
    }
}
