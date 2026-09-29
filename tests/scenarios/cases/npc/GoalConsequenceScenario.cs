using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

static class GoalConsequenceScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/goal-consequence", () => TownScenarioFactory.Arena(4657, ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 4, 1, "lawful");
            Actor thief = NpcIntentSupport.Actor(world, "thief", 1, 1, "solitary", "rebellious", "scavenger");
            world.Map.AddXpdBase(new XpdBase(owner, new[] { new Point(2, 1) }));
            world.Map.DropItemAt(new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 3 }, new Point(2, 1));
            thief.FoodPoints = Session.Get.GamePreset.HungerPoints - 1; NpcIntentSupport.Turn(world, thief);
            Check.Equal(3, NpcIntentSupport.FoodUnits(thief), "first character's own deficit produces actual resource acquisition");
            NpcKnownPerson belief = owner.Personality.Knowledge.Person(thief.PersonalityIdentity);
            Check.Equal(100, belief.Violation, "observed acquisition changes the owner's assessment of wrongdoing");
            NpcIntent response = NpcIntentSupport.Intent(owner, "confront_reported_aggressor");
            Check.Equal(NpcGoalValue.Justice, response.Generated.Value, "the changed state produces another participant's goal");
            Check.Equal(thief.PersonalityIdentity, response.Generated.SubjectId, "consequence binds the actual person involved");
            Check.Equal(belief.ThreatCause, response.CauseId, "continuation retains the actual physical cause");
            Check.Equal(true, response.CauseId > 0, "no fabricated trigger bridges the independent goals");
            Check.Equal(false, NpcIntentSupport.HasEvent(owner, "confronted"), "considering a response is not a successful confrontation");
        });
    }
}
