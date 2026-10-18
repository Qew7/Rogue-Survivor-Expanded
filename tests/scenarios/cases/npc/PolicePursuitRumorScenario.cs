using System;
using System.Drawing;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.Personality;

static class PolicePursuitRumorScenario
{
    public static Actor Cop(ScenarioWorld world, string name, int x, bool sociable)
    {
        Actor cop = new Actor(world.Game.GameActors.Policeman, world.Game.GameFactions.ThePolice, name, true, false, 0);
        cop.Controller = new CivilianAI(); cop.Personality = new PersonalityState();
        if (sociable) cop.Personality.AddTrait(new TraitInstance("sociable"));
        cop.FoodPoints = world.Game.Rules.ActorMaxFood(cop);
        cop.SleepPoints = world.Game.Rules.ActorMaxSleep(cop);
        cop.StaminaPoints = world.Game.Rules.ActorMaxSTA(cop);
        cop.Inventory.AddAll(new ItemTracker(world.Game.GameItems.POLICE_RADIO));
        NpcIntentSupport.SetGoalRefreshSlot(cop, 0);
        world.Place(cop, x, 1); return cop;
    }

    public static void Register()
    {
        ScenarioRunner.Add("npc/police-pursuit-rumor", () => TownScenarioFactory.Arena(5840,
            "........................................", "........................................", "........................................"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor source = Cop(world, "source", 1, false);
            Actor suspect = NpcIntentSupport.Actor(world, "suspect", 2, 1, "timid");
            Actor follower = NpcIntentSupport.Actor(world, "follower", 3, 1, "brave", "loyal");
            suspect.AddFollower(follower);
            Actor patrol = Cop(world, "patrol", 25, true);
            Actor gossip = NpcIntentSupport.Actor(world, "gossip", 18, 1, "sociable");

            world.Game.DoMakeAggression(suspect, source);
            Check.Equal(false, suspect.Personality.Knowledge.Facts.Any(f => f.Kind == "pursuit_announced"),
                "starting the police search tells the suspect nothing about this pursuer");
            Check.Equal(null, NpcIntentSupport.Intent(suspect, "avoid_reported_threat"),
                "suspect has no pursuit response before hearing a name");
            Check.Equal(null, NpcIntentSupport.Intent(follower, "defend_person"),
                "follower has no pursuit response before hearing a name");
            for (int turn = 0; turn < 5 && !gossip.Personality.Knowledge.Facts.Any(f => f.Kind == "pursuit_announced"); turn++)
            {
                world.Map.LocalTime.TurnCounter = turn;
                Session.Get.WorldTime.TurnCounter = turn;
                NpcIntentSupport.Turn(world, patrol);
            }
            Check.Equal(true, NpcIntentSupport.Intent(patrol, ConflictResolutionModule.PolicePursuitId) != null,
                "patrol starts a real pursuit from its police radio report");
            Check.Equal(true, PersonalitySystem.Bias(patrol, NpcPersonalityAxis.Group) >= 15,
                "sociable patrol qualifies to announce");
            Check.Equal(true, patrol.Personality.Knowledge.Facts.Any(f => f.Kind == "police_alert"),
                "patrol retains the police alert");
            Check.Equal(true, patrol.Personality.Knowledge.Facts.Any(f => f.Kind == "pursuit_announced"),
                "sociable patrol publicly announces its pursuit");
            NpcFact announced = gossip.Personality.Knowledge.Facts.Single(f => f.Kind == "pursuit_announced");
            Check.Equal(patrol.PersonalityIdentity, announced.SubjectId, "spoken report identifies the actual pursuer");
            Check.Equal(suspect.PersonalityIdentity, announced.OtherId, "spoken report identifies the pursued person");
            Check.Equal(NpcKnowledgeSource.Told, announced.Source, "listener learned the pursuit by hearing the officer");
            Check.Equal(false, suspect.Personality.Knowledge.Facts.Any(f => f.Kind == "pursuit_announced"),
                "distant suspect does not learn it when the officer speaks elsewhere");
            Check.Equal(null, NpcIntentSupport.Intent(suspect, "avoid_reported_threat"),
                "officer's speech cannot change an uninformed suspect's goal");

            world.Map.RemoveActor(gossip); world.Place(gossip, 4, 1);
            gossip.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(true, world.Try(new ActionNpcTell(gossip, world.Game, suspect, announced)),
                "bystander retells the pursuit through ordinary rumor speech");
            Check.Equal(announced.EventId, suspect.Personality.Knowledge.Facts.Single(f => f.Kind == "pursuit_announced").EventId,
                "suspect first learns the same named pursuit from the retelling");
            Check.Equal(true, NpcIntentSupport.Intent(suspect, "avoid_reported_threat") != null,
                "timid suspect chooses to evade the named pursuer after hearing");
            Check.Equal(true, NpcIntentSupport.Intent(follower, "defend_person") != null,
                "brave loyal follower chooses to defend their leader after hearing");
            Check.Equal(announced.EventId, NpcIntentSupport.Intent(follower, "defend_person").CauseId,
                "follower's response is caused by the pursuit announcement");
            Check.Equal(false, NpcIntentSupport.HasEvent(suspect, "pursuit_announced"),
                "hearsay does not make the suspect an eyewitness to the announcement");

            int count = patrol.Personality.Knowledge.Facts.Count(f => f.Kind == "pursuit_announced");
            NpcIntentSupport.Turn(world, patrol);
            Check.Equal(count, patrol.Personality.Knowledge.Facts.Count(f => f.Kind == "pursuit_announced"),
                "same patrol does not announce the same pursuit twice");
        });
    }
}
