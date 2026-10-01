using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI.Sensors;
using djack.RogueSurvivor.Gameplay.Personality;

static class NpcFactionPlansScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("factions/npc-faction-plans", () => TownScenarioFactory.Arena(4632, ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 6, 2);
            Actor leader = NpcIntentSupport.Actor(world, "leader", 1, 1, "humble");
            Actor hungry = NpcIntentSupport.Actor(world, "hungry", 2, 1, "sociable");
            Actor collector = NpcIntentSupport.Actor(world, "collector", 1, 2, "generous");
            leader.AddFollower(hungry); leader.AddFollower(collector);
            hungry.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            NpcIntentSupport.Turn(world, hungry);
            var visible = new List<Actor> { hungry, collector };
            leader.Faction = world.Game.GameFactions.TheArmy;
            Check.Equal(null, NpcStorySystem.ProposeGroupPlan(world.Game, leader, visible), "faction interest cannot invent an unknown food stock");
            world.Map.DropItemAt(new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 3 }, new Point(2, 2));
            var sensor = new LOSSensor(LOSSensor.SensingFilter.ITEMS);
            NpcKnowledgeSystem.Perceive(world.Game, leader, sensor.Sense(world.Game, leader));
            leader.Faction = world.Game.GameFactions.TheCivilians;
            Check.Equal(null, NpcStorySystem.ProposeGroupPlan(world.Game, leader, visible), "weak group preference alone does not propose supplies");
            leader.Faction = world.Game.GameFactions.TheArmy;
            NpcGroupPlan plan = NpcStorySystem.ProposeGroupPlan(world.Game, leader, visible);
            Check.Equal("group_supplies", plan.Kind, "Army supply interest changes the same informed leader's decision");
            Check.Equal(true, world.Try(new ActionNpcGroupPlan(leader, world.Game, collector, plan)), "leader actually speaks the faction-influenced task");
            Check.Equal(NpcIntentStatus.Waiting, NpcIntentSupport.Intent(leader, "coordinate_group_supplies").Status, "leader waits for a real report");
            NpcIntent goal = NpcIntentSupport.Intent(collector, "gather_group_supplies");
            Check.Equal(true, goal != null, "willing member independently accepts");
            collector.Personality.AddTrait(new TraitInstance("cruel"));
            NpcIntentSupport.Turn(world, collector);
            Check.Equal(NpcIntentStatus.Abandoned, goal.Status, "a faction plan does not override changed personal motivation");
            Check.Equal(0, NpcIntentSupport.FoodUnits(hungry), "abandonment does not fabricate delivery");
        });
    }
}
