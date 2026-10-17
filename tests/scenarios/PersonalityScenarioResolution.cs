using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityScenarioResolution
{
    public static void PreferFirstOutcome(RogueGame game, Map map)
    {
        // The first four rolls from this seed are zero modulo 2, 3 and 4.
        // Focused legacy scenarios exercise a named trait or skill outcome;
        // npc/disposition-memory checks the normal random selection separately.
        DiceRoller.WithOverride(new DiceRoller(3699), () => PersonalitySystem.ResolveDue(game, map));
    }
}
