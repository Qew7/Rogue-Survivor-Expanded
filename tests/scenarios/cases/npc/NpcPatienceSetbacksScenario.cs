using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class NpcPatienceSetbacksScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/patience-setbacks", () => TownScenarioFactory.Arena(8197,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 8, 2);
            Actor trader = NpcIntentSupport.Actor(world, "trader", 1, 1, "kind");
            Actor refuser = NpcIntentSupport.Actor(world, "refuser", 2, 1);
            Actor witness = NpcIntentSupport.Actor(world, "witness", 3, 1);
            ((djack.RogueSurvivor.Gameplay.AI.CivilianAI)refuser.Controller).Directives.CanTrade = false;

            for (int turn = 0; turn < 2; turn++)
            {
                world.Map.LocalTime.TurnCounter = turn;
                world.Game.DoTrade(trader, refuser);
            }
            Check.Equal(24, trader.Personality.Frustration(1), "two real trade refusals build frustration");
            Check.Equal(0, trader.Personality.Reactions.Count, "a single setback does not cause an outburst");
            world.Map.LocalTime.TurnCounter = 251;
            Check.Equal(0, trader.Personality.Frustration(251), "time apart lets frustration fade");
            for (int turn = 251; turn < 255; turn++)
            {
                world.Map.LocalTime.TurnCounter = turn;
                world.Game.DoTrade(trader, refuser);
            }
            Check.Equal(true, trader.Personality.Reactions.Exists(r => r.Kind == "patience_escalated"),
                "repeated refusals make a kind, otherwise unprovoked NPC snap");
            Check.Equal(true, trader.Personality.Frustration(254) >= 45,
                "frustration remains until the outburst is actually performed");
            NpcIntentSupport.Turn(world, trader);
            Check.Equal(0, trader.Personality.Frustration(254), "the performed outburst releases frustration");
            Check.Equal(true, NpcIntentSupport.HasEvent(refuser, "patience_escalated") &&
                world.Game.Rules.AreEnemies(trader, refuser), "the outburst is a real attack");
            Check.Equal(true, HasMemory(trader, "lost_patience") && HasMemory(refuser, "received_outburst") &&
                HasMemory(witness, "witnessed_outburst"), "participants and witness remember the outburst");
            NpcFact fact = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "patience_escalated");
            Check.Equal(true, fact != null && fact.CauseId > 0 &&
                NpcConversation.Chapter(world.Game, witness, null, fact).Exists(f => f.EventId == fact.CauseId),
                "the outburst and its actual refusal reach the same rumor chapter");

            Actor patient = NpcIntentSupport.Actor(world, "patient", 4, 2, "patient");
            Actor patientRefuser = NpcIntentSupport.Actor(world, "patient refuser", 5, 2);
            ((djack.RogueSurvivor.Gameplay.AI.CivilianAI)patientRefuser.Controller).Directives.CanTrade = false;
            for (int turn = 255; turn < 259; turn++)
            {
                world.Map.LocalTime.TurnCounter = turn;
                world.Game.DoTrade(patient, patientRefuser);
            }
            Check.Equal(0, patient.Personality.Reactions.Count,
                "the same four refusals do not exhaust a patient NPC's tolerance");

            Actor timid = NpcIntentSupport.Actor(world, "timid", 5, 0, "timid");
            Actor blocker = NpcIntentSupport.Actor(world, "blocker", 6, 0);
            world.Map.LocalTime.TurnCounter = 259;
            NpcEvents.Publish(world.Game, "request_refused", blocker, timid);
            Check.Equal(16, timid.Personality.Frustration(259), "refused help irritates the requester");
            var agreement = new NpcServiceAgreement { Id = 8197, Provider = blocker.PersonalityIdentity,
                ProviderName = blocker.UnmodifiedName, Patient = timid.PersonalityIdentity,
                PatientName = timid.UnmodifiedName, Shelter = timid.Location, DueTurn = 259 };
            timid.Personality.RememberService(agreement);
            NpcServiceAgreement pending = timid.Personality.ServiceAgreements[0];
            timid.Personality.SetServiceStatus(pending, NpcServiceStatus.Accepted);
            NpcServices.Expire(world.Game, timid);
            Check.Equal(NpcServiceStatus.Failed, pending.Status, "the accepted agreement actually expires");
            Check.Equal(46, timid.Personality.Frustration(259), "the missed deadline adds to earlier frustration");
            Check.Equal(true, timid.Personality.Reactions.Exists(r => r.Kind == "patience_lost"),
                "a broken agreement after refused help causes a nonviolent outburst in a timid NPC");
            NpcIntentSupport.Turn(world, timid);
            Check.Equal(true, NpcIntentSupport.HasEvent(blocker, "patience_lost") &&
                !world.Game.Rules.AreEnemies(timid, blocker), "the timid NPC vents without starting combat");
            Check.Equal(true, HasMemory(timid, "lost_patience") && HasMemory(blocker, "received_outburst"),
                "a verbal outburst also creates resolvable memories");
        });
    }

    static bool HasMemory(Actor actor, string id)
    { foreach (MemoryInstance memory in actor.Personality.Memories) if (memory.Id == id) return true; return false; }
}
