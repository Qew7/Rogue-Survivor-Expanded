using System;
using djack.RogueSurvivor.Data;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class InterestsModule
    {
        static void ObservePeople(NpcGoalContext c)
        {
            foreach (NpcKnownPerson person in c.People)
            {
                RelationshipRecord opinion = c.Owner.Personality.Person(person.Id);
                if (!person.Dead && !person.Hostile && opinion != null && opinion.Attachment >= 20)
                    Remember(c, "protect_person", person, 1, opinion.Attachment, person.SocialCause);
            }
        }
        static void ProtectPerson(NpcGoalContext c, NpcInterest interest, NpcGoalOffers offers)
        {
            NpcKnownPerson friend = c.FindPerson(p => p.Id == interest.Subject);
            if (friend == null || friend.Dead) return;
            foreach (NpcFact fact in c.Owner.Personality.Knowledge.Facts)
            {
                if (fact.Kind != "attack" || fact.Source == NpcKnowledgeSource.Told && (!fact.NamesSubject || !fact.NamesOther) ||
                    fact.SubjectId != friend.Id || c.Turn - fact.EventTurn >= 180) continue;
                NpcKnownPerson aggressor = c.FindPerson(p => p.Id == fact.OtherId);
                if (aggressor != null && !aggressor.Dead) offers.Add(aggressor, "Protection", "defend_person", 0, 1, 100, fact.Confidence,
                    fact.EventId, fact.StoryId, obligation: fact.EventId);
            }
        }
        static void ObserveTrust(NpcGoalContext c)
        {
            if (c.Owner.Personality.HasCommitments) foreach (NpcCommitment promise in c.Owner.Personality.Commitments)
            {
                if (promise.Promisor != c.Self.Id || promise.Status != NpcCommitmentStatus.Broken) continue;
                NpcKnownPerson beneficiary = c.FindPerson(p => p.Id == promise.Beneficiary);
                if (beneficiary != null && c.Owner.Personality.Interest("repair_trust", beneficiary.Id) == null)
                    Remember(c, "repair_trust", beneficiary, 1, 25, promise.Id);
            }
            foreach (NpcKnownPerson person in c.People)
            {
                if (person.Dead || person.Hostile || person.LossUnits <= 0) continue;
                NpcInterest prior = c.Owner.Personality.Interest("repair_trust", person.Id);
                if (prior != null && prior.CauseId == person.LossCause) continue;
                Remember(c, "repair_trust", person, 1, 25, person.LossCause);
                NpcInterest interest = c.Owner.Personality.Interest("repair_trust", person.Id);
                if (interest != null) { interest.CauseId = person.LossCause; interest.Need = 1; interest.LastEvidenceTurn = c.Turn; }
            }
        }
    }
}
