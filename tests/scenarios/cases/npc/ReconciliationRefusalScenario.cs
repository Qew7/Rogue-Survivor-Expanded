using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;
static class ReconciliationRefusalScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/reconciliation-refusal", () => TownScenarioFactory.Arena(4815,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 6, 2);
            Actor apologizer = NpcIntentSupport.Actor(world, "apologizer", 1, 1, "kind", "lawful");
            Actor recipient = NpcIntentSupport.Actor(world, "recipient", 2, 1, "vindictive", "suspicious");
            recipient.Personality.Opinion(apologizer.PersonalityIdentity, apologizer.UnmodifiedName).AdjustSocial(grievance: 70);
            apologizer.Personality.Knowledge.See(recipient, 0);
            apologizer.Personality.RememberInterest(new NpcInterest { DefinitionId = "repair_trust", Subject = recipient.PersonalityIdentity,
                Name = recipient.UnmodifiedName, CreatedTurn = 0, LastEvidenceTurn = 0, ExpiresTurn = 720, Need = 1 });
            NpcIntentSupport.Turn(world, apologizer);
            int prior = recipient.Personality.Person(apologizer.PersonalityIdentity).Grievance;
            NpcIntentSupport.Turn(world, recipient);
            Check.Equal(true, NpcIntentSupport.HasEvent(apologizer, "apology_refused"), "deep grievance leads to an actual refusal");
            Check.Equal(prior, recipient.Personality.Person(apologizer.PersonalityIdentity).Grievance,
                "words alone do not erase a rejected grievance");
            Check.Equal(1, apologizer.Personality.Interest("repair_trust", recipient.PersonalityIdentity).Need,
                "unresolved interest remains after the refusal");
        });
    }
}
