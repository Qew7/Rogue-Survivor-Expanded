using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.AI;
using djack.RogueSurvivor.Gameplay.AI;

sealed class ChoiceProbeAI : CivilianAI
{
    public int? Pick(RogueGame game, List<int> values, Func<int, bool> valid,
        Func<int, float> score)
    {
        ChoiceEval<int> chosen = Choose(game, values, valid, score, (a, b) => a > b);
        return chosen == null ? (int?)null : chosen.Choice;
    }

    public string PickExtended(RogueGame game, List<int> values,
        Func<int, string> valid, Func<int, string, float> score)
    {
        ChoiceEval<string> chosen = ChooseExtended(game, values, valid, score,
            (a, b) => a > b);
        return chosen == null ? null : chosen.Choice;
    }

    public int Classify(RogueGame game, List<Percept> percepts)
    {
        List<Percept> sameMap = FilterSameMap(game, percepts);
        int count = 0;
        List<Percept> enemies = FilterEnemies(game, sameMap);
        List<Percept> friends = FilterNonEnemies(game, sameMap);
        List<Percept> stacks = FilterStacks(game, sameMap);
        List<Percept> corpses = FilterCorpses(game, sameMap);
        if (enemies != null) count += enemies.Count;
        if (friends != null) count += friends.Count;
        if (stacks != null) count += stacks.Count;
        if (corpses != null) count += corpses.Count;
        return count;
    }
}
