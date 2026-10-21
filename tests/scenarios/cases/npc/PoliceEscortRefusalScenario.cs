using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class PoliceEscortRefusalScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/police-escort-refusal", () => TownScenarioFactory.Arena(9155,
            "....#....", "....#....", "....#...."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor victim = NpcIntentSupport.Actor(world, "victim", 1, 1);
            Actor thief = NpcIntentSupport.Actor(world, "thief", 3, 1, "rebellious", "brave", "cruel");
            Actor cop = NpcIntentSupport.Actor(world, "cop", 6, 1);
            cop.Faction = world.Game.GameFactions.ThePolice;
            NpcIntentSupport.Player(world, 8, 2);
            Point at = new Point(2, 1);
            world.Map.AddXpdBase(new XpdBase(victim, new[] { at }));
            ItemFood food = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(food, at);
            Check.Equal(true, world.Try(new ActionTakeItem(thief, world.Game, at, food)),
                "item is stolen before the physical handover");
            world.Place(thief, 5, 1);
            world.Game.DoGiveItemTo(thief, cop, food);
            PersonalitySystem.Report(world.Game, new SignificantEvent("police_goods_surrendered", thief, cop,
                world.Map, thief.Location.Position, world.Map.LocalTime.TurnCounter,
                causeId: food.TheftEventId, storyId: food.TheftStoryId)
                { ItemId = food.StoryIdentity, ClaimantId = food.StolenFromLeaderId,
                  ClaimantGroupId = food.StolenFromGroupId, Units = 1 });
            Check.Equal(true, NpcIntentSupport.HasEvent(cop, "police_escort_offered"),
                "officer offers a trip after receiving real stolen goods");
            for (int turn = 0; turn < 5 && !NpcIntentSupport.HasEvent(cop, "police_escort_refused"); turn++)
            { thief.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(thief); world.Map.LocalTime.TurnCounter++; }
            Check.Equal(true, NpcIntentSupport.HasEvent(cop, "police_escort_refused"),
                "defiant suspect may refuse to accompany the officer");
            Check.Equal(null, thief.Leader, "refusal does not force the suspect into the officer's group");
        });
    }
}
