using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Gameplay.Personality.Axes
{
    static class MessageTrustAxis
    {
        public static int Faction(NpcFactionPolicy policy) { return policy.MessageTrust; }
        public static int Context(Actor actor)
        { return actor.Model.Abilities.HasSanity && actor.Sanity < actor.Sheet.BaseSanity / 4 ? -8 : 0; }

        public static int Trait(TraitEffect effect)
        {
            if (effect.Axis == NpcPersonalityAxis.MessageTrust) return effect.Amount;
            if (effect.Axis == NpcPersonalityAxis.Group) return effect.Amount / 2;
            if (effect.Axis == NpcPersonalityAxis.Law) return effect.Amount / 4;
            return 0;
        }

        public static int RefutedClaimSanity(int value)
        { return value >= 20 ? -2 : -1; }
    }
}
