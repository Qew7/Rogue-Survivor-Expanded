using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Gameplay.Personality.Axes
{
    static class PatienceAxis
    {
        public static int Faction(NpcFactionPolicy policy) { return policy.Patience; }
        public static int Context(Actor actor)
        {
            int value = 0;
            if (actor.Model.Abilities.HasToEat && actor.FoodPoints < actor.Sheet.BaseFoodPoints / 4) value -= 10;
            if (actor.Model.Abilities.HasToSleep && actor.SleepPoints < actor.Sheet.BaseSleepPoints / 4) value -= 12;
            if (actor.Model.Abilities.HasSanity && actor.Sanity < actor.Sheet.BaseSanity / 4) value -= 10;
            return value;
        }
        public static int Trait(TraitEffect effect)
        { return effect.Axis == NpcPersonalityAxis.Patience ? effect.Amount : 0; }

        public static int ImmediateSanity(Actor observer, SignificantEvent e, int value)
        {
            if (observer != e.Other) return 0;
            if (e.Kind == "boundary_defied" || e.Kind == "contested_taken") return value < -20 ? -2 : -1;
            if (e.Kind == "boundary_accepted" || e.Kind == "resource_yielded") return 1;
            return 0;
        }
    }
}
