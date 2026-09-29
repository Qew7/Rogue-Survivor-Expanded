using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class MovementModule : INpcContentModule
    {
        public string Id { get { return "movement"; } }
        public void Register(NpcCatalogBuilder catalog)
        {
            catalog.Perception(NpcPerceptionKind.Surroundings, PerceiveSurroundings);
            catalog.Operator(new NpcOperatorDefinition("travel", NpcPlanAction.Travel, Travel));
            catalog.Operator(new NpcOperatorDefinition("retreat", NpcPlanAction.Retreat, Retreat));
        }
        static ActorAction Travel(NpcExecutionContext context)
        {
            NpcIntentDefinition capability = context.Game.NpcContent.Capability(context.Goal.DefinitionId);
            Location place = capability.TravelDestination == null ? context.Step.Place : capability.TravelDestination(context);
            ActorAction movement = context.Route == null ? null : context.Route(place);
            return movement == null ? null : context.PlanAction(movement: movement);
        }
        static ActorAction Retreat(NpcExecutionContext context)
        {
            Actor owner = context.Owner; RogueGame game = context.Game; ActorAction movement = null;
            int distance = game.Rules.GridDistance(owner.Location.Position, context.Goal.LastKnown.Position);
            foreach (Direction direction in Direction.COMPASS)
            {
                Location place = owner.Location + direction;
                if (!place.Map.IsInBounds(place.Position.X, place.Position.Y)) continue;
                int d = game.Rules.GridDistance(place.Position, context.Goal.LastKnown.Position);
                if (d <= distance) continue;
                ActorAction move = game.Rules.IsBumpableFor(owner, game, place);
                if (move is ActionMoveStep && move.IsLegal()) { movement = move; distance = d; }
            }
            return movement == null ? null : context.PlanAction(movement: movement);
        }
    }
}
