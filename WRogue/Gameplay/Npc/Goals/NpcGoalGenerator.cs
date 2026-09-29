using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class NpcGoalCandidate
    {
        public NpcGeneratedGoal State;
        public NpcKnownPerson Target;
        public NpcIntentDefinition Capability;
        public long Cause;
        public string Story;
    }
    static partial class NpcGoalGenerator
    {
        public const int MinimumUtility = 20;
        public static void Refresh(RogueGame game, Actor owner, bool maintain = false)
        {
            if (!NpcIntentSystem.Enabled(owner) || owner.IsSleeping) return;
            NpcGoalLifecycle.Apply(owner, Evaluate(game, owner), game.NpcContent, maintain);
        }
    }
}
