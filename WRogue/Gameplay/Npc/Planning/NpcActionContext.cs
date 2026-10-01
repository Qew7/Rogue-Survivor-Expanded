using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.Actions;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class NpcActionContext
    {
        public readonly RogueGame Game;
        public readonly Actor Owner, Target;
        public readonly NpcIntent Goal;
        public readonly NpcPlanStep Step;
        public readonly ItemFood Food;
        public readonly IList<Actor> Visible;
        public NpcActionContext(RogueGame game, Actor owner, NpcIntent goal, Actor target = null, ItemFood food = null)
        { Game = game; Owner = owner; Goal = goal; Target = target; Food = food; Visible = target == null ? new Actor[0] : new[] { target }; }
        public NpcActionContext(NpcExecutionContext context, ItemFood food = null)
        { Game = context.Game; Owner = context.Owner; Goal = context.Goal; Target = context.Target; Step = context.Step; Food = food; Visible = context.Visible; }
        public bool Owned { get { return NpcPlanExecution.Owned(Game, Owner, Goal) && (Step == null || Goal.Plan != null && Goal.Plan.Current == Step); } }
        public bool NearPerson(Guid id)
        { return Target != null && Target.PersonalityIdentity == id && !Target.IsSleeping && Target.Model.Abilities.IsIntelligent &&
            NpcIntentSystem.CanSee(Game, Owner, Target) && !Game.Rules.AreEnemies(Owner, Target) && NpcPlanExecution.Near(Game, Owner, Target.Location); }
        public NpcIntentDefinition Capability { get { return Game.NpcContent.Capability(Goal.DefinitionId); } }
        public ActorAction Action(Func<bool> legal, Action perform) { return new NpcMechanicAction(this, legal, perform); }
        public void Done(NpcPlanningState observed, bool repeat = false, bool retain = true)
        { NpcPlanExecution.Succeeded(this, observed, repeat, retain); }
        public SignificantEvent Publish(string kind, Actor other = null) { return NpcPlanExecution.Publish(Game, kind, Owner, other, Goal); }
    }
    sealed class NpcMechanicAction : ActorAction
    {
        readonly NpcActionContext context;
        readonly Func<bool> legal;
        readonly Action perform;
        public NpcMechanicAction(NpcActionContext context, Func<bool> legal, Action perform) : base(context.Owner, context.Game)
        { this.context = context; this.legal = legal; this.perform = perform; }
        public override bool IsLegal() { return context.Owned && legal(); }
        public override void Perform() { if (IsLegal()) perform(); }
    }
}
