using System;
using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class NpcPlanDomain
    {
        public readonly List<NpcPlanStep> Actions = new List<NpcPlanStep>();
        public ulong Initial;
        NpcPlanningState extraInitial;
        public NpcPlanningState InitialState
        { get { NpcPlanningState state = extraInitial; state.Low = Initial; return state; }
            set { Initial = value.Low; extraInitial = value; extraInitial.Low = 0; } }
        public int Traits;
        readonly List<Location> places = new List<Location>();
        readonly NpcPlan plan;
        public readonly RogueGame Game;
        public readonly Actor Owner;
        public readonly NpcIntent Goal;
        public readonly int Turn;
        public readonly IList<Actor> Visible;
        public readonly NpcContentCatalog Catalog;
        public NpcPlanningState Desired { get { return Catalog.Capability(Goal.DefinitionId).GetResult(Catalog, Goal.Generated); } }
        public string Resource { get { return Goal.Generated != null && Goal.Generated.Resource != null ? Goal.Generated.Resource : Catalog.Capability(Goal.DefinitionId).Resource; } }
        public NpcPlanDomain(RogueGame game, Actor owner, NpcIntent goal, IList<Actor> visible, NpcContentCatalog catalog = null)
        {
            Game = game; Owner = owner; Goal = goal; Visible = visible; plan = goal.Plan;
            Turn = owner.Location.Map.LocalTime.TurnCounter; Catalog = catalog ?? game.NpcContent;
            foreach (DecisionKind kind in new[] { DecisionKind.Group, DecisionKind.Compassion, DecisionKind.Trade,
                DecisionKind.Explore, DecisionKind.Supplies, DecisionKind.Law, DecisionKind.Courage })
                Traits = unchecked(Traits * 31 + PersonalitySystem.Bias(owner, kind, registry: Catalog.Personalities));
            foreach (Action<NpcPlanDomain> seed in Catalog.PlanSeeds) seed(this);
            if (plan != null && plan.CompletedFacts != null) foreach (string name in plan.CompletedFacts)
            { NpcPlanningState mask; if (Catalog.Facts.TryMask(name, out mask)) InitialState |= mask; }
            NpcPlanComposition.Build(this);
            NpcIntentDefinition capability = Catalog.Capability(goal.DefinitionId);
            if (capability != null && capability.BuildPlan != null) capability.BuildPlan(this);
        }
        public ulong At(Location place)
        {
            if (place.Map == null) return 0;
            int index = places.FindIndex(p => p == place);
            if (index < 0) { if (places.Count >= NpcFactLayout.MaximumPlaces) return 0; index = places.Count; places.Add(place); }
            ulong flag = NpcFactLayout.Location(index);
            if (place.Map == Owner.Location.Map && Game.Rules.GridDistance(place.Position, Owner.Location.Position) <= 1) Initial |= flag;
            return flag;
        }
        public void Travel(Location place, Guid person, ulong at, ulong requires = 0)
        {
            if (at == 0) return;
            int distance = place.Map == Owner.Location.Map ? Game.Rules.GridDistance(place.Position, Owner.Location.Position) : 12;
            Add(NpcPlanAction.Travel, place, person, requires, at, at, NpcFactLayout.Locations, Math.Max(1, distance));
        }
        public void Add(NpcPlanAction kind, Location place, Guid person, ulong requires, ulong forbids, ulong adds, ulong removes, int cost)
        {
            if (place.Map == null || Actions.Count >= 64) return;
            var step = new NpcPlanStep { Action = kind, Place = place, Target = person, Requires = requires, Forbids = forbids,
                Adds = adds, Removes = removes, Cost = Math.Max(1, cost) };
            if (plan == null || !plan.Blocked(step, Turn)) Actions.Add(step);
        }
        public void Add(string operatorId, Location place, Guid person, ulong requires, ulong forbids, ulong adds, ulong removes, int cost)
        { Add(operatorId, place, person, (NpcPlanningState)requires, (NpcPlanningState)forbids, (NpcPlanningState)adds, (NpcPlanningState)removes, cost); }
        public void Add(string operatorId, Location place, Guid person, NpcPlanningState requires, NpcPlanningState forbids, NpcPlanningState adds, NpcPlanningState removes, int cost)
        {
            if (Catalog.Operator(operatorId) == null) throw new ArgumentException("Unknown plan operator: " + operatorId);
            if (place.Map == null || Actions.Count >= 64) return;
            var step = new NpcPlanStep { OperatorId = operatorId, Place = place, Target = person, Requires = requires.Low,
                Forbids = forbids.Low, Adds = adds.Low, Removes = removes.Low, Cost = Math.Max(1, cost) };
            if (requires.Extended || forbids.Extended || adds.Extended || removes.Extended)
            {
                requires.Low = forbids.Low = adds.Low = removes.Low = 0;
                step.Extension = new NpcPlanningExtension { Requires = requires, Forbids = forbids, Adds = adds, Removes = removes };
            }
            if (plan == null || !plan.Blocked(step, Turn)) Actions.Add(step);
        }
    }
}
