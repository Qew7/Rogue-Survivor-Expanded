using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class GoalReevaluationScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/goal-reevaluation", () => TownScenarioFactory.Arena(4652, ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1, "lawful");
            Actor target = NpcIntentSupport.Actor(world, "target", 2, 1);
            owner.Personality.Knowledge.See(target, 0); NpcKnownPerson known = owner.Personality.Knowledge.Person(target.PersonalityIdentity);
            known.Danger = known.Violation = 100; known.ThreatConfidence = 90;
            NpcGoalGenerator.Refresh(world.Game, owner); NpcIntent warning = NpcIntentSystem.Select(world.Game.NpcContent, owner);
            Check.Equal(NpcGoalValue.Justice, warning.Generated.Value, "initial values favor a warning");
            owner.Personality.AddTrait(new TraitInstance("fearful"));
            NpcIntentSupport.Turn(world, owner);
            Check.Equal(NpcIntentStatus.Abandoned, warning.Status, "new values invalidate the original motivation");
            Check.Equal(NpcGoalValue.Safety, NpcIntentSystem.Select(world.Game.NpcContent, owner).Generated.Value, "unchanged facts support a newly preferred desired state");
            Check.Equal(false, NpcIntentSupport.HasEvent(owner, "confronted"), "abandoned goal creates no completed warning");
            Check.Equal(true, world.Game.Rules.GridDistance(owner.Location.Position, target.Location.Position) > 1, "new goal produces actual retreat");
        });
    }
}
