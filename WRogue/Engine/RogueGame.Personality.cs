using System.Drawing;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine
{
    partial class RogueGame
    {
        long ReportPersonalityEvent(string kind, Actor subject, Actor other, Map map, Point position,
            bool otherIsDirect = true, bool subjectIsDirect = true, long causeId = 0, string storyId = null,
            string resource = null)
        {
            if (map == null || !m_Session.GamePreset.NpcPersonalitiesEnabled) return 0;
            SignificantEvent reported = new SignificantEvent(kind, subject, other,
                map, position, map.LocalTime.TurnCounter, otherIsDirect, subjectIsDirect, causeId: causeId, storyId: storyId)
                { Resource = resource };
            PersonalitySystem.Report(this, reported);
            return reported.Id;
        }

        void DiscoverXpdBaseLosses(Actor actor)
        {
            if (m_Session == null || !m_Session.GamePreset.NpcPersonalitiesEnabled || actor.IsDead || actor.IsSleeping ||
                actor.Model.Abilities.IsUndead || !actor.Model.Abilities.IsIntelligent ||
                actor.Personality == null && !actor.IsPlayer) return;
            Map map = actor.Location.Map;
            if (!map.HasUnnoticedBaseLosses) return;
            XpdBase claim = map.XpdBaseAt(actor.Location.Position);
            if (claim == null || !claim.Owns(actor)) return;
            IList<XpdBaseLoss> losses = claim.UnnoticedLosses;
            if (losses == null) return;
            for (int i = losses.Count - 1; i >= 0; i--)
            {
                XpdBaseLoss loss = losses[i];
                if (!NpcKnowledgeSystem.Visible(this, actor, new Location(map, loss.Position))) continue;
                if (loss.Victim != null)
                {
                    List<Corpse> corpses = map.GetCorpsesAt(loss.Position);
                    if (corpses == null || !corpses.Exists(c => c.DeadGuy == loss.Victim))
                    { map.RemoveUnnoticedBaseLoss(claim, loss); continue; }
                }
                map.RemoveUnnoticedBaseLoss(claim, loss);
                if (loss.Victim != null)
                    ReportPersonalityEvent("base_raid", actor, loss.Victim, map, loss.Position,
                        false, true, loss.CauseId, loss.StoryId);
                else
                    PersonalitySystem.Report(this, new SignificantEvent("base_robbed", actor, null,
                        map, loss.Position, map.LocalTime.TurnCounter, false, true, loss.CauseId, loss.StoryId)
                        { Resource = loss.Resource, Units = loss.Units });
            }
        }

        void ReportNewPersonalityArrivals(Map map, HashSet<Actor> before, string kind)
        {
            foreach (Actor actor in map.Actors)
                if (!before.Contains(actor))
                {
                    ReportPersonalityEvent(kind, actor, null, map, actor.Location.Position, false, false);
                    break;
                }
        }

        string RaidPersonalityKind(RaidType raid, Actor source)
        {
            switch (raid)
            {
                case RaidType.NATGUARD: return "national_guard_arrival";
                case RaidType.ARMY_SUPLLIES: return "army_supplies";
                case RaidType.SURVIVORS: return "survivors_arrival";
                case RaidType.BLACKOPS: return "blackops_raid";
                case RaidType.BIKERS:
                    if (source != null && source.GangID == (int)Gameplay.GameGangs.IDs.BIKER_HELLS_SOULS)
                        return "hells_souls_raid";
                    if (source != null && source.GangID == (int)Gameplay.GameGangs.IDs.BIKER_FREE_ANGELS)
                        return "free_angels_raid";
                    return "bikers_raid";
                case RaidType.GANGSTA:
                    if (source != null && source.GangID == (int)Gameplay.GameGangs.IDs.GANGSTA_CRAPS)
                        return "craps_raid";
                    if (source != null && source.GangID == (int)Gameplay.GameGangs.IDs.GANGSTA_FLOODS)
                        return "floods_raid";
                    return "gangstas_raid";
                default: return "raid";
            }
        }
    }
}
