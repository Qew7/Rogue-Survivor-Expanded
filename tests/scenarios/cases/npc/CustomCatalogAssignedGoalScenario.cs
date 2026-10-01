using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class CustomCatalogAssignedGoalScenario
{
    sealed class CourageModule : INpcContentModule
    {
        public string Id { get { return "scenario.courage"; } }
        public void Register(NpcCatalogBuilder catalog)
        {
            catalog.Trait(new TraitDefinition("module_courage", "Module courage", false, false, null,
                new TraitEffect(DecisionKind.Courage, 35)));
        }
    }

    public static void Register()
    {
        ScenarioRunner.Add("npc/custom-catalog-assigned-goal", () => TownScenarioFactory.Arena(4828,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Game.NpcContent = PersonalityContent.Create(new CourageModule()).Content;
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1, "module_courage");
            Actor target = NpcIntentSupport.Actor(world, "target", 2, 1);
            NpcKnownPerson known = new NpcKnownPerson { Id = target.PersonalityIdentity,
                Name = target.UnmodifiedName, Place = target.Location };
            NpcContentCatalog active = world.Game.NpcContent;
            NpcIntentDefinition retaliation = active.Capability("retaliate");
            int score = retaliation.AssignedScore(active, owner, target.PersonalityIdentity);
            Check.Equal(true, score >= retaliation.Threshold,
                "module trait makes the assigned goal eligible");

            NpcIntent goal = NpcStorySystem.StartKnown(world.Game.NpcContent, owner, known, retaliation);
            Check.Equal(true, goal != null, "assigned goal starts using the active trait registry");
            Check.Equal(score, goal.Priority, "admission records the same score used for execution");
            Check.Equal(true, NpcPlanExecution.Owned(world.Game, owner, goal),
                "the active catalog keeps the assigned action legal");

            world.Game.NpcContent = PersonalityContent.Create().Content;
            Check.Equal(false, NpcPlanExecution.Owned(world.Game, owner, goal),
                "removing the module removes its trait contribution at the legality boundary");
        });
    }
}
