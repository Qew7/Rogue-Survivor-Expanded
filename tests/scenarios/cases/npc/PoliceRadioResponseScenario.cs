using System;
using System.Drawing;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.Personality;

static class PoliceRadioResponseScenario
{
    static Actor Cop(ScenarioWorld world, string name, int x, bool radio)
    {
        Actor cop = new Actor(world.Game.GameActors.Policeman, world.Game.GameFactions.ThePolice,
            name, true, false, 0);
        cop.Controller = new CivilianAI(); cop.Personality = new PersonalityState();
        cop.FoodPoints = world.Game.Rules.ActorMaxFood(cop);
        cop.SleepPoints = world.Game.Rules.ActorMaxSleep(cop);
        cop.StaminaPoints = world.Game.Rules.ActorMaxSTA(cop);
        NpcIntentSupport.SetGoalRefreshSlot(cop, 0);
        if (radio) cop.Inventory.AddAll(new ItemTracker(world.Game.GameItems.POLICE_RADIO));
        world.Place(cop, x, 1); return cop;
    }

    public static void Register()
    {
        ScenarioRunner.Add("npc/police-radio-response", () => TownScenarioFactory.Arena(5835,
            "........................", "........................", "........................"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor source = Cop(world, "source", 1, false);
            Actor suspect = NpcIntentSupport.Actor(world, "Morgan", 2, 1);
            Actor patrol = Cop(world, "patrol", 20, true);
            Actor disconnected = Cop(world, "disconnected", 19, false);
            NpcKnownPerson oldSighting = patrol.Personality.Knowledge.See(suspect, -1);
            oldSighting.Place = new Location(world.Map, new Point(7, 1));

            world.Game.DoMakeAggression(suspect, source);
            NpcFact alert = patrol.Personality.Knowledge.Facts.Single(f => f.Kind == "police_alert");
            Check.Equal(suspect.PersonalityIdentity, alert.OtherId, "dispatch identifies the suspect");
            Check.Equal(suspect.UnmodifiedName, alert.ReportOther, "police can search for the named suspect");
            Check.Equal(suspect.Location.Position, alert.Place.Position, "dispatch carries the suspect's last position");
            Check.Equal(suspect.Location.Position,
                patrol.Personality.Knowledge.Person(suspect.PersonalityIdentity).Place.Position,
                "newer dispatch corrects an older sighting of the suspect");
            Check.Equal(false, disconnected.Personality.Knowledge.Facts.Any(f => f.Kind == "police_alert"),
                "patrol without a radio does not learn the dispatch");

            int startX = patrol.Location.Position.X;
            for (int turn = 0; turn < 6 && patrol.Location.Position.X == startX; turn++)
            {
                world.Map.LocalTime.TurnCounter = turn;
                Session.Get.WorldTime.TurnCounter = turn;
                NpcIntentSupport.Turn(world, patrol);
            }
            Check.Equal(true, patrol.Location.Position.X < startX,
                "production police AI moves toward the reported position before seeing the suspect");
            NpcIntent pursuit = NpcIntentSupport.Intent(patrol, ConflictResolutionModule.PolicePursuitId);
            Check.Equal(true, pursuit != null && pursuit.CauseId == alert.EventId,
                "police pursuit records the radio alert as its cause");
            Check.Equal(null, NpcIntentSupport.Intent(disconnected, ConflictResolutionModule.PolicePursuitId),
                "officer without the report has no radio-driven pursuit");

            patrol.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(true, world.Try(new ActionNpcTell(patrol, world.Game, disconnected, alert)),
                "a patrol can pass the dispatch to an officer without a radio");
            Check.Equal(alert.EventId, disconnected.Personality.Knowledge.Facts.Single(f => f.Kind == "police_alert").EventId,
                "the other officer receives the same report by word of mouth");
            int disconnectedX = disconnected.Location.Position.X;
            for (int turn = 10; turn < 16 && disconnected.Location.Position.X == disconnectedX; turn++)
            {
                world.Map.LocalTime.TurnCounter = turn;
                Session.Get.WorldTime.TurnCounter = turn;
                NpcIntentSupport.Turn(world, disconnected);
            }
            Check.Equal(true, disconnected.Location.Position.X < disconnectedX,
                "officer also pursues the suspect after hearing the rumor from a colleague");
            Check.Equal(alert.EventId, NpcIntentSupport.Intent(disconnected, ConflictResolutionModule.PolicePursuitId).CauseId,
                "retold dispatch remains the cause of the response");
        });
    }
}
