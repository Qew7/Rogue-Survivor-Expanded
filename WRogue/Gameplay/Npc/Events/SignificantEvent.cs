using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class SignificantEvent
    {
        public long Id { get; internal set; }
        public readonly long CauseId;
        public readonly string StoryId;
        public readonly string Kind;
        public readonly Actor Subject;
        public readonly bool SubjectIsDirect;
        public readonly Actor Other;
        public readonly bool OtherIsDirect;
        public readonly Map Map;
        public readonly System.Drawing.Point Position;
        public readonly int Turn;
        public NpcGroupPlan Task;
        public string Resource;
        public Location ResourcePlace;
        public int Units, ModelId = -1;
        public Guid ItemId;

        public SignificantEvent(string kind, Actor subject, Actor other, Map map,
            System.Drawing.Point position, int turn, bool otherIsDirect = true,
            bool subjectIsDirect = true, long causeId = 0, string storyId = null)
        {
            CauseId = causeId; StoryId = storyId;
            Kind = kind;
            Subject = subject;
            SubjectIsDirect = subjectIsDirect;
            Other = other;
            OtherIsDirect = otherIsDirect;
            Map = map;
            Position = position;
            Turn = turn;
        }
    }
}
