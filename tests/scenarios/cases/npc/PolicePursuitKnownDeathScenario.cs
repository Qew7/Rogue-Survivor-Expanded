using System;
using System.Drawing;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.Personality;

static class PolicePursuitKnownDeathScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/police-pursuit-known-death", () => TownScenarioFactory.Arena(5842,
            "........................................", "........................................", "........................................"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor source = PolicePursuitRumorScenario.Cop(world, "source", 1, false);
            Actor suspect = NpcIntentSupport.Actor(world, "suspect", 2, 1);
            Actor patrol = PolicePursuitRumorScenario.Cop(world, "patrol", 25, true);
            NpcIntentSupport.Actor(world, "listener", 18, 1);
            world.Game.DoMakeAggression(suspect, source);
            patrol.Personality.Knowledge.Person(suspect.PersonalityIdentity).Dead = true;
            for (int turn = 0; turn < 3; turn++)
            {
                world.Map.LocalTime.TurnCounter = turn;
                Session.Get.WorldTime.TurnCounter = turn;
                NpcIntentSupport.Turn(world, patrol);
            }
            Check.Equal(null, NpcIntentSupport.Intent(patrol, ConflictResolutionModule.PolicePursuitId),
                "officer who knows the suspect died starts no pursuit");
            Check.Equal(false, patrol.Personality.Knowledge.Facts.Any(f => f.Kind == "pursuit_announced"),
                "no pursuit announcement is made about a known dead suspect");
        });
    }
}
