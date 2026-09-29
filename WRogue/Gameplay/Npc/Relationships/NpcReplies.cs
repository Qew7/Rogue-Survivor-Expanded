using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcReplies
    {
        internal static void Reply(Actor owner, Actor target, SignificantEvent source, string kind, string yes, string no)
        {
            if (target == null || owner.Personality.Reactions.Count >= 4) return;
            owner.Personality.Reactions.Add(new NpcReaction(target, kind.EndsWith("accepted") || kind.EndsWith("yielded") ? yes : no,
                source.Id, source.Turn, kind, source.StoryId));
        }
    }
}
