using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.MapObjects;

namespace djack.RogueSurvivor.Engine.Actions
{
    class ActionUseStorageContainer : ActorAction
    {
        readonly StorageContainer m_Container;
        readonly bool m_Open;

        public ActionUseStorageContainer(Actor actor, RogueGame game, StorageContainer container, bool open)
            : base(actor, game)
        {
            m_Container = container;
            m_Open = open;
        }

        public override bool IsLegal()
        {
            return m_Game.Rules.CanActorUseStorageContainer(m_Actor, m_Container, m_Open, out m_FailReason);
        }

        public override void Perform()
        {
            m_Game.DoUseStorageContainer(m_Actor, m_Container, m_Open);
        }
    }
}
