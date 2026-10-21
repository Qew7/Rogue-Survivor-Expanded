using System;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Engine.Items
{
    enum NoteTopic : byte { Location = 1, CacheSupplies = 2, Event = 3,
        RequestReturn = 4, Warning = 5, SearchIntent = 6, Confession = 7, Regret = 8,
        Taunt = 9, Justification = 10, Plea = 11, Threat = 12 }

    [Serializable]
    class ItemNote : Item
    {
        string m_Text;
        Guid m_AuthorId;
        string m_Signature;
        NoteTopic m_Topic;
        Location m_Place;
        Guid m_CacheId;
        string m_Resource;
        int m_Units;
        int m_WrittenTurn;
        NpcFact m_Claim;

        public Guid Id { get { return StoryIdentity; } }
        public bool IsBlank { get { return m_Text == null; } }
        public string Text { get { return m_Text; } }
        public string Signature { get { return m_Signature; } }
        public NoteTopic Topic { get { return m_Topic; } }
        public Location Place { get { return m_Place; } }
        public Guid CacheId { get { return m_CacheId; } }
        public string Resource { get { return m_Resource; } }
        public int Units { get { return m_Units; } }
        public int WrittenTurn { get { return m_WrittenTurn; } }
        public NpcFact Claim { get { return m_Claim; } }
        internal Guid AuthorId { get { return m_AuthorId; } }
        public Guid SignedAuthorId { get { return m_Signature == null ? Guid.Empty : m_AuthorId; } }

        public ItemNote(ItemModel model) : base(model) { var id = StoryIdentity; }

        public void Write(Actor author, NoteTopic topic, Location place, Guid cacheId,
            string resource, int units, bool signed)
        { Write(author, topic, place, cacheId, resource, units, signed, null); }

        public void Write(Actor author, NoteTopic topic, Location place, Guid cacheId,
            string resource, int units, bool signed, NpcFact evidence)
        {
            if (!IsBlank || author == null || place.Map == null)
                throw new InvalidOperationException("note cannot be written");
            if (topic < NoteTopic.Location || topic > NoteTopic.Threat)
                throw new ArgumentOutOfRangeException("topic");
            if (topic == NoteTopic.CacheSupplies && (cacheId == Guid.Empty || units <= 0 || String.IsNullOrEmpty(resource)))
                throw new ArgumentException("cache note needs observed supplies");
            if (topic != NoteTopic.Location && topic != NoteTopic.CacheSupplies &&
                topic != NoteTopic.SearchIntent && evidence == null)
                throw new ArgumentException("report note needs known evidence");
            m_AuthorId = author.PersonalityIdentity;
            m_Signature = signed ? author.UnmodifiedName : null;
            m_Topic = topic;
            m_Place = place;
            m_CacheId = cacheId;
            m_Resource = resource;
            m_Units = units;
            m_WrittenTurn = author.Location.Map.LocalTime.TurnCounter;
            m_Claim = evidence == null ? null : evidence.Retell(m_AuthorId, m_WrittenTurn,
                Math.Min(90, evidence.Confidence));
            if (!signed && m_Claim != null)
            {
                if (m_Claim.SubjectId == m_AuthorId)
                { m_Claim.SubjectId = Guid.Empty; m_Claim.SubjectName = null; m_Claim.SubjectReportName = null; }
                if (m_Claim.OtherId == m_AuthorId)
                { m_Claim.OtherId = Guid.Empty; m_Claim.OtherName = null; m_Claim.OtherReportName = null; }
                m_Claim.SourceId = Guid.Empty;
            }
            m_Text = Gameplay.Personality.NpcNoteTemplates.Render(author, topic, place,
                resource, units, evidence);
            if (signed) m_Text += "\n\n" + m_Signature;
        }
    }
}
