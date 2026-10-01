using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Gameplay.Personality;
namespace djack.RogueSurvivor.Engine.Actions
{
    sealed class ActionNpcGroupPlan : ActorAction
    {
        readonly Actor listener;
        readonly NpcGroupPlan plan;
        NpcCollectiveDefinition Definition { get { return plan == null ? null : m_Game.NpcContent.Collective(plan.Kind); } }
        public ActionNpcGroupPlan(Actor actor, RogueGame game, Actor listener, NpcGroupPlan plan) : base(actor, game) { this.listener = listener; this.plan = plan; }
        public override bool IsLegal()
        { return NpcIntentSystem.Enabled(m_Actor) && !m_Actor.IsSleeping && Definition != null && plan.Destination.Map != null &&
            m_Actor.Location.Map.LocalTime.TurnCounter < plan.Deadline && listener != null && !listener.IsSleeping &&
            (Definition.Scope == NpcCollectiveScope.Group ? m_Actor.SocialGroup != null && m_Actor.SocialGroup.LeaderId == m_Actor.PersonalityIdentity && listener.SocialGroup == m_Actor.SocialGroup &&
                (m_Actor.SocialGroup.Plan == null || m_Actor.SocialGroup.Plan.Finished) :
                m_Actor.Personality.FactionPlan == null || m_Actor.Personality.FactionPlan.Finished) &&
            (Definition.Scope == NpcCollectiveScope.Group || listener.Faction != null && m_Actor.Faction != null && listener.Faction.ID == m_Actor.Faction.ID) &&
            Definition.CanCommunicate(m_Actor, listener, plan) &&
            NpcIntentSystem.CanSee(m_Game, m_Actor, listener) && m_Game.Rules.GridDistance(m_Actor.Location.Position, listener.Location.Position) <= 4 &&
            !m_Game.Rules.AreEnemies(m_Actor, listener) && Session.Get.NpcDirector.CanOpen(m_Actor.Location.Map,
                m_Actor.Location.Map.LocalTime.TurnCounter, ScopeKey(), plan.Destination, plan.CollectorId); }
        string ScopeKey() { return Definition.Scope == NpcCollectiveScope.Group ? "group:" + m_Actor.SocialGroup.Identity :
            "faction:" + m_Actor.Faction.ID + ":" + m_Actor.PersonalityIdentity; }
        public override void Perform()
        {
            if (!IsLegal()) return;
            SocialGroup group = Definition.Scope == NpcCollectiveScope.Group ? m_Actor.SocialGroup : null;
            string id = group != null ? "group:" + group.Identity.ToString("N") + ":" + (group.PlanSequence + 1) :
                "faction:" + m_Actor.Faction.ID + ":" + m_Actor.PersonalityIdentity.ToString("N") + ":" + m_Actor.Personality.NextFactionPlanSequence();
            NpcStory story = Session.Get.NpcDirector.Open(id, plan.Kind, m_Actor, plan.CauseId, plan.Deadline,
                ScopeKey(), plan.Destination, plan.CollectorId);
            if (story == null) return;
            story.RequiresDeliveryReport = Definition.RequiresDeliveryReport;
            plan.StoryId = id;
            if (group != null) { group.PlanSequence++; group.Plan = plan; group.NextPlanTurn = m_Actor.Location.Map.LocalTime.TurnCounter + 180; }
            else { m_Actor.Personality.FactionPlan = plan; m_Actor.Personality.NextFactionPlanTurn = m_Actor.Location.Map.LocalTime.TurnCounter + 180; }
            m_Game.DoSay(m_Actor, listener, Definition.Message(m_Actor, plan), RogueGame.Sayflags.IS_STORY | RogueGame.Sayflags.IS_REQUEST,
                plan.CauseId, id);
            if (Definition.CoordinatorCapability != null)
            {
                NpcIntent goal = NpcStorySystem.StartKnown(m_Game.NpcContent, m_Actor,
                    m_Actor.Personality.Knowledge.Person(plan.BeneficiaryId),
                    m_Game.NpcContent.Capability(Definition.CoordinatorCapability), plan.CauseId, id,
                    groupId: group == null ? Guid.Empty : group.Identity);
                if (goal != null) goal.Status = NpcIntentStatus.Waiting;
            }
            PersonalitySystem.Report(m_Game, new SignificantEvent(Definition.EventId, m_Actor, listener, m_Actor.Location.Map,
                m_Actor.Location.Position, m_Actor.Location.Map.LocalTime.TurnCounter, causeId: plan.CauseId, storyId: id) { Task = plan });
        }
    }
}
