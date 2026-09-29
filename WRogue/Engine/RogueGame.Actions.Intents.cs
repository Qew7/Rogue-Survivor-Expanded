using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.Actions;

namespace djack.RogueSurvivor.Engine
{
    partial class RogueGame
    {
        internal void DoNpcIntent(Actor actor, Actor target, NpcIntent intent, ItemFood food)
        { new ActionNpcIntent(actor, this, intent, target, food).Perform(); }
    }
}
