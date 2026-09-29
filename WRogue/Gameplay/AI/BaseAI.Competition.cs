using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Gameplay.AI
{
    abstract partial class BaseAI
    {
        ActorAction ResourceAccess(RogueGame game, NpcIntent goal, NpcPlanStep step, List<Actor> visible, string resource, out bool allowed)
        {
            Guid id = Session.Get.NpcDirector.ReservationOwner(step.Place, goal.StoryId);
            if (id == Guid.Empty || id == m_Actor.PersonalityIdentity) { allowed = Session.Get.NpcDirector.Reserve(goal.StoryId, step.Place); return null; }
            allowed = false; Actor holder = NpcIntentSystem.VisibleTarget(visible, id);
            if (holder == null) return null;
            NpcResourceDispute dispute = m_Actor.Personality.Dispute(step.Place, id);
            if (dispute != null && dispute.Response == "resource_refused" &&
                PersonalitySystem.Bias(m_Actor, DecisionKind.Law) + PersonalitySystem.Bias(m_Actor, DecisionKind.Courage) / 2 < 0)
            { allowed = true; return null; }
            var action = new ActionNpcResourceConflict(m_Actor, game, goal, step, holder, resource);
            return action.IsLegal() ? action : null;
        }
    }
}
