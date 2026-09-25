using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;

static class XpdCharWeaponDeliveryScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("xpd/char-weapon-delivery", () => TownScenarioFactory.Arena(4432,
            ".......", ".......", ".......", ".......", "......."), world =>
        {
            Session.Get.GameMode = GameMode.GM_XPD;
            Actor spectator = SkillScenario.Actor(world);
            spectator.Controller = new PlayerController();
            world.Place(spectator, 6, 4);
            world.SetPlayer(spectator);
            Actor guard = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCHARCorporation, "guard", false, false, 0);
            guard.Controller = new CHARGuardAI();
            world.Map.PlaceActorAt(guard, new Point(2, 2));
            XpdBase home = new XpdBase(guard, new[] {
                new Point(1, 1), new Point(2, 1), new Point(2, 2) });
            home.SetWeaponRoom(new Rectangle(1, 1, 1, 1));
            world.Map.AddXpdBase(home);
            ItemMeleeWeapon loot = new ItemMeleeWeapon(world.Game.GameItems.CROWBAR);
            world.Map.DropItemAt(loot, new Point(5, 2));
            AIController ai = (AIController)guard.Controller;
            ai.SetOrder(new ActorOrder(ActorTasks.SCAVENGE_SUPPLIES,
                new Location(world.Map, new Point(2, 2))));
            for (int turn = 0; turn < 65 && ai.Order != null; turn++)
            {
                guard.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(guard);
            }
            Check.Equal(null, ai.Order, "CHAR guard completes weapon supply order");
            Check.Equal(true, world.Map.GetItemsAt(new Point(1, 1)) != null &&
                world.Map.GetItemsAt(new Point(1, 1)).Contains(loot),
                "CHAR guard leaves auto-equipped weapon in storage");
        });
    }
}
