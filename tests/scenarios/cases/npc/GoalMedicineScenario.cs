using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

static class GoalMedicineScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/goal-medicine", () => TownScenarioFactory.Arena(4653, ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 6, 2);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1, "scavenger");
            int maxHP = world.Game.Rules.ActorMaxHPs(owner); owner.HitPoints = maxHP - 5;
            var medicine = new ItemMedicine(world.Game.GameItems.MEDIKIT); world.Map.DropItemAt(medicine, new Point(1, 2));
            Check.Equal(0, owner.Personality.Events.Count, "injury state itself suffices without an attack event");
            NpcIntentSupport.Turn(world, owner); NpcIntent goal = NpcIntentSupport.Intent(owner, "restore_health");
            Check.Equal(NpcGoalValue.Recovery, goal.Generated.Value, "health deficit generates a recovery goal");
            Check.Equal(maxHP, goal.Generated.Desired, "desired state is bound to this actor's actual maximum health");
            Check.Equal(true, owner.Inventory.Contains(medicine), "plan actually acquires the perceived medicine");
            Check.Equal(maxHP - 5, owner.HitPoints, "acquisition alone predicts no recovered HP");
            world.Map.LocalTime.TurnCounter = 1; NpcIntentSupport.Turn(world, owner);
            Check.Equal(maxHP, owner.HitPoints, "existing medicine action restores real HP");
            Check.Equal(false, owner.Inventory.Contains(medicine), "treatment consumes its real resource");
            Check.Equal(NpcIntentStatus.Completed, goal.Status, "recovery completes from actual state");
            Check.Equal(true, NpcIntentSupport.HasEvent(owner, "treated_wounds"), "treatment outcome is recorded once");
        });
    }
}
