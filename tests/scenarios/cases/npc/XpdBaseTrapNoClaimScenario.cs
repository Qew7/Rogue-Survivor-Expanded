using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;

static class XpdBaseTrapNoClaimScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("xpd/base-trap-no-claim", () => TownScenarioFactory.Arena(4423,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GameMode = GameMode.GM_XPD;
            Actor spectator = SkillScenario.Actor(world);
            spectator.Controller = new PlayerController();
            world.Place(spectator, 4, 2);
            world.SetPlayer(spectator);
            Actor guard = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "guard", false, false, 0);
            guard.Controller = new CivilianAI();
            world.Map.PlaceActorAt(guard, new Point(2, 1));
            Check.Equal(true, guard.Inventory.AddAll(new ItemTrap(world.Game.GameItems.BARBED_WIRE)),
                "unclaimed guard carries trap");
            guard.ActionPoints = Rules.BASE_ACTION_COST;
            world.NpcTurn(guard);
            Inventory ground = world.Map.GetItemsAt(new Point(2, 1));
            Check.Equal(true, ground == null || !ground.HasItemMatching(item => item is ItemTrap),
                "base defense does not place trap without a claim");
        });
    }
}
