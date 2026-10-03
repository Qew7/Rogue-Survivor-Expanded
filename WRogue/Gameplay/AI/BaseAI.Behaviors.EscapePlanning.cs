using System;
using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.AI;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Gameplay.AI
{
    abstract partial class BaseAI
    {
        struct EscapeStep
        {
            public Point Position, First;
            public int Depth;
            public EscapeStep(Point position, Point first, int depth)
            { Position = position; First = first; Depth = depth; }
        }

        // Planning aptitude changes how far an NPC considers visible escape routes.
        // Courage still decides whether to flee; it does not grant planning skill.
        int EscapePlanDepth(RogueGame game)
        {
            if (!NpcCourage.CanFear(m_Actor) || m_Actor.Personality == null ||
                !game.Session.GamePreset.NpcPersonalitiesEnabled) return 0;
            int depth = 0;
            if (PersonalitySystem.HasTrait(m_Actor, "organized") ||
                PersonalitySystem.HasTrait(m_Actor, "disciplined")) depth = 4;
            else if (PersonalitySystem.HasTrait(m_Actor, "vigilant") ||
                PersonalitySystem.HasTrait(m_Actor, "cautious") ||
                PersonalitySystem.HasTrait(m_Actor, "pragmatic")) depth = 3;
            else if (PersonalitySystem.HasTrait(m_Actor, "adaptable")) depth = 2;
            if (PersonalitySystem.HasTrait(m_Actor, "impulsive") ||
                PersonalitySystem.HasTrait(m_Actor, "careless") ||
                PersonalitySystem.HasTrait(m_Actor, "hotheaded")) depth = Math.Max(0, depth - 2);
            return depth;
        }

        ActorAction BehaviorPlannedEscape(RogueGame game, List<Percept> enemies, HashSet<Point> visible)
        {
            int depth = EscapePlanDepth(game);
            if (depth == 0 || !m_Actor.Model.Abilities.AI_CanUseAIExits) return null;
            Map map = m_Actor.Location.Map;
            Point origin = m_Actor.Location.Position;
            Exit here = map.GetExitAt(origin);
            if (UsableEscapeExit(here) && game.Rules.CanActorUseExit(m_Actor, origin))
                return new ActionUseExit(m_Actor, origin, game);
            // Plan only through terrain this actor can currently see. Ranged fire makes
            // a short walking route unsafe; use the usual combat decision instead.
            foreach (Percept percept in enemies)
            {
                Actor foe = percept.Percepted as Actor;
                if (foe == null || foe.Location.Map != map || percept.Turn != map.LocalTime.TurnCounter)
                    continue;
                ItemRangedWeapon weapon = foe.GetEquippedRangedWeapon();
                if (weapon != null && weapon.Ammo > 0)
                    return null;
            }
            if (visible == null)
                visible = LOS.ComputeFOVFor(game.Rules, m_Actor, map.LocalTime,
                    game.Session.World.Weather);
            var seen = new HashSet<Point> { origin };
            var queue = new Queue<EscapeStep>();
            queue.Enqueue(new EscapeStep(origin, origin, 0));
            while (queue.Count > 0)
            {
                EscapeStep step = queue.Dequeue();
                if (step.Depth >= depth) continue;
                foreach (Direction direction in Direction.COMPASS)
                {
                    Point next = step.Position + direction;
                    if (!visible.Contains(next) || !seen.Add(next) ||
                        !game.Rules.IsWalkableFor(m_Actor, map, next.X, next.Y) ||
                        IsAnyUnsafeDamagingTrapThere(game, map, next) ||
                        !EscapeStepSafe(game, map, next, enemies)) continue;
                    Point first = step.Depth == 0 ? next : step.First;
                    Exit exit = map.GetExitAt(next);
                    if (UsableEscapeExit(exit))
                    {
                        Direction firstDirection = Direction.FromVector(first.X - origin.X, first.Y - origin.Y);
                        ActorAction move = game.Rules.IsBumpableFor(m_Actor, game, m_Actor.Location + firstDirection);
                        return IsValidFleeingAction(move) ? new ActionBump(m_Actor, game, firstDirection) : null;
                    }
                    queue.Enqueue(new EscapeStep(next, first, step.Depth + 1));
                }
            }
            return null;
        }

        bool UsableEscapeExit(Exit exit)
        {
            if (exit == null || !exit.IsAnAIExit || exit.ToMap == null ||
                !exit.ToMap.IsInBounds(exit.ToPosition) ||
                !exit.ToMap.IsWalkable(exit.ToPosition.X, exit.ToPosition.Y) ||
                exit.ToMap.GetActorAt(exit.ToPosition) != null) return false;
            if (m_Actor.HasLeader && m_Actor.Leader.Location.Map != exit.ToMap) return false;
            return true;
        }

        bool EscapeStepSafe(RogueGame game, Map map, Point point, List<Percept> enemies)
        {
            foreach (Percept percept in enemies)
            {
                Actor foe = percept.Percepted as Actor;
                if (foe == null || foe.IsDead || foe.Location.Map != map ||
                    percept.Turn != map.LocalTime.TurnCounter) continue;
                if (game.Rules.GridDistance(point, foe.Location.Position) <= 1) return false;
            }
            return true;
        }
    }
}
