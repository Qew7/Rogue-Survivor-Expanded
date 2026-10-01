using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcEvents
    {
        public static SignificantEvent Publish(RogueGame game, string kind, Actor subject, Actor other, long cause = 0, string story = null)
        {
            SignificantEvent source = new SignificantEvent(kind, subject, other, subject.Location.Map,
                subject.Location.Position, subject.Location.Map.LocalTime.TurnCounter, causeId: cause, storyId: story);
            PersonalitySystem.Report(game, source); return source;
        }
    }
}
