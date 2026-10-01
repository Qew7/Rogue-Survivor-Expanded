using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine.Actions
{
    sealed class ActionNpcRestitutionDemand : ActorAction
    {
        readonly NpcIntent goal;
        readonly NpcPlanStep step;
        readonly Actor target;
        public ActionNpcRestitutionDemand(Actor actor, RogueGame game, NpcIntent goal, NpcPlanStep step, Actor target) : base(actor, game)
        { this.goal = goal; this.step = step; this.target = target; }
        public override bool IsLegal()
        { return NpcPlanExecution.Owned(m_Game, m_Actor, goal) && goal.Plan != null && goal.Plan.Current == step && step.Action == NpcPlanAction.DemandRestitution &&
            target != null && target.PersonalityIdentity == goal.TargetId && !target.IsSleeping && NpcIntentSystem.CanSee(m_Game, m_Actor, target) &&
            NpcPlanExecution.Near(m_Game, m_Actor, target.Location) && !m_Game.Rules.AreEnemies(m_Actor, target) &&
            m_Actor.Personality.Attachments.Exists(a => a.Kind == "place" && a.MissingUnits > 0 && a.Resource == "food" && a.Person == target.PersonalityIdentity); }
        public override void Perform()
        {
            if (!IsLegal()) return;
            NpcAttachment loss = m_Actor.Personality.Attachments.Find(a => a.Kind == "place" && a.MissingUnits > 0 && a.Resource == "food" && a.Person == target.PersonalityIdentity);
            m_Game.DoSay(m_Actor, target, "Replace the " + loss.MissingUnits + " food units lost from our supplies.", RogueGame.Sayflags.IS_STORY | RogueGame.Sayflags.IS_REQUEST,
                goal.CauseId, goal.StoryId);
            var source = new SignificantEvent("restitution_requested", m_Actor, target, m_Actor.Location.Map, m_Actor.Location.Position,
                m_Actor.Location.Map.LocalTime.TurnCounter, causeId: goal.CauseId, storyId: goal.StoryId) { Units = loss.MissingUnits, ResourcePlace = loss.Place, Resource = "food" };
            PersonalitySystem.Report(m_Game, source);
            NpcIntentSystem.Finish(m_Game.NpcContent, m_Actor, goal, NpcIntentStatus.Completed,
                "actually demanded compensation without predicting compliance");
        }
    }
}
