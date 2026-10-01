using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class ContentCatalogRebindScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/content-catalog-rebind", () => TownScenarioFactory.Arena(4694, ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 6, 2);
            world.Game.NpcContent = PersonalityContent.Create(new NpcRestContent()).Content;
            Actor owner = NpcIntentSupport.Actor(world, "resting", 1, 1, "restful"); owner.StaminaPoints = Rules.STAMINA_MIN_FOR_ACTIVITY;
            NpcGoalGenerator.Refresh(world.Game, owner); NpcIntent goal = NpcIntentSupport.Intent(owner, "take_breath");
            owner.ActionPoints = Rules.BASE_ACTION_COST;
            ActorAction original = NpcPlanRuntime.Choose(world.Game, owner, goal, new[] { owner }, null);
            Check.Equal(true, original.IsLegal(), "original catalog binds a legal action");
            NpcPlanStep old = goal.Plan.Current; NpcPlanningState previous = goal.Plan.DesiredState;
            world.Game.NpcContent = PersonalityContent.Create(new LayoutChange(), new NpcRestContent()).Content;
            NpcPlanningState desired = world.Game.NpcContent.Facts.Mask("rest.fact.11");
            Check.Equal(false, previous.Equals(desired), "added content shifts the symbolic fact's physical slot");
            ActorAction rebound = NpcPlanRuntime.Choose(world.Game, owner, goal, new[] { owner }, null);
            Check.Equal(false, Object.ReferenceEquals(old, goal.Plan.Current), "stale steps are discarded before execution");
            Check.Equal(goal.Plan.DesiredState, desired, "desired state is reconstructed from stable content IDs");
            Check.Equal(world.Game.NpcContent.Fingerprint, goal.Plan.CatalogFingerprint, "rebound plan retains the current revision");
            Check.Equal(false, original.IsLegal(), "old action cannot execute a replacement binding");
            Check.Equal(false, NpcIntentSupport.HasEvent(owner, "regained_stamina"), "replanning alone creates no physical outcome");
            rebound.Perform();
            Check.Equal(Rules.STAMINA_MIN_FOR_ACTIVITY + Rules.STAMINA_REGEN_WAIT, owner.StaminaPoints, "rebound executor changes real stamina");
            Check.Equal(NpcIntentStatus.Completed, goal.Status, "same goal completes after catalog change");
        });
    }
    sealed class LayoutChange : INpcContentModule
    {
        public string Id { get { return "scenario.layout-change"; } }
        public void Register(NpcCatalogBuilder catalog) { catalog.Fact("aaa.extra"); }
    }
}
