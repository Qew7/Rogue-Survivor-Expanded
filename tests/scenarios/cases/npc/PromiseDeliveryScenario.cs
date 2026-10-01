using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class PromiseDeliveryScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/promise-delivery", () => TownScenarioFactory.Arena(4662, ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            Actor recipient = NpcIntentSupport.Actor(world, "recipient", 2, 1, "sociable");
            Actor helper = NpcIntentSupport.Actor(world, "helper", 1, 1, "kind", "honest");
            recipient.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            NpcIntentSupport.Turn(world, recipient);
            Check.Equal(0, helper.Personality.Commitments.Count, "a considered reply is not a spoken promise");
            Check.Equal(false, helper.Personality.Reactions.Exists(r => r.Kind == "request_refused"), "a proposed promise does not also queue a contradictory refusal");
            NpcIntentSupport.Turn(world, helper);
            Check.Equal(1, helper.Personality.Commitments.Count, "actual speech creates the commitment");
            NpcCommitment promise = helper.Personality.Commitments[0];
            Check.Equal(NpcCommitmentStatus.Active, promise.Status, "speech does not fabricate delivery");
            Check.Equal(promise.Id, recipient.Personality.Commitments[0].Id, "both participants retain the same promise identity");
            world.Map.DropItemAt(new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 3 }, new Point(1, 2));
            world.Map.LocalTime.TurnCounter = 1; NpcIntentSupport.Turn(world, helper);
            Check.Equal(3, NpcIntentSupport.FoodUnits(helper), "promisor actually finds supplies");
            world.Map.LocalTime.TurnCounter = 2; NpcIntentSupport.Turn(world, helper);
            Check.Equal(1, NpcIntentSupport.FoodUnits(recipient), "promised resource is physically delivered");
            Check.Equal(NpcCommitmentStatus.Kept, promise.Status, "real transfer fulfils the commitment");
            Check.Equal(NpcCommitmentStatus.Kept, recipient.Personality.Commitments[0].Status, "recipient knows fulfilment from participation");
            Check.Equal(true, recipient.Personality.Person(helper.PersonalityIdentity).Trust >= 15, "kept word changes future trust");
            Check.Equal(true, NpcIntentSupport.HasEvent(recipient, "promise_kept"), "fulfilled promise has a causal event");
        });
    }
}
