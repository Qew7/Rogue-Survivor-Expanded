using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;
namespace djack.RogueSurvivor.Gameplay.AI
{
    abstract partial class BaseAI
    {
        protected ActorAction BehaviorNpcPlan(RogueGame game, NpcIntent goal, List<Actor> visible)
        { return NpcPlanRuntime.Choose(game, m_Actor, goal, visible, place => BehaviorNpcKnownRoute(game, place)); }
    }
}
