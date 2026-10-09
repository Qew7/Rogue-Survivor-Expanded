using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Gameplay.AI.Sensors;

static partial class RestSimulationBenchmarks
{
    static double ProcessAgeSeconds()
    {
        return (DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds;
    }

    sealed class FovState
    {
        public Map Map;
        public Point Position;
        public int Range;
        public int Turn;
    }

    static void ProbeFovReuse()
    {
        var previous = new Dictionary<Actor, FovState>();
        int calls = 0, unchanged = 0, sameTurn = 0;
        LOSSensor.ProfileFovReuse = (actor, range) =>
        {
            calls++;
            FovState old;
            if (previous.TryGetValue(actor, out old) && old.Map == actor.Location.Map &&
                old.Position == actor.Location.Position && old.Range == range)
            {
                unchanged++;
                if (old.Turn == actor.Location.Map.LocalTime.TurnCounter) sameTurn++;
            }
            previous[actor] = new FovState { Map = actor.Location.Map,
                Position = actor.Location.Position, Range = range,
                Turn = actor.Location.Map.LocalTime.TurnCounter };
        };
        try { RunOne(false); }
        finally { LOSSensor.ProfileFovReuse = null; }
        Console.WriteLine("FOV reuse upper bound: {0}/{1} unchanged position/range, {2} within same turn",
            unchanged, calls, sameTurn);
    }
}
