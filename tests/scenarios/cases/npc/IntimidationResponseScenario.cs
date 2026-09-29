using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;
static class IntimidationResponseScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/intimidation-response", () => TownScenarioFactory.Arena(4803,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 6, 2);
            Actor enforcer = NpcIntentSupport.Actor(world, "enforcer", 1, 1, "hotheaded", "vindictive");
            Actor witness = NpcIntentSupport.Actor(world, "witness", 2, 1, "fearful");
            enforcer.Personality.Knowledge.See(witness, 0);
            NpcKnownPerson known = enforcer.Personality.Knowledge.Person(witness.PersonalityIdentity);
            known.Violation = known.ViolationConfidence = 100; known.ViolationCause = 13;
            NpcIntentSupport.Turn(world, enforcer);
            Check.Equal(true, NpcIntentSupport.HasEvent(witness, "threatened"), "a forceful trait chooses an actual threat");
            Check.Equal(true, witness.Personality.Person(enforcer.PersonalityIdentity).Fear > 0, "fear is attributed to the person who spoke");
            NpcIntentSupport.Turn(world, witness);
            Check.Equal(true, NpcIntentSupport.HasEvent(enforcer, "threat_accepted"), "the frightened target replies from its own traits");
            Check.Equal(false, enforcer.Personality.Knowledge.Person(witness.PersonalityIdentity).Violation > 0,
                "observed submission changes the speaker's belief");
            int count = enforcer.Personality.Events.Count; NpcIntentSupport.Turn(world, enforcer);
            Check.Equal(true, enforcer.Personality.Events.Count >= count, "the interaction leaves an ordered record");
        });
    }
}
