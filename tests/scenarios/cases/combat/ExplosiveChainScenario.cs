using System;
using System.Drawing;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

static class ExplosiveChainScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("combat/explosive-chain", () => TownScenarioFactory.Arena(4584,
            "..........", "..........", "..........", "..........", "..........",
            "..........", "..........", "..........", "..........", ".........."), world =>
        {
            Actor player = SkillScenario.Actor(world);
            world.Place(player, 1, 1);
            world.SetPlayer(player);
            Point first = new Point(7, 7);
            Point second = new Point(8, 7);
            ItemGrenadePrimed soon = new ItemGrenadePrimed(world.Game.GameItems.GRENADE_PRIMED);
            ItemGrenadePrimed later = new ItemGrenadePrimed(world.Game.GameItems.GRENADE_PRIMED);
            soon.FuseTimeLeft = 1;
            later.FuseTimeLeft = 10;
            world.Map.DropItemAt(soon, first);
            world.Map.DropItemAt(later, second);
            Type flags = typeof(RogueGame).GetNestedType("SimFlags", BindingFlags.NonPublic);
            Check.Call(world.Game, "NextMapTurn", new[] { typeof(Map), flags },
                world.Map, Enum.Parse(flags, "NOT_SIMULATING"));
            Check.Equal(false, world.Map.GetItemsAt(first) != null &&
                world.Map.GetItemsAt(first).Contains(soon), "expired explosive detonates");
            Check.Equal(false, world.Map.GetItemsAt(second) != null &&
                world.Map.GetItemsAt(second).Contains(later), "nearby explosive chains");
        });
    }
}
