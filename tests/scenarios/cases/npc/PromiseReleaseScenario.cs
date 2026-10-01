using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

static class PromiseReleaseScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/promise-release", () => TownScenarioFactory.Arena(4678, ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            Actor recipient = NpcIntentSupport.Actor(world, "recipient", 2, 1, "sociable", "kind");
            Actor helper = NpcIntentSupport.Actor(world, "helper", 1, 1, "kind", "honest");
            recipient.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            NpcIntentSupport.Turn(world, recipient); NpcIntentSupport.Turn(world, helper);
            Actor donor = NpcIntentSupport.Actor(world, "donor", 3, 1); ItemFood gift = NpcIntentSupport.Food(world, donor, 1);
            world.Game.DoGiveItemTo(donor, recipient, gift);
            NpcCommitment promise = helper.Personality.Commitments[0];
            Check.Equal(NpcCommitmentStatus.Active, promise.Status, "another person's gift does not silently fulfil or release a promise");
            recipient.FoodPoints = world.Game.Rules.ActorMaxFood(recipient);
            NpcIntentSupport.Turn(world, recipient);
            NpcIntentSupport.Turn(world, recipient);
            Check.Equal(NpcCommitmentStatus.Released, promise.Status, "compassionate recipient explicitly releases the helper");
            Check.Equal(NpcCommitmentStatus.Released, recipient.Personality.Commitments[0].Status, "actual conversation changes both participants' knowledge");
            Check.Equal(true, NpcIntentSupport.HasEvent(helper, "promise_released"), "release is an observable action");
            world.Map.LocalTime.TurnCounter = 180; djack.RogueSurvivor.Gameplay.Personality.NpcIntentSystem.AdvanceClock(world.Game, world.Map);
            Check.Equal(false, NpcIntentSupport.HasEvent(recipient, "promise_broken"), "released obligation causes no false betrayal at its former deadline");
        });
    }
}
