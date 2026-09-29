using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;
static class RetaliationScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/retaliation", () => TownScenarioFactory.Arena(4808,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 6, 2);
            Actor victim = NpcIntentSupport.Actor(world, "victim", 1, 1, "vindictive", "brave");
            Actor attacker = NpcIntentSupport.Actor(world, "attacker", 2, 1);
            int before = victim.HitPoints; world.Game.DoMeleeAttack(attacker, victim);
            Check.Equal(true, victim.HitPoints <= before, "recorded injury comes from real combat");
            int attackerBefore = attacker.HitPoints;
            NpcIntentSupport.Turn(world, victim);
            Check.Equal(true, NpcIntentSupport.HasEvent(victim, "retaliated"), "grievance and courage produce an actual counterattack");
            Check.Equal(true, attacker.HitPoints <= attackerBefore, "combat follows the normal damage rule");
            NpcIntent goal = NpcIntentSupport.Intent(victim, "retaliate");
            Check.Equal(NpcIntentStatus.Completed, goal.Status, "only the actual strike finishes the retaliation goal");
        });
    }
}
