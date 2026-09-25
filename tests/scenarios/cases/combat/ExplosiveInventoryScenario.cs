using System;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

static class ExplosiveInventoryScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("combat/explosive-inventory", () => TownScenarioFactory.Arena(4592,
            "............", "............", "............", "............",
            "............", "............"), world =>
        {
            Actor player = SkillScenario.Actor(world);
            world.Place(player, 1, 1);
            world.SetPlayer(player);
            Actor carrier = SkillScenario.Actor(world);
            carrier.HitPoints = 1000;
            world.Place(carrier, 9, 3);
            ItemGrenadePrimed grenade = new ItemGrenadePrimed(world.Game.GameItems.GRENADE_PRIMED);
            grenade.FuseTimeLeft = 1;
            Check.Equal(true, carrier.Inventory.AddAll(grenade), "carrier holds primed grenade");
            Type flags = typeof(RogueGame).GetNestedType("SimFlags", BindingFlags.NonPublic);
            Check.Call(world.Game, "NextMapTurn", new[] { typeof(Map), flags },
                world.Map, Enum.Parse(flags, "NOT_SIMULATING"));
            Check.Equal(false, carrier.Inventory.Contains(grenade),
                "carried grenade detonates when its fuse expires");
        });
    }
}
