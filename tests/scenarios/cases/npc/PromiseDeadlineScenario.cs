using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class PromiseDeadlineScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/promise-deadline", () => TownScenarioFactory.Arena(4666, ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            Actor recipient = NpcIntentSupport.Actor(world, "recipient", 2, 1, "sociable", "trusting");
            Actor helper = NpcIntentSupport.Actor(world, "helper", 1, 1, "kind", "honest");
            Actor witness = NpcIntentSupport.Actor(world, "witness", 5, 1);
            recipient.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            NpcIntentSupport.Turn(world, recipient); NpcIntentSupport.Turn(world, helper);
            NpcCommitment promise = recipient.Personality.Commitments[0];
            world.Map.LocalTime.TurnCounter = promise.DueTurn - 1; NpcIntentSystem.AdvanceClock(world.Game, world.Map);
            Check.Equal(NpcCommitmentStatus.Active, promise.Status, "a promise is not broken before the deadline");
            world.Map.LocalTime.TurnCounter = promise.DueTurn; NpcIntentSystem.AdvanceClock(world.Game, world.Map);
            Check.Equal(NpcCommitmentStatus.Broken, promise.Status, "recipient assesses the actual missing delivery at the deadline");
            Check.Equal(true, NpcIntentSupport.HasEvent(recipient, "promise_broken"), "private disappointment has an attributed record");
            Check.Equal(false, NpcIntentSupport.HasEvent(witness, "promise_broken"), "bystanders cannot observe a private conclusion");
            Check.Equal(0, witness.Personality.Commitments.Count, "hearing a promise creates no obligation for a bystander");
            RelationshipRecord relation = recipient.Personality.Person(helper.PersonalityIdentity);
            Check.Equal(true, relation.Feeling < 0, "broken promise changes personal attitude");
            int memories = relation.Memories.Count; NpcIntentSystem.AdvanceClock(world.Game, world.Map);
            Check.Equal(memories, relation.Memories.Count, "deadline is assessed only once");
            MemoryInstance memory = null; foreach (MemoryInstance candidate in relation.Memories) if (candidate.Id == "promise_broken") memory = candidate;
            world.Map.LocalTime.TurnCounter = memory.ResolveTurn; PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(true, recipient.Personality.HasTrait("disillusioned"), "lasting experience changes later decision weights");
            Check.Equal(true, relation.Memories.Contains(memory), "resolved disappointment remains in relationship history");
        });
    }
}
