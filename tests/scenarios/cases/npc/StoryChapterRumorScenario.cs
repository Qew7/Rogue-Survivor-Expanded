using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class StoryChapterRumorScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/story-chapter-rumor", () => TownScenarioFactory.Arena(4635,
            "...#....", "...#....", "...#...."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = NpcIntentSupport.Player(world, 7, 2);
            Actor teller = NpcIntentSupport.Actor(world, "teller", 0, 1, "sociable");
            Actor victim = NpcIntentSupport.Actor(world, "victim", 1, 1);
            Actor attacker = NpcIntentSupport.Actor(world, "attacker", 2, 1);
            Actor listener = NpcIntentSupport.Actor(world, "listener", 6, 1);
            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", victim, attacker,
                world.Map, victim.Location.Position, 0, storyId: "chapter"));
            PersonalitySystem.Report(world.Game, new SignificantEvent("shared_food", victim, attacker,
                world.Map, victim.Location.Position, 1, storyId: "chapter"));
            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", victim, attacker,
                world.Map, victim.Location.Position, 2, storyId: "other"));
            NpcFact chosen = teller.Personality.Knowledge.Facts.First(f => f.StoryId == "chapter" && f.Kind == "shared_food");
            Check.Equal(0, listener.Personality.Knowledge.Facts.Count, "wall hides original events");
            world.Place(teller, 5, 1);
            NpcConversation.ShareRumor(world.Game, teller, listener, chosen, false);
            Check.Equal(2, listener.Personality.Knowledge.Facts.Count(f => f.StoryId == "chapter"),
                "listener learns both real facts from one speech");
            Check.Equal(0, listener.Personality.Knowledge.Facts.Count(f => f.StoryId == "other"),
                "unrelated report stays separate");
            Check.Equal(true, player.Personality.HeardJournal.Last().Text.Contains(" After that, "),
                "player hears a connected chapter rather than isolated fragments");
            Check.Equal(false, NpcConversation.EligibleListener(teller, listener, chosen),
                "chapter is not repeated at unchanged confidence");

            world.Place(teller, 0, 1);
            for (int turn = 3; turn < 15; turn++)
                PersonalitySystem.Report(world.Game, new SignificantEvent("shared_food", victim, attacker,
                    world.Map, victim.Location.Position, turn, storyId: "chapter"));
            world.Map.LocalTime.TurnCounter = 15;
            Session.Get.WorldTime.TurnCounter = 15;
            NpcFact latest = teller.Personality.Knowledge.Facts.Last(f => f.StoryId == "chapter");
            world.Place(teller, 5, 1);
            NpcConversation.ShareRumor(world.Game, teller, listener, latest, false);
            Check.Equal(12, listener.Personality.Knowledge.Facts.Count(f => f.StoryId == "chapter"),
                "one later speech carries ten recent linked facts");
            Check.Equal(true, listener.Personality.Knowledge.Facts.Any(f => f.EventId == latest.EventId),
                "chapter retains its chosen latest fact");
        });
    }
}
