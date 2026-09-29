using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;
static class NpcLongRunSupport
{
    public static void Run(ScenarioWorld world, int variant)
    {
        Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
        Actor player = NpcIntentSupport.Player(world, 11, 3);
        player.FoodPoints = player.SleepPoints = player.StaminaPoints = 1000000;
        Actor leader = NpcIntentSupport.Actor(world, "leader", 1, 1, "loyal", variant == 0 ? "kind" : "organized");
        Actor helper = NpcIntentSupport.Actor(world, "helper", 2, 1, variant == 1 ? "healer" : "protective", "brave");
        Actor needy = NpcIntentSupport.Actor(world, "needy", 3, 1, "sociable", "trusting");
        Actor outsider = NpcIntentSupport.Actor(world, "outsider", 9, 1, variant == 2 ? "vindictive" : "solitary");
        leader.AddFollower(helper); leader.AddFollower(needy);
        needy.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
        if (variant == 1) needy.HitPoints = 2;
        for (int x = 1; x <= 8; x++) world.Map.DropItemAt(new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 24 }, new Point(x, 2));
        world.Map.DropItemAt(new ItemMedicine(world.Game.GameItems.MEDIKIT) { Quantity = 3 }, new Point(6, 2));
        int initialFood = Units(world);
        NpcIntentSupport.Turn(world, needy);
        Actor[] actors = { leader, helper, needy, outsider };
        MethodInfo next = typeof(RogueGame).GetMethod("NextMapTurn", BindingFlags.Instance | BindingFlags.NonPublic);
        Type flags = typeof(RogueGame).GetNestedType("SimFlags", BindingFlags.NonPublic);
        Check.Equal(true, next != null && flags != null, "real turn pipeline is available");
        object simulation = Enum.ToObject(flags, 0);
        int turns = 14 * WorldTime.TURNS_PER_DAY;
        for (int t = 0; t < turns; t++)
        {
            next.Invoke(world.Game, new[] { world.Map, simulation });
            Session.Get.WorldTime.TurnCounter = world.Map.LocalTime.TurnCounter;
            if (t % 5 != 0) continue;
            foreach (Actor actor in actors)
            {
                if (actor.IsDead) continue;
                actor.ActionPoints = Rules.BASE_ACTION_COST;
                int attempts = 0;
                while (actor.ActionPoints > 0 && attempts++ < 10)
                    Check.Equal(true, world.NpcTurn(actor), "long run NPC chooses a legal action");
                Check.Equal(true, attempts <= 10 && actor.ActionPoints <= 0, "preparation has a finite turn budget");
                int active = 0;
                foreach (NpcIntent goal in actor.Personality.Intents) if (!goal.Finished) active++;
                Check.Equal(true, active <= 4 && actor.Personality.Intents.Count <= 12, "goal budget remains bounded for weeks");
                Check.Equal(true, actor.Personality.Interests.Count <= 16, "lasting interests remain bounded for weeks");
                Check.Equal(true, actor.Personality.Knowledge.Facts.Count <= 48 && actor.Personality.Knowledge.Places.Count <= 16,
                    "belief stores remain bounded for weeks");
            }
            Check.Equal(true, Units(world) <= initialFood, "weeks of actions cannot manufacture food");
            foreach (NpcStory story in Session.Get.NpcDirector.Stories)
                Check.Equal(false, story.Parents.Contains(story.Id), "a story cannot parent itself");
            Check.Equal(true, Session.Get.NpcDirector.Stories.Count <= 64, "long story history remains bounded");
        }
        Check.Equal(14, world.Map.LocalTime.Day, "simulation crossed fourteen complete days");
        var kinds = new HashSet<string>();
        foreach (Actor actor in actors) foreach (ObservedEvent e in actor.Personality.Events) kinds.Add(e.Kind);
        Check.Equal(true, kinds.Count >= 3, "independent circumstances create varied recorded events");
    }
    static int Units(ScenarioWorld world)
    {
        int result = 0;
        foreach (Actor actor in world.Map.Actors) if (actor.Inventory != null) result += NpcIntentSupport.FoodUnits(actor);
        for (int x = 0; x < world.Map.Width; x++) for (int y = 0; y < world.Map.Height; y++)
        {
            Inventory pile = world.Map.GetItemsAt(new Point(x, y));
            if (pile == null) continue;
            foreach (Item item in pile.Items) if (item is ItemFood) result += item.Quantity;
        }
        return result;
    }
}
