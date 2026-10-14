using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class NpcPatienceDefiedWarningScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/patience-defied-warning", () => TownScenarioFactory.Arena(8198,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 8, 2);
            Actor warner = NpcIntentSupport.Actor(world, "warner", 1, 1, "hotheaded", "lawful");
            Actor defier = NpcIntentSupport.Actor(world, "defier", 2, 1, "rebellious");
            warner.Personality.Knowledge.See(defier, 0);
            NpcKnownPerson belief = warner.Personality.Knowledge.Person(defier.PersonalityIdentity);
            belief.Violation = belief.ViolationConfidence = 100;
            NpcIntentSupport.Turn(world, warner);
            NpcIntentSupport.Turn(world, defier);
            Check.Equal(true, NpcIntentSupport.HasEvent(warner, "boundary_defied"),
                "the warned NPC actually defies the warning");
            Check.Equal(true, warner.Personality.Reactions.Exists(r => r.Kind == "patience_escalated"),
                "defiance exhausts the warner's patience too");
            NpcIntentSupport.Turn(world, warner);
            Check.Equal(true, NpcIntentSupport.HasEvent(defier, "patience_escalated"),
                "the warner's reaction becomes a real, attributable action");
        });
    }
}
