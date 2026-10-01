using System;
using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class SafetyPlanOperators
    {
        public static void Shelter(NpcPlanDomain d)
        {
                ulong at = d.At(d.Goal.Destination);
                if (!d.Owner.Location.Map.GetTileAt(d.Owner.Location.Position).IsInside) d.Initial &= ~at;
                d.Travel(d.Goal.Destination, Guid.Empty, at);
                d.Add(NpcPlanAction.EnterShelter, d.Goal.Destination, Guid.Empty, at, 0, (ulong)NpcPlanFact.Sheltered, 0, 1);
        }
        public static void Avoid(NpcPlanDomain d)
        {
                const ulong away = NpcFactLayout.Away;
                if (d.Goal.LastKnown.Map != d.Owner.Location.Map || d.Game.Rules.GridDistance(d.Owner.Location.Position, d.Goal.LastKnown.Position) >= 5) d.Initial |= away;
                d.Add(NpcPlanAction.Retreat, d.Goal.LastKnown, d.Goal.TargetId, 0, away, away, 0, 2);
                d.Add(NpcPlanAction.ConfirmSafety, d.Goal.LastKnown, d.Goal.TargetId, away, 0, (ulong)NpcPlanFact.Safe, 0, 1);
        }
        public static void Leave(NpcPlanDomain d)
        { d.Add(NpcPlanAction.LeaveGroup, d.Goal.LastKnown, d.Goal.TargetId, 0, 0, (ulong)NpcPlanFact.Left, 0, 1); }
        public static Location ShelterDestination(NpcExecutionContext context)
        {
                    Location place = context.Step.Place;
                    if (place.Map == context.Owner.Location.Map)
                    {
                        int best = Int32.MaxValue;
                        for (int x = context.Step.Place.Position.X - 1; x <= context.Step.Place.Position.X + 1; x++)
                            for (int y = context.Step.Place.Position.Y - 1; y <= context.Step.Place.Position.Y + 1; y++)
                            {
                                Map map = place.Map;
                                if (!map.IsInBounds(x, y) || !map.GetTileAt(x, y).IsInside || map.GetActorAt(x, y) != null) continue;
                                Location candidate = new Location(map, new Point(x, y)); if (!NpcKnowledgeSystem.Visible(context.Game, context.Owner, candidate)) continue;
                                int d = context.Game.Rules.GridDistance(context.Owner.Location.Position, candidate.Position);
                                if (d < best) { place = candidate; best = d; }
                            }
                    }
            return place;
        }
    }
}
