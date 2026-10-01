using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcEpisodeProgress
    {
        public static string FoodDelivery(RogueGame game, NpcStory story, SignificantEvent source)
        {
            if (source.Other != null && story.Roles.Exists(r => r.ActorId == source.Subject.PersonalityIdentity &&
                r.TargetId == source.Other.PersonalityIdentity && game.NpcContent.Capability(r.Goal) != null && game.NpcContent.Capability(r.Goal).ReportAfterDelivery)) return "delivered";
            return story.RequiresDeliveryReport || story.Roles.Exists(r => game.NpcContent.Capability(r.Goal) != null && game.NpcContent.Capability(r.Goal).ReportAfterDelivery) || !story.Roles.TrueForAll(r => r.Status == NpcIntentStatus.Completed) ? null : "completed";
        }
    }
}
