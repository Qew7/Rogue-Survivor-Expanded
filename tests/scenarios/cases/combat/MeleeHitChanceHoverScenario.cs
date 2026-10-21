using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class MeleeHitChanceHoverScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("combat/melee-hit-chance-hover", () => TownScenarioFactory.Arena(4782,
            ".....", ".....", "....."), world =>
        {
            Actor player = SkillScenario.Actor(world);
            world.Map.PlaceActorAt(player, new Point(1, 1));
            world.SetPlayer(player);
            Actor enemy = SkillScenario.Actor(world, true);
            world.Map.PlaceActorAt(enemy, new Point(2, 1));
            Actor neutral = SkillScenario.Actor(world);
            world.Map.PlaceActorAt(neutral, new Point(3, 1));

            Check.Equal(0, world.Game.Rules.ComputeChancesHit(0, 0), "equal zero rolls never hit");
            Check.Equal(25, world.Game.Rules.ComputeChancesHit(1, 0), "two-die roll matches exact chance");
            Check.Equal(18, world.Game.Rules.ComputeChancesHit(1, 1), "ties miss");

            string[] hidden = (string[])Check.Call(world.Game, "DescribeActor", enemy);
            Check.Equal(false, HasChance(hidden), "hint is off by default");
            GameOptions options = RogueGame.Options;
            options.ShowMeleeHitChances = true;
            typeof(RogueGame).GetField("s_Options", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, options);
            string[] shown = (string[])Check.Call(world.Game, "DescribeActor", enemy);
            string playerChance = "Your melee hit : " + world.Game.Rules.ComputeChancesMeleeHit(player, enemy) + "%";
            string enemyChance = "Their melee hit : " + world.Game.Rules.ComputeChancesMeleeHit(enemy, player) + "%";
            Check.Equal(true, Array.IndexOf(shown, playerChance) >= 0, "hover shows player's chance");
            Check.Equal(true, Array.IndexOf(shown, enemyChance) >= 0, "hover shows enemy's chance");
            Check.Equal(false, HasChance((string[])Check.Call(world.Game, "DescribeActor", neutral)),
                "neutral actor has no combat hint");

            string path = Path.Combine(Path.GetTempPath(), "melee-hint-" + Guid.NewGuid().ToString("N"));
            try
            {
                GameOptions.Save(options, path);
                Check.Equal(true, GameOptions.Load(path).ShowMeleeHitChances,
                    "hint toggle survives settings save and load");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }

    static bool HasChance(string[] lines)
    {
        foreach (string line in lines)
            if (line.StartsWith("Your melee hit : ")) return true;
        return false;
    }
}
