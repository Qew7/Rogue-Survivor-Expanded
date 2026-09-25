using System;
using System.Drawing;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.AI;

sealed class RouteFinderProbe
{
    readonly object finder;
    readonly PropertyInfo allowedActions;
    readonly MethodInfo canReach;
    readonly Func<Point, Point, int> distance;
    readonly RogueGame game;
    readonly Actor actor;

    public RouteFinderProbe(RogueGame game, Actor actor)
    {
        this.game = game;
        this.actor = actor;
        CivilianAI ai = new CivilianAI();
        actor.Controller = ai;
        Type type = typeof(CivilianAI).Assembly.GetType(
            "djack.RogueSurvivor.Gameplay.AI.Tools.RouteFinder", true);
        finder = Activator.CreateInstance(type, new object[] { ai });
        allowedActions = type.GetProperty("AllowedActions");
        canReach = type.GetMethod("CanReachSimple");
        distance = game.Rules.GridDistance;
    }

    public bool CanReach(Point destination, int allowed = 0)
    {
        allowedActions.SetValue(finder, Enum.ToObject(allowedActions.PropertyType, allowed), null);
        Point start = actor.Location.Position;
        return (bool)canReach.Invoke(finder,
            new object[] { game, destination, distance(start, destination), distance });
    }
}
