using System.Drawing;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine
{
    partial class RogueGame
    {
        void ReportPersonalityEvent(string kind, Actor subject, Actor other, Map map, Point position,
            bool otherIsDirect = true, bool subjectIsDirect = true, long causeId = 0, string storyId = null)
        {
            if (map == null || !m_Session.GamePreset.NpcPersonalitiesEnabled) return;
            PersonalitySystem.Report(this, new SignificantEvent(kind, subject, other,
                map, position, map.LocalTime.TurnCounter, otherIsDirect, subjectIsDirect, causeId: causeId, storyId: storyId));
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
