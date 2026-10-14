using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality.Axes;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    // Short-lived reactions change recoverable sanity, never saved disposition experience.
    static class NpcImmediateEmotion
    {
        public static void Apply(RogueGame game, Actor actor, int delta)
        {
            if (delta == 0 || actor == null || actor.IsPlayer || actor.Personality == null ||
                !actor.Model.Abilities.HasSanity || !Session.Get.GamePreset.NpcPersonalitiesEnabled) return;
            actor.Sanity = Math.Max(0, Math.Min(game.Rules.ActorMaxSanity(actor), actor.Sanity + delta));
        }

        public static void Observe(RogueGame game, Actor observer, SignificantEvent e)
        {
            if (observer.IsPlayer || !observer.Model.Abilities.HasSanity ||
                !Session.Get.GamePreset.NpcPersonalitiesEnabled) return;
            int delta;
            switch (e.Kind)
            {
                case "kill_human": case "murder":
                    delta = HumanLifeAxis.ImmediateSanity(observer, e,
                        PersonalitySystem.Disposition(observer, NpcPersonalityAxis.HumanLife, game.NpcContent));
                    break;
                case "helped": case "abandoned":
                    delta = CareCircleAxis.ImmediateSanity(observer, e,
                        PersonalitySystem.Disposition(observer, NpcPersonalityAxis.CareCircle, game.NpcContent));
                    break;
                case "starvation": case "supplies_lost": case "base_theft":
                    delta = ScarcityAxis.ImmediateSanity(observer, e,
                        PersonalitySystem.Disposition(observer, NpcPersonalityAxis.Scarcity, game.NpcContent));
                    break;
                case "boundary_defied": case "boundary_accepted": case "resource_yielded": case "contested_taken":
                    delta = PatienceAxis.ImmediateSanity(observer, e,
                        PersonalitySystem.Disposition(observer, NpcPersonalityAxis.Patience, game.NpcContent));
                    break;
                default: return;
            }
            Apply(game, observer, delta);
        }

        public static int BondDeathLoss(Actor actor, NpcContentCatalog catalog)
        {
            if (actor.IsPlayer || actor.Personality == null || !Session.Get.GamePreset.NpcPersonalitiesEnabled)
                return Rules.SANITY_HIT_BOND_DEATH;
            int life = PersonalitySystem.Disposition(actor, NpcPersonalityAxis.HumanLife, catalog);
            int care = PersonalitySystem.Disposition(actor, NpcPersonalityAxis.CareCircle, catalog);
            if (actor.Personality.HasTrait("maniac") || life <= -25 && care <= -20)
                return WorldTime.TURNS_PER_HOUR / 4;
            if (actor.Personality.HasTrait("kind") || actor.Personality.HasTrait("loyal") || care >= 20)
                return 3 * WorldTime.TURNS_PER_HOUR;
            return WorldTime.TURNS_PER_HOUR;
        }

        public static int Radio(Actor listener, NpcFact fact, NpcContentCatalog catalog)
        {
            if (listener.Personality == null || !listener.Model.Abilities.HasSanity) return 0;
            RelationshipRecord subject = fact.NamesSubject ? listener.Personality.Person(fact.SubjectId) : null;
            RelationshipRecord other = fact.NamesOther ? listener.Personality.Person(fact.OtherId) : null;
            bool closeSubject = subject != null && (subject.Attachment >= 20 || subject.Feeling >= 30);
            bool closeOther = other != null && (other.Attachment >= 20 || other.Feeling >= 30);
            bool npc = !listener.IsPlayer && Session.Get.GamePreset.NpcPersonalitiesEnabled;
            bool timid = listener.Personality.HasTrait("timid") || listener.Personality.HasTrait("fearful");
            if (closeSubject && (fact.Kind == "attack" || fact.Kind == "death" || fact.Kind == "murder"))
            {
                int life = npc ? PersonalitySystem.Disposition(listener, NpcPersonalityAxis.HumanLife, catalog) : 0;
                int care = npc ? PersonalitySystem.Disposition(listener, NpcPersonalityAxis.CareCircle, catalog) : 0;
                if (npc && (listener.Personality.HasTrait("maniac") || life <= -25 && care <= -20)) return 0;
                return timid || npc && (life >= 20 || care >= 20 || listener.Personality.HasTrait("kind")) ? -2 : -1;
            }
            if ((fact.Kind == "stolen_goods_found" || fact.Kind == "stolen_goods_passed") &&
                (closeOther || fact.OtherId == listener.PersonalityIdentity ||
                 listener.SocialGroup != null && listener.SocialGroup.Identity == fact.ClaimantGroupId))
                return timid || npc && (PersonalitySystem.Disposition(listener, NpcPersonalityAxis.Scarcity, catalog) >= 20 ||
                    listener.Personality.HasTrait("frugal")) ? -2 : -1;
            if (fact.Kind == "psychopaths_arrival" && timid &&
                (!npc || PersonalitySystem.Disposition(listener, NpcPersonalityAxis.MessageTrust, catalog) >= -20)) return -1;
            if ((closeSubject || closeOther) &&
                (fact.Kind == "shared_food" || fact.Kind == "shared_medicine" || fact.Kind == "promise_kept"))
            {
                int care = npc ? PersonalitySystem.Disposition(listener, NpcPersonalityAxis.CareCircle, catalog) : 0;
                if (npc && care <= -25 && (listener.Personality.HasTrait("cruel") ||
                    listener.Personality.HasTrait("maniac"))) return 0;
                return npc && (care >= 20 || listener.Personality.HasTrait("kind")) ? 2 : 1;
            }
            return 0;
        }
    }
}
