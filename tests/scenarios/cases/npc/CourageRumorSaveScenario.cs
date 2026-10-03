using System;
using System.IO;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.Personality;

static class CourageRumorSaveScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/courage-rumor-save", () => TownScenarioFactory.Arena(5802,
            ".........", ".........", ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor witness = NpcIntentSupport.Actor(world, "witness", 1, 2);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 3, 2, "timid");
            Actor zombie = new Actor(world.Game.GameActors.Zombie, world.Game.GameFactions.TheUndeads,
                "zombie", false, false, 0);
            world.Place(zombie, 4, 2);
            witness.Personality.Opinion(zombie.PersonalityIdentity, zombie.UnmodifiedName);
            owner.HitPoints = owner.StaminaPoints = 1;
            owner.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(true, world.NpcTurn(owner), "real NPC completes the frightened retreat");
            NpcFact fact = witness.Personality.Knowledge.Facts.First(f => f.Kind == "fled_in_fear");
            Check.Equal(true, fact.NamesOther, "identified threat can be repeated in a rumor");
            Actor listener = NpcIntentSupport.Actor(world, "listener", 0, 2);
            witness.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(true, world.Try(new ActionNpcTell(witness, world.Game, listener, fact)),
                "eyewitness tells the rumor through the real speech action");
            NpcKnownPerson known = listener.Personality.Knowledge.Person(zombie.PersonalityIdentity);
            Check.Equal(70, known.Danger, "rumor marks the actual threat as dangerous");
            Check.Equal(fact.EventId, known.ThreatCause, "danger retains its real evidence id");
            Check.Equal(false, NpcKnowledgeSystem.Hear(world.Game, listener, witness, fact), "repeat report has no second effect");

            int count = owner.Personality.Events.Count;
            var noMove = new ActionFearRetreat(owner, world.Game, new ActionWait(owner, world.Game), zombie);
            Check.Equal(true, noMove.IsLegal(), "failed retreat fixture action is legal");
            noMove.Perform();
            Check.Equal(count, owner.Personality.Events.Count, "waiting in place cannot create a second flight event");

            string path = Path.Combine(Path.GetTempPath(), "courage-rumor-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.Load<Session>(path);
                Map map = loaded.World[0, 0].EntryMap; map.ReconstructAuxiliaryFields();
                Actor savedOwner = NpcIntentSupport.Find(map, owner.PersonalityIdentity);
                Actor savedWitness = NpcIntentSupport.Find(map, witness.PersonalityIdentity);
                Actor savedListener = NpcIntentSupport.Find(map, listener.PersonalityIdentity);
                Check.Equal(true, savedOwner.Personality.Memories.Any(m => m.Id == "frightened_escape"), "pending fear survives save and load");
                Check.Equal(true, savedWitness.Personality.Knowledge.Facts.Any(f => f.Kind == "fled_in_fear" && f.EventId == fact.EventId),
                    "eyewitness fact survives save and load");
                Check.Equal(fact.EventId, savedListener.Personality.Knowledge.Person(zombie.PersonalityIdentity).ThreatCause,
                    "reported danger retains its source after load");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
