using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class NpcPlanDomain
    {
        public readonly List<NpcPlanStep> Actions = new List<NpcPlanStep>();
        public ulong Initial;
        public int Traits;
        readonly List<Location> places = new List<Location>();
        const ulong AtMask = 0x0000FFFFFFFF0000UL;
        readonly RogueGame game;
        readonly Actor owner;
        readonly NpcIntent goal;
        readonly NpcPlan plan;
        readonly int turn;
        public NpcPlanDomain(RogueGame game, Actor owner, NpcIntent goal, IList<Actor> visible)
        {
            this.game = game; this.owner = owner; this.goal = goal; plan = goal.Plan;
            turn = owner.Location.Map.LocalTime.TurnCounter;
            int food = 0;
            foreach (Item item in owner.Inventory.Items)
                if (item is ItemFood && !item.IsEquipped && !game.Rules.IsFoodSpoiled((ItemFood)item, turn)) food += item.Quantity;
            if (food > 0) Initial |= (ulong)NpcPlanFact.Food;
            if (food >= 2 && !game.Rules.IsActorHungry(owner)) Initial |= (ulong)NpcPlanFact.SpareFood;
            if (goal.Progress >= 2) Initial |= (ulong)NpcPlanFact.Delivered;
            if (goal.Announced) Initial |= (ulong)NpcPlanFact.Requested;
            foreach (DecisionKind kind in new[] { DecisionKind.Group, DecisionKind.Compassion, DecisionKind.Trade,
                DecisionKind.Explore, DecisionKind.Supplies, DecisionKind.Law, DecisionKind.Courage })
                Traits = unchecked(Traits * 31 + PersonalitySystem.Bias(owner, kind));
            NpcIntentMethod method = NpcIntentContent.Find(goal.DefinitionId).Method;
            bool supply = method == NpcIntentMethod.GatherFood || method == NpcIntentMethod.ShareFood || method == NpcIntentMethod.ObtainFood ||
                method == NpcIntentMethod.RequestFood;
            if (supply)
            {
                foreach (NpcFact offer in owner.Personality.Knowledge.Facts)
                {
                    if (offer.Kind != "food_offered" || offer.OtherId != owner.PersonalityIdentity || offer.Confidence < 40 || turn - offer.EventTurn > 60) continue;
                    NpcKnownPerson seller = owner.Personality.Knowledge.Person(offer.SubjectId); if (seller == null || seller.Dead) continue;
                    bool hasPayment = false; foreach (Item item in owner.Inventory.Items) if (!(item is ItemFood) && !item.IsEquipped && !item.IsUnique) hasPayment = true;
                    if (!hasPayment) continue;
                    ulong at = At(seller.Place); Travel(seller.Place, seller.Id, at);
                    Add(NpcPlanAction.BarterFood, seller.Place, seller.Id, at, 0,
                        (ulong)(NpcPlanFact.Food | NpcPlanFact.SpareFood), 0, 12 - PersonalitySystem.Bias(owner, DecisionKind.Trade) / 3);
                }
                foreach (NpcKnownPlace cache in owner.Personality.Knowledge.Places)
                {
                    if (cache.Kind != "food" || cache.Units <= 0 || turn - cache.SeenTurn > 180) continue;
                    ulong at = At(cache.Place); if (at == 0) continue;
                    Travel(cache.Place, Guid.Empty, at);
                    ulong gain = (ulong)NpcPlanFact.Food;
                    if (food + cache.Units >= 2) gain |= (ulong)NpcPlanFact.SpareFood;
                    Add(NpcPlanAction.PickupFood, cache.Place, Guid.Empty, at, 0, gain, 0,
                        9 - PersonalitySystem.Bias(owner, DecisionKind.Explore) / 4 - PersonalitySystem.Bias(owner, DecisionKind.Supplies) / 4 +
                        cache.Risk * Math.Max(0, 6 + PersonalitySystem.Bias(owner, DecisionKind.Law) / 3 - PersonalitySystem.Bias(owner, DecisionKind.Courage) / 4));
                }
                var peers = new List<Actor>(visible); peers.Sort((a, b) => a.PersonalityIdentity.CompareTo(b.PersonalityIdentity));
                int n = 0;
                foreach (Actor peer in peers)
                {
                    if (n >= 8 || peer.IsSleeping || peer == owner ||
                        (peer.PersonalityIdentity == goal.TargetId && method != NpcIntentMethod.RequestFood && method != NpcIntentMethod.ObtainFood) ||
                        !peer.Model.Abilities.IsIntelligent || game.Rules.AreEnemies(owner, peer) ||
                        owner.Personality.Knowledge.WasTold(-goal.Sequence, peer.PersonalityIdentity)) continue;
                    ulong at = At(peer.Location), asked = 1UL << (48 + n++);
                    Travel(peer.Location, peer.PersonalityIdentity, at);
                    int cost = 15 - PersonalitySystem.Bias(owner, DecisionKind.Group) / 3 - PersonalitySystem.Bias(owner, DecisionKind.Compassion) / 5 -
                        PersonalitySystem.Attitude(owner, peer) / 5 - (owner.Leader == peer ? 5 : 0);
                    Add(NpcPlanAction.AskFood, peer.Location, peer.PersonalityIdentity, at, asked | (ulong)NpcPlanFact.Food,
                        asked | (ulong)NpcPlanFact.Food, 0, cost);
                    Add(NpcPlanAction.AskFood, peer.Location, peer.PersonalityIdentity, at | (ulong)NpcPlanFact.Food,
                        asked | (ulong)NpcPlanFact.SpareFood, asked | (ulong)NpcPlanFact.SpareFood, 0, cost);
                }
            }
            ulong targetAt = At(goal.LastKnown);
            if (method == NpcIntentMethod.GatherFood || method == NpcIntentMethod.ShareFood)
            {
                Travel(goal.LastKnown, goal.TargetId, targetAt);
                Add(NpcPlanAction.GiveFood, goal.LastKnown, goal.TargetId, targetAt | (ulong)NpcPlanFact.SpareFood,
                    (ulong)NpcPlanFact.Delivered, (ulong)NpcPlanFact.Delivered, (ulong)NpcPlanFact.SpareFood, 2);
                if (method == NpcIntentMethod.GatherFood && goal.CoordinatorPlace.Map != null)
                {
                    ulong at = At(goal.CoordinatorPlace); Travel(goal.CoordinatorPlace, goal.CoordinatorId, at);
                    Add(NpcPlanAction.ReportDelivery, goal.CoordinatorPlace, goal.CoordinatorId,
                        at | (ulong)NpcPlanFact.Delivered, 0, (ulong)NpcPlanFact.Reported, 0, 2);
                }
            }
            else if (method == NpcIntentMethod.SeekPerson || method == NpcIntentMethod.ConfrontPerson)
            {
                Actor target = NpcIntentSystem.VisibleTarget(visible, goal.TargetId);
                if (target != null || !NpcKnowledgeSystem.Visible(game, owner, goal.LastKnown)) Initial |= (ulong)NpcPlanFact.LocationKnown;
                Travel(goal.LastKnown, goal.TargetId, targetAt, (ulong)NpcPlanFact.LocationKnown);
                Add(method == NpcIntentMethod.SeekPerson ? NpcPlanAction.Reunite : NpcPlanAction.Warn,
                    goal.LastKnown, goal.TargetId, targetAt | (ulong)NpcPlanFact.LocationKnown, 0, NpcGoalPlanner.Desired(method), 0, 1);
                if (target == null) foreach (Actor peer in visible)
                {
                    var question = new Engine.Actions.ActionNpcAskLocation(owner, game, peer, goal);
                    if (question.IsLegal()) Add(NpcPlanAction.AskLocation, peer.Location, peer.PersonalityIdentity,
                        0, (ulong)NpcPlanFact.LocationKnown, (ulong)NpcPlanFact.LocationKnown, 0, 4);
                }
            }
            else if (method == NpcIntentMethod.ReachShelter)
            {
                ulong at = At(goal.Destination);
                if (!owner.Location.Map.GetTileAt(owner.Location.Position).IsInside) Initial &= ~at;
                Travel(goal.Destination, Guid.Empty, at);
                Add(NpcPlanAction.EnterShelter, goal.Destination, Guid.Empty, at, 0, (ulong)NpcPlanFact.Sheltered, 0, 1);
            }
            else if (method == NpcIntentMethod.AvoidPerson)
            {
                const ulong away = 1UL << 11;
                if (goal.LastKnown.Map != owner.Location.Map || game.Rules.GridDistance(owner.Location.Position, goal.LastKnown.Position) >= 5) Initial |= away;
                Add(NpcPlanAction.Retreat, goal.LastKnown, goal.TargetId, 0, away, away, 0, 2);
                Add(NpcPlanAction.ConfirmSafety, goal.LastKnown, goal.TargetId, away, 0, (ulong)NpcPlanFact.Safe, 0, 1);
            }
            else if (method == NpcIntentMethod.LeaveGroup)
                Add(NpcPlanAction.LeaveGroup, goal.LastKnown, goal.TargetId, 0, 0, (ulong)NpcPlanFact.Left, 0, 1);
        }
        ulong At(Location place)
        {
            if (place.Map == null) return 0;
            int index = places.FindIndex(p => p == place);
            if (index < 0) { if (places.Count >= 32) return 0; index = places.Count; places.Add(place); }
            ulong flag = 1UL << (16 + index);
            if (place.Map == owner.Location.Map && game.Rules.GridDistance(place.Position, owner.Location.Position) <= 1) Initial |= flag;
            return flag;
        }
        void Travel(Location place, Guid person, ulong at, ulong requires = 0)
        {
            if (at == 0) return;
            int distance = place.Map == owner.Location.Map ? game.Rules.GridDistance(place.Position, owner.Location.Position) : 12;
            Add(NpcPlanAction.Travel, place, person, requires, at, at, AtMask, Math.Max(1, distance));
        }
        void Add(NpcPlanAction kind, Location place, Guid person, ulong requires, ulong forbids, ulong adds, ulong removes, int cost)
        {
            if (place.Map == null || Actions.Count >= 64) return;
            var step = new NpcPlanStep { Action = kind, Place = place, Target = person, Requires = requires, Forbids = forbids,
                Adds = adds, Removes = removes, Cost = Math.Max(1, cost) };
            if (plan == null || !plan.Blocked(step, turn)) Actions.Add(step);
        }
    }
}
