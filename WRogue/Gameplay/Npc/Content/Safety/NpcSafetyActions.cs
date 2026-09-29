using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.Actions;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcSafetyActions
    {
        public static ActorAction Leave(NpcActionContext c)
        { return c.Action(() => c.Owner.Leader != null && c.Owner.Leader.PersonalityIdentity == c.Goal.TargetId, () => {
            Actor leader = c.Owner.Leader;
            if (NpcIntentSystem.CanSee(c.Game, c.Owner, leader)) c.Game.DoSay(c.Owner, leader, "After what happened, I won't stay in your group.", RogueGame.Sayflags.IS_FREE_ACTION);
            c.Game.SpendActorActionPoints(c.Owner, Rules.BASE_ACTION_COST);
            c.Owner.SetTrustIn(leader, c.Owner.TrustInLeader); leader.RemoveFollower(c.Owner); c.Owner.TrustInLeader = Rules.TRUST_NEUTRAL;
            var ai = c.Owner.Controller as djack.RogueSurvivor.Gameplay.AI.BaseAI; if (ai != null) ai.SetOrder(null);
            c.Publish("left_group", leader); c.Done((ulong)NpcPlanFact.Left);
        }); }
        public static ActorAction Confirm(NpcActionContext c)
        { return c.Action(() => c.Goal.LastKnown.Map != c.Owner.Location.Map || c.Game.Rules.GridDistance(c.Owner.Location.Position, c.Goal.LastKnown.Position) >= 5, () => {
            c.Game.DoWait(c.Owner); c.Publish("withdrew"); c.Done((ulong)NpcPlanFact.Safe);
        }); }
        public static ActorAction Shelter(NpcActionContext c)
        { return c.Action(() => c.Goal.Destination.Map == c.Owner.Location.Map && NpcPlanExecution.Near(c.Game, c.Owner, c.Goal.Destination) &&
            c.Owner.Location.Map.GetTileAt(c.Owner.Location.Position).IsInside, () => {
            c.Game.DoWait(c.Owner);
            NpcValueDefinition value = c.Goal.Generated == null ? null : c.Game.NpcContent.Value(c.Goal.Generated);
            c.Publish(value == null || value.ArrivalEvent == null ? "shelter_reached" : value.ArrivalEvent);
            c.Done((ulong)NpcPlanFact.Sheltered);
        }); }
    }
}
