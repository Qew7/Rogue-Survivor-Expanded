using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class RobberyEligibilityScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/robbery-eligibility", () => TownScenarioFactory.Arena(9766,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Map.Lighting = Lighting.LIT;
            Actor victim = NpcIntentSupport.Actor(world, "victim", 2, 1);
            Actor lawful = NpcIntentSupport.Actor(world, "lawful civilian", 1, 1, "lawful", "kind", "timid");
            Actor weak = NpcIntentSupport.Actor(world, "weak civilian", 3, 1, "cruel", "selfish", "brave");
            NpcIntentSupport.Player(world, 8, 2);
            NpcIntentSupport.Food(world, victim, 1);
            lawful.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(lawful);
            weak.HitPoints = 1;
            weak.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(weak);
            Check.Equal(true, lawful.Personality.Knowledge.Person(victim.PersonalityIdentity) != null &&
                weak.Personality.Knowledge.Person(victim.PersonalityIdentity) != null,
                "both civilians have actually seen the carried food");
            Check.Equal(false, NpcGoalGenerator.Evaluate(world.Game, lawful).Any(g => g.Capability.Id == "robbery_demand"),
                "lawful and compassionate civilian declines the threat despite an available item");
            Check.Equal(false, NpcGoalGenerator.Evaluate(world.Game, weak).Any(g => g.Capability.Id == "robbery_demand"),
                "injured civilian does not threaten a healthier carrier");
        });
    }
}
