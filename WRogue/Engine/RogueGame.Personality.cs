using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine
{
    partial class RogueGame
    {
        void ReportPersonalityEvent(string kind, Actor subject, Actor other, Map map, Point position,
            bool otherIsDirect = true, bool subjectIsDirect = true)
        {
            if (map == null || !m_Session.GamePreset.NpcPersonalitiesEnabled) return;
            PersonalitySystem.Report(this, new SignificantEvent(kind, subject, other,
                map, position, map.LocalTime.TurnCounter, otherIsDirect, subjectIsDirect));
        }
    }
}
