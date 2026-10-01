using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine
{
    partial class RogueGame
    {
        NpcContentCatalog npcContent;
        // Runtime composition can be supplied by content packs or deterministic scenarios.
        internal NpcContentCatalog NpcContent
        {
            get { return npcContent ?? NpcContentCatalog.Default; }
            set { npcContent = value; }
        }
    }
}
