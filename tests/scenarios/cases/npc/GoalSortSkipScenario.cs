using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class GoalSortSkipScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/goal-sort-skip", () => TownScenarioFactory.Arena(8113,
            "......", "......", "......"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1, "kind");
            Actor otherOwner = NpcIntentSupport.Actor(world, "other owner", 3, 1, "kind");
            Actor target = NpcIntentSupport.Actor(world, "target", 2, 1);
            NpcKnownPerson known = owner.Personality.Knowledge.See(target, 0);
            NpcIntentDefinition capability = world.Game.NpcContent.Capability(FoodAidModule.AnswerFoodRequestId);
            NpcGeneratedGoal active = Care(known, 40, 100);
            NpcIntent intent = owner.Personality.StartGoal(capability.Id, owner, known, 0, 60, 0,
                active.Utility, 0, null, 0, active);
            Check.Equal(true, intent != null, "fixture starts a generated goal");

            var low = new List<NpcGoalCandidate> {
                new NpcGoalCandidate { Target = known, Capability = capability, State = Care(known, 0, 0) },
                new NpcGoalCandidate { Target = known, Capability = capability, State = Care(known, 0, 0) }
            };
            NpcGoalLifecycle.Apply(owner, low, world.Game.NpcContent, false);
            Check.Equal(0, intent.Generated.Utility, "existing goal is refreshed before low-utility candidates are skipped");
            Check.Equal(1, owner.Personality.Intents.Count, "low-utility candidates do not create another goal");

            NpcKnownPerson otherKnown = otherOwner.Personality.Knowledge.See(target, 0);
            var viable = new List<NpcGoalCandidate> {
                new NpcGoalCandidate { Target = otherKnown, Capability = capability, State = Care(otherKnown, 40, 100) }
            };
            NpcGoalLifecycle.Apply(otherOwner, viable, world.Game.NpcContent, false);
            Check.Equal(1, otherOwner.Personality.Intents.Count, "a viable candidate still starts a goal");

            Actor thirdOwner = NpcIntentSupport.Actor(world, "third owner", 4, 1, "kind");
            Actor secondTarget = NpcIntentSupport.Actor(world, "second target", 5, 1);
            NpcKnownPerson firstKnown = thirdOwner.Personality.Knowledge.See(target, 0);
            NpcKnownPerson secondKnown = thirdOwner.Personality.Knowledge.See(secondTarget, 0);
            var tied = new List<NpcGoalCandidate> {
                new NpcGoalCandidate { Target = secondKnown, Capability = capability, State = Care(secondKnown, 40, 100) },
                new NpcGoalCandidate { Target = firstKnown, Capability = capability, State = Care(firstKnown, 40, 100) },
                new NpcGoalCandidate { Target = firstKnown, Capability = capability, State = Care(firstKnown, 0, 0) }
            };
            NpcGoalLifecycle.Apply(thirdOwner, tied, world.Game.NpcContent, false);
            Check.Equal(2, thirdOwner.Personality.Intents.Count, "tied viable goals remain available");
            Check.Equal(true, string.CompareOrdinal(thirdOwner.Personality.Intents[0].Generated.Key,
                thirdOwner.Personality.Intents[1].Generated.Key) < 0, "tied viable goals keep their key order");
        });
    }

    static NpcGeneratedGoal Care(NpcKnownPerson target, int utility, int deficit)
    {
        return new NpcGeneratedGoal { Value = NpcGoalValue.Care, SubjectId = target.Id,
            Current = 100 - deficit, Desired = 100, Deficit = deficit, Importance = 40,
            Confidence = 100, Utility = utility };
    }
}
