using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class GoalBiasReuseScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/goal-bias-reuse", () => TownScenarioFactory.Arena(4939,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 8, 2);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1, "lawful");
            Actor first = NpcIntentSupport.Actor(world, "first", 2, 1);
            Actor second = NpcIntentSupport.Actor(world, "second", 3, 1);
            foreach (Actor target in new[] { first, second })
            {
                owner.Personality.Knowledge.See(target, 0);
                NpcKnownPerson known = owner.Personality.Knowledge.Person(target.PersonalityIdentity);
                known.Danger = known.Violation = known.ThreatConfidence = 100;
            }
            Compare(world, owner);
            owner.Personality.AddTrait(new TraitInstance("fearful"));
            Compare(world, owner);
        });
    }

    static void Compare(ScenarioWorld world, Actor owner)
    {
        var candidates = NpcGoalGenerator.Evaluate(world.Game, owner);
        Check.Equal(true, candidates.Count > 1, "multiple goals exercise shared and per-person values");
        foreach (NpcGoalCandidate candidate in candidates)
        {
            NpcGeneratedGoal goal = candidate.State;
            NpcValueDefinition definition = world.Game.NpcContent.Value(goal);
            NpcGeneratedGoal fresh = NpcValues.Evaluate(world.Game.NpcContent, owner, definition,
                goal.SubjectId, goal.Current, goal.Desired, goal.Deficit, goal.Confidence, goal.Result);
            Check.Equal(fresh.Importance, goal.Importance, "shared biases preserve each goal's importance");
            Check.Equal(fresh.Utility, goal.Utility, "shared biases preserve goal priority");
        }
    }
}
