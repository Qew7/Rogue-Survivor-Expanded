using System;
using djack.RogueSurvivor.Data;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class InterestsModule : INpcContentModule
    {
        public string Id { get { return "lasting-interests"; } }
        public void Register(NpcCatalogBuilder c)
        {
            c.GoalSource(new NpcInterestSystem());
            c.Value(new NpcValueDefinition("FoodReserve", "Keep a reserve of usable food", null, m => 20 + m.Supplies / 4, true));
            c.Fact("FoodReserve"); for (int i = 0; i <= 6; i++) c.Fact("FoodStock" + i);
            c.Capability(new NpcIntentDefinition("stockpile_food", "Build a food reserve", 180, 180) {
                Resource = "food", ResultFacts = (catalog, g) => catalog.Facts.Mask("FoodReserve") });
            c.PlanSeed(StockpilePlanOperators.Seed);
            c.OperatorSource(new NpcOperatorSource("food.reserve", catalog => catalog.Facts.Mask("FoodReserve"), StockpilePlanOperators.Build));
            c.Interest(new NpcInterestDefinition("food_reserve", context => {
                int supplies = PersonalitySystem.Bias(context.Owner, DecisionKind.Supplies);
                if (supplies >= 15) Remember(context, "food_reserve", context.Self, 3, supplies);
            }, (context, interest, offers) => {
                if (PersonalitySystem.Bias(context.Owner, DecisionKind.Supplies) < 15) return;
                int units = context.FoodUnits;
                offers.Add(context.Self, "FoodReserve", "stockpile_food", units, interest.Need,
                    units < interest.Need ? 100 : 0, 100, interest.CauseId, resource: "food", self: true);
            }));
            c.Value(new NpcValueDefinition("Residence", "Return to my chosen shelter", null, m => 20 + m.Group - PersonalitySystem.Bias(m.Owner, DecisionKind.Explore), true)
                { ArrivalEvent = "home_maintained" });
            c.Event(new NpcEventDefinition("home_maintained", NpcRecordCategory.Life, true, e => (e.Subject ?? "Someone") + " returned to their chosen shelter."));
            c.Interest(new NpcInterestDefinition("residence", ObserveHome, (context, interest, offers) => {
                if (interest.Place.Map == null) return;
                bool home = context.Owner.Location.Map == interest.Place.Map && NpcGoalContext.Distance(context.Owner.Location, interest.Place) <= 1 &&
                    context.Owner.Location.Map.GetTileAt(context.Owner.Location.Position).IsInside;
                offers.Add(context.Self, "Residence", "seek_group_shelter", home ? 1 : 0, 1, home ? 0 : 100, 100,
                    interest.CauseId, objectPlace: interest.Place, self: true);
            }));
            c.Interest(new NpcInterestDefinition("protect_person", ObservePeople, ProtectPerson));
            c.Interest(new NpcInterestDefinition("repair_trust", ObserveTrust, (context, interest, offers) => {
                NpcKnownPerson person = context.FindPerson(p => p.Id == interest.Subject);
                if (person != null && !person.Hostile && person.LossUnits == 0 && interest.Need > 0 && context.Turn - interest.LastEvidenceTurn < 3 * WorldTime.TURNS_PER_DAY)
                    offers.Add(person, "RepairTrust", "apologize", 0, 1, 100, 100, interest.CauseId);
            }));
            c.On("apology_accepted", NpcObservationPhase.Relationships, SetTrustNeed);
            c.On("apology_refused", NpcObservationPhase.Relationships, SetTrustNeed);
        }
        static void Remember(NpcGoalContext c, string id, NpcKnownPerson subject, int need, int weight, long cause = 0)
        { c.Owner.Personality.RememberInterest(new NpcInterest { DefinitionId = id, Subject = subject.Id, Name = subject.Name, Place = subject.Place,
            CreatedTurn = c.Turn, LastEvidenceTurn = c.Turn, ExpiresTurn = c.Turn + 14 * WorldTime.TURNS_PER_DAY, Weight = weight, Need = need, CauseId = cause }); }
        static void ObserveHome(NpcGoalContext c)
        {
            NpcInterest prior = c.Owner.Personality.Interest("residence", c.Self.Id);
            if ((prior != null && prior.ExpiresTurn > c.Turn) || PersonalitySystem.Bias(c.Owner, DecisionKind.Explore) >= 0 ||
                !c.Owner.Location.Map.GetTileAt(c.Owner.Location.Position).IsInside) return;
            Remember(c, "residence", c.Self, 1, 20);
        }
        static void SetTrustNeed(NpcObservation o)
        { if (o.Source.Other == o.Owner && o.Source.Subject != null) {
            NpcInterest interest = o.Owner.Personality.Interest("repair_trust", o.Source.Subject.PersonalityIdentity);
            if (interest != null) interest.Need = o.Source.Kind == "apology_accepted" ? 0 : 1;
        } }
    }
}
