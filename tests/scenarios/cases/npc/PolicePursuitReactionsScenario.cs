using System;
using System.Drawing;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.Personality;

static class PolicePursuitReactionsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/police-pursuit-reactions", () => TownScenarioFactory.Arena(5841,
            "........................................", "........................................", "........................................"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor source = PolicePursuitRumorScenario.Cop(world, "source", 1, false);
            Actor suspect = NpcIntentSupport.Actor(world, "bold suspect", 2, 1, "brave", "hotheaded", "rebellious");
            Actor stranger = NpcIntentSupport.Actor(world, "stranger", 5, 1, "timid");
            Actor patrol = PolicePursuitRumorScenario.Cop(world, "patrol", 25, true);
            Actor quiet = PolicePursuitRumorScenario.Cop(world, "quiet", 26, false);
            quiet.Personality.AddTrait(new TraitInstance("solitary"));
            Actor gossip = NpcIntentSupport.Actor(world, "gossip", 18, 1, "sociable");
            world.Game.DoMakeAggression(suspect, source);
            Check.Equal(null, NpcIntentSupport.Intent(suspect, "retaliate"),
                "bold suspect does not counterattack merely because the search started");
            for (int turn = 0; turn < 5 && !gossip.Personality.Knowledge.Facts.Any(f => f.Kind == "pursuit_announced"); turn++)
            {
                world.Map.LocalTime.TurnCounter = turn;
                Session.Get.WorldTime.TurnCounter = turn;
                NpcIntentSupport.Turn(world, patrol);
            }
            NpcFact fact = gossip.Personality.Knowledge.Facts.Single(f => f.Kind == "pursuit_announced");
            Check.Equal(null, NpcIntentSupport.Intent(suspect, "retaliate"),
                "the suspect still has no counterattack goal while out of hearing range");
            for (int turn = 6; turn < 9; turn++)
            {
                world.Map.LocalTime.TurnCounter = turn;
                Session.Get.WorldTime.TurnCounter = turn;
                NpcIntentSupport.Turn(world, quiet);
            }
            Check.Equal(false, quiet.Personality.Knowledge.Facts.Any(f => f.Kind == "pursuit_announced" &&
                f.SubjectId == quiet.PersonalityIdentity), "solitary officer pursues without making a public announcement");
            world.Map.RemoveActor(gossip); world.Place(gossip, 4, 1);
            gossip.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(true, world.Try(new ActionNpcTell(gossip, world.Game, suspect, fact)),
                "pursuit reaches the suspect in an ordinary retold rumor");
            Check.Equal(true, NpcIntentSupport.Intent(suspect, "retaliate") != null,
                "aggressive suspect chooses to strike back against the named pursuer");
            Check.Equal(patrol.PersonalityIdentity, NpcIntentSupport.Intent(suspect, "retaliate").TargetId,
                "counterattack targets the particular officer named in the rumor");
            Check.Equal(null, NpcIntentSupport.Intent(stranger, "avoid_reported_threat"),
                "unrelated timid listener does not respond as if personally pursued");
        });
    }
}
