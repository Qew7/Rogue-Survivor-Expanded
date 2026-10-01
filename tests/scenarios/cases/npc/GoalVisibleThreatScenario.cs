using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class GoalVisibleThreatScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/goal-visible-threat", () => TownScenarioFactory.Arena(4659, ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1, "timid");
            Actor zombie = new Actor(world.Game.GameActors.Zombie, world.Game.GameFactions.TheUndeads, "zombie", false, false, 0);
            world.Place(zombie, 2, 1); owner.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(0, owner.Personality.Events.Count, "an enemy in sight has not yet attacked");
            Check.Equal(true, world.NpcTurn(owner), "real controller responds to its actual perception");
            NpcIntent goal = NpcIntentSupport.Intent(owner, "avoid_reported_threat");
            Check.Equal(NpcGoalValue.Safety, goal.Generated.Value, "visible risk generates a desired state without an incident");
            Check.Equal(zombie.PersonalityIdentity, goal.Generated.SubjectId, "risk concerns the perceived enemy");
            Check.Equal(0L, goal.CauseId, "no attack event is invented to create the goal");
            Check.Equal(NpcIntentStatus.Paused, goal.Status, "existing immediate combat policy keeps priority");
            Check.Equal(false, NpcIntentSupport.HasEvent(owner, "withdrew"), "generation does not publish a completed planned withdrawal");
        });
    }
}
