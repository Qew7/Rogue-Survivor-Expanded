using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcConflictActions
    {
        static bool Talkable(NpcActionContext c) { return c.NearPerson(c.Goal.TargetId); }
        public static ActorAction Threaten(NpcActionContext c)
        { return c.Action(() => Talkable(c), () => {
            c.Game.DoSay(c.Owner, c.Target, "Keep away from us. Next time I'll fight back.", RogueGame.Sayflags.NONE);
            c.Publish("threatened", c.Target); c.Done(c.Game.NpcContent.Facts.Mask("ThreatCommunicated"));
        }); }
        public static ActorAction Apologize(NpcActionContext c)
        { return c.Action(() => Talkable(c), () => {
            c.Game.DoSay(c.Owner, c.Target, "I let you down. I'm sorry. Can we try again?", RogueGame.Sayflags.NONE);
            c.Publish("apologized", c.Target); c.Done(c.Game.NpcContent.Facts.Mask("ApologyCommunicated"));
        }); }
        public static ActorAction Expel(NpcActionContext c)
        { return c.Action(() => Talkable(c) && c.Target.Leader == c.Owner && c.Owner.SocialGroup != null &&
            c.Owner.SocialGroup.LeaderId == c.Owner.PersonalityIdentity, () => {
            c.Game.DoSay(c.Owner, c.Target, "You've put us at risk. You must leave the group.", RogueGame.Sayflags.NONE);
            c.Owner.RemoveFollower(c.Target); c.Target.TrustInLeader = Rules.TRUST_NEUTRAL;
            c.Publish("member_expelled", c.Target); c.Done(c.Game.NpcContent.Facts.Mask("MemberExpelled"));
        }); }
        public static ActorAction Retaliate(NpcActionContext c) { return Strike(c, "retaliated", "Retaliated"); }
        public static ActorAction Defend(NpcActionContext c) { return Strike(c, "defended_person", "Defended"); }
        static ActorAction Strike(NpcActionContext c, string kind, string fact)
        {
            return c.Action(() => c.Target != null && c.Target.PersonalityIdentity == c.Goal.TargetId && !c.Target.IsDead &&
                NpcIntentSystem.CanSee(c.Game, c.Owner, c.Target) && c.Game.Rules.CanActorMeleeAttack(c.Owner, c.Target), () => {
                c.Game.DoMeleeAttack(c.Owner, c.Target, c.Goal);
                c.Publish(kind, c.Target); c.Done(c.Game.NpcContent.Facts.Mask(fact));
            });
        }
    }
}
