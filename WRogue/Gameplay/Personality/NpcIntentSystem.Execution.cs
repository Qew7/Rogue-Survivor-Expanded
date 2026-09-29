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
                if (actor.SocialGroup != null && actor.SocialGroup.LeaderId == actor.PersonalityIdentity && actor.SocialGroup.Plan != null &&
                    !actor.SocialGroup.Plan.Finished && map.LocalTime.TurnCounter >= actor.SocialGroup.Plan.Deadline)
                { actor.SocialGroup.Plan.Stage = "failed"; actor.SocialGroup.Plan.Destination = default(Location); }
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
            NpcGoalGenerator.Refresh(game, owner, true);
            if (!owner.Personality.HasPendingSocialState) return;
            int turn = owner.Location.Map.LocalTime.TurnCounter;
            owner.Personality.Reactions.RemoveAll(r => turn > r.Deadline);
            foreach (NpcIntent intent in owner.Personality.IntentList)
            {
                if (intent.Finished) continue;
                if (intent.GroupId != Guid.Empty && (owner.SocialGroup == null || owner.SocialGroup.Identity != intent.GroupId))
                { Finish(owner, intent, NpcIntentStatus.Abandoned, "left the group that assigned this goal"); continue; }
                NpcIntentDefinition definition = NpcIntentContent.Find(intent.DefinitionId);
                if (definition == null) { Finish(owner, intent, NpcIntentStatus.Abandoned, "unknown intent definition"); continue; }
                if (turn >= intent.Deadline) { Finish(owner, intent, NpcIntentStatus.Failed, "deadline expired"); continue; }
                if (intent.Generated != null && intent.Generated.Deficit == 0 &&
                    (intent.Generated.Value == NpcGoalValue.Care || intent.Generated.Value == NpcGoalValue.Reciprocity || intent.Generated.Value == NpcGoalValue.Recovery))
                { Finish(owner, intent, NpcIntentStatus.Completed, "observed that the desired state was satisfied"); continue; }
                if (definition.Method == NpcIntentMethod.LeaveGroup &&
                    (owner.Leader == null || owner.Leader.PersonalityIdentity != intent.TargetId))
                { Finish(owner, intent, NpcIntentStatus.Abandoned, "group membership changed"); continue; }
                if (definition.Method == NpcIntentMethod.RequestFood && (!game.Rules.IsActorHungry(owner) || HasFood(game, owner)))
                { Finish(owner, intent, NpcIntentStatus.Completed, "food need was satisfied"); continue; }
                if (definition.Method == NpcIntentMethod.ObtainFood && (!game.Rules.IsActorHungry(owner) || HasFood(game, owner)))
                { Finish(owner, intent, NpcIntentStatus.Completed, "observed that usable food is available"); continue; }
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
                    if (definition.Method != NpcIntentMethod.LeaveGroup && definition.Method != NpcIntentMethod.AvoidPerson &&
                        (intent.Generated == null || intent.Generated.SubjectId != owner.PersonalityIdentity) && game.Rules.AreEnemies(owner, target))
                    { Finish(owner, intent, NpcIntentStatus.Abandoned, "target became hostile"); continue; }
                }
                if (definition.Score(owner, intent) < definition.ThresholdFor(intent))
                { Finish(owner, intent, NpcIntentStatus.Abandoned, "motivation changed"); continue; }
                bool pause = definition.Method != NpcIntentMethod.LeaveGroup && (danger || followingOrder ||
                    game.Rules.IsActorTired(owner) || (definition.Method == NpcIntentMethod.ShareFood && game.Rules.IsActorHungry(owner)));
                if (pause) intent.Status = NpcIntentStatus.Paused;
                else if (intent.Status == NpcIntentStatus.Paused) intent.Status = intent.Announced && definition.Method == NpcIntentMethod.RequestFood
                    ? NpcIntentStatus.Waiting : NpcIntentStatus.Active;
            }
        }
        public static NpcIntent Select(Actor owner, bool departureOnly = false)
        {
            if (!Enabled(owner) || !owner.Personality.HasIntentState) return null;
            int turn = owner.Location.Map.LocalTime.TurnCounter; NpcIntent best = null; int bestScore = Int32.MinValue;
            foreach (NpcIntent intent in owner.Personality.IntentList)
            {
                if (intent.Finished || intent.Status == NpcIntentStatus.Paused || turn < intent.NextAttempt ||
                    (intent.DefinitionId == NpcIntentContent.Request.Id && intent.Announced &&
                        (intent.Plan == null || intent.Plan.Desired != (ulong)NpcPlanFact.Food))) continue;
                if (departureOnly && intent.DefinitionId != NpcIntentContent.Leave.Id) continue;
                NpcIntentDefinition definition = NpcIntentContent.Find(intent.DefinitionId); if (definition == null) continue;
                if (definition.Method == NpcIntentMethod.Coordinate) continue;
                int score = definition.Score(owner, intent);
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
        public static ItemFood SpareFood(RogueGame game, Actor owner, Actor target)
        {
            if (owner.Inventory == null || target.Inventory == null || game.Rules.IsActorHungry(owner)) return null;
            int units = 0; ItemFood best = null;
            foreach (Item item in owner.Inventory.Items)
            {
                ItemFood food = item as ItemFood;
                if (food == null || food.IsEquipped || game.Rules.IsFoodSpoiled(food, owner.Location.Map.LocalTime.TurnCounter)) continue;
                units += food.Quantity; string reason;
                if (food.Quantity > 0 && game.Rules.CanActorGiveItemTo(owner, target, OneFood(food), out reason) && best == null) best = food;
            }
            return units >= 2 ? best : null;
        }
        public static ItemFood OneFood(ItemFood food)
        { return food.IsPerishable ? new ItemFood(food.Model, food.BestBefore.TurnCounter) : new ItemFood(food.Model); }
    }
}
