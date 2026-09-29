using System;
using djack.RogueSurvivor.Data;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class ConflictResolutionModule : INpcContentModule, INpcGoalSource
    {
        public string Id { get { return "conflict-resolution"; } }
        public void Register(NpcCatalogBuilder c)
        {
            c.GoalSource(this);
            RegisterMechanic(c, "Protection", "Defend someone I value", "defend_person", "Defended",
                m => 25 + m.Compassion + m.Courage + m.Group / 2 + m.CommunitySecurity, NpcConflictActions.Defend, true);
            RegisterMechanic(c, "Retaliation", "Retaliate for remembered harm", "retaliate", "Retaliated",
                m => m.Courage - m.Compassion - m.Law + m.Grievance / 2, NpcConflictActions.Retaliate, true);
            RegisterMechanic(c, "Intimidation", "Deter remembered misconduct", "intimidate", "ThreatCommunicated",
                m => m.Courage - m.Law - m.Compassion + m.Grievance / 3, NpcConflictActions.Threaten);
            RegisterMechanic(c, "RepairTrust", "Attempt to repair a relationship", "apologize", "ApologyCommunicated",
                m => 20 + m.Compassion + m.Law + m.Attachment / 2, NpcConflictActions.Apologize);
            RegisterMechanic(c, "GroupSecurity", "Exclude a dangerous companion", "expel_member", "MemberExpelled",
                m => 15 + m.Group + m.Law + m.Courage + m.Grievance / 3, NpcConflictActions.Expel);
            c.Capabilities["defend_person"].OnTargetDeath = killed => new NpcIntentOutcome(NpcIntentStatus.Completed, "the known threat died");
            c.Capabilities["retaliate"].OnTargetDeath = killed => new NpcIntentOutcome(killed ? NpcIntentStatus.Completed : NpcIntentStatus.Abandoned,
                killed ? "actually retaliated against the attacker" : "someone else ended the threat");
            RegisterEvents(c);
        }
        static void RegisterMechanic(NpcCatalogBuilder c, string value, string description, string id, string fact,
            Func<NpcMotivation, int> importance, Func<NpcActionContext, djack.RogueSurvivor.Data.ActorAction> action, bool combat = false)
        {
            c.Fact(fact); c.Value(new NpcValueDefinition(value, description, null, importance));
            c.Capability(new NpcIntentDefinition(id, description, 180, 360) { ResultFacts = (catalog, g) => catalog.Facts.Mask(fact),
                DirectAction = action, AssignedScore = (owner, target) => importance(new NpcMotivation(owner, target)), AllowHostile = combat, ActDuringDanger = combat, AllowQuestions = true });
            c.Operator(new NpcOperatorDefinition(id, null, context => action(new NpcActionContext(context))));
            c.OperatorSource(new NpcOperatorSource(id, catalog => catalog.Facts.Mask(fact), d => {
                ulong at = d.At(d.Goal.LastKnown); if (at == 0) return;
                d.Travel(d.Goal.LastKnown, d.Goal.TargetId, at);
                d.Add(id, d.Goal.LastKnown, d.Goal.TargetId, at, 0, d.Catalog.Facts.Mask(fact), default(NpcPlanningState), 1);
            }));
        }
        public void Evaluate(NpcGoalContext c, NpcGoalOffers offers)
        {
            Actor owner = c.Owner;
            foreach (NpcKnownPerson person in c.People)
            {
                if (person.Dead || person.Id == c.Self.Id) continue;
                RelationshipRecord opinion = owner.Personality.Person(person.Id);
                bool recent = c.Turn - person.ThreatTurn < 180;
                if (recent && person.Danger > 0 && opinion != null && opinion.Grievance >= 20)
                    offers.Add(person, "Retaliation", "retaliate", 0, 1, person.Danger, person.ThreatConfidence, person.ThreatCause);
                if (!person.Hostile && person.Violation > 0 && person.ViolationConfidence >= 60)
                    offers.Add(person, "Intimidation", "intimidate", 0, 1, person.Violation, person.ViolationConfidence, person.ViolationCause);
                if (owner.SocialGroup != null && owner.SocialGroup.LeaderId == c.Self.Id &&
                    owner.SocialGroup.Members.Contains(person.Id) && person.Violation >= 80 && person.ViolationConfidence >= 80)
                    offers.Add(person, "GroupSecurity", "expel_member", 0, 1, person.Violation, person.ViolationConfidence, person.ViolationCause);
            }
            foreach (NpcFact fact in owner.Personality.Knowledge.Facts)
            {
                if (fact.Kind != "attack" || c.Turn - fact.EventTurn >= 180 || fact.SubjectId == c.Self.Id) continue;
                NpcKnownPerson victim = c.FindPerson(p => p.Id == fact.SubjectId), aggressor = c.FindPerson(p => p.Id == fact.OtherId);
                if (victim == null || victim.Dead || aggressor == null || aggressor.Dead) continue;
                RelationshipRecord attachment = owner.Personality.Person(victim.Id);
                bool close = attachment != null && attachment.Attachment >= 20 || victim.GroupId != Guid.Empty && owner.SocialGroup != null && victim.GroupId == owner.SocialGroup.Identity;
                if (close || owner.Faction != null && victim.FactionId == owner.Faction.ID && c.Catalog.FactionPolicy(owner.Faction.ID).Security > 0) offers.Add(aggressor, "Protection", "defend_person", 0, 1, 100, fact.Confidence, fact.EventId,
                    fact.StoryId, obligation: fact.EventId);
            }
        }
    }
}
