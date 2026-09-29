using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static partial class NpcIntentSystem
    {
        public static void AdvanceClock(RogueGame game, Map map)
        {
            if (!Session.Get.GamePreset.NpcPersonalitiesEnabled) return;
            Session.Get.NpcDirector.Advance(map, map.LocalTime.TurnCounter);
            foreach (Actor actor in map.Actors)
            {
                game.NpcContent.Advance(NpcClockPhase.MapTurn, new NpcClockContext(game, actor));
                if (actor.Personality != null && actor.Personality.HasIntentState)
                    foreach (NpcIntent intent in actor.Personality.IntentList)
                        if (!intent.Finished && map.LocalTime.TurnCounter >= intent.Deadline)
                            Finish(actor, intent, NpcIntentStatus.Failed, "deadline expired");
            }
        }
        public static bool CanSee(RogueGame game, Actor owner, Actor target)
        {
            return owner != null && target != null && !target.IsDead && owner.Location.Map == target.Location.Map &&
                game.Rules.GridDistance(owner.Location.Position, target.Location.Position) <=
                    game.Rules.ActorFOV(owner, owner.Location.Map.LocalTime, game.Session.World.Weather) &&
                LOS.CanTraceViewLine(owner.Location, target.Location.Position);
        }
        public static Actor VisibleTarget(IList<Actor> visible, Guid id)
        { foreach (Actor actor in visible) if (actor.PersonalityIdentity == id) return actor; return null; }
        public static void Maintain(RogueGame game, Actor owner, IList<Actor> visible, bool danger, bool followingOrder)
        {
            if (!Enabled(owner)) return;
            game.NpcContent.Advance(NpcClockPhase.BeforeDecision, new NpcClockContext(game, owner));
            NpcGoalGenerator.Refresh(game, owner, true);
            if (!owner.Personality.HasPendingSocialState) return;
            int turn = owner.Location.Map.LocalTime.TurnCounter;
            owner.Personality.Reactions.RemoveAll(r => turn > r.Deadline);
            foreach (NpcIntent intent in owner.Personality.IntentList)
            {
                if (intent.Finished) continue;
                if (intent.GroupId != Guid.Empty && (owner.SocialGroup == null || owner.SocialGroup.Identity != intent.GroupId))
                { Finish(owner, intent, NpcIntentStatus.Abandoned, "left the group that assigned this goal"); continue; }
                NpcIntentDefinition definition = game.NpcContent.Capability(intent.DefinitionId);
                if (definition == null) { Finish(owner, intent, NpcIntentStatus.Abandoned, "unknown intent definition"); continue; }
                if (turn >= intent.Deadline) { Finish(owner, intent, NpcIntentStatus.Failed, "deadline expired"); continue; }
                NpcValueDefinition value = intent.Generated == null ? null : game.NpcContent.Value(intent.Generated);
                if (intent.Generated != null && intent.Generated.Deficit == 0 && value != null && value.CompleteWhenSatisfied)
                { Finish(owner, intent, NpcIntentStatus.Completed, "observed that the desired state was satisfied"); continue; }
                NpcIntentOutcome outcome = definition.Assess == null ? null : definition.Assess(game, owner, intent);
                if (outcome != null) { Finish(owner, intent, outcome.Status, outcome.Reason); continue; }
                Actor target = VisibleTarget(visible, intent.TargetId);
                NpcKnownPerson known = owner.Personality.HasKnowledge ? owner.Personality.Knowledge.Person(intent.TargetId) : null;
                if (target == null && known != null && known.SeenTurn > intent.LastKnownTurn && known.Confidence >= 40)
                { intent.LastKnown = known.Place; intent.LastKnownTurn = known.SeenTurn; }
                Actor coordinator = VisibleTarget(visible, intent.CoordinatorId);
                if (coordinator != null) intent.CoordinatorPlace = coordinator.Location;
                if (target != null)
                {
                    intent.LastKnown = target.Location;
                    intent.LastKnownTurn = turn;
                    intent.KnownAttitude = PersonalitySystem.Attitude(owner, target);
                    if (!definition.AllowHostile &&
                        (intent.Generated == null || intent.Generated.SubjectId != owner.PersonalityIdentity) && game.Rules.AreEnemies(owner, target))
                    { Finish(owner, intent, NpcIntentStatus.Abandoned, "target became hostile"); continue; }
                }
                if (definition.Score(owner, intent, game.NpcContent.Personalities) < definition.ThresholdFor(intent))
                { Finish(owner, intent, NpcIntentStatus.Abandoned, "motivation changed"); continue; }
                bool pause = !definition.Departure && (danger || followingOrder ||
                    (definition.PauseWhenTired && game.Rules.IsActorTired(owner)) || (definition.PauseWhenHungry && game.Rules.IsActorHungry(owner)));
                if (pause) intent.Status = NpcIntentStatus.Paused;
                else if (intent.Status == NpcIntentStatus.Paused) intent.Status = intent.Announced && definition.WaitingAfterAnnouncement
                    ? NpcIntentStatus.Waiting : NpcIntentStatus.Active;
            }
        }
        public static NpcIntent Select(Actor owner, bool departureOnly = false, NpcContentCatalog catalog = null)
        {
            if (!Enabled(owner) || !owner.Personality.HasIntentState) return null;
            catalog = catalog ?? NpcContentCatalog.Default;
            int turn = owner.Location.Map.LocalTime.TurnCounter; NpcIntent best = null; int bestScore = Int32.MinValue;
            foreach (NpcIntent intent in owner.Personality.IntentList)
            {
                NpcIntentDefinition definition = catalog.Capability(intent.DefinitionId); if (definition == null) continue;
                if (intent.Finished || intent.Status == NpcIntentStatus.Paused || turn < intent.NextAttempt || !definition.Selectable ||
                    (definition.WaitingAfterAnnouncement && intent.Announced &&
                        (intent.Plan == null || !intent.Plan.DesiredState.Equals(definition.GetResult(catalog, intent.Generated))))) continue;
                if (departureOnly && !definition.Departure) continue;
                int score = definition.Score(owner, intent, catalog.Personalities);
                if (best == null || score > bestScore || (score == bestScore && intent.Sequence < best.Sequence))
                { best = intent; bestScore = score; }
            }
            return best;
        }
        public static void Block(Actor owner, NpcIntent intent, string reason)
        {
            intent.NextAttempt = owner.Location.Map.LocalTime.TurnCounter + 8;
            if (++intent.BlockedAttempts >= 8) Finish(owner, intent, NpcIntentStatus.Failed, reason);
        }

    }
}
