using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.Actions;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcContactActions
    {
        public static ActorAction Reunite(NpcActionContext c)
        { return c.Action(() => c.NearPerson(c.Goal.TargetId), () => {
            c.Game.DoSay(c.Owner, c.Target, "There you are. I was looking for you.", RogueGame.Sayflags.NONE);
            c.Publish("reunited", c.Target); c.Done((ulong)NpcPlanFact.Contact);
        }); }
        public static ActorAction Warn(NpcActionContext c)
        { return c.Action(() => c.NearPerson(c.Goal.TargetId), () => {
            c.Game.DoSay(c.Owner, c.Target, c.Owner.Personality.Knowledge.Facts.Exists(f => f.Kind == "promise_broken" && f.EventId == c.Goal.CauseId) ?
                "You promised to help. What happened?" : "I know what happened. Leave us and our belongings alone.", RogueGame.Sayflags.NONE);
            c.Publish("confronted", c.Target); c.Done((ulong)NpcPlanFact.Warned);
        }); }
    }
}
