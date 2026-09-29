using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static partial class NpcSocialSystem
    {
        public static void PrepareReply(RogueGame game, Actor owner, SignificantEvent source)
        {
            if (source.Other != owner || source.Subject == null || owner.Personality.Reactions.Count >= 4 ||
                game.Rules.AreEnemies(owner, source.Subject) || (source.Kind != "requested_food" && source.Kind != "requested_medicine")) return;
            bool medical = source.Kind == "requested_medicine";
            bool supplies = medical ? NpcPlanExecution.Medicine(game, owner, owner.Location, true) != null : NpcIntentSystem.SpareFood(game, owner, source.Subject) != null;
            int compassion = PersonalitySystem.Bias(owner, DecisionKind.Compassion), law = PersonalitySystem.Bias(owner, DecisionKind.Law);
            if (medical && supplies && PersonalitySystem.Bias(owner, DecisionKind.Trade) >= 15 && compassion <= 0)
            { owner.Personality.Reactions.Add(new NpcReaction(source.Subject, "I'll exchange medicine for other supplies.", source.Id, source.Turn, "medicine_offered", source.StoryId)); return; }
            if (!supplies && compassion > 0 && law > 0 && owner.Personality.CanRememberCommitment && source.Subject.Personality.CanRememberCommitment &&
                !owner.Personality.Commitments.Exists(p => p.Status == NpcCommitmentStatus.Active &&
                p.Promisor == owner.PersonalityIdentity && p.Beneficiary == source.Subject.PersonalityIdentity && p.Resource == (medical ? "medicine" : "food")))
                owner.Personality.Reactions.Add(new NpcReaction(source.Subject, medical ? "I'll bring you medicine before the deadline." : "I'll bring you food before the deadline.",
                    source.Id, source.Turn, medical ? "medicine_promised" : "food_promised", source.StoryId));
            else if (medical && compassion <= 0 && !supplies)
                owner.Personality.Reactions.Add(new NpcReaction(source.Subject, "I can't spare medicine.", source.Id, source.Turn, "request_refused", source.StoryId));
        }
    }
}
