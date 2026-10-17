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
    sealed class NpcSupplyPermission
    {
        public Guid Grantor;
        public Location Place;
        public string Resource;
        public int ExpiresTurn, Units;
        public long CauseId;
    }
    enum NpcServiceStatus { Offered, Accepted, Completed, Refused, Failed }
    [Serializable]
    sealed class NpcServiceAgreement
    {
        public long Id, CauseId;
        public Guid Provider, Patient;
        public string ProviderName, PatientName, StoryId;
        public Location Shelter;
        public int DueTurn;
        public NpcServiceStatus Status;
        public NpcServiceAgreement Copy() { return (NpcServiceAgreement)MemberwiseClone(); }
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
        [System.Runtime.Serialization.OptionalField] List<NpcSupplyPermission> m_Permissions;
        [System.Runtime.Serialization.OptionalField] List<NpcServiceAgreement> m_ServiceAgreements;
        [NonSerialized] bool? m_HasOpenServiceAgreements;
        [System.Runtime.Serialization.OptionalField] Guid m_KnownSupplyRuleGroup;
        [System.Runtime.Serialization.OptionalField] int m_KnownSupplyRule;
        [System.Runtime.Serialization.OptionalField] long m_KnownSupplyRuleEvent;
        public bool HasCommitments { get { return m_Commitments != null && m_Commitments.Count > 0; } }
        public bool HasDisputes { get { return m_Disputes != null && m_Disputes.Count > 0; } }
        public bool HasAttachments { get { return m_Attachments != null && m_Attachments.Count > 0; } }
        public bool CanRememberCommitment { get { return m_Commitments == null || m_Commitments.Count < 16 ||
            m_Commitments.Exists(p => p.Status != NpcCommitmentStatus.Active); } }
        public List<NpcCommitment> Commitments { get { return m_Commitments ?? (m_Commitments = new List<NpcCommitment>()); } }
        public List<NpcResourceDispute> Disputes { get { return m_Disputes ?? (m_Disputes = new List<NpcResourceDispute>()); } }
        public List<NpcAttachment> Attachments { get { return m_Attachments ?? (m_Attachments = new List<NpcAttachment>()); } }
        public List<NpcSupplyPermission> Permissions { get { return m_Permissions ?? (m_Permissions = new List<NpcSupplyPermission>()); } }
        public List<NpcServiceAgreement> ServiceAgreements { get { return m_ServiceAgreements ?? (m_ServiceAgreements = new List<NpcServiceAgreement>()); } }
        public bool HasServiceAgreements { get { return m_ServiceAgreements != null && m_ServiceAgreements.Count > 0; } }
        public bool CanRememberService { get { return m_ServiceAgreements == null || m_ServiceAgreements.Count < 8 ||
            m_ServiceAgreements.Exists(a => a.Status != NpcServiceStatus.Offered && a.Status != NpcServiceStatus.Accepted); } }
        public bool HasOpenServiceAgreements
        {
            get
            {
                if (!m_HasOpenServiceAgreements.HasValue)
                    m_HasOpenServiceAgreements = m_ServiceAgreements != null && m_ServiceAgreements.Exists(a =>
                        a.Status == NpcServiceStatus.Offered || a.Status == NpcServiceStatus.Accepted);
                return m_HasOpenServiceAgreements.Value;
            }
        }
        public void SetServiceStatus(NpcServiceAgreement agreement, NpcServiceStatus status)
        { agreement.Status = status; m_HasOpenServiceAgreements = null; }
        public int KnownSupplyRule(Guid group) { return group == m_KnownSupplyRuleGroup ? m_KnownSupplyRule : 0; }
        public void LearnSupplyRule(Guid group, int rule, long eventId)
        {
            if (group == Guid.Empty || group == m_KnownSupplyRuleGroup && eventId <= m_KnownSupplyRuleEvent) return;
            m_KnownSupplyRuleGroup = group; m_KnownSupplyRule = rule; m_KnownSupplyRuleEvent = eventId;
        }
        public bool RememberService(NpcServiceAgreement agreement)
        {
            if (ServiceAgreements.Exists(a => a.Id == agreement.Id)) return false;
            if (ServiceAgreements.Count >= 8)
            {
                int old = ServiceAgreements.FindIndex(a => a.Status != NpcServiceStatus.Offered && a.Status != NpcServiceStatus.Accepted);
                if (old < 0) return false;
                ServiceAgreements.RemoveAt(old);
            }
            ServiceAgreements.Add(agreement.Copy()); m_HasOpenServiceAgreements = null; return true;
        }
        public void Permit(NpcSupplyPermission permission)
        {
            Permissions.RemoveAll(p => p.Grantor == permission.Grantor && p.Place == permission.Place && p.Resource == permission.Resource);
            if (Permissions.Count >= 16) Permissions.RemoveAt(0);
            Permissions.Add(permission);
        }
        public int AvailablePermission(Guid grantor, Location place, string resource, int turn)
        {
            if (m_Permissions == null) return 0;
            NpcSupplyPermission permission = m_Permissions.Find(p => p.Grantor == grantor && p.Place == place &&
                p.Resource == resource && p.ExpiresTurn >= turn && p.Units > 0);
            return permission == null ? 0 : permission.Units;
        }
        public long UsePermission(Guid grantor, Location place, string resource, int turn, int requestedUnits, out int permittedUnits)
        {
            permittedUnits = 0;
            if (m_Permissions == null || requestedUnits <= 0) return 0;
            NpcSupplyPermission permission = m_Permissions.Find(p => p.Grantor == grantor && p.Place == place &&
                p.Resource == resource && p.ExpiresTurn >= turn && p.Units > 0);
            if (permission == null) return 0;
            long cause = permission.CauseId;
            permittedUnits = Math.Min(permission.Units, requestedUnits);
            permission.Units -= permittedUnits;
            if (permission.Units == 0) m_Permissions.Remove(permission);
            return cause;
        }
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
