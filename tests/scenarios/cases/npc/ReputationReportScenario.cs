using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;

static class ReputationReportScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/reputation-report", () => TownScenarioFactory.Arena(4676, ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            Actor recipient = NpcIntentSupport.Actor(world, "recipient", 2, 1, "sociable", "trusting");
            Actor helper = NpcIntentSupport.Actor(world, "helper", 1, 1, "kind", "honest");
            recipient.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            NpcIntentSupport.Turn(world, recipient); NpcIntentSupport.Turn(world, helper);
            Actor listener = NpcIntentSupport.Actor(world, "listener", 2, 0, "loyal");
            listener.Personality.Knowledge.See(helper, 0);
            world.Map.LocalTime.TurnCounter = 180; NpcPromises.Expire(recipient);
            NpcFact fact = recipient.Personality.Knowledge.Facts.Find(f => f.Kind == "promise_broken");
            Check.Equal(false, NpcIntentSupport.HasEvent(listener, "promise_broken"), "private disappointment is not publicly broadcast");
            Check.Equal(true, world.Try(new ActionNpcTell(recipient, world.Game, listener, fact)), "recipient actually tells another person their assessment");
            Check.Equal(true, listener.Personality.Person(helper.PersonalityIdentity).Feeling < 0, "accepted report affects the helper's reputation");
            Check.Equal(0, listener.Personality.Knowledge.Person(helper.PersonalityIdentity).SeenTurn, "report of an overdue promise invents no recent sighting");
            Check.Equal(false, NpcIntentSupport.HasEvent(listener, "promise_broken"), "hearing a report does not fabricate direct participation");
            int feeling = listener.Personality.Person(helper.PersonalityIdentity).Feeling;
            Check.Equal(false, world.Try(new ActionNpcTell(recipient, world.Game, listener, fact)), "the same report is not repeated to one recipient");
            Check.Equal(feeling, listener.Personality.Person(helper.PersonalityIdentity).Feeling, "duplicate report cannot farm reputation penalties");
        });
    }
}
