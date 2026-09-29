using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Data
{
    sealed partial class ResidentRecords
    {
        public void PlanChanged(Actor actor, NpcIntent intent)
        {
            ResidentRecord record = Register(actor); if (record == null) return;
            var methods = new System.Collections.Generic.List<string>();
            foreach (NpcPlanStep step in intent.Plan.Steps) methods.Add(step.Action.ToString());
            int turn = actor.Location.Map.LocalTime.TurnCounter;
            record.Add("plan:" + intent.Sequence + ":" + record.Entries.Count, turn,
                "Plan: " + System.String.Join(" → ", methods.ToArray()) + ". [story " + intent.StoryId + "]",
                new ObservedEvent("goal_plan", turn, actor.UnmodifiedName, intent.TargetName, true,
                    causeId: intent.CauseId, storyId: intent.StoryId));
        }
        public void IntentChanged(Actor actor, NpcIntent intent, string state, string reason)
        {
            ResidentRecord record = Register(actor); if (record == null) return;
            int turn = state == "started" ? intent.StartedTurn : intent.FinishedTurn;
            string targetName = intent.Generated != null && intent.Generated.SubjectId == actor.PersonalityIdentity ? actor.UnmodifiedName : intent.TargetName;
            ObservedEvent entry = new ObservedEvent("goal_" + state, turn, actor.UnmodifiedName, targetName,
                true, false, actor.PersonalityIdentity, intent.Generated == null ? intent.TargetId : intent.Generated.SubjectId, causeId: intent.CauseId, storyId: intent.StoryId);
            NpcIntentDefinition definition = NpcIntentContent.Find(intent.DefinitionId);
            record.Add("goal:" + intent.Sequence + ":" + state, turn,
                "Intent " + state + ": " + (intent.Generated != null ? intent.Generated.Description : definition == null ? intent.DefinitionId : definition.Name) + "; target " + targetName +
                "; " + reason + ". [story " + intent.StoryId + "]", entry, supportingCauses: intent.Generated == null ? null : intent.Generated.Causes);
        }
    }
}
