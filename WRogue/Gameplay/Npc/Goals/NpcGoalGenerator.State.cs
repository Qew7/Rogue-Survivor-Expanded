using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    static partial class NpcGoalGenerator
    {
        public static List<NpcGoalCandidate> Evaluate(RogueGame game, Actor owner, NpcContentCatalog catalog = null)
        {
            if (!NpcIntentSystem.Enabled(owner)) return new List<NpcGoalCandidate>();
            catalog = catalog ?? game.NpcContent;
            Action<string, long> profile = RogueGame.ProfileSimulationPhase;
            long start = profile == null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();
            foreach (TraitInstance trait in owner.Personality.TraitList)
            {
                TraitDefinition definition = catalog.Personalities.Trait(trait.Id);
                if (definition != null && definition.Interest != null) definition.Interest.Prepare(owner, trait);
            }
            var context = new NpcGoalContext(game, owner, catalog); var offers = new NpcGoalOffers(context);
            if (profile != null) profile("goal context", System.Diagnostics.Stopwatch.GetTimestamp() - start);
            foreach (INpcGoalSource source in catalog.GoalSources)
            {
                start = profile == null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();
                source.Evaluate(context, offers);
                if (profile != null) profile("source " + source.GetType().Name, System.Diagnostics.Stopwatch.GetTimestamp() - start);
            }
            return offers.Candidates;
        }
    }
}
