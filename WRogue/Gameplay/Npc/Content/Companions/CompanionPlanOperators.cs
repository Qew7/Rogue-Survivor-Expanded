using System;
using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class CompanionPlanOperators
    {
        public static void Build(NpcPlanDomain d, bool warn)
        {
            IList<Actor> visible = d.Visible;
            ulong targetAt = d.At(d.Goal.LastKnown);
                Actor target = NpcIntentSystem.VisibleTarget(visible, d.Goal.TargetId);
                if (target != null || !NpcKnowledgeSystem.Visible(d.Game, d.Owner, d.Goal.LastKnown)) d.Initial |= (ulong)NpcPlanFact.LocationKnown;
                d.Travel(d.Goal.LastKnown, d.Goal.TargetId, targetAt, (ulong)NpcPlanFact.LocationKnown);
                d.Add(!warn ? NpcPlanAction.Reunite : NpcPlanAction.Warn,
                    d.Goal.LastKnown, d.Goal.TargetId, targetAt | (ulong)NpcPlanFact.LocationKnown, 0, (ulong)(warn ? NpcPlanFact.Warned : NpcPlanFact.Contact), 0, 1);
                if (warn && d.Owner.Personality.HasAttachments && d.Owner.Personality.Attachments.Exists(a => a.Kind == "place" && a.MissingUnits > 0 && a.Resource == "food" && a.Person == d.Goal.TargetId))
                {
                    d.Actions.RemoveAll(a => a.Action == NpcPlanAction.Warn);
                    d.Add(NpcPlanAction.Warn, d.Goal.LastKnown, d.Goal.TargetId, targetAt | (ulong)NpcPlanFact.LocationKnown, 0, (ulong)NpcPlanFact.Warned, 0,
                        5 - PersonalitySystem.Bias(d.Owner, DecisionKind.Courage) / 5 - PersonalitySystem.Bias(d.Owner, DecisionKind.Law) / 10);
                    d.Add(NpcPlanAction.DemandRestitution, d.Goal.LastKnown, d.Goal.TargetId, targetAt | (ulong)NpcPlanFact.LocationKnown, 0, (ulong)NpcPlanFact.Warned, 0,
                        8 - PersonalitySystem.Bias(d.Owner, DecisionKind.Supplies) / 3 - PersonalitySystem.Bias(d.Owner, DecisionKind.Trade) / 3);
                }
                if (target == null) foreach (Actor peer in visible)
                {
                    var question = new Engine.Actions.ActionNpcAskLocation(d.Owner, d.Game, peer, d.Goal);
                    if (question.IsLegal()) d.Add(NpcPlanAction.AskLocation, peer.Location, peer.PersonalityIdentity,
                        0, (ulong)NpcPlanFact.LocationKnown, (ulong)NpcPlanFact.LocationKnown, 0, 4);
                }
        }
    }
}
