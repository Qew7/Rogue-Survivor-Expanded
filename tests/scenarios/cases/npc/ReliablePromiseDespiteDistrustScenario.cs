using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class ReliablePromiseDespiteDistrustScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/reliable-promise-despite-distrust", () => TownScenarioFactory.Arena(4824,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 8, 2);
            Actor recipient = NpcIntentSupport.Actor(world, "recipient", 2, 1, "sociable");
            Actor helper = NpcIntentSupport.Actor(world, "helper", 1, 1, "kind", "honest");
            recipient.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            NpcIntentSupport.Turn(world, recipient);
            NpcIntentSupport.Turn(world, helper);
            NpcCommitment promise = helper.Personality.Commitments[0];
            Check.Equal(NpcCommitmentStatus.Active, promise.Status, "spoken promise is still active");
            helper.Personality.AddTrait(new TraitInstance("reliable"));
            NpcKnownPerson known = helper.Personality.Knowledge.Person(recipient.PersonalityIdentity);
            known.Violation = known.ViolationConfidence = 100;
            NpcIntentSupport.Food(world, helper, 3);
            Check.Equal(true, PersonalitySystem.Attitude(helper, recipient) <= -30,
                "serious distrust blocks a new group invitation");

            world.Map.LocalTime.TurnCounter = 1;
            NpcIntentSupport.Turn(world, helper);
            Check.Equal(NpcCommitmentStatus.Kept, promise.Status,
                "reliable promisor still delivers the owed food despite distrust");
            Check.Equal(true, NpcIntentSupport.HasEvent(recipient, "promise_kept"),
                "real delivery records that the promise was kept");
        });
    }
}
