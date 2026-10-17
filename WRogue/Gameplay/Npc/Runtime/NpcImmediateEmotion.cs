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

        public static void Observe(RogueGame game, Actor observer, SignificantEvent e, NpcEventDefinition definition)
        {
            if (definition == null || definition.ImmediateSanity == null || observer.IsPlayer || !observer.Model.Abilities.HasSanity ||
                !Session.Get.GamePreset.NpcPersonalitiesEnabled) return;
            Apply(game, observer, definition.ImmediateSanity(observer, e, game.NpcContent));
        }

        public static int HumanLife(Actor observer, SignificantEvent e, NpcContentCatalog catalog)
        { return HumanLifeAxis.ImmediateSanity(observer, e, PersonalitySystem.Disposition(observer, NpcPersonalityAxis.HumanLife, catalog)); }
        public static int CareCircle(Actor observer, SignificantEvent e, NpcContentCatalog catalog)
        { return CareCircleAxis.ImmediateSanity(observer, e, PersonalitySystem.Disposition(observer, NpcPersonalityAxis.CareCircle, catalog)); }
        public static int Scarcity(Actor observer, SignificantEvent e, NpcContentCatalog catalog)
        { return ScarcityAxis.ImmediateSanity(observer, e, PersonalitySystem.Disposition(observer, NpcPersonalityAxis.Scarcity, catalog)); }
        public static int Patience(Actor observer, SignificantEvent e, NpcContentCatalog catalog)
        { return PatienceAxis.ImmediateSanity(observer, e, PersonalitySystem.Disposition(observer, NpcPersonalityAxis.Patience, catalog)); }

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
            NpcEventDefinition definition = catalog.Event(fact.Kind);
            return definition == null || definition.RadioSanity == null ? 0 : definition.RadioSanity(listener, fact, catalog);
        }

        static bool Close(Actor listener, Guid id)
        {
            RelationshipRecord opinion = listener.Personality.Person(id);
            return opinion != null && (opinion.Attachment >= 20 || opinion.Feeling >= 30);
        }

        public static int RadioCloseLoss(Actor listener, NpcFact fact, NpcContentCatalog catalog)
        {
            if (!fact.NamesSubject || !Close(listener, fact.SubjectId)) return 0;
            bool npc = !listener.IsPlayer && Session.Get.GamePreset.NpcPersonalitiesEnabled;
            bool timid = listener.Personality.HasTrait("timid") || listener.Personality.HasTrait("fearful");
            int life = npc ? PersonalitySystem.Disposition(listener, NpcPersonalityAxis.HumanLife, catalog) : 0;
            int care = npc ? PersonalitySystem.Disposition(listener, NpcPersonalityAxis.CareCircle, catalog) : 0;
            if (npc && (listener.Personality.HasTrait("maniac") || life <= -25 && care <= -20)) return 0;
            return timid || npc && (life >= 20 || care >= 20 || listener.Personality.HasTrait("kind")) ? -2 : -1;
        }

        public static int RadioStolenGoods(Actor listener, NpcFact fact, NpcContentCatalog catalog)
        {
            if ((!fact.NamesOther || !Close(listener, fact.OtherId)) &&
                fact.OtherId != listener.PersonalityIdentity &&
                (listener.SocialGroup == null || listener.SocialGroup.Identity != fact.ClaimantGroupId)) return 0;
            bool timid = listener.Personality.HasTrait("timid") || listener.Personality.HasTrait("fearful");
            bool npc = !listener.IsPlayer && Session.Get.GamePreset.NpcPersonalitiesEnabled;
            return timid || npc && (PersonalitySystem.Disposition(listener, NpcPersonalityAxis.Scarcity, catalog) >= 20 ||
                listener.Personality.HasTrait("frugal")) ? -2 : -1;
        }

        public static int RadioPsychopathArrival(Actor listener, NpcFact fact, NpcContentCatalog catalog)
        {
            if (!listener.Personality.HasTrait("timid") && !listener.Personality.HasTrait("fearful")) return 0;
            return listener.IsPlayer || !Session.Get.GamePreset.NpcPersonalitiesEnabled ||
                PersonalitySystem.Disposition(listener, NpcPersonalityAxis.MessageTrust, catalog) >= -20 ? -1 : 0;
        }

        public static int RadioGoodNews(Actor listener, NpcFact fact, NpcContentCatalog catalog)
        {
            if ((!fact.NamesSubject || !Close(listener, fact.SubjectId)) &&
                (!fact.NamesOther || !Close(listener, fact.OtherId))) return 0;
            bool npc = !listener.IsPlayer && Session.Get.GamePreset.NpcPersonalitiesEnabled;
            int care = npc ? PersonalitySystem.Disposition(listener, NpcPersonalityAxis.CareCircle, catalog) : 0;
            if (npc && care <= -25 && (listener.Personality.HasTrait("cruel") ||
                listener.Personality.HasTrait("maniac"))) return 0;
            return npc && (care >= 20 || listener.Personality.HasTrait("kind")) ? 2 : 1;
        }
    }
}
