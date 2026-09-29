using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class GroupsModule : INpcContentModule
    {
        public string Id { get { return "groups"; } }
        public void Register(NpcCatalogBuilder catalog)
        {
            catalog.Clock(NpcClockPhase.MapTurn, c => {
                Actor actor = c.Owner; Map map = actor.Location.Map;
                if (actor.SocialGroup != null && actor.SocialGroup.LeaderId == actor.PersonalityIdentity && actor.SocialGroup.Plan != null &&
                    !actor.SocialGroup.Plan.Finished && map.LocalTime.TurnCounter >= actor.SocialGroup.Plan.Deadline)
                { actor.SocialGroup.Plan.Stage = "failed"; actor.SocialGroup.Plan.Destination = default(Location); }
            });
            var gather = new NpcIntentDefinition("gather_group_supplies", "Gather supplies for a companion",
            NpcIntentMethod.GatherFood, 35, 30, WorldTime.TURNS_PER_DAY, 180, 1, new NpcIntentWeight(DecisionKind.Compassion, 1), new NpcIntentWeight(DecisionKind.Supplies, 1), new NpcIntentWeight(DecisionKind.Explore, 1, 2));
            gather.ReportAfterDelivery = true;
            gather.BuildPlan = d => { FoodPlanOperators.Build(d, false, true, true); };
            gather.Result = (c, g) => (ulong)(NpcPlanFact.Delivered | NpcPlanFact.Reported);
            catalog.Capability(gather);
            var coordinate = new NpcIntentDefinition("coordinate_group_supplies", "Coordinate supplies for the group",
            NpcIntentMethod.Coordinate, 20, 25, WorldTime.TURNS_PER_DAY, 180, 0, new NpcIntentWeight(DecisionKind.Group, 1), new NpcIntentWeight(DecisionKind.Compassion, 1, 2));
            coordinate.Selectable = false;
            coordinate.Result = (c, g) => (ulong)(NpcPlanFact.None);
            catalog.Capability(coordinate);
            var shelter = new NpcIntentDefinition("seek_group_shelter", "Reach the group's known shelter",
            NpcIntentMethod.ReachShelter, 25, 35, 180, 180, 0, new NpcIntentWeight(DecisionKind.Group, 1), new NpcIntentWeight(DecisionKind.Courage, -1, 2));
            shelter.BuildPlan = d => { SafetyPlanOperators.Shelter(d); };
            shelter.Result = (c, g) => (ulong)(NpcPlanFact.Sheltered);
            shelter.TravelArrived = a => a.Location.Map.GetTileAt(a.Location.Position).IsInside;
            shelter.TravelDestination = SafetyPlanOperators.ShelterDestination;
            catalog.Capability(shelter);
            RegisterContent(catalog);
        }
        void RegisterContent(NpcCatalogBuilder catalog)
        {
            catalog.Perception(NpcPerceptionKind.Surroundings, PerceiveSurroundings);
            RegisterEvents(catalog);
            catalog.Operator(new NpcOperatorDefinition("group.report_delivery", NpcPlanAction.ReportDelivery, c => c.PlanAction()));
            catalog.Memory(new MemoryDefinition("completed_group_delivery", "Completed a group supply mission", 2, 5,
                new[] { new MemoryTrigger("supplies_delivered", (a, e) => a == e.Subject) },
                new MemoryOutcome(null, "selfless", null), new MemoryOutcome(null, null, Skills.IDs.LEADERSHIP))
                .Relate(MemoryRelationRole.Other, 5, MemoryRelationRole.Other), false);
            catalog.Memory(new MemoryDefinition("reached_group_shelter", "Reached the group's shelter", 2, 5,
                new[] { new MemoryTrigger("shelter_reached", (a, e) => a == e.Subject) },
                new MemoryOutcome(null, "protector", null), new MemoryOutcome(null, null, Skills.IDs.STRONG_PSYCHE))
                .Relate(MemoryRelationRole.None, 0, MemoryRelationRole.Subject), false);
            catalog.Memory(new MemoryDefinition("witnessed_group_succession", "Witnessed a new group leader", 2, 5,
                new[] { new MemoryTrigger("group_succession", (a, e) => a.SocialGroup != null && e.Subject != null && a.SocialGroup == e.Subject.SocialGroup) },
                new MemoryOutcome(null, "protector", null), new MemoryOutcome(null, null, Skills.IDs.LEADERSHIP))
                .Relate(MemoryRelationRole.Subject, 0, MemoryRelationRole.Subject), false);
        }
        void RegisterEvents(NpcCatalogBuilder catalog)
        {
            catalog.Event(new NpcEventDefinition("supplies_requested", NpcRecordCategory.None, false, e => (e.Subject ?? "Someone") + " asked " + (e.Other ?? "someone") + " to gather supplies for the group.", null) { StoryStage = (g, s, e) => "contacted" });
            catalog.Event(new NpcEventDefinition("supplies_delivered", NpcRecordCategory.None, false, e => (e.Subject ?? "Someone") + " reported the completed delivery to " + (e.Other ?? "someone") + ".", null) { StoryStage = (g, s, e) => "completed" });
            catalog.Event(new NpcEventDefinition("task_declined", NpcRecordCategory.None, false, e => (e.Subject ?? "Someone") + " declined " + (e.Other ?? "someone") + "'s task.", null) { StoryStage = (g, s, e) => "failed" });
            catalog.Event(new NpcEventDefinition("shelter_suggested", NpcRecordCategory.None, false, e => (e.Subject ?? "Someone") + " proposed moving the group to known shelter.", null) { StoryStage = (g, s, e) => "contacted" });
            catalog.Event(new NpcEventDefinition("shelter_declined", NpcRecordCategory.None, false, e => (e.Subject ?? "Someone") + " declined " + (e.Other ?? "someone") + "'s shelter proposal.", null));
            catalog.Event(new NpcEventDefinition("shelter_reached", NpcRecordCategory.None, false, e => (e.Subject ?? "Someone") + " reached the agreed shelter.", null) { StoryStage = (g, s, e) => s.Roles.TrueForAll(r => r.Status == NpcIntentStatus.Completed) ? "completed" : null });
            catalog.Event(new NpcEventDefinition("group_succession", NpcRecordCategory.None, false, e => (e.Subject ?? "Someone") + " succeeded " + (e.Other ?? "someone") + " as group leader.", null));
            catalog.On("group_succession", NpcObservationPhase.Knowledge, OnKnowledge);
            catalog.On("shelter_suggested", NpcObservationPhase.Knowledge, OnKnowledge);
            catalog.On("supplies_delivered", NpcObservationPhase.Knowledge, OnKnowledge);
            catalog.On("supplies_requested", NpcObservationPhase.Knowledge, OnKnowledge);
            catalog.On("task_declined", NpcObservationPhase.Knowledge, OnKnowledge);
        }
        static void OnKnowledge(NpcObservation observation)
        {
            RogueGame game = observation.Game; Actor owner = observation.Owner;
            SignificantEvent source = observation.Source; bool direct = observation.Direct;
            PersonalityState state = owner.Personality; NpcKnowledge knowledge = state.Knowledge;
            if (source.Kind == "group_succession" && source.Subject != null && source.Subject.SocialGroup != null)
            { RelationshipRecord knownGroup = owner.Personality.Group(source.Subject.SocialGroup.Identity);
                if (knownGroup != null) knownGroup.Name = source.Subject.SocialGroup.LeaderName; }
            if (source.Kind == "supplies_requested" && source.Other == owner && source.Task != null)
                NpcStorySystem.AcceptTask(game, owner, source);
            if (source.Kind == "supplies_requested" && source.Task != null && source.Task.BeneficiaryId == owner.PersonalityIdentity)
                foreach (NpcIntent goal in owner.Personality.Intents)
                    if (!goal.Finished && goal.DefinitionId == NpcIntentContent.Request.Id) goal.Deadline = Math.Max(goal.Deadline, source.Task.Deadline);
            if (source.Kind == "supplies_delivered" && source.Other == owner)
                foreach (NpcIntent goal in owner.Personality.Intents)
                    if (!goal.Finished && goal.DefinitionId == NpcIntentContent.Coordinate.Id && goal.StoryId == source.StoryId)
                        NpcIntentSystem.Finish(owner, goal, NpcIntentStatus.Completed, "collector reported successful delivery");
            if (source.Kind == "task_declined" && source.Other == owner)
                foreach (NpcIntent goal in owner.Personality.Intents)
                    if (!goal.Finished && goal.DefinitionId == NpcIntentContent.Coordinate.Id && goal.StoryId == source.StoryId)
                        NpcIntentSystem.Finish(owner, goal, NpcIntentStatus.Failed, "collector declined the task");
            if (source.Kind == "shelter_suggested" && owner.SocialGroup != null && source.Subject != null &&
                owner.SocialGroup == source.Subject.SocialGroup && source.Task != null)
                NpcStorySystem.AcceptShelter(game, owner, source);
        }
    }
}
