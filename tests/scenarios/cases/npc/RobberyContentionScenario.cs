using System;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI.Sensors;
using djack.RogueSurvivor.Gameplay.Personality;

static class RobberyContentionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/robbery-contention", () => TownScenarioFactory.Arena(9762,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Map.Lighting = Lighting.LIT;
            Actor first = NpcIntentSupport.Actor(world, "first robber", 1, 1, "cruel", "selfish", "brave");
            Actor second = NpcIntentSupport.Actor(world, "second robber", 3, 1, "cruel", "selfish", "brave");
            Actor victim = NpcIntentSupport.Actor(world, "victim", 2, 1, "timid", "kind");
            NpcIntentSupport.Player(world, 8, 2);
            ItemFood food = NpcIntentSupport.Food(world, victim, 1);
            var sight = new LOSSensor(LOSSensor.SensingFilter.ACTORS);
            NpcKnowledgeSystem.Perceive(world.Game, second, sight.Sense(world.Game, second));
            NpcGoalGenerator.Refresh(world.Game, second);
            Check.Equal(true, NpcIntentSupport.Intent(second, "robbery_demand") != null,
                "second robber has already chosen a plan for the same item before the first one acts");
            for (int i = 0; i < 15 && !NpcIntentSupport.HasEvent(first, "robbery_demanded"); i++)
            { first.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(first); world.Map.LocalTime.TurnCounter++; }
            Check.Equal(true, NpcIntentSupport.HasEvent(first, "robbery_demanded"), "first robber makes a specific demand");
            second.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(second);
            Check.Equal(false, second.Personality.Events.Any(e => e.Kind == "robbery_demanded" &&
                e.SubjectId == second.PersonalityIdentity), "second robber does not duplicate the pending demand");
            for (int i = 0; i < 12 && !NpcIntentSupport.HasEvent(victim, "robbery_dropped"); i++)
            { victim.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(victim); world.Map.LocalTime.TurnCounter++; }
            Check.Equal(true, NpcIntentSupport.HasEvent(victim, "robbery_dropped"), "victim surrenders the particular stack");
            for (int i = 0; i < 12 && !NpcIntentSupport.HasEvent(first, "robbery_taken"); i++)
            { first.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(first); world.Map.LocalTime.TurnCounter++; }
            Check.Equal(true, first.Inventory.Contains(food), "first robber takes the item");
            for (int i = 0; i < 5; i++)
            { second.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(second); world.Map.LocalTime.TurnCounter++; }
            Check.Equal(false, second.Personality.Events.Any(e => e.Kind == "robbery_demanded" &&
                e.SubjectId == second.PersonalityIdentity && e.OtherId == victim.PersonalityIdentity),
                "second robber does not demand an item the original victim no longer carries");
            Check.Equal(NpcIntentStatus.Abandoned, NpcIntentSupport.Intent(second, "robbery_demand").Status,
                "second robber's stale plan is abandoned after the first takes the item");
            Check.Equal(false, victim.Inventory.Contains(food), "original victim is not charged for the same stack twice");
        });
    }
}
