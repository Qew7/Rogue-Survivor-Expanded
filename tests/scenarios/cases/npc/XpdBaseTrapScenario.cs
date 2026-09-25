using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;

static class XpdBaseTrapScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("xpd/base-trap", () => TownScenarioFactory.Arena(4422,
            ".......", ".......", ".......", ".......", "......."), world =>
        {
            Session.Get.GameMode = GameMode.GM_XPD;
            Actor spectator = SkillScenario.Actor(world);
            spectator.Controller = new PlayerController();
            world.Place(spectator, 6, 4);
            world.SetPlayer(spectator);
            Actor guard = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "guard", false, false, 0);
            guard.Controller = new CivilianAI();
            world.Map.PlaceActorAt(guard, new Point(2, 2));
            XpdBase home = new XpdBase(guard, new[] { new Point(2, 2) });
            world.Map.AddXpdBase(home);
            Check.Equal(true, guard.Inventory.AddAll(new ItemTrap(world.Game.GameItems.BARBED_WIRE)),
                "guard carries trap");
            for (int turn = 0; turn < 12; turn++)
            {
                guard.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(guard);
                if (FindTrap(world.Map, new Point(2, 2)) != null) break;
            }
            ItemTrap placed = FindTrap(world.Map, new Point(2, 2));
            Check.Equal(true, placed != null && placed.IsActivated,
                "NPC sets an armed trap on base boundary");
            Check.Equal(true, world.Game.Rules.IsSafeFromTrap(placed, guard),
                "trap owner is safe");
            Actor ally = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "ally", false, false, 0);
            Check.Equal(true, world.Game.Rules.IsSafeFromTrap(placed, ally),
                "faction base ally is safe");
            Check.Equal(false, world.Game.Rules.IsSafeFromTrap(placed, spectator),
                "outsider can trigger trap");

            string path = Path.Combine(Path.GetTempPath(), "base-trap-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, world.Map);
                Map loaded = (Map)BinarySaveStore.Load(path, null);
                loaded.ReconstructAuxiliaryFields();
                ItemTrap saved = FindTrap(loaded, new Point(2, 2));
                Check.Equal(true, saved != null && saved.BaseOwner != null &&
                    saved.BaseOwner.Contains(new Point(2, 2)), "trap base survives save/load");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }

    static ItemTrap FindTrap(Map map, Point point)
    {
        Inventory inventory = map.GetItemsAt(point);
        if (inventory == null) return null;
        foreach (Item item in inventory.Items)
            if (item is ItemTrap) return (ItemTrap)item;
        return null;
    }
}
