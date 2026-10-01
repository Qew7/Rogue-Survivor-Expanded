using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class FoodAidModule : INpcContentModule, INpcGoalSource
    {
        public const string AnswerFoodRequestId = "answer_food_request";
        public string Id { get { return "foodaid"; } }
        public void Register(NpcCatalogBuilder catalog)
        {
            catalog.OperatorSource(new NpcOperatorSource("food.deliver", c => (ulong)NpcPlanFact.Delivered, FoodPlanOperators.Delivery,
                c => (ulong)NpcPlanFact.SpareFood, d => d.Resource == null || d.Resource == "food"));
            catalog.GoalSource(this);
            catalog.Value(new NpcValueDefinition("Care", "Provide needed food", NpcGoalValue.Care, m => 10 + m.Compassion + Math.Min(0, m.Trade) + m.Feeling / 4 + m.Attachment / 4 + Math.Max(0, m.Group) / 10, true));
            catalog.Value(new NpcValueDefinition("Reciprocity", "Reduce a personal debt", NpcGoalValue.Reciprocity, m => 25 + m.Compassion + m.Trade + m.Feeling / 2, true)
                { AfterDelivery = c => c.Owner.Personality.Person(c.Target.PersonalityIdentity) != null && c.Owner.Personality.Person(c.Target.PersonalityIdentity).Debt > c.Goal.Generated.Desired });
            var repay = new NpcIntentDefinition("repay_aid", "Repay remembered aid",
            NpcIntentMethod.ShareFood, 25, 20, 2 * WorldTime.TURNS_PER_DAY, WorldTime.TURNS_PER_DAY, 1,
            new NpcIntentWeight(DecisionKind.Compassion, 1), new NpcIntentWeight(DecisionKind.Trade, 1));

            repay.Result = (c, g) => (ulong)(NpcPlanFact.Delivered);
            repay.PauseWhenHungry = true;
            repay.SocialPriority = r => r.Debt / 2;
            repay.Resource = "food";
            repay.DirectAction = NpcFoodActions.Give;
            catalog.Capability(repay);
            var help = new NpcIntentDefinition(AnswerFoodRequestId, "Answer a food request",
            NpcIntentMethod.ShareFood, 25, 20, 60, 60, 1,
            new NpcIntentWeight(DecisionKind.Compassion, 1), new NpcIntentWeight(DecisionKind.Trade, 1));

            help.Result = (c, g) => (ulong)(NpcPlanFact.Delivered);
            help.PauseWhenHungry = true;
            help.Resource = "food";
            help.DirectAction = NpcFoodActions.Give;
            catalog.Capability(help);
            RegisterContent(catalog);
        }
        public void Evaluate(NpcGoalContext context, NpcGoalOffers offers)
        {
            Actor owner = context.Owner; NpcKnownPerson self = context.Self;
            IList<NpcKnownPerson> people = context.People; int turn = context.Turn;
            foreach (NpcKnownPerson person in people)
            {
                if (person.Id == self.Id || person.Dead) continue;
                RelationshipRecord opinion = owner.Personality.Person(person.Id);
                if (!person.Hostile && (turn - person.FoodNeedTurn <= 60 || context.Pending(NpcGoalValue.Care, person.Id)))
                    offers.Add(person, NpcGoalValue.Care, context.Catalog.Capability(AnswerFoodRequestId), 100 - person.FoodNeed, 100,
                        turn - person.FoodNeedTurn <= 60 ? person.FoodNeed : 0, person.FoodConfidence, person.NeedCause, person.NeedStory);
                if (!person.Hostile && opinion != null && (turn >= person.ReciprocityTurn || context.Pending(NpcGoalValue.Reciprocity, person.Id)) &&
                    (opinion.Debt > 0 || context.Pending(NpcGoalValue.Reciprocity, person.Id)))
                {
                    NpcIntent existing = owner.Personality.IntentList.Find(i => !i.Finished && i.Generated != null &&
                        i.Generated.Value == NpcGoalValue.Reciprocity && i.Generated.SubjectId == person.Id);
                    int desired = existing == null ? Math.Max(0, opinion.Debt - 10) : existing.Generated.Desired;
                    offers.Add(person, NpcGoalValue.Reciprocity, context.Catalog.Capability("repay_aid"), opinion.Debt, desired,
                        Math.Min(100, Math.Max(0, opinion.Debt - desired) * 10), 100, person.SocialCause);
                }
            }
        }
        void RegisterContent(NpcCatalogBuilder catalog)
        {
            RegisterEvents(catalog);
            catalog.Operator(new NpcOperatorDefinition("food.give", NpcPlanAction.GiveFood, c => NpcFoodActions.Give(new NpcActionContext(c))));
            catalog.Memory(new MemoryDefinition("refused_aid", "A request for aid was declined", 2, 6,
                new[] { new MemoryTrigger("request_refused", (a, e) => a == e.Other) },
                new MemoryOutcome(null, "mistrustful", null), new MemoryOutcome(null, null, Skills.IDs.CHARISMATIC))
                .Relate(MemoryRelationRole.Subject, -10), false);
        }
        void RegisterEvents(NpcCatalogBuilder catalog)
        {
            catalog.Event(new NpcEventDefinition("helped", NpcRecordCategory.Help, true, e => (e.Other ?? "someone") + " helped " + (e.Subject ?? "Someone") + ".", null));
            catalog.Event(new NpcEventDefinition("shared_food", NpcRecordCategory.Help, false, e => (e.Subject ?? "Someone") + " shared food with " + (e.Other ?? "someone") + ".", null) { StoryStage = (g, s, e) => NpcEpisodeProgress.FoodDelivery(g, s, e) });
            catalog.Event(new NpcEventDefinition("request_refused", NpcRecordCategory.Help, false, e => (e.Subject ?? "Someone") + " declined " + (e.Other ?? "someone") + "'s request.", null));
            catalog.Event(new NpcEventDefinition("aid_acknowledged", NpcRecordCategory.Help, false, e => (e.Subject ?? "Someone") + " acknowledged aid from " + (e.Other ?? "someone") + ".", null));
            catalog.On("helped", NpcObservationPhase.Knowledge, OnKnowledge);
            catalog.On("shared_food", NpcObservationPhase.Knowledge, OnKnowledge);
            catalog.On("helped", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("shared_food", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("helped", NpcObservationPhase.Goals, OnGoals);
            catalog.On("request_refused", NpcObservationPhase.Goals, OnGoals);
            catalog.On("requested_food", NpcObservationPhase.Replies, OnReplies);
        }
        static void OnKnowledge(NpcObservation observation)
        {
            Actor owner = observation.Owner;
            SignificantEvent source = observation.Source;
            bool seesSubject = observation.SeesSubject, seesOther = observation.SeesOther;
            NpcKnowledge knowledge = owner.Personality.Knowledge;
            NpcKnownPerson other = !seesOther ? null : knowledge.Person(source.Other.PersonalityIdentity);
            if (source.Kind == "shared_food" && seesSubject && seesOther && source.Subject != owner && source.StoryId != null)
                foreach (NpcIntent goal in owner.Personality.Intents)
                    if (!goal.Finished && goal.TargetId == source.Other.PersonalityIdentity && goal.StoryId == source.StoryId)
                    {
                        if (goal.DefinitionId == AnswerFoodRequestId)
                            NpcIntentSystem.Finish(observation.Game.NpcContent, owner, goal,
                                NpcIntentStatus.Completed, "observed that the recipient received food");
                        else if (goal.DefinitionId == GroupsModule.GatherSuppliesId)
                        {
                            goal.Progress = 2; goal.NextAttempt = source.Turn;
                            if (goal.Plan != null) { goal.Plan.LastEventId = source.Id; goal.Plan.Invalidate(); goal.Plan.NextPlanningTurn = source.Turn; }
                        }
                    }
            if (source.Kind == "shared_food" && other != null)
            { other.FoodNeed = 0; other.FoodNeedTurn = source.Turn; knowledge.Revision++; }
            if (source.Kind == "helped" && source.Subject == owner && other != null)
            { other.SocialCause = source.Id; other.ReciprocityTurn = source.StoryId == null ? source.Turn : source.Turn + WorldTime.TURNS_PER_DAY; }
        }
        static void OnRelationships(NpcObservation observation)
        {
            Actor owner = observation.Owner;
            bool seesOther = observation.SeesOther;
            SignificantEvent source = observation.Source;
            NpcKnowledge knowledge = owner.Personality.Knowledge;
            NpcKnownPerson subject = source.Subject == null ? null : knowledge.Person(source.Subject.PersonalityIdentity);
            NpcKnownPerson other = source.Other == null ? null : knowledge.Person(source.Other.PersonalityIdentity);
            if (seesOther && source.Kind == "helped" && source.Subject == owner && source.Other != null)
                owner.Personality.Opinion(source.Other.PersonalityIdentity, source.Other.UnmodifiedName).AdjustSocial(trust: 5, attachment: 4, debt: 10);
            if (seesOther && source.Kind == "shared_food" && source.Subject == owner && source.Other != null)
                owner.Personality.Opinion(source.Other.PersonalityIdentity, source.Other.UnmodifiedName).AdjustSocial(attachment: 2, debt: -10);
            if (subject != null && source.Kind == "helped" && source.Subject == owner && other != null)
                owner.Personality.Attach(new NpcAttachment { Kind = "person", Person = other.Id, Name = other.Name, Weight = 20, CauseId = source.Id });
        }
        static void OnGoals(NpcObservation observation)
        {
            RogueGame game = observation.Game; Actor owner = observation.Owner;
            bool seesOther = observation.SeesOther;
            SignificantEvent source = observation.Source;
            PersonalityState state = owner.Personality;
            if (seesOther && source.Kind == "helped" && source.Subject == owner && source.Other != null)
            {
                foreach (NpcIntent intent in state.Intents)
                    if (!intent.Finished && (intent.DefinitionId == NutritionModule.RequestFoodId ||
                        intent.DefinitionId == NutritionModule.ObtainFoodId) &&
                        (NpcFoodSupply.HasFood(game, owner) || !game.Rules.IsActorHungry(owner)))
                        NpcIntentSystem.Finish(game.NpcContent, owner, intent,
                            NpcIntentStatus.Completed, "received needed supplies");
                if (state.Reactions.Count < 4)
                {
                    string text = PersonalitySystem.Bias(owner, DecisionKind.Compassion) < 0 ? "About time." :
                        PersonalitySystem.Bias(owner, DecisionKind.Group) < 0 ? "Thanks." :
                        "Thank you, " + source.Other.UnmodifiedName + ". I needed that.";
                    state.Reactions.Add(new NpcReaction(source.Other, text, source.Id, source.Turn, storyId: source.StoryId));
                }
            }
            if (source.Kind == "request_refused" && source.Other == owner && source.Subject != null)
                foreach (NpcIntent intent in state.Intents)
                    if (!intent.Finished && intent.DefinitionId == NutritionModule.RequestFoodId &&
                        intent.TargetId == source.Subject.PersonalityIdentity && intent.StoryId == source.StoryId)
                    {
                        if (intent.Plan == null) NpcIntentSystem.Finish(game.NpcContent, owner, intent,
                            NpcIntentStatus.Failed, "request was declined");
                        else { intent.Plan.Invalidate(); intent.Plan.NextPlanningTurn = source.Turn; intent.NextAttempt = source.Turn + 1; intent.Status = NpcIntentStatus.Active; }
                    }
        }
        static void OnReplies(NpcObservation observation)
        {
            RogueGame game = observation.Game; Actor owner = observation.Owner;
            SignificantEvent source = observation.Source;
            PersonalityState state = owner.Personality;
            if (source.Kind == "requested_food" && source.Other == owner && source.Subject != null &&
                !game.Rules.AreEnemies(owner, source.Subject))
            {
                bool answering = state.Reactions.Exists(r => r.CauseId == source.Id && r.TargetId == source.Subject.PersonalityIdentity && r.Kind == "food_promised");
                foreach (NpcIntent intent in state.Intents)
                    if (!intent.Finished && intent.DefinitionId == AnswerFoodRequestId && intent.TargetId == source.Subject.PersonalityIdentity) answering = true;
                if (!answering && state.Reactions.Count < 4)
                    state.Reactions.Add(new NpcReaction(source.Subject, "I'm keeping my supplies.", source.Id,
                        source.Turn, "request_refused", source.StoryId));
            }
        }
    }
}
