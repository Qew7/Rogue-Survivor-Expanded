using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class GoalStateScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/goal-state", () => TownScenarioFactory.Arena(4650, ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 6, 2);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1, "generous");
            Actor friend = NpcIntentSupport.Actor(world, "friend", 2, 1);
            owner.Personality.Knowledge.See(friend, 0);
            owner.Personality.Opinion(friend.PersonalityIdentity, friend.UnmodifiedName).AdjustSocial(debt: 10);
            NpcIntentSupport.Food(world, owner, 3);
            Check.Equal(0, owner.Personality.Events.Count, "state has no triggering event");
            NpcGoalGenerator.Refresh(world.Game, owner);
            NpcIntent goal = NpcIntentSupport.Intent(owner, "repay_aid");
            Check.Equal(NpcGoalValue.Reciprocity, goal.Generated.Value, "an unresolved debt produces a desired state");
            Check.Equal(10, goal.Generated.Current, "goal records the observed current debt");
            Check.Equal(0L, goal.CauseId, "generation invents no event to explain the state");
            NpcIntentSupport.Turn(world, owner);
            Check.Equal(1, NpcIntentSupport.FoodUnits(friend), "real controller pursues the state-generated goal");
            Check.Equal(0, owner.Personality.Person(friend.PersonalityIdentity).Debt, "actual gift reduces the actual debt");
            Check.Equal(NpcIntentStatus.Completed, goal.Status, "goal completes after execution");
            int count = owner.Personality.Intents.Count; NpcGoalGenerator.Refresh(world.Game, owner);
            Check.Equal(count, owner.Personality.Intents.Count, "satisfied state produces no duplicate goal");
        });
    }
}
