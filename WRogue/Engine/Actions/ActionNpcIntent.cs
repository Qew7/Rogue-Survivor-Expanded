using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine.Actions
{
    sealed class ActionNpcIntent : ActorAction
    {
        readonly ActorAction mechanic;
        public ActionNpcIntent(Actor actor, RogueGame game, NpcIntent goal, Actor target = null, ItemFood food = null) : base(actor, game)
        {
            NpcIntentDefinition definition = goal == null ? null : game.NpcContent.Capability(goal.DefinitionId);
            mechanic = definition == null || definition.DirectAction == null ? null : definition.DirectAction(new NpcActionContext(game, actor, goal, target, food));
        }
        public override bool IsLegal() { return mechanic != null && mechanic.IsLegal(); }
        public override void Perform() { if (IsLegal()) mechanic.Perform(); }
    }
}
