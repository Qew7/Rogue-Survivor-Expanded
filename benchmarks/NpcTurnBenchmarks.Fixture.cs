using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.AI;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.AI.Sensors;
using djack.RogueSurvivor.Gameplay.Generators;

static partial class NpcTurnBenchmarks
{
    static Sample RunOne(bool profile = false)
    {
        ScenarioWorld world = TownScenarioFactory.Create(7360, false);
        Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
        Session.Get.GamePreset.Skeletons = false;
        Session.Get.GamePreset.ZombieMasters = false;
        GameOptions options = RogueGame.Options;
        options.MaxCivilians = 75;
        options.MaxUndeads = 200;
        options.DayZeroUndeadsPercent = 100;
        OptionsField.SetValue(null, options);

        BaseTownGenerator.Parameters parameters = BaseTownGenerator.DEFAULT_PARAMS;
        parameters.MapWidth = parameters.MapHeight = 100;
        parameters.District = Session.Get.World[0, 0];
        parameters.GenerateHospital = false;
        parameters.GeneratePoliceStation = false;
        Map map = new StdTownGenerator(world.Game, parameters).Generate(7360);
        parameters.District.EntryMap = map;
        Session.Get.CurrentMap = map;
        world = new ScenarioWorld(7360, map, world.Game);

        Sample sample = new Sample();
        int civilians = 0, zombies = 0;
        foreach (Actor actor in map.Actors)
        {
            if (actor.Model.Abilities.IsUndead)
            {
                actor.Controller = new TimedZombieAI(sample.Zombies);
                zombies++;
            }
            else
            {
                actor.Controller = new TimedCivilianAI(sample.Civilians);
                civilians++;
            }
        }
        sample.InitialCivilians = civilians;
        sample.InitialZombies = zombies;
        if (civilians < 75 || zombies != 200)
            throw new InvalidOperationException("NPC fixture population changed: " + civilians + "/" + zombies);

        Actor player = new Actor(world.Game.GameActors.MaleCivilian,
            world.Game.GameFactions.TheCivilians, "observer", false, false, 0);
        player.Controller = new PlayerController();
        player.IsInvincible = true;
        for (int y = 0; y < map.Height && player.Location.Map == null; y++)
            for (int x = 0; x < map.Width && player.Location.Map == null; x++)
                if (map.IsWalkable(x, y) && map.GetActorAt(x, y) == null)
                    world.Place(player, x, y);
        world.SetPlayer(player);
        player.ActionPoints = 0;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        double profileStart = profile ? ProcessAgeSeconds() : 0;
        long runStart = Stopwatch.GetTimestamp();
        currentSample = sample;
        try { RunTurns(world, map, player, sample); }
        finally { currentSample = null; }
        sample.Total = Stopwatch.GetTimestamp() - runStart;
        if (profile)
            Console.WriteLine("PROFILE_WINDOW {0:F3} {1:F3}", profileStart, ProcessAgeSeconds());
        if (map.LocalTime.TurnCounter != Turns || sample.Civilians.Actions == 0 || sample.Zombies.Actions == 0)
            throw new InvalidOperationException("NPC fixture did not advance both actor types");
        return sample;
    }

    static double ProcessAgeSeconds()
    {
        return (DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void RunTurns(ScenarioWorld world, Map map, Actor player, Sample sample)
    {
        for (int turn = 0; turn < Turns; turn++)
        {
            long start = Stopwatch.GetTimestamp();
            NextMapTurn.Invoke(world.Game, new[] { (object)map, FullTurn });
            sample.MapTurn += Stopwatch.GetTimestamp() - start;
            Session.Get.WorldTime.TurnCounter = map.LocalTime.TurnCounter;
            player.ActionPoints = 0;
            sample.TurnsDone++;
            for (int step = 0; step < 5000; step++)
            {
                start = Stopwatch.GetTimestamp();
                Actor actor = world.Game.Rules.GetNextActorToAct(map, map.LocalTime.TurnCounter);
                sample.Dispatch += Stopwatch.GetTimestamp() - start;
                if (actor == null) break;
                if (actor.IsPlayer) { actor.ActionPoints = 0; continue; }
                Role role = actor.Model.Abilities.IsUndead ? sample.Zombies : sample.Civilians;
                currentRole = role;
                start = Stopwatch.GetTimestamp();
                ActorAction action = actor.Controller.GetAction(world.Game);
                role.Choose += Stopwatch.GetTimestamp() - start;
                currentRole = null;
                start = Stopwatch.GetTimestamp();
                bool legal = action != null && action.IsLegal();
                role.Legal += Stopwatch.GetTimestamp() - start;
                if (!legal) throw new InvalidOperationException("NPC chose an illegal action: " + actor.Name);
                start = Stopwatch.GetTimestamp();
                action.Perform();
                role.Perform += Stopwatch.GetTimestamp() - start;
                role.Actions++;
                if (step == 4999) throw new InvalidOperationException("NPC action loop did not finish");
            }
        }
    }
}
