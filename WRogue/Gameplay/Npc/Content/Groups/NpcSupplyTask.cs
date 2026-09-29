using System;
using djack.RogueSurvivor.Data;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class NpcSupplyTask
    {
        readonly string id, resource, eventId, capability;
        readonly NpcCollectiveScope scope;
        readonly Func<NpcCollectiveContext, int> willingness;
        readonly Func<NpcKnownPerson, int, bool> needed;
        readonly Func<NpcKnownPerson, long> cause;
        public NpcSupplyTask(string id, string resource, string eventId, string capability,
            Func<NpcCollectiveContext, int> willingness, Func<NpcKnownPerson, int, bool> needed, Func<NpcKnownPerson, long> cause, NpcCollectiveScope scope = NpcCollectiveScope.Group)
        { this.id = id; this.resource = resource; this.eventId = eventId; this.capability = capability; this.willingness = willingness; this.needed = needed; this.cause = cause; this.scope = scope; }
        public void Register(NpcCatalogBuilder catalog)
        {
            catalog.Collective(new NpcCollectiveDefinition(id, eventId, Propose,
                (a, b, p) => b.PersonalityIdentity == p.CollectorId && NpcIntentSystem.Enabled(b),
                (a, p) => "Fetch " + resource + " near " + p.Destination.Map.Name + " for " +
                    (a.Personality.Knowledge.Person(p.BeneficiaryId) == null ? "our companion" : a.Personality.Knowledge.Person(p.BeneficiaryId).Name) + ". Then report back.",
                (g, a, e) => NpcGroupTasks.AcceptSupply(g, a, e, resource, capability)) { Scope = scope, RequiresDeliveryReport = true, CoordinatorCapability = scope == NpcCollectiveScope.Group ? "coordinate_group_supplies" : null });
            catalog.On(eventId, NpcObservationPhase.Knowledge, o => NpcStorySystem.AcceptCollective(o.Game, o.Owner, o.Source));
        }
        NpcCollectiveOffer Propose(NpcCollectiveContext c)
        {
            int priority = willingness(c); if (priority < 15) return null;
            NpcKnownPlace cache = c.Knowledge.Places.Find(p => p.Kind == resource && p.Units >= 2 && c.Turn - p.SeenTurn < 180);
            if (cache == null) return null;
            foreach (NpcKnownPerson beneficiary in c.Knowledge.People)
            {
                if (beneficiary.Dead || beneficiary.Hostile || !(scope == NpcCollectiveScope.Group ? c.Group != null && c.Group.Members.Contains(beneficiary.Id) : beneficiary.FactionId == c.Leader.Faction.ID && beneficiary.Id != c.Leader.PersonalityIdentity) || !needed(beneficiary, c.Turn)) continue;
                Actor collector = null; int score = Int32.MinValue;
                NpcIntentDefinition definition = c.Game.NpcContent.Capability(capability);
                foreach (Actor member in c.Visible)
                {
                    if (member.IsPlayer || member.IsSleeping || member.PersonalityIdentity == beneficiary.Id || (scope == NpcCollectiveScope.Group ? member.SocialGroup != c.Group : member.Faction.ID != c.Leader.Faction.ID) || !NpcIntentSystem.Enabled(member)) continue;
                    int value = definition.ScoreKnown(member, 0, false, c.Game.NpcContent.Personalities);
                    if (value >= definition.Threshold && value > score) { collector = member; score = value; }
                }
                if (collector != null) return new NpcCollectiveOffer(new NpcGroupPlan { Kind = id, Stage = "proposed", CauseId = cause(beneficiary),
                    CollectorId = collector.PersonalityIdentity, BeneficiaryId = beneficiary.Id, Destination = cache.Place,
                    Deadline = c.Turn + WorldTime.TURNS_PER_DAY }, priority);
            }
            return null;
        }
    }
}
