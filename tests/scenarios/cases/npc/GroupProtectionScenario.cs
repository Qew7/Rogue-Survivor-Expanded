using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;
static class GroupProtectionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/group-protection", () => TownScenarioFactory.Arena(4807,
            "........", "........", "........"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 7, 2);
            Actor leader = NpcIntentSupport.Actor(world, "leader", 1, 1, "loyal", "brave");
            Actor guard = NpcIntentSupport.Actor(world, "guard", 2, 1, "protective", "brave");
            Actor victim = NpcIntentSupport.Actor(world, "victim", 1, 2, "sociable");
            Actor attacker = NpcIntentSupport.Actor(world, "attacker", 2, 2, "vindictive");
            leader.AddFollower(guard); leader.AddFollower(victim);
            world.Game.DoMeleeAttack(attacker, victim);
            NpcGroupPlan plan = NpcStorySystem.ProposeGroupPlan(world.Game, leader, new List<Actor> { guard, victim, attacker });
            Check.Equal(true, plan != null && plan.Kind == "group_protection", "group proposes defense from a witnessed attack");
            Check.Equal(true, world.Try(new ActionNpcGroupPlan(leader, world.Game, guard, plan)), "leader really communicates the task");
            NpcIntent assigned = NpcIntentSupport.Intent(guard, "defend_person");
            Check.Equal(true, assigned != null && Session.Get.NpcDirector.Find(plan.StoryId).Parents.Contains(assigned.StoryId), "guard independently accepts according to protective traits");
            NpcIntentSupport.Turn(world, guard);
            Check.Equal(true, NpcIntentSupport.HasEvent(attacker, "defended_person"), "guard acts against a real attacker");
            Check.Equal(NpcIntentStatus.Completed, assigned.Status, "actual intervention completes its assigned goal");
        });
    }
}
