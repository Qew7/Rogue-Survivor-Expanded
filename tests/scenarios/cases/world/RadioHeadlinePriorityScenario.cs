using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class RadioHeadlinePriorityScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/radio-headline-priority", () => TownScenarioFactory.Arena(8197,
            "........", "........", "........"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.Seed = 8197;
            Actor requester = NpcIntentSupport.Actor(world, "requester", 2, 1);
            Actor helper = NpcIntentSupport.Actor(world, "helper", 3, 1);
            requester.Personality.Knowledge.Learn(new NpcFact {
                EventId = 901, Kind = "requested_medicine", StoryId = "medical-search",
                EventTurn = 0, LearnedTurn = 0, Place = requester.Location,
                Source = NpcKnowledgeSource.Told, Confidence = 80, Hops = 1,
                SubjectName = "Ada", OtherName = "Bo" });
            helper.Personality.Knowledge.Learn(new NpcFact {
                EventId = 902, Kind = "shared_food", StoryId = "helped-neighbor",
                EventTurn = 0, LearnedTurn = 0, Place = helper.Location,
                Source = NpcKnowledgeSource.Told, Confidence = 80, Hops = 1,
                SubjectName = "Cy", OtherName = "Dee" });
            Session.Get.WorldTime.TurnCounter = 25 * WorldTime.TURNS_PER_HOUR;
            RadioProgram varied = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 0, 25);
            Check.Equal(902L, varied.EventId,
                "the survivor station favors completed help over a routine medicine request");
            helper.IsDead = true;
            Session.Get.WorldTime.TurnCounter += WorldTime.TURNS_PER_HOUR;
            RadioProgram requestOnly = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 0, 26);
            Check.Equal(901L, requestOnly.EventId,
                "a medicine request remains reportable when it is the available news");
        });
    }
}
