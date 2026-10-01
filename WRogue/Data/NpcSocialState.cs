using System;
using System.Collections.Generic;

namespace djack.RogueSurvivor.Data
{
    enum NpcCommitmentStatus { Active, Kept, Broken, Released }
    [Serializable]
    sealed class NpcCommitment
    {
        public long Id, CauseId;
        public Guid Promisor, Beneficiary;
        public string PromisorName, BeneficiaryName, Resource, StoryId;
        public Guid GroupId;
        public string GroupName, FactionName;
        public int FactionId = -1;
        public int DueTurn, Units = 1;
        public NpcCommitmentStatus Status;
        public bool OutcomeReported;
        public NpcCommitment Copy() { return (NpcCommitment)MemberwiseClone(); }
    }
    [Serializable]
    sealed class NpcResourceDispute
    {
        public Location Place;
        public Guid Other;
        public string StoryId, Resource;
        public long CauseId;
        public int Turn;
        public string Response;
    }
    [Serializable]
    sealed class NpcAttachment
    {
        public string Kind, Name, Resource;
        public Guid Person;
        public Guid ItemId;
        public Location Place;
        public int ModelId = -1, Weight, MissingUnits;
        public long CauseId;
    }
    sealed partial class PersonalityState
    {
        List<NpcCommitment> m_Commitments;
        List<NpcResourceDispute> m_Disputes;
        List<NpcAttachment> m_Attachments;
        public bool HasCommitments { get { return m_Commitments != null && m_Commitments.Count > 0; } }
        public bool HasDisputes { get { return m_Disputes != null && m_Disputes.Count > 0; } }
        public bool HasAttachments { get { return m_Attachments != null && m_Attachments.Count > 0; } }
        public bool CanRememberCommitment { get { return m_Commitments == null || m_Commitments.Count < 16 ||
            m_Commitments.Exists(p => p.Status != NpcCommitmentStatus.Active); } }
        public List<NpcCommitment> Commitments { get { return m_Commitments ?? (m_Commitments = new List<NpcCommitment>()); } }
        public List<NpcResourceDispute> Disputes { get { return m_Disputes ?? (m_Disputes = new List<NpcResourceDispute>()); } }
        public List<NpcAttachment> Attachments { get { return m_Attachments ?? (m_Attachments = new List<NpcAttachment>()); } }
        public bool RememberCommitment(NpcCommitment promise)
        {
            if (Commitments.Exists(p => p.Id == promise.Id)) return false;
            if (Commitments.Count >= 16)
            { int old = Commitments.FindIndex(p => p.Status != NpcCommitmentStatus.Active); if (old < 0) return false; Commitments.RemoveAt(old); }
            Commitments.Add(promise.Copy()); return true;
        }
        public NpcResourceDispute Dispute(Location place, Guid other)
        { return m_Disputes == null ? null : m_Disputes.Find(d => d.Place == place && d.Other == other); }
        public void RememberDispute(NpcResourceDispute dispute)
        {
            Disputes.RemoveAll(d => d.Place == dispute.Place && d.Other == dispute.Other);
            if (Disputes.Count >= 16) Disputes.RemoveAt(0); Disputes.Add(dispute);
        }
        public void Attach(NpcAttachment attachment)
        {
            NpcAttachment old = Attachments.Find(a => a.Kind == attachment.Kind && a.Person == attachment.Person &&
                a.Place == attachment.Place && a.ModelId == attachment.ModelId && a.ItemId == attachment.ItemId && a.Resource == attachment.Resource);
            if (old != null) { old.Weight = Math.Max(old.Weight, attachment.Weight); return; }
            if (Attachments.Count >= 16) Attachments.RemoveAt(0); Attachments.Add(attachment);
        }
    }
}
