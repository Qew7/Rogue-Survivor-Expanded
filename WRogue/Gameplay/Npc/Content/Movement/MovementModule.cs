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
            catalog.Event(new NpcEventDefinition("building_explored", NpcRecordCategory.World, true,
                e => (e.Subject ?? "Someone") + " explored a building.",
                f => (f.ReportSubject ?? "someone") + " explored a building",
                NpcEventFields.Subject) { ReportActorRole = NpcReportActorRole.None });
            catalog.Perception(NpcPerceptionKind.Surroundings, PerceiveSurroundings);
            catalog.Operator(new NpcOperatorDefinition("travel", NpcPlanAction.Travel, Travel,
                archiveText: step => ResidentRecords.TravelText(step.Place)));
            catalog.Operator(new NpcOperatorDefinition("retreat", NpcPlanAction.Retreat, Retreat,
                archiveText: step => "retreat"));
        }
        static ActorAction Wrap(NpcExecutionContext context, ActorAction movement)
        {
            var c = new NpcActionContext(context);
            return c.Action(() => movement.IsLegal(), () => {
                movement.Perform();
                NpcIntentDefinition definition = c.Capability;
                bool arrived = context.Step.Action == NpcPlanAction.Retreat ?
                    c.Goal.LastKnown.Map != c.Owner.Location.Map || c.Game.Rules.GridDistance(c.Owner.Location.Position, c.Goal.LastKnown.Position) >= 5 :
                    NpcPlanExecution.Near(c.Game, c.Owner, context.Step.Place) && (definition.TravelArrived == null || definition.TravelArrived(c.Owner));
                if (arrived && c.Goal.Plan.Current == context.Step) c.Goal.Plan.Cursor++;
            });
        }
        static ActorAction Travel(NpcExecutionContext context)
        {
            NpcIntentDefinition capability = context.Game.NpcContent.Capability(context.Goal.DefinitionId);
            Location place = capability.TravelDestination == null ? context.Step.Place : capability.TravelDestination(context);
            ActorAction movement = context.Route == null ? null : context.Route(place);
            return movement == null ? null : Wrap(context, movement);
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
            return movement == null ? null : Wrap(context, movement);
        }
    }
}
