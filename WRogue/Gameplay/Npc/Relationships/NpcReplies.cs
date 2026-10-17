using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcReplies
    {
        internal static void Reply(Actor owner, Actor target, SignificantEvent source, string kind, string yes, string no)
        {
            if (target == null || owner.Personality.Reactions.Count >= 4) return;
            owner.Personality.Reactions.Add(new NpcReaction(target, kind.EndsWith("accepted") || kind.EndsWith("yielded") ? yes : no,
                source.Id, source.Turn, kind, source.StoryId));
        }
        internal static bool Setback(NpcObservation observation, int severity, string words,
            string attackKind = "patience_escalated", string verbalKind = "patience_lost",
            bool victimIsSubject = false)
        {
            Actor owner = observation.Owner;
            Actor target = victimIsSubject ? observation.Source.Other : observation.Source.Subject;
            if (owner != (victimIsSubject ? observation.Source.Subject : observation.Source.Other) ||
                target == owner) return false;
            return Setback(observation.Game, owner, target, observation.Source.Id,
                observation.Source.Turn, observation.Source.StoryId, severity, words, attackKind, verbalKind);
        }
        internal static bool Setback(RogueGame game, Actor owner, Actor target, long causeId, int turn,
            string storyId, int severity, string words, string attackKind = "patience_escalated",
            string verbalKind = "patience_lost")
        {
            if (owner == null || owner.IsPlayer || !NpcIntentSystem.Enabled(owner)) return false;
            int pressure = owner.Personality.ChangeFrustration(turn, severity);
            int patience = PersonalitySystem.Disposition(owner, NpcPersonalityAxis.Patience, game.NpcContent);
            if (pressure < Math.Max(15, Math.Min(95, 45 + patience)) || target == null || target.IsDead ||
                target.IsSleeping || owner.Personality.Reactions.Count >= 4 ||
                !NpcIntentSystem.CanSee(game, owner, target) || game.Rules.AreEnemies(owner, target)) return false;
            if (owner.Personality.Reactions.Exists(r => r.TargetId == target.PersonalityIdentity &&
                (r.Kind == "patience_lost" || r.Kind == "patience_escalated" ||
                    r.Kind == "boundary_escalated" || r.Kind == "boundary_defied")))
                return true;
            bool attack = PersonalitySystem.Bias(owner, NpcPersonalityAxis.Courage) >= 0 &&
                game.Rules.CanActorMeleeAttack(owner, target);
            owner.Personality.Reactions.Add(new NpcReaction(target, words, causeId, turn,
                attack ? attackKind : verbalKind, storyId, attackTarget: attack));
            return true;
        }
    }
}
