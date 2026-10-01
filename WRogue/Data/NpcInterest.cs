using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
namespace djack.RogueSurvivor.Data
{
    [Serializable]
    sealed class NpcInterest
    {
        public string DefinitionId, Name;
        public Guid Subject;
        public Location Place;
        public int CreatedTurn, LastEvidenceTurn, ExpiresTurn, Weight, Need;
        public long CauseId;
    }
    sealed partial class PersonalityState
    {
        [OptionalField] List<NpcInterest> m_Interests;
        public bool HasInterests { get { return m_Interests != null && m_Interests.Count != 0; } }
        public IList<NpcInterest> Interests { get { return m_Interests == null ? (IList<NpcInterest>)new NpcInterest[0] : m_Interests.AsReadOnly(); } }
        public NpcInterest Interest(string id, Guid subject)
        { return m_Interests == null ? null : m_Interests.Find(i => i.DefinitionId == id && i.Subject == subject); }
        public void RememberInterest(NpcInterest interest)
        {
            if (String.IsNullOrWhiteSpace(interest.DefinitionId) || interest.Subject == Guid.Empty || interest.ExpiresTurn <= interest.CreatedTurn) throw new ArgumentException("Invalid lasting interest.");
            NpcInterest existing = Interest(interest.DefinitionId, interest.Subject);
            if (existing != null) { if (existing.ExpiresTurn <= interest.CreatedTurn) { existing.CreatedTurn = interest.CreatedTurn; existing.Place = interest.Place; existing.Need = interest.Need; existing.CauseId = interest.CauseId; } existing.ExpiresTurn = Math.Max(existing.ExpiresTurn, interest.ExpiresTurn); existing.LastEvidenceTurn = interest.LastEvidenceTurn; existing.Weight = interest.Weight; return; }
            if (m_Interests == null) m_Interests = new List<NpcInterest>();
            m_Interests.RemoveAll(i => i.ExpiresTurn <= interest.CreatedTurn);
            if (m_Interests.Count >= 16) return;
            m_Interests.Add(interest);
        }
    }
}
