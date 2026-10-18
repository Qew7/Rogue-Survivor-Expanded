using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class GoalDispositionSinglePassScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/goal-disposition-single-pass", () => TownScenarioFactory.Arena(8141,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 2, 1);
            NpcContentCatalog catalog = world.Game.NpcContent;
            Compare(owner, catalog);
            foreach (TraitDefinition definition in catalog.Personalities.AllTraits)
            {
                owner.Personality.AddTrait(new TraitInstance(definition.Id));
                Compare(owner, catalog);
            }
            Session.Get.GamePreset.NpcPersonalitiesEnabled = false;
            Compare(owner, catalog);
        });
    }

    static void Compare(Actor owner, NpcContentCatalog catalog)
    {
        int humanLife, careCircle, messageTrust, scarcity, patience;
        PersonalitySystem.GoalDispositions(owner, catalog, out humanLife, out careCircle,
            out messageTrust, out scarcity, out patience);
        Check.Equal(PersonalitySystem.Disposition(owner, NpcPersonalityAxis.HumanLife, catalog), humanLife, "human life matches");
        Check.Equal(PersonalitySystem.Disposition(owner, NpcPersonalityAxis.CareCircle, catalog), careCircle, "care circle matches");
        Check.Equal(PersonalitySystem.Disposition(owner, NpcPersonalityAxis.MessageTrust, catalog), messageTrust, "message trust matches");
        Check.Equal(PersonalitySystem.Disposition(owner, NpcPersonalityAxis.Scarcity, catalog), scarcity, "scarcity matches");
        Check.Equal(PersonalitySystem.Disposition(owner, NpcPersonalityAxis.Patience, catalog), patience, "patience matches");
    }
}
