using System;
using System.Collections.Generic;

namespace djack.RogueSurvivor.Data
{
    [Serializable]
    sealed class SocialGroup
    {
        // The founder's ID initializes the group namespace; later leaders never replace it.
        public readonly Guid Identity;
        public Guid LeaderId;
        public string LeaderName;
        public readonly int FactionId;
        public readonly List<Guid> Members = new List<Guid>();
        public long PlanSequence;
        public int NextPlanTurn;
        public NpcGroupPlan Plan;
        [System.Runtime.Serialization.OptionalField] public int SupplyRule;
        [System.Runtime.Serialization.OptionalField] public long LastSupplyRuleEvent;
        public SocialGroup(Actor founder, Guid identity)
        { Identity = identity; LeaderId = founder.PersonalityIdentity; LeaderName = founder.UnmodifiedName;
            FactionId = founder.Faction == null ? -1 : founder.Faction.ID; Members.Add(founder.PersonalityIdentity); }
    }
    [Serializable]
    sealed class NpcGroupPlan
    {
        public string StoryId, Kind, Stage;
        public Guid CollectorId, BeneficiaryId;
        public long CauseId;
        public Location Destination;
        public int Deadline;
        public bool Finished { get { return Stage == "completed" || Stage == "failed" || Stage == "declined"; } }
    }
    partial class Actor
    {
        SocialGroup m_SocialGroup;
        long m_GroupSequence;
        public SocialGroup SocialGroup { get {
            if (m_SocialGroup == null)
            {
                if (m_Leader != null) return m_Leader.SocialGroup;
                if (CountFollowers > 0) InitializeSocialGroup();
            }
            return m_SocialGroup; } }
        void InitializeSocialGroup()
        {
            lock (this)
            {
                if (m_SocialGroup != null) return;
                m_SocialGroup = new SocialGroup(this, m_GroupSequence++ == 0 ? PersonalityIdentity : Guid.NewGuid());
                AssignSocialGroup(this, m_SocialGroup);
            }
        }
        static void AssignSocialGroup(Actor actor, SocialGroup group)
        {
            actor.m_SocialGroup = group;
            if (!group.Members.Contains(actor.PersonalityIdentity)) group.Members.Add(actor.PersonalityIdentity);
            if (actor.m_Followers != null) foreach (Actor child in actor.m_Followers) AssignSocialGroup(child, group);
        }
        static void DetachSocialGroup(Actor actor, SocialGroup group)
        {
            if (group != null) group.Members.Remove(actor.PersonalityIdentity);
            actor.m_SocialGroup = null;
            if (actor.m_Followers != null) foreach (Actor child in actor.m_Followers) DetachSocialGroup(child, group);
        }
        void JoinSocialGroup(Actor follower)
        {
            if (m_SocialGroup == null && m_Leader != null) m_SocialGroup = m_Leader.SocialGroup;
            if (m_SocialGroup == null) InitializeSocialGroup();
            AssignSocialGroup(follower, m_SocialGroup);
        }
        void LeaveSocialGroup(Actor follower)
        {
            DetachSocialGroup(follower, m_SocialGroup);
        }
        public void TransferSocialGroupTo(Actor successor)
        {
            if (successor == null || successor.IsDead || successor.Leader != this) throw new ArgumentException("Successor must be a living follower.");
            SocialGroup group = m_SocialGroup ?? new SocialGroup(this, m_GroupSequence++ == 0 ? PersonalityIdentity : Guid.NewGuid());
            var survivors = new List<Actor>(m_Followers);
            RemoveAllFollowers(); m_SocialGroup = null;
            successor.m_SocialGroup = group; group.Members.Clear(); group.Members.Add(successor.PersonalityIdentity);
            AssignSocialGroup(successor, group);
            group.LeaderId = successor.PersonalityIdentity; group.LeaderName = successor.UnmodifiedName;
            foreach (Actor follower in survivors)
                if (follower != successor && !follower.IsDead)
                { successor.AddFollower(follower); follower.TrustInLeader = follower.GetTrustIn(successor); }
        }
    }
}
