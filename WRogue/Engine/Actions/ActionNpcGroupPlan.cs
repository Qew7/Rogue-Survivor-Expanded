using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine.Actions
{
    sealed class ActionNpcGroupPlan : ActorAction
    {
        readonly Actor listener;
        readonly NpcGroupPlan plan;
        public ActionNpcGroupPlan(Actor actor, RogueGame game, Actor listener, NpcGroupPlan plan) : base(actor, game) { this.listener = listener; this.plan = plan; }
        public override bool IsLegal()
        { return NpcIntentSystem.Enabled(m_Actor) && !m_Actor.IsSleeping && plan != null && plan.Destination.Map != null &&
            m_Actor.Location.Map.LocalTime.TurnCounter < plan.Deadline && listener != null && !listener.IsSleeping &&
            m_Actor.SocialGroup != null && m_Actor.SocialGroup.LeaderId == m_Actor.PersonalityIdentity && listener.SocialGroup == m_Actor.SocialGroup &&
            (plan.Kind == "group_shelter" || (plan.Kind == "group_supplies" && listener.PersonalityIdentity == plan.CollectorId && NpcIntentSystem.Enabled(listener))) &&
            (m_Actor.SocialGroup.Plan == null || m_Actor.SocialGroup.Plan.Finished) && NpcIntentSystem.CanSee(m_Game, m_Actor, listener) &&
            m_Game.Rules.GridDistance(m_Actor.Location.Position, listener.Location.Position) <= 4 &&
            !m_Game.Rules.AreEnemies(m_Actor, listener) &&
            Session.Get.NpcDirector.CanOpen(m_Actor.Location.Map, m_Actor.Location.Map.LocalTime.TurnCounter,
                "group:" + m_Actor.SocialGroup.Identity, plan.Destination, plan.CollectorId); }
        public override void Perform()
        {
            if (!IsLegal()) return;
            SocialGroup group = m_Actor.SocialGroup;
            string id = "group:" + group.Identity.ToString("N") + ":" + (group.PlanSequence + 1);
            NpcStory story = Session.Get.NpcDirector.Open(id, plan.Kind, m_Actor, plan.CauseId, plan.Deadline,
                "group:" + group.Identity, plan.Destination, plan.CollectorId);
            if (story == null) return;
            story.RequiresDeliveryReport = plan.Kind == "group_supplies";
            group.PlanSequence++; plan.StoryId = id; group.Plan = plan; group.NextPlanTurn = m_Actor.Location.Map.LocalTime.TurnCounter + 180;
            NpcKnownPerson beneficiary = m_Actor.Personality.Knowledge.Person(plan.BeneficiaryId);
            m_Game.DoSay(m_Actor, listener, plan.Kind == "group_supplies" ?
                "Fetch the food near " + plan.Destination.Map.Name + " for " + (beneficiary == null ? "our companion" : beneficiary.Name) + ". Then report back." :
                "Let's take shelter near " + plan.Destination.Map.Name + ".", RogueGame.Sayflags.NONE);
            if (plan.Kind == "group_supplies")
            {
                NpcIntent coordination = NpcStorySystem.StartKnown(m_Actor, beneficiary, NpcIntentContent.Coordinate,
                    plan.CauseId, id, groupId: group.Identity);
                if (coordination != null) coordination.Status = NpcIntentStatus.Waiting;
            }
            var source = new SignificantEvent(plan.Kind == "group_supplies" ? "supplies_requested" : "shelter_suggested",
                m_Actor, listener, m_Actor.Location.Map, m_Actor.Location.Position, m_Actor.Location.Map.LocalTime.TurnCounter,
                causeId: plan.CauseId, storyId: id) { Task = plan };
            PersonalitySystem.Report(m_Game, source);
        }
    }
}
