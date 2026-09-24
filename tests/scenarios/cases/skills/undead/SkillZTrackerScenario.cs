using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillZTrackerScenario
{
    public static void Register()
    {
        SkillScenario.Register("z-tracker", Skills.IDs.Z_TRACKER, (w, a) => w.Game.Rules.ActorSmell(a), true, false);
    }
}
