using djack.RogueSurvivor.Data;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class NpcInterestSystem : INpcGoalSource
    {
        public void Evaluate(NpcGoalContext context, NpcGoalOffers offers)
        {
            foreach (NpcInterestDefinition definition in context.Catalog.Interests) definition.Observe(context);
            if (!context.Owner.Personality.HasInterests) return;
            foreach (NpcInterest interest in context.Owner.Personality.Interests)
            {
                if (context.Turn >= interest.ExpiresTurn) continue;
                NpcInterestDefinition definition = context.Catalog.Interest(interest.DefinitionId);
                if (definition != null) definition.Evaluate(context, interest, offers);
            }
        }
    }
}
