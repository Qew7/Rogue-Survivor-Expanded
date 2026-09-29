using djack.RogueSurvivor.Data;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcContentActions
    {
        public static void FoodUnavailable(NpcExecutionContext c)
        { if (NpcKnowledgeSystem.Visible(c.Game, c.Owner, c.Step.Place) && NpcPlanExecution.FoodOnGround(c.Game, c.Owner, c.Step.Place) == null) Empty(c, "food"); }
        public static void MedicineUnavailable(NpcExecutionContext c)
        { if (NpcKnowledgeSystem.Visible(c.Game, c.Owner, c.Step.Place) && NpcPlanExecution.Medicine(c.Game, c.Owner, c.Step.Place) == null) Empty(c, "medicine"); }
        static void Empty(NpcExecutionContext c, string kind)
        { c.Owner.Personality.Knowledge.RememberPlace(new NpcKnownPlace(c.Step.Place, kind, c.Owner.Location.Map.LocalTime.TurnCounter)); }
    }
}
