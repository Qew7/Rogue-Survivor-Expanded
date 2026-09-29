using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class MovementModule
    {
        static void PerceiveSurroundings(NpcPerceptionContext c)
        {
            RogueGame game = c.Game; Actor actor = c.Owner;
            Map map = actor.Location.Map;
            NpcKnowledge knowledge = actor.Personality.Knowledge;
            foreach (var entry in map.ExitEntries)
                if (NpcKnowledgeSystem.Visible(game, actor, new Location(map, entry.Key)))
                {
                    Location from = new Location(map, entry.Key), to = new Location(entry.Value.ToMap, entry.Value.ToPosition);
                    knowledge.Exits.RemoveAll(e => e.From == from); knowledge.Exits.Add(new NpcKnownExit(from, to));
                    if (knowledge.Exits.Count > 32) knowledge.Exits.RemoveAt(0);
                }
        }
    }
}
