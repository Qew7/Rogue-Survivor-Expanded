using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class NutritionModule : INpcContentModule, INpcGoalSource
    {
        public const string ObtainFoodId = "obtain_food";
        public const string RequestFoodId = "request_food";
        public string Id { get { return "nutrition"; } }
        public void Register(NpcCatalogBuilder catalog)
        {
            catalog.OperatorSource(new NpcOperatorSource("food.acquire", c => (ulong)(NpcPlanFact.Food | NpcPlanFact.SpareFood), FoodPlanOperators.Acquisition));
            catalog.Event(new NpcEventDefinition("food_cache", describeReport: f => "there was food"));
            catalog.OnReport("food_cache", c => c.RememberPlace("food"));
            catalog.Resource(new NpcResourceDefinition("food", (g, a) => g.Rules.IsActorHungry(a)));
            catalog.GoalSource(this);
            catalog.Value(new NpcValueDefinition("Nutrition", "Have usable food", NpcGoalValue.Nutrition, m => 70, true, RequestFoodId)
                { EquivalentCapabilities = new[] { RequestFoodId, ObtainFoodId } });
            var obtain = new NpcIntentDefinition(ObtainFoodId, "Obtain usable food",
            NpcIntentMethod.ObtainFood, 35, 20, 180, 180, 0, new NpcIntentWeight(DecisionKind.Explore, 1),
            new NpcIntentWeight(DecisionKind.Supplies, 1), new NpcIntentWeight(DecisionKind.Group, -1, 2));

            obtain.Result = (c, g) => (ulong)(NpcPlanFact.Food);
            obtain.Assess = (g, a, i) => !g.Rules.IsActorHungry(a) || NpcFoodSupply.HasFood(g, a) ? new NpcIntentOutcome(NpcIntentStatus.Completed, "observed that usable food is available") : null;
            obtain.Resource = "food";
            catalog.Capability(obtain);
            var request = new NpcIntentDefinition(RequestFoodId, "Ask for food",
            NpcIntentMethod.RequestFood, 35, 20, 60, 180, 1,
            new NpcIntentWeight(DecisionKind.Group, 1), new NpcIntentWeight(DecisionKind.Trade, 1, 2));

            request.Result = (c, g) => (ulong)(NpcPlanFact.Food);
            request.CompleteEpisodeOnSuccess = true;
            request.WaitingAfterAnnouncement = true;
            request.Assess = (g, a, i) => !g.Rules.IsActorHungry(a) || NpcFoodSupply.HasFood(g, a) ? new NpcIntentOutcome(NpcIntentStatus.Completed, "food need was satisfied") : null;
            request.Resource = "food";
            request.DirectAction = NpcFoodActions.Request;
            catalog.Capability(request);
            RegisterContent(catalog);
        }
        public void Evaluate(NpcGoalContext context, NpcGoalOffers offers)
        {
            Actor owner = context.Owner; NpcKnownPerson self = context.Self;
            IList<NpcKnownPerson> people = context.People; int turn = context.Turn;
            if (context.Hungry || context.Pending(NpcGoalValue.Nutrition, self.Id))
            {
                NpcKnownPerson listener = null; int best = Int32.MinValue;
                foreach (NpcKnownPerson person in people)
                {
                    if (person.Id == self.Id || person.Hostile || person.Dead || person.SeenTurn != turn || person.Place.Map != owner.Location.Map) continue;
                    int preference = NpcValues.Importance(context.Catalog, owner, NpcGoalValue.Belonging, person.Id) -
                        NpcGoalContext.Distance(owner.Location, person.Place);
                    if (owner.Leader != null && owner.Leader.PersonalityIdentity == person.Id) preference += 10;
                    if (preference > best) { best = preference; listener = person; }
                }
                bool social = listener != null && 35 + PersonalitySystem.Bias(owner, DecisionKind.Group) + PersonalitySystem.Bias(owner, DecisionKind.Trade) / 2 >= 20;
                int available = context.HasFood || !context.Hungry ? 100 : 0;
                offers.Add(social ? listener : self, NpcGoalValue.Nutrition,
                    context.Catalog.Capability(social ? RequestFoodId : ObtainFoodId),
                    available, 100, 100 - available, 100, self: true);
            }
        }
        void RegisterContent(NpcCatalogBuilder catalog)
        {
            catalog.Perception(NpcPerceptionKind.Items, PerceiveItems);
            RegisterEvents(catalog);
            catalog.PlanSeed(FoodPlanOperators.Seed);
            catalog.Operator(new NpcOperatorDefinition("food.take", NpcPlanAction.PickupFood, c => NpcFoodActions.Take(new NpcActionContext(c)), "food", NpcContentActions.FoodUnavailable));
            catalog.Operator(new NpcOperatorDefinition("food.ask", NpcPlanAction.AskFood, c => NpcFoodActions.Request(new NpcActionContext(c))));
            catalog.Operator(new NpcOperatorDefinition("food.trade", NpcPlanAction.BarterFood, c => new ActionNpcBarter(c.Owner, c.Game, c.Goal, c.Step, c.Target)));
            catalog.Memory(new MemoryDefinition("traded_for_food", "Exchanged supplies for food", 2, 5,
                new[] { new MemoryTrigger("bartered_food", (a, e) => a == e.Subject) },
                new MemoryOutcome(null, null, Skills.IDs.CHARISMATIC)).Relate(MemoryRelationRole.Other, 2), false);
        }
        void RegisterEvents(NpcCatalogBuilder catalog)
        {
            catalog.OnReport("requested_food", HearNeed);
            catalog.Event(new NpcEventDefinition("requested_food", NpcRecordCategory.Help, true, e => (e.Subject ?? "Someone") + " asked " + (e.Other ?? "someone") + " for food.", f => f.SubjectName + " asked for food") { StoryStage = (g, s, e) => "contacted", AudibleReport = true, PlayerReply = new NpcPlayerReply("food", "food_promised", "request_refused", "Yes, I'll bring you food.", "No, I can't help with food.") });
            catalog.Event(new NpcEventDefinition("food_offered", NpcRecordCategory.Help, true, e => (e.Subject ?? "Someone") + " offered to exchange food with " + (e.Other ?? "someone") + ".", null));
            catalog.Event(new NpcEventDefinition("bartered_food", NpcRecordCategory.Help, false, e => (e.Subject ?? "Someone") + " obtained food by exchanging supplies with " + (e.Other ?? "someone") + ".", null));
            catalog.Event(new NpcEventDefinition("supplies_acquired", NpcRecordCategory.None, false, e => (e.Subject ?? "Someone") + " acquired the supplies they were seeking.", null) { StoryStage = (g, s, e) => "acquired" });
            catalog.On("food_offered", NpcObservationPhase.Knowledge, OnKnowledge);
            catalog.On("requested_food", NpcObservationPhase.Knowledge, OnKnowledge);
            catalog.On("requested_food", NpcObservationPhase.Goals, OnGoals);
        }
        static void OnKnowledge(NpcObservation observation)
        {
            Actor owner = observation.Owner;
            SignificantEvent source = observation.Source; bool direct = observation.Direct;
            bool seesSubject = observation.SeesSubject, seesOther = observation.SeesOther;
            PersonalityState state = owner.Personality; NpcKnowledge knowledge = state.Knowledge;
            NpcKnownPerson subject = !seesSubject ? null : knowledge.Person(source.Subject.PersonalityIdentity);
            int confidence = direct ? 100 : 90;
            if (source.Kind == "food_offered" && source.Other == owner)
                foreach (NpcIntent goal in owner.Personality.Intents)
                    if (!goal.Finished && goal.DefinitionId == RequestFoodId && goal.StoryId == source.StoryId && goal.Plan != null)
                    { goal.Plan.Desired = (ulong)NpcPlanFact.Food; goal.Plan.Invalidate(); goal.Plan.NextPlanningTurn = source.Turn; }
            if (source.Kind == "requested_food" && subject != null)
            {
                subject.FoodNeed = 100; subject.FoodNeedTurn = source.Turn; subject.FoodConfidence = confidence;
                subject.NeedCause = source.Id; subject.NeedStory = source.StoryId; knowledge.Revision++;
            }
        }
        static void OnGoals(NpcObservation observation)
        {
            RogueGame game = observation.Game; Actor owner = observation.Owner;
            SignificantEvent source = observation.Source; bool direct = observation.Direct;
            PersonalityState state = owner.Personality; NpcKnowledge knowledge = state.Knowledge;
            if (NpcPlanExecution.OfferTrade(game, owner, source)) { observation.SkipGoals = true; observation.SkipReplies = true; }
        }
    }
}
