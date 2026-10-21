using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

namespace djack.RogueSurvivor.Engine.Actions
{
    class ActionWriteNote : ActorAction
    {
        readonly ItemNote m_Note;
        readonly NoteTopic m_Topic;
        readonly StorageContainer m_Cache;
        readonly bool m_Signed;
        readonly NpcFact m_Evidence;

        public ActionWriteNote(Actor actor, RogueGame game, ItemNote note, NoteTopic topic,
            StorageContainer cache = null, bool signed = true, NpcFact evidence = null) : base(actor, game)
        {
            m_Note = note;
            m_Topic = topic;
            m_Cache = cache;
            m_Signed = signed;
            m_Evidence = evidence;
        }

        public override bool IsLegal()
        {
            return m_Game.Rules.CanActorWriteNote(m_Actor, m_Note, m_Topic, m_Cache, out m_FailReason, m_Evidence);
        }

        public override void Perform()
        {
            m_Game.DoWriteNote(m_Actor, m_Note, m_Topic, m_Cache, m_Signed, m_Evidence);
        }
    }
}
