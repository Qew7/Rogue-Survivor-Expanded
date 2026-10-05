using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.AI;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.AI.Sensors;
using djack.RogueSurvivor.Gameplay.Generators;

static partial class NpcTurnBenchmarks
{
    sealed class TimedCivilianAI : CivilianAI
    {
        readonly Role role;
        public TimedCivilianAI(Role role) { this.role = role; }

        protected override List<Percept> UpdateSensors(RogueGame game)
        {
            long start = Stopwatch.GetTimestamp();
            List<Percept> result = base.UpdateSensors(game);
            role.Sense += Stopwatch.GetTimestamp() - start;
            return result;
        }

        protected override ActorAction SelectAction(RogueGame game, List<Percept> percepts)
        {
            long start = Stopwatch.GetTimestamp();
            ActorAction result = base.SelectAction(game, percepts);
            long elapsed = Stopwatch.GetTimestamp() - start;
            role.Decide += elapsed;
            role.AddAction(result, elapsed);
            return result;
        }
    }

    sealed class TimedZombieAI : ZombieAI
    {
        readonly Role role;
        public TimedZombieAI(Role role) { this.role = role; }

        protected override List<Percept> UpdateSensors(RogueGame game)
        {
            long start = Stopwatch.GetTimestamp();
            List<Percept> result = base.UpdateSensors(game);
            role.Sense += Stopwatch.GetTimestamp() - start;
            return result;
        }

        protected override ActorAction SelectAction(RogueGame game, List<Percept> percepts)
        {
            long start = Stopwatch.GetTimestamp();
            ActorAction result = base.SelectAction(game, percepts);
            long elapsed = Stopwatch.GetTimestamp() - start;
            role.Decide += elapsed;
            role.AddAction(result, elapsed);
            return result;
        }
    }
}
