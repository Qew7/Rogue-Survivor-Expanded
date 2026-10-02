using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Data
{
    sealed partial class ResidentRecords
    {
        public void PlanChanged(Actor actor, NpcIntent intent)
        {
            ResidentRecord record = Register(actor); if (record == null) return;
            var methods = new System.Collections.Generic.List<string>();
            foreach (NpcPlanStep step in intent.Plan.Steps) methods.Add(PlanActionText(step));
            int turn = actor.Location.Map.LocalTime.TurnCounter;
            record.Add("plan:" + intent.Sequence + ":" + record.Entries.Count, turn,
                "To " + GoalText(intent, NpcContentCatalog.Default).ToLowerInvariant() + ", " + actor.UnmodifiedName +
                " plans to " + System.String.Join(", then ", methods.ToArray()) + ".",
                new ObservedEvent("goal_plan", turn, actor.UnmodifiedName, intent.TargetName, true,
                    causeId: intent.CauseId, storyId: intent.StoryId));
        }
        public void IntentChanged(Actor actor, NpcIntent intent, string state, string reason, NpcContentCatalog catalog)
        {
            ResidentRecord record = Register(actor); if (record == null) return;
            int turn = state == "started" ? intent.StartedTurn : intent.FinishedTurn;
            string targetName = intent.Generated != null && intent.Generated.SubjectId == actor.PersonalityIdentity ? actor.UnmodifiedName : intent.TargetName;
            ObservedEvent entry = new ObservedEvent("goal_" + state, turn, actor.UnmodifiedName, targetName,
                true, false, actor.PersonalityIdentity, intent.Generated == null ? intent.TargetId : intent.Generated.SubjectId, causeId: intent.CauseId, storyId: intent.StoryId);
            string goal = GoalText(intent, catalog);
            string text;
            if (state == "started")
            {
                text = actor.UnmodifiedName + " decided to " + goal.ToLowerInvariant();
                if (targetName != actor.UnmodifiedName && !System.String.IsNullOrEmpty(targetName)) text += " for " + targetName;
                if (intent.Generated != null)
                {
                    string context = GoalContext(intent.Generated.DefinitionId ?? intent.Generated.Value.ToString(),
                        intent.Generated.Deficit, intent.Generated.Importance, targetName);
                    if (context != null) text += ". " + context;
                }
                string trait = TraitReason(reason);
                if (trait != null) text += ". " + trait;
                text += ".";
            }
            else
            {
                string outcome = state == "completed" ? "achieved" : state == "abandoned" ? "gave up" : "could not achieve";
                text = actor.UnmodifiedName + " " + outcome + " the goal to " + goal.ToLowerInvariant();
                if (!System.String.IsNullOrEmpty(reason)) text += ": " + ReadableReason(reason);
                text += ".";
            }
            record.Add("goal:" + intent.Sequence + ":" + state, turn, text, entry,
                supportingCauses: intent.Generated == null ? null : intent.Generated.Causes);
        }

        static string GoalText(NpcIntent intent, NpcContentCatalog catalog)
        {
            NpcIntentDefinition definition = catalog.Capability(intent.DefinitionId);
            return intent.Generated != null ? intent.Generated.Description : definition == null ? intent.DefinitionId : definition.Name;
        }

        static string TraitReason(string reason)
        {
            const string marker = "because trait ";
            int start = reason == null ? -1 : reason.IndexOf(marker, System.StringComparison.Ordinal);
            if (start < 0) return null;
            string detail = reason.Substring(start + marker.Length);
            const string raised = " raised this goal's importance by ";
            int split = detail.IndexOf(raised, System.StringComparison.Ordinal);
            return split < 0 ? null : "The " + detail.Substring(0, split) + " trait made this goal more compelling";
        }

        internal static string GoalContext(string value, int deficit, int importance, string target)
        {
            if (deficit < 60) return null;
            string need;
            switch (value)
            {
                case "Nutrition": need = "Food was running low"; break;
                case "FoodReserve": need = "Their food stores were running low"; break;
                case "Care": need = "They believed " + (System.String.IsNullOrEmpty(target) ? "someone" : target) + " needed food"; break;
                case "MedicalCare": need = "They believed " + (System.String.IsNullOrEmpty(target) ? "someone" : target) + " needed treatment"; break;
                case "Recovery": need = "Their injuries needed attention"; break;
                case "Safety": need = "They believed danger was close"; break;
                default: return null;
            }
            return need + (importance >= 120 ? ", so this felt urgent" : "");
        }

        static string ReadableReason(string reason)
        {
            if (reason == "observed that the desired state was satisfied") return "the result was already in place";
            if (reason == "motivation changed") return "their priorities changed";
            if (reason == "deadline expired") return "time ran out";
            return reason.TrimEnd('.');
        }

        static string PlanActionText(NpcPlanStep step)
        {
            if (!System.String.IsNullOrEmpty(step.OperatorId) &&
                step.OperatorId.IndexOf('.') < 0 && step.OperatorId != "travel" && step.OperatorId != "retreat" &&
                step.OperatorId != step.Action.ToString())
                return Humanize(step.OperatorId);
            switch (step.Action)
            {
                case NpcPlanAction.Travel: return "travel to the destination";
                case NpcPlanAction.EnterShelter: return "enter the shelter";
                case NpcPlanAction.PickupFood: return "collect food";
                case NpcPlanAction.AskFood: return "ask for food";
                case NpcPlanAction.GiveFood: return "give food";
                case NpcPlanAction.ReportDelivery: return "report the delivery";
                case NpcPlanAction.AskLocation: return "ask for directions";
                case NpcPlanAction.Reunite: return "reunite with someone";
                case NpcPlanAction.Warn: return "warn someone";
                case NpcPlanAction.Retreat: return "retreat";
                case NpcPlanAction.ConfirmSafety: return "check that someone is safe";
                case NpcPlanAction.LeaveGroup: return "leave the group";
                case NpcPlanAction.BarterFood: return "trade for food";
                case NpcPlanAction.PickupMedicine: return "collect medicine";
                case NpcPlanAction.UseMedicine: return "use medicine";
                case NpcPlanAction.AskMedicine: return "ask for medicine";
                case NpcPlanAction.GiveMedicine: return "give medicine";
                case NpcPlanAction.TreatPerson: return "treat someone";
                case NpcPlanAction.BarterMedicine: return "trade for medicine";
                case NpcPlanAction.PickupValuedItem: return "retrieve a valued item";
                case NpcPlanAction.DemandRestitution: return "demand restitution";
                default: return Humanize(step.OperatorId ?? step.Action.ToString());
            }
        }

        static string Humanize(string name)
        { return (name ?? "act").Replace('_', ' ').Replace('-', ' ').ToLowerInvariant(); }
    }
}
