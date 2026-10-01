using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class CustomCatalogGoalRecordScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/custom-catalog-goal-record", () => TownScenarioFactory.Arena(4826,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Game.NpcContent = PersonalityContent.Create(new NpcRestContent()).Content;
            Actor owner = NpcIntentSupport.Actor(world, "resting", 1, 1, "restful");
            owner.StaminaPoints = Rules.STAMINA_MIN_FOR_ACTIVITY;

            NpcGoalGenerator.Refresh(world.Game, owner);
            NpcIntent goal = NpcIntentSupport.Intent(owner, "take_breath");
            Check.Equal(true, goal != null, "the custom module starts a real goal");
            bool attributed = false;
            foreach (ResidentEntry entry in Session.Get.ResidentRecords.Register(owner).Entries)
                if (entry.Kind == "goal_started" && entry.Text.Contains("Recover stamina") &&
                    entry.Text.Contains("because trait Restful")) attributed = true;
            Check.Equal(true, attributed, "records resolve custom goal prose and trait influence from the active catalog");

            NpcKnownPerson self = new NpcKnownPerson { Id = owner.PersonalityIdentity,
                Name = owner.UnmodifiedName, Place = owner.Location };
            Check.Equal(null, NpcStorySystem.StartKnown(owner, self, null, catalog: world.Game.NpcContent),
                "a removed capability cannot create an assigned intention");
        });
    }
}
