using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class NpcExecutionContext
    {
        public readonly RogueGame Game;
        public readonly Actor Owner, Target;
        public readonly NpcIntent Goal;
        public readonly NpcPlanStep Step;
        public readonly IList<Actor> Visible;
        public readonly Func<Location, ActorAction> Route;
        public NpcExecutionContext(RogueGame game, Actor owner, NpcIntent goal, NpcPlanStep step,
            IList<Actor> visible, Func<Location, ActorAction> route)
        { Game = game; Owner = owner; Goal = goal; Step = step; Visible = visible; Route = route; Target = NpcIntentSystem.VisibleTarget(visible, step.Target); }
    }

    interface INpcActionGuard { NpcActionAccess Check(NpcExecutionContext context, NpcOperatorDefinition action); }
    sealed class NpcActionAccess
    {
        public readonly bool Allowed;
        public readonly ActorAction Reaction;
        public NpcActionAccess(bool allowed, ActorAction reaction = null) { Allowed = allowed; Reaction = reaction; }
    }
}
