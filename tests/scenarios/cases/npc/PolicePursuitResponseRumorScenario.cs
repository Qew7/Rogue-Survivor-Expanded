using System;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.Personality;

static class PolicePursuitResponseRumorScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/police-pursuit-response-rumor", () => TownScenarioFactory.Arena(5844,
            "........................................", "........................................", "........................................"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor source = PolicePursuitRumorScenario.Cop(world, "source", 1, false);
            Actor suspect = NpcIntentSupport.Actor(world, "suspect", 2, 1, "timid", "sociable");
            Actor patrol = PolicePursuitRumorScenario.Cop(world, "patrol", 25, true);
            Actor relay = NpcIntentSupport.Actor(world, "relay", 18, 1, "sociable");
            Actor listener = NpcIntentSupport.Actor(world, "listener", 12, 1, "sociable");
            Actor quiet = NpcIntentSupport.Actor(world, "quiet", 30, 1, "solitary");
            world.Game.DoMakeAggression(suspect, source);
            world.Map.RemoveActor(source); world.Place(source, 35, 2);
            Check.Equal(null, ConflictResolutionModule.AnnouncePursuitResponse(world.Game, suspect,
                new[] { listener }), "an uninformed suspect cannot announce a response");
            for (int turn = 0; turn < 5 && !relay.Personality.Knowledge.Facts.Any(f => f.Kind == "pursuit_announced"); turn++)
            {
                world.Map.LocalTime.TurnCounter = turn;
                Session.Get.WorldTime.TurnCounter = turn;
                NpcIntentSupport.Turn(world, patrol);
            }
            NpcFact pursuit = relay.Personality.Knowledge.Facts.Single(f => f.Kind == "pursuit_announced");
            world.Map.RemoveActor(relay); world.Place(relay, 4, 1);
            relay.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(true, world.Try(new ActionNpcTell(relay, world.Game, suspect, pursuit)),
                "suspect learns the named pursuit by ordinary speech");
            Check.Equal(true, NpcIntentSupport.Intent(suspect, "avoid_reported_threat") != null,
                "hearing the pursuit creates a real evasion response");
            Check.Equal(false, suspect.Personality.Knowledge.Facts.Any(f => f.Kind == "pursuit_response_announced"),
                "learning the pursuit alone does not announce the suspect's response");
            world.Map.RemoveActor(listener); world.Place(listener, 5, 1);
            NpcIntentSupport.Turn(world, suspect);
            NpcFact response = listener.Personality.Knowledge.Facts.Single(f => f.Kind == "pursuit_response_announced");
            Check.Equal(pursuit.EventId, response.CauseId, "response cites the pursuit actually heard");
            Check.Equal(suspect.PersonalityIdentity, response.SubjectId, "response names the pursued speaker");
            Check.Equal(patrol.PersonalityIdentity, response.OtherId, "response names the known pursuer");
            Check.Equal("avoid_reported_threat", response.Resource, "reported plan is the actual evasion goal");
            Check.Equal(NpcKnowledgeSource.Told, response.Source, "listener learns the response through speech");
            Check.Equal(false, quiet.Personality.Knowledge.Facts.Any(f => f.Kind == "pursuit_response_announced"),
                "distant NPC does not learn the response");
            NpcIntentSupport.Turn(world, suspect);
            Check.Equal(1, suspect.Personality.Knowledge.Facts.Count(f => f.Kind == "pursuit_response_announced"),
                "the same reaction is not announced twice");
            world.Map.RemoveActor(listener); world.Place(listener, 12, 1);
            world.Map.RemoveActor(quiet); world.Place(quiet, 13, 1);
            listener.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(true, world.Try(new ActionNpcTell(listener, world.Game, quiet, response)),
                "ordinary listeners can retell the suspect's response");
            Check.Equal(response.EventId, quiet.Personality.Knowledge.Facts.Single(f =>
                f.Kind == "pursuit_response_announced").EventId, "retelling preserves the same response event");
        });
    }
}
