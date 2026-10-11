using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class GoalInertOfferScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/goal-inert-offer", () => TownScenarioFactory.Arena(8124,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1, "kind");
            Actor target = NpcIntentSupport.Actor(world, "target", 2, 1);
            NpcKnownPerson known = owner.Personality.Knowledge.See(target, 0);
            NpcIntentDefinition capability = world.Game.NpcContent.Capability(FoodAidModule.AnswerFoodRequestId);
            var context = new NpcGoalContext(world.Game, owner, world.Game.NpcContent);
            Check.Equal(false, context.Pending(NpcGoalValue.Care, known.Id), "no active goal is pending");
            var complete = new NpcGoalOffers(context);
            var refresh = new NpcGoalOffers(context, true);
            for (int i = 0; i < 192; i++)
            {
                complete.Add(known, NpcGoalValue.Care, capability, 100, 100, 0, 100);
                refresh.Add(known, NpcGoalValue.Care, capability, 100, 100, 0, 100);
            }
            Check.Equal(192, complete.Candidates.Count, "full evaluation retains inert offers");
            Check.Equal(0, refresh.Candidates.Count, "refresh omits inert offers without an active generated goal");
            refresh.Add(known, NpcGoalValue.Care, capability, 0, 100, 100, 100);
            Check.Equal(0, refresh.Candidates.Count, "omitted offers still consume the candidate limit");
            NpcGoalLifecycle.Apply(owner, refresh.Candidates, world.Game.NpcContent, true);
            Check.Equal(0, owner.Personality.Intents.Count, "inert offers start no goal");

            var active = new NpcGeneratedGoal { Value = NpcGoalValue.Care, SubjectId = known.Id,
                Current = 60, Desired = 100, Deficit = 40, Importance = 100,
                Confidence = 100, Utility = 40 };
            NpcIntent intent = owner.Personality.StartGoal(capability.Id, owner, known, 0, 60, 0,
                active.Utility, 0, null, 0, active);
            Check.Equal(true, intent != null, "fixture starts an active generated goal");
            context = new NpcGoalContext(world.Game, owner, world.Game.NpcContent);
            Check.Equal(true, context.Pending(NpcGoalValue.Care, known.Id), "active goal is pending");
            refresh = new NpcGoalOffers(context, true);
            refresh.Add(known, NpcGoalValue.Care, capability, 100, 100, 0, 100);
            Check.Equal(1, refresh.Candidates.Count, "active goal keeps its satisfying offer");
            NpcGoalLifecycle.Apply(owner, refresh.Candidates, world.Game.NpcContent, true);
            Check.Equal(0, intent.Generated.Utility, "active goal receives the updated state");
            Check.Equal(true, intent.Finished, "satisfied goal completes normally");
            Check.Equal(false, new NpcGoalContext(world.Game, owner, world.Game.NpcContent).Pending(NpcGoalValue.Care, known.Id),
                "finished goal is no longer pending");

            Actor otherOwner = NpcIntentSupport.Actor(world, "other owner", 4, 1, "kind");
            NpcKnownPerson otherKnown = otherOwner.Personality.Knowledge.See(target, 0);
            var positive = new NpcGoalOffers(new NpcGoalContext(world.Game, otherOwner, world.Game.NpcContent), true);
            positive.Add(otherKnown, NpcGoalValue.Care, capability, 0, 100, 100, 100);
            Check.Equal(1, positive.Candidates.Count, "positive need remains available");
            NpcGoalLifecycle.Apply(otherOwner, positive.Candidates, world.Game.NpcContent, true);
            Check.Equal(1, otherOwner.Personality.Intents.Count, "positive need still starts a goal");
        });
    }
}
