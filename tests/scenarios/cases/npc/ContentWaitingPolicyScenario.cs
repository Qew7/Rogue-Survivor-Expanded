using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class ContentWaitingPolicyScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/content-waiting-policy", () => TownScenarioFactory.Arena(4695, ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 6, 2);
            world.Game.NpcContent = PersonalityContent.Create(new NpcRestContent(true)).Content;
            Actor owner = NpcIntentSupport.Actor(world, "resting", 1, 1, "restful"); owner.StaminaPoints = Rules.STAMINA_MIN_FOR_ACTIVITY;
            NpcGoalGenerator.Refresh(world.Game, owner); NpcIntent goal = NpcIntentSupport.Intent(owner, "take_breath");
            goal.Announced = true;
            Check.Equal(null, NpcIntentSystem.Select(world.Game.NpcContent, owner), "announced wait policy blocks an unbound plan");
            owner.ActionPoints = Rules.BASE_ACTION_COST;
            NpcPlanRuntime.Choose(world.Game, owner, goal, new[] { owner }, null);
            Check.Equal(true, goal.Plan.DesiredState.Extended, "desired state includes named facts above bit 63");
            Check.Same(goal, NpcIntentSystem.Select(world.Game.NpcContent, owner), "valid extended plan resumes an announced goal");
            NpcIntentSupport.Turn(world, owner);
            Check.Equal(NpcIntentStatus.Completed, goal.Status, "ordinary AI executes the registered action after waiting");
            Check.Equal(true, NpcIntentSupport.HasEvent(owner, "regained_stamina"), "completed physical action produces its event");
        });
    }
}
