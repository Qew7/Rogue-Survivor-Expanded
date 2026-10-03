using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

static partial class NpcSafetyBenchmarks
{
    sealed class Result
    {
        public string Group, Name;
        public int Runs, Alive, Died, Escaped, Slept, TookFood, AteFood, SleepActions;
        public int FirstAway, Fled, CornerDeaths, NearEnemyWaits, TowardAfterAway, TotalLife;
        public int SleepNearEnemy, VoluntarySleepNearEnemy, Collapsed;
        public double ElapsedMs;
        public void Add(Result x)
        {
            Runs++; Alive += x.Alive; Died += x.Died; Escaped += x.Escaped;
            Slept += x.Slept; TookFood += x.TookFood; AteFood += x.AteFood; SleepActions += x.SleepActions;
            FirstAway += x.FirstAway; Fled += x.Fled; CornerDeaths += x.CornerDeaths;
            NearEnemyWaits += x.NearEnemyWaits; TowardAfterAway += x.TowardAfterAway; TotalLife += x.TotalLife;
            SleepNearEnemy += x.SleepNearEnemy; VoluntarySleepNearEnemy += x.VoluntarySleepNearEnemy;
            Collapsed += x.Collapsed;
        }
    }

    public static void Run()
    {
        TextWriter display = Console.Out;
        var cases = new[] {
            new { Group="route", Name="open", Layout="open", Danger=true, Exit=false, Inside=false, Food=0, FoodX=5, Sleep=0 },
            new { Group="route", Name="corridor", Layout="corridor", Danger=true, Exit=false, Inside=false, Food=0, FoodX=5, Sleep=0 },
            new { Group="route", Name="branch", Layout="branch", Danger=true, Exit=false, Inside=false, Food=0, FoodX=5, Sleep=0 },
            new { Group="route", Name="corridor_exit", Layout="corridor", Danger=true, Exit=true, Inside=false, Food=0, FoodX=5, Sleep=0 },
            new { Group="route", Name="branch_exit", Layout="branch", Danger=true, Exit=true, Inside=false, Food=0, FoodX=5, Sleep=0 },
            new { Group="route", Name="turn_exit_ordinary", Layout="turn_exit", Danger=true, Exit=true, Inside=false, Food=0, FoodX=5, Sleep=0 },
            new { Group="route", Name="turn_exit_organized", Layout="turn_exit", Danger=true, Exit=true, Inside=false, Food=0, FoodX=5, Sleep=0 },
            new { Group="route", Name="turn_exit_adaptable", Layout="turn_exit", Danger=true, Exit=true, Inside=false, Food=0, FoodX=5, Sleep=0 },
            new { Group="route", Name="turn_exit_impulsive", Layout="turn_exit", Danger=true, Exit=true, Inside=false, Food=0, FoodX=5, Sleep=0 },
            new { Group="route", Name="turn_no_exit_organized", Layout="turn_exit", Danger=true, Exit=false, Inside=false, Food=0, FoodX=5, Sleep=0 },
            new { Group="route", Name="long_exit_ordinary", Layout="long_turn_exit", Danger=true, Exit=true, Inside=false, Food=0, FoodX=5, Sleep=0 },
            new { Group="route", Name="long_exit_organized", Layout="long_turn_exit", Danger=true, Exit=true, Inside=false, Food=0, FoodX=5, Sleep=0 },
            new { Group="route", Name="long_exit_organized_hungry", Layout="long_turn_exit", Danger=true, Exit=true, Inside=false, Food=3, FoodX=5, Sleep=0 },
            new { Group="route", Name="long_exit_organized_sleepy", Layout="long_turn_exit", Danger=true, Exit=true, Inside=false, Food=0, FoodX=5, Sleep=2 },
            new { Group="route", Name="long_exit_organized_exhausted", Layout="long_turn_exit", Danger=true, Exit=true, Inside=false, Food=0, FoodX=5, Sleep=1 },
            new { Group="route", Name="long_exit_organized_tired", Layout="long_turn_exit", Danger=true, Exit=true, Inside=false, Food=0, FoodX=5, Sleep=0 },
            new { Group="route", Name="long_exit_organized_insane", Layout="long_turn_exit", Danger=true, Exit=true, Inside=false, Food=0, FoodX=5, Sleep=0 },
            new { Group="route", Name="long_exit_cautious", Layout="long_turn_exit", Danger=true, Exit=true, Inside=false, Food=0, FoodX=5, Sleep=0 },
            new { Group="route", Name="long_exit_adaptable", Layout="long_turn_exit", Danger=true, Exit=true, Inside=false, Food=0, FoodX=5, Sleep=0 },
            new { Group="food", Name="danger_no_food", Layout="open", Danger=true, Exit=false, Inside=false, Food=0, FoodX=5, Sleep=0 },
            new { Group="food", Name="danger_hungry_no_food", Layout="open", Danger=true, Exit=false, Inside=false, Food=3, FoodX=5, Sleep=0 },
            new { Group="food", Name="danger_sated_food_underfoot", Layout="open", Danger=true, Exit=false, Inside=false, Food=4, FoodX=5, Sleep=0 },
            new { Group="food", Name="danger_food_underfoot", Layout="open", Danger=true, Exit=false, Inside=false, Food=1, FoodX=5, Sleep=0 },
            new { Group="food", Name="danger_food_toward_enemy", Layout="open", Danger=true, Exit=false, Inside=false, Food=1, FoodX=6, Sleep=0 },
            new { Group="food", Name="danger_food_inventory", Layout="open", Danger=true, Exit=false, Inside=false, Food=2, FoodX=5, Sleep=0 },
            new { Group="food", Name="quiet_food_underfoot", Layout="open", Danger=false, Exit=false, Inside=false, Food=1, FoodX=5, Sleep=0 },
            new { Group="sleep", Name="danger_rested_inside", Layout="open", Danger=true, Exit=false, Inside=true, Food=0, FoodX=5, Sleep=0 },
            new { Group="sleep", Name="danger_exhausted_inside", Layout="open", Danger=true, Exit=false, Inside=true, Food=0, FoodX=5, Sleep=1 },
            new { Group="sleep", Name="quiet_exhausted_inside", Layout="open", Danger=false, Exit=false, Inside=true, Food=0, FoodX=5, Sleep=1 },
            new { Group="sleep", Name="quiet_exhausted_outside", Layout="open", Danger=false, Exit=false, Inside=false, Food=0, FoodX=5, Sleep=1 },
            new { Group="sleep", Name="hidden_enemy_exhausted_inside", Layout="corner", Danger=true, Exit=false, Inside=true, Food=0, FoodX=5, Sleep=1 },
        };
        var summaries = new List<Result>();
        Console.SetOut(TextWriter.Null);
        try
        {
            foreach (var test in cases)
            {
                var summary = new Result { Group = test.Group, Name = test.Name };
                Stopwatch timer = Stopwatch.StartNew();
                for (int seed = 7000; seed < 7100; seed++)
                    summary.Add(Run(test.Group, test.Name, seed, test.Layout, test.Danger,
                        test.Exit, test.Inside, test.Food, test.FoodX, test.Sleep));
                timer.Stop();
                summary.ElapsedMs = timer.Elapsed.TotalMilliseconds;
                summaries.Add(summary);
            }
        }
        finally { Console.SetOut(display); }
        Console.WriteLine("NPC safety experiment: 100 seeds per independent case; 30 danger turns / 20 quiet turns");
        Console.WriteLine("group case runs alive dead exit sleep took ate first-away fled corner-deaths near-waits toward sleep-near chosen-sleep-near collapse sleep-action avg-life ms-per-run");
        foreach (Result s in summaries)
            Console.WriteLine(s.Group + " " + s.Name + " " + s.Runs + " " + s.Alive + " " + s.Died + " " +
                s.Escaped + " " + s.Slept + " " + s.TookFood + " " + s.AteFood + " " + s.FirstAway + " " +
                s.Fled + " " + s.CornerDeaths + " " + s.NearEnemyWaits + " " + s.TowardAfterAway + " " +
                s.SleepNearEnemy + " " + s.VoluntarySleepNearEnemy + " " + s.Collapsed + " " + s.SleepActions + " " +
                ((double)s.TotalLife / s.Runs).ToString("F1", CultureInfo.InvariantCulture) + " " +
                (s.ElapsedMs / s.Runs).ToString("F2", CultureInfo.InvariantCulture));
    }
}
