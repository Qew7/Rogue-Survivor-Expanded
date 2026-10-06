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
            if (map.CountExits == 0) return;
            // Exit discovery uses a grid radius and a direct ray, unlike the sensor's round FOV.
            int maxRange = game.Rules.ActorFOV(actor, map.LocalTime, game.Session.World.Weather);
            foreach (var entry in map.ExitEntries)
                if (NpcKnowledgeSystem.Visible(game, actor, new Location(map, entry.Key), maxRange))
                {
                    Location from = new Location(map, entry.Key), to = new Location(entry.Value.ToMap, entry.Value.ToPosition);
                    knowledge.Exits.RemoveAll(e => e.From == from); knowledge.Exits.Add(new NpcKnownExit(from, to));
                    if (knowledge.Exits.Count > 32) knowledge.Exits.RemoveAt(0);
                }
        }
    }
}
