using System.Drawing;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class PolicePlayerReturnScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/police-player-return", () => TownScenarioFactory.Arena(9154,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Map.Lighting = Lighting.LIT;
            Actor victim = NpcIntentSupport.Actor(world, "victim", 1, 1);
            Actor player = NpcIntentSupport.Player(world, 3, 1);
            Actor cop = NpcIntentSupport.Actor(world, "cop", 3, 2, "lawful", "brave");
            cop.Faction = world.Game.GameFactions.ThePolice;
            cop.Personality.Opinion(player.PersonalityIdentity, player.UnmodifiedName);
            Point at = new Point(2, 1);
            world.Map.AddXpdBase(new XpdBase(victim, new[] { at }));
            ItemFood food = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(food, at);
            Check.Equal(true, world.Try(new ActionTakeItem(player, world.Game, at, food)),
                "player actually steals a base item in view of the officer");
            NpcFact theft = cop.Personality.Knowledge.Facts.Find(f => f.Kind == "base_theft");
            Check.Equal(true, theft != null && theft.NamesSubject && theft.Source == NpcKnowledgeSource.Witness,
                "officer has direct evidence about the player");
            for (int i = 0; i < 12 && !NpcIntentSupport.HasEvent(cop, "police_return_requested"); i++)
            { cop.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(cop); world.Map.LocalTime.TurnCounter++; }
            Check.Equal(true, NpcIntentSupport.HasEvent(cop, "police_return_requested"),
                "officer demands the identified goods from the player");
            Check.Equal(true, NpcConversation.Pending(player, cop) != null,
                "player receives the direct request to hand over property");
            Check.Equal(true, world.Try(new ActionPlayerTalk(player, world.Game, cop, true)),
                "player accepts the officer's request through the talk action");
            Check.Equal(true, cop.Inventory.Items.Any(i => i.IsStolen && i.TheftEventId == theft.EventId),
                "accepted player reply physically transfers the stolen item");
            Check.Equal(true, NpcIntentSupport.HasEvent(cop, "police_goods_surrendered"),
                "player handover creates the same reportable outcome");
        });
    }
}
