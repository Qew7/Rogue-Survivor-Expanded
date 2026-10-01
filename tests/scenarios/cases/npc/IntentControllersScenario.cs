using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.Personality;

static class IntentControllersScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/intent-controllers", () => TownScenarioFactory.Arena(4609, "...", "...", "..."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor helper = NpcIntentSupport.Player(world, 1, 1);
            Actor[] actors = { NpcIntentSupport.Actor(world, "civilian", 0, 1, "generous"),
                NpcIntentSupport.Actor(world, "biker", 2, 1, "generous"),
                NpcIntentSupport.Actor(world, "soldier", 1, 0, "generous"),
                NpcIntentSupport.Actor(world, "guard", 1, 2, "generous") };
            actors[0].Controller = new CivilianAI(); actors[1].Controller = new GangAI();
            actors[2].Controller = new SoldierAI(); actors[3].Controller = new CHARGuardAI();
            for (int i = 0; i < actors.Length; i++)
            {
                Actor actor = actors[i]; NpcIntentSupport.Food(world, actor, 2);
                PersonalitySystem.Report(world.Game, new SignificantEvent("helped", actor, helper, world.Map, actor.Location.Position, 0));
                NpcIntentSupport.Turn(world, actor); NpcIntentSupport.Turn(world, actor);
                Check.Equal(NpcIntentStatus.Completed, actor.Personality.Intents[0].Status, "controller completes repayment: " + actor.Name);
                Check.Equal(1, NpcIntentSupport.FoodUnits(actor), "controller retains reserve: " + actor.Name);
                Check.Equal(i + 1, NpcIntentSupport.FoodUnits(helper), "each real controller transfers one unit");
            }
        });
    }
}
