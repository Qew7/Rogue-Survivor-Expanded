using System;
using System.Drawing;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;

// One process, separate fixtures and seeds for routing, food, and sleep.
static partial class NpcSafetyBenchmarks
{
    static Result Run(string group, string name, int seed, string layout, bool danger,
        bool exit, bool inside, int foodState, int foodPosition, int sleepState)
    {
        ScenarioWorld world = TownScenarioFactory.Arena(seed, Rows(layout));
        Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
        NpcIntentSupport.Player(world, 12, 6).HitPoints = 10000;
        if (inside)
            for (int y = 0; y < 7; y++) for (int x = 0; x <= 10; x++)
                if (world.Map.GetTileAt(x, y).Model.IsWalkable) world.Map.GetTileAt(x, y).IsInside = true;
        string trait = name.Contains("organized") ? "organized" :
            name.Contains("cautious") ? "cautious" :
            name.Contains("adaptable") ? "adaptable" :
            name.Contains("impulsive") ? "impulsive" : "timid";
        Actor civilian = name.Contains("ordinary") ? NpcIntentSupport.Actor(world, "civilian", 5, 3) :
            NpcIntentSupport.Actor(world, "civilian", 5, 3, trait);
        civilian.HitPoints = world.Game.Rules.ActorMaxHPs(civilian) / 2;
        civilian.FoodPoints = foodState == 0 || foodState == 4 ? world.Game.Rules.ActorMaxFood(civilian) :
            Session.Get.GamePreset.HungerPoints - 1;
        civilian.SleepPoints = sleepState == 0 ? world.Game.Rules.ActorMaxSleep(civilian) : 0;
        int initialFood = civilian.FoodPoints;
        Type simFlag = typeof(RogueGame).GetNestedType("SimFlags", BindingFlags.NonPublic);
        object normalSim = Enum.ToObject(simFlag, 0);
        ItemFood ration = null;
        if (foodState == 1 || foodState == 2 || foodState == 4)
        {
            ration = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            if (foodState == 2) civilian.Inventory.AddAll(ration);
            else world.Map.DropItemAt(ration, new Point(foodPosition, 3));
        }
        Actor zombie = null;
        if (danger)
        {
            zombie = new Actor(world.Game.GameActors.Zombie, world.Game.GameFactions.TheUndeads,
                "zombie", false, false, 0);
            zombie.Controller = new ZombieAI();
            world.Place(zombie, 7, 3);
        }
        if (exit)
        {
            var annex = new Map(seed + 100000, "refuge", 3, 3);
            for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++)
                annex.SetTileModelAt(x, y, world.Game.GameTiles.FLOOR_ASPHALT);
            world.Map.District.AddUniqueMap(annex);
            Point exitPoint = layout == "branch" ? new Point(3, 2) :
                layout == "turn_exit" ? new Point(4, 1) :
                layout == "long_turn_exit" ? new Point(4, 0) : new Point(1, 3);
            world.Map.SetExitAt(exitPoint, new Exit(annex, new Point(1, 1)) { IsAnAIExit = true });
        }
        var result = new Result { Group = group, Name = name };
        bool everAway = false;
        int limit = danger ? 30 : 20;
        civilian.ActionPoints = 0;
        if (zombie != null) zombie.ActionPoints = 0;
        foreach (Actor actor in world.Map.Actors) if (actor.IsPlayer) actor.ActionPoints = 0;
        for (int turn = 1; turn <= limit; turn++)
        {
            Check.Call(world.Game, "NextMapTurn", new[] { typeof(Map), simFlag }, world.Map, normalSim);
            Session.Get.WorldTime.TurnCounter = world.Map.LocalTime.TurnCounter;
            if (civilian.IsDead)
            {
                result.Died = 1; result.TotalLife = turn; break;
            }
            if (civilian.IsSleeping)
            {
                result.Slept = result.Collapsed = 1;
                if (zombie != null && world.Game.Rules.GridDistance(civilian.Location.Position, zombie.Location.Position) <= 3)
                    result.SleepNearEnemy = 1;
                result.TotalLife = turn; break;
            }
            foreach (Actor actor in world.Map.Actors) if (actor.IsPlayer) actor.ActionPoints = 0;
            bool terminal = false;
            for (int step = 0; step < 30; step++)
            {
                Actor actor = world.Game.Rules.GetNextActorToAct(world.Map, world.Map.LocalTime.TurnCounter);
                if (actor == null) break;
                if (actor.IsPlayer || (actor == zombie && name == "hidden_enemy_exhausted_inside"))
                { actor.ActionPoints = 0; continue; }
                if (actor != civilian && actor != zombie)
                { actor.ActionPoints = 0; continue; }
                int before = zombie == null || actor != civilian ? -1 :
                    world.Game.Rules.GridDistance(civilian.Location.Position, zombie.Location.Position);
                ActorAction action = actor.Controller.GetAction(world.Game);
                if (action == null || !action.IsLegal()) throw new Exception("Illegal action: " + name + " seed " + seed);
                string kind = action.GetType().Name;
                action.Perform();
                if (actor == civilian)
                {
                    if (kind == "ActionSleep") result.SleepActions = 1;
                    if (kind == "ActionWait" && zombie != null && before <= 3) result.NearEnemyWaits++;
                    if (civilian.IsSleeping)
                    {
                        result.Slept = 1;
                        if (zombie != null && before <= 3)
                            result.SleepNearEnemy = result.VoluntarySleepNearEnemy = 1;
                        result.TotalLife = turn; terminal = true; break;
                    }
                    if (civilian.Location.Map != world.Map)
                    { result.Escaped = 1; result.TotalLife = turn; terminal = true; break; }
                    if (ration != null && foodState != 2 && civilian.Inventory.Contains(ration)) result.TookFood = 1;
                    if (civilian.FoodPoints > initialFood) result.AteFood = 1;
                    if (zombie != null)
                    {
                        int after = world.Game.Rules.GridDistance(civilian.Location.Position, zombie.Location.Position);
                        if (turn == 1 && after > before) result.FirstAway = 1;
                        if (everAway && after < before) result.TowardAfterAway = 1;
                        if (after > before) everAway = true;
                    }
                }
                if (civilian.IsDead)
                {
                    result.Died = 1; result.TotalLife = turn;
                    if (civilian.Location.Map == world.Map && civilian.Location.Position.X <= 1) result.CornerDeaths = 1;
                    terminal = true; break;
                }
                if (step == 29) throw new Exception("Action loop did not finish: " + name + " seed " + seed);
            }
            if (terminal) break;
            if (turn == limit) { result.Alive = 1; result.TotalLife = turn; }
        }
        result.Fled = NpcIntentSupport.HasEvent(civilian, "fled_in_fear") ? 1 : 0;
        return result;
    }

}
