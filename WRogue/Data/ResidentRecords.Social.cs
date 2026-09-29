namespace djack.RogueSurvivor.Data
{
    sealed partial class ResidentRecords
    {
        static string SocialText(ObservedEvent e)
        {
            string a = e.Subject ?? "Someone", b = e.Other ?? "someone";
            switch (e.Kind)
            {
                case "resource_contested": return a + " asked " + b + " to yield disputed supplies.";
                case "resource_yielded": return a + " yielded disputed supplies to " + b + ".";
                case "resource_refused": return a + " refused to yield supplies to " + b + ".";
                case "contested_taken": return a + " took supplies despite " + b + "'s refusal.";
                case "food_promised": return a + " promised to bring food to " + b + " within 180 turns.";
                case "medicine_promised": return a + " promised to bring medicine to " + b + " within 180 turns.";
                case "promise_kept": return a + " fulfilled their promise to " + b + ".";
                case "promise_broken": return b + " concluded that " + a + "'s promise was overdue.";
                case "boundary_accepted": return a + " accepted " + b + "'s boundary.";
                case "boundary_defied": return a + " rejected " + b + "'s boundary.";
                case "requested_medicine": return a + " asked " + b + " for medicine.";
                case "medicine_offered": return a + " offered medicine in exchange for supplies to " + b + ".";
                case "bartered_medicine": return a + " obtained medicine by trading with " + b + ".";
                case "shared_medicine": return a + " gave medicine to " + b + ".";
                case "treated_person": return a + " treated " + b + "'s wounds.";
                case "restitution_given": return a + " replaced supplies lost by " + b + ".";
                case "restitution_requested": return a + " asked " + b + " to compensate lost supplies.";
                case "restitution_refused": return a + " refused " + b + "'s demand for compensation.";
                case "promise_released": return a + " explicitly released " + b + " from a promise.";
                case "valued_item_acquired": return a + " acquired a personally valued kind of item.";
                case "home_reached": return a + " returned to their threatened home.";
                default: return null;
            }
        }
    }
}
