using System;
using System.Collections.Generic;

static class AIChoiceScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/choice-selection", () => TownScenarioFactory.Arena(4533,
            "...", "...", "..."), world =>
        {
            ChoiceProbeAI ai = new ChoiceProbeAI();
            List<int> choices = new List<int> { 1, -1, 3, 4, 5 };
            int calls = 0;
            int? chosen = ai.Pick(world.Game, choices, value => value >= 0,
                value => { calls++; return value == 5 ? float.NaN : value; });
            Check.Equal(4, chosen, "highest legal finite score wins");
            Check.Equal(4, calls, "each legal choice evaluated once");
            Check.Equal(null, ai.Pick(world.Game, choices, value => false,
                value => value), "no legal choice");
            Check.Equal(null, ai.Pick(world.Game, new List<int>(), value => true,
                value => value), "empty choices");
            string extended = ai.PickExtended(world.Game, choices,
                value => value < 0 ? null : "option " + value,
                (value, data) => value == 5 ? float.NaN : value);
            Check.Equal("option 4", extended, "extended choice keeps associated data");
            int? tied = ai.Pick(world.Game, new List<int> { 1, 3, 4 },
                value => true, value => value >= 3 ? 10 : 0);
            Check.Equal(true, tied == 3 || tied == 4, "ties select only top candidates");
        });
    }
}
