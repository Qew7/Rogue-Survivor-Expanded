using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Gameplay.Personality.Axes
{
    static class CareCircleAxis
    {
        public static int Faction(NpcFactionPolicy policy) { return policy.CareCircle; }
        public static int Context(Actor actor)
        {
            int value = 0;
            if (actor.Model.Abilities.HasToEat && actor.FoodPoints < actor.Sheet.BaseFoodPoints / 4) value -= 8;
            if (actor.Model.Abilities.HasToSleep && actor.SleepPoints < actor.Sheet.BaseSleepPoints / 4) value -= 5;
            return value;
        }

        public static int Trait(TraitEffect effect)
        {
            if (effect.Axis == NpcPersonalityAxis.CareCircle) return effect.Amount;
            if (effect.Axis == NpcPersonalityAxis.Group) return effect.Amount / 2;
            if (effect.Axis == NpcPersonalityAxis.Compassion) return effect.Amount / 3;
            return 0;
        }

        public static int ImmediateSanity(Actor observer, SignificantEvent e, int value)
        {
            if (observer != e.Subject) return 0;
            if (e.Kind == "helped") return value >= 20 || observer.Personality.HasTrait("kind") ? 2 : 1;
            if (e.Kind == "abandoned") return value >= 20 || observer.Personality.HasTrait("loyal") ? -2 : -1;
            return 0;
        }
    }
}
