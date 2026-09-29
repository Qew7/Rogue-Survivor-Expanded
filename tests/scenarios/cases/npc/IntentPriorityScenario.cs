using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;

static class IntentPriorityScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/intent-priority", () => TownScenarioFactory.Arena(4608, ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 4, 2);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1, "cruel", "honest");
            Actor helper = NpcIntentSupport.Actor(world, "helper", 2, 1);
            NpcIntentSupport.Food(world, owner, 3);
            PersonalitySystem.Report(world.Game, new SignificantEvent("helped", owner, helper, world.Map, owner.Location.Position, 0));
            NpcIntent intent = owner.Personality.Intents[0];
            NpcIntentSystem.Maintain(world.Game, owner, new[] { helper }, false, true);
            Check.Equal(NpcIntentStatus.Paused, intent.Status, "explicit order pauses the personal goal");
            var gift = new ActionNpcIntent(owner, world.Game, intent, helper, NpcFoodSupply.SpareFood(world.Game, owner, helper));
            Check.Equal(false, gift.IsLegal(), "paused intention cannot be performed through a stale action");
            NpcIntentSystem.Maintain(world.Game, owner, new[] { helper }, false, false);
            Check.Equal(NpcIntentStatus.Active, intent.Status, "goal resumes after the order");
            Actor zombie = new Actor(world.Game.GameActors.Zombie, world.Game.GameFactions.TheUndeads, "zombie", false, false, 0);
            world.Place(zombie, 1, 0);
            int units = NpcIntentSupport.FoodUnits(helper);
            NpcIntentSupport.Turn(world, owner);
            Check.Equal(units, NpcIntentSupport.FoodUnits(helper), "real controller handles visible enemy before repayment");
            Check.Equal(NpcIntentStatus.Paused, intent.Status, "combat preserves the interrupted intention");
            world.Map.RemoveActor(zombie);
            MemoryInstance change = new MemoryInstance("killed_person", 0, 50, "victim", false);
            change.RememberEvidence("kill_human", 1); owner.Personality.AddMemory(change);
            world.Map.LocalTime.TurnCounter = 50; PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(true, owner.Personality.HasTrait("maniac"), "real memory resolution changes personality");
            NpcIntentSystem.Maintain(world.Game, owner, new Actor[0], false, false);
            Check.Equal(NpcIntentStatus.Abandoned, intent.Status, "changed traits revise motivation even when target is unseen");
            Check.Equal("motivation changed", intent.Outcome, "abandonment records the actual reason");
        });
    }
}
