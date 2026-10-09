using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class PossessionsModule : INpcContentModule, INpcGoalSource
    {
        public string Id { get { return "possessions"; } }
        public void Register(NpcCatalogBuilder catalog)
        {
            catalog.OperatorSource(new NpcOperatorSource("item.acquire", c => (ulong)NpcPlanFact.ValuedItem, PossessionPlanOperators.Build,
                available: d => d.Goal.Generated != null && d.Goal.Generated.ModelId >= 0));
            catalog.Resource(new NpcResourceDefinition("item", (g, a, source) => a.Personality != null && a.Personality.HasAttachments &&
                a.Personality.Attachments.Exists(x => x.Kind == "item" && (source.ModelId < 0 || x.ModelId == source.ModelId) &&
                    (source.ItemId == Guid.Empty || x.ItemId == Guid.Empty || x.ItemId == source.ItemId))));
            catalog.GoalSource(this);
            catalog.Value(new NpcValueDefinition("Possession", "Recover a valued kind of item", NpcGoalValue.Possession, m => 30 + m.Supplies, false)
                { IdentitySuffix = g => ":" + g.ModelId + ":" + g.ItemId.ToString("N") });
            catalog.Value(new NpcValueDefinition("ProtectHome", "Return to threatened shelter", NpcGoalValue.ProtectHome, m => 25 + m.Group + m.Supplies, false) { ArrivalEvent = "home_reached" });
            var valueditem = new NpcIntentDefinition("recover_valued_item", "Recover a valued item", NpcIntentMethod.ObtainValuedItem, 25, 20, 180, 180, 0);

            valueditem.Result = (c, g) => (ulong)(NpcPlanFact.ValuedItem);
            catalog.Capability(valueditem);
            RegisterContent(catalog);
        }
        public void Evaluate(NpcGoalContext context, NpcGoalOffers offers)
        {
            Actor owner = context.Owner; NpcKnownPerson self = context.Self;
            IList<NpcKnownPerson> people = context.People; int turn = context.Turn, maxHP = context.MaxHP;
            if (owner.Personality.HasAttachments) foreach (NpcAttachment attachment in owner.Personality.Attachments)
            {
                if (attachment.Kind == "item")
                {
                    bool owned = false; foreach (Item item in owner.Inventory.Items) if (item.Model.ID == attachment.ModelId && (attachment.ItemId == Guid.Empty || item.StoryIdentity == attachment.ItemId)) owned = true;
                    string kind = attachment.ItemId == Guid.Empty ? "item:" + attachment.ModelId : "item:" + attachment.ItemId.ToString("N");
                    bool known = owner.Personality.Knowledge.Places.Exists(p => p.Kind == kind && p.Units > 0 && turn - p.SeenTurn <= 180);
                    if (!owned && known) offers.Add(self, NpcGoalValue.Possession, context.Catalog.Capability("recover_valued_item"),
                        0, 1, 100, 100, attachment.CauseId, resource: "item", model: attachment.ModelId, itemId: attachment.ItemId);
                }
                if (attachment.Kind == "place" && attachment.Place.Map != null && (attachment.MissingUnits > 0 || context.AnyPerson(p =>
                    p.Danger > 0 && p.Place.Map == attachment.Place.Map && turn - p.ThreatTurn < 180 && NpcGoalContext.Distance(p.Place, attachment.Place) < 5)) &&
                    owner.Location.Map == attachment.Place.Map && (NpcGoalContext.Distance(owner.Location, attachment.Place) > 1 || context.Pending(NpcGoalValue.ProtectHome, self.Id)))
                    offers.Add(self, NpcGoalValue.ProtectHome, context.Catalog.Capability("seek_group_shelter"),
                        0, 1, 100, 100, attachment.CauseId, resource: "home", objectPlace: attachment.Place);
            }
        }
        void RegisterContent(NpcCatalogBuilder catalog)
        {
            catalog.Perception(NpcPerceptionKind.Items, PerceiveItems);
            catalog.Perception(NpcPerceptionKind.Surroundings, c => NpcHomeObservation.RememberHome(c.Owner));
            RegisterEvents(catalog);
            catalog.Operator(new NpcOperatorDefinition("item.take", NpcPlanAction.PickupValuedItem, c => new ActionNpcValuedItem(c.Owner, c.Game, c.Goal, c.Step), "item", archiveText: step => "retrieve a valued item"));
        }
        void RegisterEvents(NpcCatalogBuilder catalog)
        {
            catalog.Event(new NpcEventDefinition("valued_item_acquired", NpcRecordCategory.None, false, e => (e.Subject ?? "Someone") + " acquired a personally valued kind of item.", null));
            catalog.Event(new NpcEventDefinition("home_reached", NpcRecordCategory.None, false, e => (e.Subject ?? "Someone") + " returned to their threatened home.", null));
            catalog.Event(new NpcEventDefinition("supplies_lost", NpcRecordCategory.None, true,
                e => (e.Subject ?? "Someone") + " lost supplies.",
                f => f.NamesOther ? f.ReportOther + " took " + (f.Units > 0 ? f.Units + " units of " : "") +
                    (f.Resource == "food" ? "food" : "stored supplies") + " from " + (f.ReportSubject ?? "someone") + "'s storage" :
                    (f.ReportSubject ?? "Someone") + "'s base lost " +
                    (f.Units > 0 ? f.Units + " units of " : "") +
                    (f.Resource == "food" ? "food" : "stored supplies") + " from storage")
                { ReportActorRole = NpcReportActorRole.Other, SelfReportTone = NpcSelfReportTone.Harmful });
            catalog.Event(new NpcEventDefinition("base_robbed", NpcRecordCategory.World, true,
                e => (e.Subject ?? "Someone") + " discovered that their base was robbed.",
                f => (f.ReportSubject ?? "someone") + " found their group's base robbed of " +
                    f.Units + " " + (f.Resource == "food" || f.Resource == "medicine" ?
                        (f.Units == 1 ? "unit of " : "units of ") + f.Resource :
                        f.Units == 1 ? "item" : "items"))
                { ReportActorRole = NpcReportActorRole.None, CanObserve = (a, e) => a == e.Subject });
            catalog.On("supplies_lost", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("base_robbed", NpcObservationPhase.Relationships, OnRelationships);
        }
        static void OnRelationships(NpcObservation observation)
        {
            Actor owner = observation.Owner;
            SignificantEvent source = observation.Source; bool direct = observation.Direct;
            PersonalityState state = owner.Personality; NpcKnowledge knowledge = state.Knowledge;
            NpcKnownPerson other = source.Other == null ? null : knowledge.Person(source.Other.PersonalityIdentity);
            if ((source.Kind == "supplies_lost" || source.Kind == "base_robbed") && source.Subject == owner)
            {
                Guid culprit = other == null ? Guid.Empty : other.Id;
                owner.Personality.Attach(new NpcAttachment { Kind = "place", Person = culprit, Resource = source.Resource, Place = new Location(source.Map, source.Position), Name = source.Map.Name, Weight = 40 });
                NpcAttachment home = owner.Personality.Attachments.Find(a => a.Kind == "place" && a.Person == culprit && a.Resource == source.Resource && a.Place == new Location(source.Map, source.Position));
                home.MissingUnits += Math.Max(1, source.Units); home.CauseId = source.Id;
            }
        }
    }
}
