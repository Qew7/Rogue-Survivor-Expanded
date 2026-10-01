using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.Actions;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcGroupSupplyActions
    {
        public static ActorAction Stage(NpcActionContext c)
        {
            if (c.Goal.Progress == 0) return NpcFoodActions.Take(c);
            if (c.Goal.Progress == 1) return NpcFoodActions.Give(c);
            return Report(c);
        }
        public static ActorAction Report(NpcActionContext c)
        { return c.Action(() => c.Goal.Progress >= 2 && c.NearPerson(c.Goal.CoordinatorId), () => {
            c.Game.DoSay(c.Owner, c.Target, "The delivery is complete.", RogueGame.Sayflags.NONE);
            c.Publish("supplies_delivered", c.Target); c.Done((ulong)NpcPlanFact.Reported);
        }); }
    }
}
