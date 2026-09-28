using System;
using System.Collections.Generic;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;

static class NpcIntentSupport
{
    public static Actor Actor(ScenarioWorld world, string name, int x, int y, params string[] traits)
    {
        Actor actor = new Actor(world.Game.GameActors.MaleCivilian, world.Game.GameFactions.TheCivilians, name, true, false, 0);
        actor.Controller = new CivilianAI(); actor.Personality = new PersonalityState();
        foreach (string trait in traits) actor.Personality.AddTrait(new TraitInstance(trait));
        actor.FoodPoints = world.Game.Rules.ActorMaxFood(actor);
        actor.SleepPoints = world.Game.Rules.ActorMaxSleep(actor);
        actor.StaminaPoints = world.Game.Rules.ActorMaxSTA(actor);
        world.Place(actor, x, y); return actor;
    }
    public static Actor Player(ScenarioWorld world, int x, int y)
    {
        Actor actor = Actor(world, "player", x, y); actor.Controller = new PlayerController(); world.SetPlayer(actor); return actor;
    }
    public static ItemFood Food(ScenarioWorld world, Actor actor, int quantity)
    {
        ItemFood food = new ItemFood(world.Game.GameItems.CANNED_FOOD); food.Quantity = quantity;
        Check.Equal(true, actor.Inventory.AddAll(food), "fixture food fits"); return food;
    }
    public static int FoodUnits(Actor actor)
    {
        int units = 0; foreach (Item item in actor.Inventory.Items) if (item is ItemFood) units += item.Quantity; return units;
    }
    public static void Turn(ScenarioWorld world, Actor actor)
    {
        actor.ActionPoints = Rules.BASE_ACTION_COST;
        Check.Equal(true, world.NpcTurn(actor), "production controller performs a legal action");
        Check.Equal(0, actor.ActionPoints, "social action spends exactly one turn");
    }
    public static NpcIntent Intent(Actor actor, string id)
    { foreach (NpcIntent intent in actor.Personality.Intents) if (intent.DefinitionId == id) return intent; return null; }
    public static bool HasEvent(Actor actor, string kind)
    { foreach (ObservedEvent e in actor.Personality.Events) if (e.Kind == kind) return true; return false; }
    public static ScenarioWorld Restore(ScenarioWorld world, Session loaded)
    {
        typeof(Session).GetField("s_TheSession", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, loaded);
        typeof(RogueGame).GetField("m_Session", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(world.Game, loaded);
        typeof(RogueGame).GetField("m_Rules", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(world.Game, new Rules(loaded.GameDiceRoller));
        Map map = loaded.CurrentMap; map.ReconstructAuxiliaryFields();
        ScenarioWorld restored = new ScenarioWorld(world.Seed, map, world.Game);
        foreach (Actor actor in map.Actors) if (actor.IsPlayer) { restored.SetPlayer(actor); break; }
        return restored;
    }
    public static Actor Find(Map map, Guid id)
    { foreach (Actor actor in map.Actors) if (actor.PersonalityIdentity == id) return actor; throw new Exception("missing saved actor"); }
}
