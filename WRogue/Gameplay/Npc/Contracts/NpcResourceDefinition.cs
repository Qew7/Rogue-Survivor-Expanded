using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class NpcResourceDefinition
    {
        public readonly string Id;
        public readonly Func<RogueGame, Actor, SignificantEvent, bool> OwnNeed;
        public NpcResourceDefinition(string id, Func<RogueGame, Actor, bool> ownNeed)
        { Id = id; OwnNeed = (game, actor, source) => ownNeed(game, actor); }
        public NpcResourceDefinition(string id, Func<RogueGame, Actor, SignificantEvent, bool> ownNeed)
        { Id = id; OwnNeed = ownNeed; }
    }
}
