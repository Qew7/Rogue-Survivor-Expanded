using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.MapObjects;

namespace djack.RogueSurvivor.Engine.Actions
{
    class ActionInspectStorage : ActorAction
    {
        readonly StorageContainer m_Storage;

        public ActionInspectStorage(Actor actor, RogueGame game, StorageContainer storage)
            : base(actor, game) { m_Storage = storage; }

        public override bool IsLegal()
        { return m_Game.Rules.CanActorInspectStorage(m_Actor, m_Storage, out m_FailReason); }

        public override void Perform()
        { m_Game.DoInspectStorage(m_Actor, m_Storage); }
    }
}
