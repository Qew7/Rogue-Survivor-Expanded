using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Engine;
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
}
