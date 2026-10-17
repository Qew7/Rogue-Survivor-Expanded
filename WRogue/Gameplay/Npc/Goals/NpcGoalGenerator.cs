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
            Action<string, long> profile = RogueGame.ProfileSimulationPhase;
            long start = profile == null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();
            List<NpcGoalCandidate> candidates = Evaluate(game, owner, null, true);
            if (profile != null) profile("goal evaluate", System.Diagnostics.Stopwatch.GetTimestamp() - start);
            start = profile == null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();
            NpcGoalLifecycle.Apply(owner, candidates, game.NpcContent, maintain);
            if (profile != null) profile("goal apply", System.Diagnostics.Stopwatch.GetTimestamp() - start);
        }
    }
}
