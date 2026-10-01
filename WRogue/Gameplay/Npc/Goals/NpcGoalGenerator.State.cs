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
            foreach (TraitInstance trait in owner.Personality.Traits)
            {
                TraitDefinition definition = catalog.Personalities.Trait(trait.Id);
                if (definition != null && definition.Interest != null) definition.Interest.Prepare(owner, trait);
            }
            var context = new NpcGoalContext(game, owner, catalog); var offers = new NpcGoalOffers(context);
            foreach (INpcGoalSource source in catalog.GoalSources) source.Evaluate(context, offers);
            return offers.Candidates;
        }
    }
}
