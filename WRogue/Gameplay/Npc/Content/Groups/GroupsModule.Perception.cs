using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class GroupsModule
    {
        static void PerceiveSurroundings(NpcPerceptionContext c)
        {
            Actor actor = c.Owner;
            int turn = c.Turn, risk = c.Risk; Map map = actor.Location.Map;
            NpcKnowledge knowledge = actor.Personality.Knowledge;
            if (map.GetTileAt(actor.Location.Position).IsInside)
                knowledge.RememberPlace(new NpcKnownPlace(actor.Location, "shelter", turn));
        }
    }
}
