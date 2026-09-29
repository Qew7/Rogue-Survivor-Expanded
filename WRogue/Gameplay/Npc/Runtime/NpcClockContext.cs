using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    enum NpcClockPhase { MapTurn, BeforeDecision }
    sealed class NpcClockContext
    {
        public readonly RogueGame Game;
        public readonly Actor Owner;
        public NpcClockContext(RogueGame game, Actor owner) { Game = game; Owner = owner; }
    }
}
