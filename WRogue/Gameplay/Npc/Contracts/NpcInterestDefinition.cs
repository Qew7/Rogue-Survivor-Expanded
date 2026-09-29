using System;
using djack.RogueSurvivor.Data;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class NpcInterestDefinition
    {
        public readonly string Id;
        public readonly Action<NpcGoalContext> Observe;
        public readonly Action<NpcGoalContext, NpcInterest, NpcGoalOffers> Evaluate;
        public NpcInterestDefinition(string id, Action<NpcGoalContext> observe, Action<NpcGoalContext, NpcInterest, NpcGoalOffers> evaluate)
        { Id = id; Observe = observe; Evaluate = evaluate; }
    }
}
