using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;

static class RequestRepeatPolicyScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/request-repeat-policy", () => TownScenarioFactory.Arena(8206,
            ".........", ".........", "........."), world =>
        {
            Session original = Session.Get;
            original.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            var city = new World(1);
            city[0, 0] = world.Map.District;
            original.World = city;
            NpcIntentSupport.Player(world, 8, 2);
            Actor seeker = NpcIntentSupport.Actor(world, "seeker", 1, 1, "loyal", "protective");
            Actor missing = NpcIntentSupport.Actor(world, "missing", 5, 1);
            Actor listener = NpcIntentSupport.Actor(world, "listener", 2, 1);
            Actor other = NpcIntentSupport.Actor(world, "other", 0, 1);
            NpcIntent goal = NpcStorySystem.StartKnown(world.Game.NpcContent, seeker,
                new NpcKnownPerson { Id = missing.PersonalityIdentity, Name = missing.UnmodifiedName,
                    Place = new Location(world.Map, new Point(5, 1)) },
                world.Game.NpcContent.Capability("seek_companion"));
            Check.Equal(true, goal != null, "a real search goal starts");
            var ask = new ActionNpcAskLocation(seeker, world.Game, listener, goal);
            Check.Equal(true, ask.IsLegal(), "a nearby person can be asked");
            ask.Perform();
            Check.Equal(true, NpcIntentSupport.HasEvent(listener, "asked_location"),
                "the question is actually spoken and recorded");
            Check.Equal(false, ask.IsLegal(), "the same goal cannot ask this listener again");
            string speech = "Have you seen " + missing.UnmodifiedName + "?";
            NpcKnowledge knowledge = seeker.Personality.Knowledge;
            long newGoal = goal.Sequence + 1000;
            Check.Equal(false, knowledge.CanRequest(newGoal, listener.PersonalityIdentity, speech, 1),
                "recreating the goal cannot repeat the same words to the same listener");
            Check.Equal(true, knowledge.CanRequest(newGoal, other.PersonalityIdentity, speech, 1),
                "the same request can reach another person");
            Check.Equal(true, knowledge.CanRequest(newGoal, listener.PersonalityIdentity, "Where is the shelter?", 1),
                "a distinct request can reach the original listener");

            Guid seekerId = seeker.PersonalityIdentity, listenerId = listener.PersonalityIdentity;
            string path = Path.Combine(Path.GetTempPath(), "request-repeat-policy-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, original);
                Session loaded = BinarySaveStore.Load<Session>(path);
                Map map = loaded.World[0, 0].EntryMap;
                map.ReconstructAuxiliaryFields();
                Actor savedSeeker = NpcIntentSupport.Find(map, seekerId);
                Actor savedListener = NpcIntentSupport.Find(map, listenerId);
                NpcKnowledge saved = savedSeeker.Personality.Knowledge;
                map.LocalTime.TurnCounter = WorldTime.TURNS_PER_DAY - 1;
                Check.Equal(false, saved.CanRequest(newGoal, savedListener.PersonalityIdentity, speech,
                    map.LocalTime.TurnCounter), "the request remains blocked across save/load until the day ends");
                map.LocalTime.TurnCounter = WorldTime.TURNS_PER_DAY;
                Check.Equal(true, saved.CanRequest(newGoal, savedListener.PersonalityIdentity, speech,
                    map.LocalTime.TurnCounter), "a new goal may ask again at the one-day boundary");
                Check.Equal(false, saved.CanRequest(goal.Sequence, savedListener.PersonalityIdentity, speech,
                    map.LocalTime.TurnCounter), "the same completed goal stays blocked after a day");
            }
            finally
            {
                Session.Restore(original);
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
