using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    static partial class NpcStorySystem
    {
        public static NpcGroupPlan ProposeGroupPlan(RogueGame game, Actor leader, IList<Actor> visible)
        { return Propose(game, leader, visible, NpcCollectiveScope.Group); }
        public static NpcGroupPlan ProposeFactionPlan(RogueGame game, Actor leader, IList<Actor> visible)
        { return Propose(game, leader, visible, NpcCollectiveScope.Faction); }
        static NpcGroupPlan Propose(RogueGame game, Actor leader, IList<Actor> visible, NpcCollectiveScope scope)
        {
            if (!NpcIntentSystem.Enabled(leader)) return null;
            int turn = leader.Location.Map.LocalTime.TurnCounter;
            SocialGroup group = leader.SocialGroup;
            if (scope == NpcCollectiveScope.Group ? group == null || group.LeaderId != leader.PersonalityIdentity ||
                group.Members.Count < 2 || turn < group.NextPlanTurn || group.Plan != null && !group.Plan.Finished :
                turn < leader.Personality.NextFactionPlanTurn || leader.Personality.FactionPlan != null && !leader.Personality.FactionPlan.Finished) return null;
            var context = new NpcCollectiveContext(game, leader, visible); NpcCollectiveOffer best = null;
            string key = scope == NpcCollectiveScope.Group ? "group:" + group.Identity : "faction:" + leader.Faction.ID + ":" + leader.PersonalityIdentity;
            foreach (NpcCollectiveDefinition definition in game.NpcContent.Collectives)
            {
                if (definition.Scope != scope) continue;
                NpcCollectiveOffer candidate = definition.Propose(context);
                if (candidate == null || candidate.Priority < 15 || candidate.Plan == null || candidate.Plan.Kind != definition.Id ||
                    candidate.Plan.Destination.Map == null || candidate.Plan.Deadline <= turn ||
                    !Session.Get.NpcDirector.CanOpen(leader.Location.Map, turn, key, candidate.Plan.Destination, candidate.Plan.CollectorId)) continue;
                if (best == null || candidate.Priority > best.Priority) best = candidate;
            }
            return best == null ? null : best.Plan;
        }
        public static void AcceptCollective(RogueGame game, Actor owner, SignificantEvent source)
        {
            if (!NpcIntentSystem.Enabled(owner) || source.Task == null || source.Subject == null) return;
            NpcCollectiveDefinition definition = game.NpcContent.Collective(source.Task.Kind);
            if (definition == null || (definition.Scope == NpcCollectiveScope.Group ?
                owner.SocialGroup == null || owner.SocialGroup != source.Subject.SocialGroup :
                owner.Faction == null || source.Subject.Faction == null || owner.Faction.ID != source.Subject.Faction.ID)) return;
            definition.Accept(game, owner, source);
        }
    }
}
