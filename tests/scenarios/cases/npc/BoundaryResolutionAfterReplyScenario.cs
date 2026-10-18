using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class BoundaryResolutionAfterReplyScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/boundary-resolution-after-reply", () => TownScenarioFactory.Arena(4821,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 8, 2);
            Actor warner = NpcIntentSupport.Actor(world, "warner", 1, 1, "hotheaded", "brave", "lawful");
            Actor offender = NpcIntentSupport.Actor(world, "offender", 2, 1, "kind", "lawful");
            NpcKnownPerson known = warner.Personality.Knowledge.See(offender, 0);
            known.Violation = known.ViolationConfidence = 100; known.ViolationCause = 13;

            NpcIntentSupport.Turn(world, warner);
            Check.Equal(true, NpcIntentSupport.HasEvent(offender, "confronted"), "warning is actually spoken");
            Check.Equal(100, known.Violation, "speech alone does not settle the violation");
            NpcIntentSupport.Turn(world, offender);
            Check.Equal(true, NpcIntentSupport.HasEvent(warner, "boundary_accepted"), "offender answers from its own traits");
            Check.Equal(100, known.Violation, "impatient warner does not accept the answer as enough");

            warner.Personality.AddTrait(new TraitInstance("kind"));
            warner.Personality.AddTrait(new TraitInstance("patient"));
            world.Map.LocalTime.TurnCounter = 190;
            NpcIntentSupport.Turn(world, warner);
            Check.Equal(100, known.Violation, "a repeated warning still awaits the answer");
            NpcIntentSupport.Turn(world, offender);
            Check.Equal(0, known.Violation, "patient compassionate warner accepts the answer");
            Check.Equal(true, known.AcknowledgedViolation != 0, "only resolution acknowledges the original misconduct");
        });
    }
}
