using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Gameplay.Personality.Axes
{
    static class HumanLifeAxis
    {
        public static int Faction(NpcFactionPolicy policy) { return policy.HumanLife; }
        public static int Context(Actor actor)
        { return actor.Model.Abilities.HasSanity && actor.Sanity < actor.Sheet.BaseSanity / 4 ? -5 : 0; }

        public static int Trait(TraitEffect effect)
        {
            if (effect.Axis == NpcPersonalityAxis.HumanLife) return effect.Amount;
            if (effect.Axis == NpcPersonalityAxis.Compassion) return effect.Amount * 3 / 4;
            if (effect.Axis == NpcPersonalityAxis.Law) return effect.Amount / 4;
            return 0;
        }

        public static int ImmediateSanity(Actor observer, SignificantEvent e, int value)
        {
            if (e.Kind == "kill_human" && observer == e.Other)
                return value <= -25 || observer.Personality.HasTrait("maniac") ? 0 :
                    value >= 20 || observer.Personality.HasTrait("kind") ? -3 : -1;
            if (e.Kind == "murder" && observer != e.Other &&
                e.Subject != null && observer.Leader != e.Subject && e.Subject.Leader != observer)
                return value <= -25 || observer.Personality.HasTrait("maniac") ? 0 :
                    value >= 20 || observer.Personality.HasTrait("kind") ? -2 : -1;
            return 0;
        }
    }
}
