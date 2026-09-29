using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class NpcResourceDefinition
    {
        public readonly string Id;
        public readonly Func<RogueGame, Actor, bool> OwnNeed;
        public NpcResourceDefinition(string id, Func<RogueGame, Actor, bool> ownNeed)
        { Id = id; OwnNeed = ownNeed; }
    }
}
