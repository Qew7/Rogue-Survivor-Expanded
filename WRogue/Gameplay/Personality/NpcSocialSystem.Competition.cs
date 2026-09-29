using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static partial class NpcSocialSystem
    {
        static void ObserveDispute(RogueGame game, Actor owner, SignificantEvent source, bool direct)
        {
            if (source.Subject == null || source.Other == null || source.ResourcePlace.Map == null || (owner != source.Subject && owner != source.Other)) return;
            Actor peer = owner == source.Subject ? source.Other : source.Subject;
            NpcResourceDispute dispute = owner.Personality.Dispute(source.ResourcePlace, peer.PersonalityIdentity);
            if (source.Kind == "resource_contested")
            {
                dispute = new NpcResourceDispute { Place = source.ResourcePlace, Other = peer.PersonalityIdentity,
                    StoryId = source.StoryId, Turn = source.Turn, CauseId = source.Id, Resource = source.Resource };
                owner.Personality.RememberDispute(dispute);
                if (owner == source.Other && NpcIntentSystem.Enabled(owner))
                {
                    int willing = PersonalitySystem.Bias(owner, DecisionKind.Compassion) + PersonalitySystem.Bias(owner, DecisionKind.Trade) / 2 -
                        PersonalitySystem.Bias(owner, DecisionKind.Supplies) / 2 + NpcValues.KnownAttitude(owner, source.Subject.PersonalityIdentity) / 4;
                    bool ownNeed = source.Resource == "food" ? game.Rules.IsActorHungry(owner) : owner.HitPoints < game.Rules.ActorMaxHPs(owner);
                    Reply(owner, source.Subject, source, willing >= 15 && !ownNeed ? "resource_yielded" : "resource_refused",
                        "You can have those supplies. I'll find another way.", "I need those supplies too.");
                    NpcReaction reply = owner.Personality.Reactions.FindLast(r => r.CauseId == source.Id);
                    if (reply != null) { reply.ResourcePlace = source.ResourcePlace; reply.Resource = source.Resource; }
                }
            }
            else if (dispute != null)
            {
                dispute.Response = source.Kind; dispute.CauseId = source.Id; dispute.Turn = source.Turn;
                owner.Personality.Opinion(peer.PersonalityIdentity, peer.UnmodifiedName).AdjustSocial(
                    trust: source.Kind == "resource_yielded" ? 5 : source.Kind == "contested_taken" ? -15 : -2,
                    grievance: source.Kind == "contested_taken" ? 15 : 0);
                foreach (NpcIntent intent in owner.Personality.Intents)
                    if (!intent.Finished && intent.Plan != null)
                    { intent.Plan.Invalidate(); intent.NextAttempt = source.Turn + 1; intent.Plan.NextPlanningTurn = source.Turn + 1; }
            }
        }
        public static bool RespectRefusal(Actor owner, Location place)
        {
            if (!owner.Personality.HasDisputes) return false;
            int turn = owner.Location.Map.LocalTime.TurnCounter;
            return owner.Personality.Disputes.Exists(d => d.Place == place && turn - d.Turn < 180 &&
                (d.Response == null || d.Response == "resource_refused" &&
                PersonalitySystem.Bias(owner, DecisionKind.Law) + PersonalitySystem.Bias(owner, DecisionKind.Courage) / 2 >= 0));
        }
        public static void Taken(RogueGame game, Actor owner, NpcIntent goal, Location place, string resource)
        {
            if (!owner.Personality.HasDisputes) return;
            NpcResourceDispute dispute = owner.Personality.Disputes.Find(d => d.Place == place && d.Response == "resource_refused");
            if (dispute == null) return;
            Actor peer = null;
            foreach (Actor actor in owner.Location.Map.Actors)
                if (actor.PersonalityIdentity == dispute.Other && NpcIntentSystem.CanSee(game, owner, actor)) { peer = actor; break; }
            if (peer == null) return;
            var source = new SignificantEvent("contested_taken", owner, peer, owner.Location.Map, owner.Location.Position,
                owner.Location.Map.LocalTime.TurnCounter, otherIsDirect: false, causeId: dispute.CauseId, storyId: goal.StoryId)
                { ResourcePlace = place, Resource = resource };
            PersonalitySystem.Report(game, source); dispute.Response = "taken";
        }
    }
}
