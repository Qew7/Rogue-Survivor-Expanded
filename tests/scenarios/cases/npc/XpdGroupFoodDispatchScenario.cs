using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;

static class XpdGroupFoodDispatchScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("xpd/group-food-dispatch", () => TownScenarioFactory.Arena(4435,
            ".......", ".......", ".......", ".......", "......."), world =>
        {
            Session.Get.GameMode = GameMode.GM_XPD;
            Actor spectator = SkillScenario.Actor(world);
            spectator.Controller = new PlayerController();
            world.Place(spectator, 6, 4);
            world.SetPlayer(spectator);
            Actor leader = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "leader", false, false, 0);
            leader.Controller = new CivilianAI();
            world.Map.PlaceActorAt(leader, new Point(2, 2));
            Actor follower = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "follower", false, false, 0);
            follower.Controller = new CivilianAI();
            world.Map.PlaceActorAt(follower, new Point(3, 2));
            leader.AddFollower(follower);
            XpdBase home = new XpdBase(leader, new[] {
                new Point(2, 2), new Point(3, 2), new Point(2, 3), new Point(3, 3) });
            world.Map.AddXpdBase(home);
            // A foreign claim must not make the group's own food count twice.
            world.Map.AddXpdBase(new XpdBase(spectator, new[] { new Point(6, 4) }));
            for (int i = 0; i < 9; i++)
            {
                ItemFood food = new ItemFood(world.Game.GameItems.GROCERIES);
                world.Map.DropItemAt(food, new Point(2, 3));
            }
            for (int i = 0; i < 3; i++)
            {
                ItemFood food = new ItemFood(world.Game.GameItems.GROCERIES);
                Check.Equal(true, follower.Inventory.AddAll(food), "follower carries food");
            }
            ItemFood outside = new ItemFood(world.Game.GameItems.GROCERIES);
            world.Map.DropItemAt(outside, new Point(5, 2));
            ItemMeleeWeapon weapon = new ItemMeleeWeapon(world.Game.GameItems.CROWBAR);
            world.Map.DropItemAt(weapon, new Point(4, 2));
            world.Map.LocalTime.TurnCounter = 26; // 26 + 2 + 2 = 30.
            leader.FoodPoints = 200;
            follower.FoodPoints = 0;
            leader.ActionPoints = Rules.BASE_ACTION_COST;
            leader.Controller.GetAction(world.Game);
            Check.Equal(null, ((OrderableAI)follower.Controller).Order,
                "twelve groceries cover current hunger plus two days with leader at 200");

            leader.FoodPoints = 0; // The same food now falls below hunger plus two days.
            leader.ActionPoints = Rules.BASE_ACTION_COST;
            world.NpcTurn(leader);
            Check.Equal(ActorTasks.SCAVENGE_SUPPLIES,
                ((OrderableAI)follower.Controller).Order.Task,
                "leader sends follower only when group food is insufficient");
            string path = Path.Combine(Path.GetTempPath(),
                "group-food-dispatch-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, world.Map);
                Map loaded = (Map)BinarySaveStore.Load(path, null);
                loaded.ReconstructAuxiliaryFields();
                Actor savedFollower = null;
                foreach (Actor actor in loaded.Actors)
                    if (actor.Name == "follower") savedFollower = actor;
                Check.Equal(true, savedFollower != null, "follower survives save and load");
                Check.Equal(true, savedFollower.Controller is OrderableAI,
                    "follower controller survives save and load");
                Check.Equal(true, ((OrderableAI)savedFollower.Controller).Order != null,
                    "food order remains assigned after load");
                Check.Equal(ActorTasks.SCAVENGE_SUPPLIES,
                    ((OrderableAI)savedFollower.Controller).Order.Task,
                    "food order survives save and load");
                Check.Equal(true, typeof(OrderableAI).GetField("m_XpdFoodOnly",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(savedFollower.Controller),
                    "food-only route survives save and load");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
            for (int turn = 0; turn < 45; turn++)
            {
                follower.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(follower);
                if (world.Map.GetItemsAt(new Point(2, 2)) != null &&
                    world.Map.GetItemsAt(new Point(2, 2)).Contains(outside)) break;
            }
            Check.Equal(true, world.Map.GetItemsAt(new Point(2, 2)) != null &&
                world.Map.GetItemsAt(new Point(2, 2)).Contains(outside),
                "ordered follower returns supplies to base");
            Check.Equal(true, world.Map.GetItemsAt(new Point(4, 2)) != null &&
                world.Map.GetItemsAt(new Point(4, 2)).Contains(weapon),
                "food shortage sends follower for food before weapons");
        });
    }
}
