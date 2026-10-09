using System;
using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.AI;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI.Sensors;

static class SightSparseResourcesScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/sight-sparse-resources", () => TownScenarioFactory.Arena(7983,
            "....................", "....................", "....................", "....................",
            "....................", "....................", "....................", "....................",
            "....................", "....................", "....................", "....................",
            "....................", "....................", "....................", "....................",
            "....................", "....................", "....................", "...................."), world =>
        {
            world.Map.Lighting = Lighting.LIT;
            Actor observer = SkillScenario.Actor(world);
            world.Place(observer, 10, 10);
            LOSSensor sight = new LOSSensor(LOSSensor.SensingFilter.ITEMS |
                LOSSensor.SensingFilter.CORPSES);
            Check.Equal(0, sight.Sense(world.Game, observer).Count, "empty map has no resource percepts");

            Point itemA = new Point(11, 10), itemB = new Point(9, 10);
            Point corpseA = new Point(10, 11), corpseB = new Point(10, 9);
            ItemFood foodA = new ItemFood(world.Game.GameItems.GROCERIES);
            ItemFood foodB = new ItemFood(world.Game.GameItems.GROCERIES);
            world.Map.DropItemAt(foodB, itemB);
            world.Map.DropItemAt(foodA, itemA);
            Corpse deadA = new Corpse(SkillScenario.Actor(world), 5, 5, 0, 0, 1);
            Corpse deadB = new Corpse(SkillScenario.Actor(world), 5, 5, 0, 0, 1);
            Corpse deadC = new Corpse(SkillScenario.Actor(world), 5, 5, 0, 0, 1);
            world.Map.AddCorpseAt(deadB, corpseB);
            world.Map.AddCorpseAt(deadA, corpseA);
            world.Map.AddCorpseAt(deadC, corpseA);

            HashSet<Point> fov = LOS.ComputeFOVFor(world.Game.Rules, observer,
                world.Map.LocalTime, world.Game.Session.World.Weather);
            var expectedItems = new List<Point>();
            var expectedCorpses = new List<Point>();
            foreach (Point p in fov)
            {
                if (p == itemA || p == itemB) expectedItems.Add(p);
                if (p == corpseA || p == corpseB) expectedCorpses.Add(p);
            }
            var seenItems = new List<Point>();
            var seenCorpses = new List<Point>();
            foreach (Percept percept in sight.Sense(world.Game, observer))
            {
                if (percept.Percepted is Inventory) seenItems.Add(percept.Location.Position);
                List<Corpse> corpses = percept.Percepted as List<Corpse>;
                if (corpses != null)
                {
                    seenCorpses.Add(percept.Location.Position);
                    if (percept.Location.Position == corpseA)
                        Check.Equal(2, corpses.Count, "stacked corpses share one percept");
                }
            }
            Check.Equal(String.Join(",", expectedItems), String.Join(",", seenItems),
                "item percepts preserve FOV order");
            Check.Equal(String.Join(",", expectedCorpses), String.Join(",", seenCorpses),
                "corpse percepts preserve FOV order");
            world.Map.RemoveItemAt(foodA, itemA);
            world.Map.RemoveItemAt(foodB, itemB);
            world.Map.RemoveCorpse(deadA);
            world.Map.RemoveCorpse(deadB);
            world.Map.RemoveCorpse(deadC);
            Check.Equal(0, sight.Sense(world.Game, observer).Count,
                "removing the last item and corpse restores empty fast path");
        });
    }
}
