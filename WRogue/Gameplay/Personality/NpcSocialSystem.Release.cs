using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static partial class NpcSocialSystem
    {
        static void ReleaseUnneeded(RogueGame game, Actor owner, SignificantEvent source)
        {
            if (!owner.Personality.HasCommitments) return;
            foreach (NpcCommitment promise in owner.Personality.Commitments)
            {
                if (promise.Status != NpcCommitmentStatus.Active || promise.Beneficiary != owner.PersonalityIdentity || promise.Promisor == source.Subject.PersonalityIdentity ||
                    promise.Resource != (source.Kind == "shared_food" ? "food" : "medicine") || owner.Personality.Reactions.Count >= 4 ||
                    owner.Personality.Reactions.Exists(r => r.Kind == "promise_released" && r.CauseId == promise.Id)) continue;
                Actor promisor = null;
                foreach (Actor actor in owner.Location.Map.Actors)
                    if (actor.PersonalityIdentity == promise.Promisor && NpcIntentSystem.CanSee(game, owner, actor)) { promisor = actor; break; }
                if (promisor != null) owner.Personality.Reactions.Add(new NpcReaction(promisor, "Someone brought what I needed. You're released from that promise.",
                    promise.Id, source.Turn, "promise_released", promise.StoryId));
            }
        }
    }
}
