using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

// A complete extension in one file, exercising the same contracts as shipped content.
sealed class NpcRestContent : INpcContentModule, INpcGoalSource
{
    readonly bool waitingAfterAnnouncement;
    public NpcRestContent(bool waitingAfterAnnouncement = false) { this.waitingAfterAnnouncement = waitingAfterAnnouncement; }
    public string Id { get { return "scenario.rest"; } }
    public void Register(NpcCatalogBuilder catalog)
    {
        catalog.Trait(new TraitDefinition("restful", "Restful", false, false, null, new TraitEffect(DecisionKind.Supplies, 30)));
        catalog.Trait(new TraitDefinition("rested_resolve", "Rested resolve", true, false, "restful", new TraitEffect(DecisionKind.Courage, 5)));
        for (int i = 0; i < 12; i++) catalog.Fact("rest.fact." + i.ToString("D2"));
        catalog.Event(new NpcEventDefinition("regained_stamina", NpcRecordCategory.Life, true,
            e => e.Subject + " regained stamina by taking a breath.", required: NpcEventFields.Subject | NpcEventFields.PositiveUnits));
        catalog.Memory(new MemoryDefinition("successful_rest", "Recovered stamina", 1, 1,
            new[] { new MemoryTrigger("regained_stamina", (a, e) => a == e.Subject) }, new MemoryOutcome(null, "rested_resolve", null)));
        catalog.Value(new NpcValueDefinition("stamina", "Recover stamina", null, m => 60 + m.Supplies, true));
        var rest = new NpcIntentDefinition("take_breath", "Take a breath", 60, 180);
        rest.PauseWhenTired = false;
        rest.WaitingAfterAnnouncement = waitingAfterAnnouncement;
        rest.ResultFacts = (c, g) => c.Facts.Mask("rest.fact.11");
        rest.BuildPlan = d => d.Add("rest", d.Owner.Location, d.Owner.PersonalityIdentity,
            default(NpcPlanningState), d.Catalog.Facts.Mask("rest.fact.11"), d.Catalog.Facts.Mask("rest.fact.11"), default(NpcPlanningState), 1);
        catalog.Capability(rest);
        catalog.Operator(new NpcOperatorDefinition("rest", null, c => new RestAction(c)));
        catalog.GoalSource(this);
    }
    public void Evaluate(NpcGoalContext context, NpcGoalOffers offers)
    {
        if (!context.Owner.Personality.HasTrait("restful")) return;
        int desired = Rules.STAMINA_MIN_FOR_ACTIVITY + Rules.STAMINA_REGEN_WAIT;
        offers.Add(context.Self, "stamina", "take_breath", context.Owner.StaminaPoints, desired,
            Math.Min(100, Math.Max(0, desired - context.Owner.StaminaPoints) * 100 / Rules.STAMINA_REGEN_WAIT), 100);
    }
    sealed class RestAction : ActorAction
    {
        readonly NpcExecutionContext context;
        public RestAction(NpcExecutionContext context) : base(context.Owner, context.Game) { this.context = context; }
        public override bool IsLegal()
        { return NpcPlanExecution.Owned(m_Game, m_Actor, context.Goal) && context.Goal.Plan.Current == context.Step &&
            context.Step.OperatorId == "rest" && m_Actor.StaminaPoints < Rules.STAMINA_MIN_FOR_ACTIVITY + Rules.STAMINA_REGEN_WAIT; }
        public override void Perform()
        {
            if (!IsLegal()) return;
            int before = m_Actor.StaminaPoints; m_Game.DoWait(m_Actor);
            if (m_Actor.StaminaPoints <= before) return;
            var source = new SignificantEvent("regained_stamina", m_Actor, null, m_Actor.Location.Map, m_Actor.Location.Position,
                m_Actor.Location.Map.LocalTime.TurnCounter, causeId: context.Goal.CauseId, storyId: context.Goal.StoryId) { Units = m_Actor.StaminaPoints - before };
            PersonalitySystem.Report(m_Game, source);
            NpcIntentSystem.Finish(m_Actor, context.Goal, NpcIntentStatus.Completed, "actually regained stamina");
        }
    }
}
