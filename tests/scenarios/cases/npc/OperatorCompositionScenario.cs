using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;
sealed class ReadyForTravelContent : INpcContentModule, INpcGoalSource
{
    public string Id { get { return "scenario.ready-for-travel"; } }
    public void Register(NpcCatalogBuilder c)
    {
        c.Value(new NpcValueDefinition("ReadyForTravel", "Become healthy and provisioned", null, m => 200));
        c.Capability(new NpcIntentDefinition("ready_for_travel", "Become healthy and provisioned", 180) {
            ResultFacts = (catalog, g) => (ulong)(NpcPlanFact.Food | NpcPlanFact.Healthy) });
        c.GoalSource(this);
    }
    public void Evaluate(NpcGoalContext c, NpcGoalOffers offers)
    {
        bool ready = c.Owner.HitPoints >= c.MaxHP && c.HasFood;
        offers.Add(c.Self, "ReadyForTravel", "ready_for_travel", ready ? 1 : 0, 1, ready ? 0 : 100, 100, self: true);
    }
}
static class OperatorCompositionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/operator-composition", () => TownScenarioFactory.Arena(4801,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 6, 2);
            world.Game.NpcContent = PersonalityContent.Create(new ReadyForTravelContent()).Content;
            Actor owner = NpcIntentSupport.Actor(world, "provisioner", 1, 1);
            owner.FoodPoints = Session.Get.GamePreset.HungerPoints - 1; owner.HitPoints = 1;
            for (int i = 0; i < 3; i++) owner.Inventory.AddAll(new ItemMedicine(world.Game.GameItems.MEDIKIT));
            world.Map.DropItemAt(new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 2 }, new Point(2, 1));
            for (int turn = 0; turn < 12; turn++)
            {
                world.Map.LocalTime.TurnCounter = turn; NpcIntentSupport.Turn(world, owner);
                NpcIntent goal = NpcIntentSupport.Intent(owner, "ready_for_travel");
                if (goal != null && goal.Finished) break;
            }
            NpcIntent complete = NpcIntentSupport.Intent(owner, "ready_for_travel");
            Check.Equal(true, complete != null && complete.Status == NpcIntentStatus.Completed,
                "a new capability reaches food and health through registered operators without a plan builder");
            Check.Equal(true, NpcIntentSupport.FoodUnits(owner) > 0 && owner.HitPoints >= world.Game.Rules.ActorMaxHPs(owner),
                "both effects are confirmed against real state");
        });
    }
}
