using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;
static class ReconciliationScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/reconciliation", () => TownScenarioFactory.Arena(4804,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 6, 2);
            Actor apologizer = NpcIntentSupport.Actor(world, "apologizer", 1, 1, "kind", "lawful");
            Actor recipient = NpcIntentSupport.Actor(world, "recipient", 2, 1, "forgiving");
            recipient.Personality.Opinion(apologizer.PersonalityIdentity, apologizer.UnmodifiedName).AdjustSocial(grievance: 8);
            apologizer.Personality.Knowledge.See(recipient, 0);
            apologizer.Personality.RememberInterest(new NpcInterest { DefinitionId = "repair_trust", Subject = recipient.PersonalityIdentity,
                Name = recipient.UnmodifiedName, CreatedTurn = 0, LastEvidenceTurn = 0, ExpiresTurn = 720, Need = 1 });
            NpcIntentSupport.Turn(world, apologizer);
            Check.Equal(true, NpcIntentSupport.HasEvent(recipient, "apologized"), "lasting trust interest leads to a spoken apology");
            int prior = recipient.Personality.Person(apologizer.PersonalityIdentity).Grievance;
            NpcIntentSupport.Turn(world, recipient);
            Check.Equal(true, NpcIntentSupport.HasEvent(apologizer, "apology_accepted"), "recipient can accept from its own traits");
            Check.Equal(true, recipient.Personality.Person(apologizer.PersonalityIdentity).Grievance < prior,
                "an accepted reply reduces grievance after it is actually spoken");
        });
    }
}
