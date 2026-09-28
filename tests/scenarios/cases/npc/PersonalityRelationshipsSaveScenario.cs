using System;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityRelationshipsSaveScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-relationships-save", () => TownScenarioFactory.Arena(4553,
            ".....", ".....", "....."), world =>
        {
            Session original = Session.Get;
            original.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor observer = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "observer", false, false, 0);
            observer.Personality = new PersonalityState();
            Actor helper = new Actor(world.Game.GameActors.FemaleCivilian,
                world.Game.GameFactions.TheBikers, "helper", false, false, 0);
            Actor leader = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheBikers, "leader", false, false, 0);
            world.Place(observer, 1, 1);
            world.Place(helper, 2, 1);
            world.Place(leader, 3, 1);
            leader.AddFollower(helper);
            Guid helperId = helper.PersonalityIdentity;
            Guid leaderId = leader.PersonalityIdentity;
            PersonalitySystem.Report(world.Game, new SignificantEvent("helped", observer, helper,
                world.Map, observer.Location.Position, world.Map.LocalTime.TurnCounter));
            Check.Equal(2, observer.Personality.Memories.Count, "help starts personal and faction memories");

            string path = Path.Combine(Path.GetTempPath(), "relations-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.Load<Session>(path);
                Map map = loaded.World[0, 0].EntryMap;
                map.ReconstructAuxiliaryFields();
                Actor savedObserver = map.GetActorAt(1, 1);
                Actor savedHelper = map.GetActorAt(2, 1);
                Check.Equal(helperId, savedHelper.PersonalityIdentity,
                    "personal identity survives save and load");
                RelationshipRecord person = savedObserver.Personality.Person(helperId);
                Check.Equal(true, person != null, "personal relationship survives save and load");
                Check.Equal(30, person.Feeling, "lasting attitude survives save and load");
                Check.Equal(2, savedObserver.Personality.Memories.Count,
                    "pending personal memory survives save and load");
                Check.Same(savedObserver.Personality.Memories[0], person.Memories[0],
                    "pending queue and relationship share one memory instance");
                Check.Equal(2, savedObserver.Personality.Faction(savedHelper.Faction.ID).Memories.Count,
                    "faction relationship and episode survive save and load");
                Check.Equal(2, savedObserver.Personality.Group(leaderId).Memories.Count,
                    "leader group relationship and episode survive save and load");
                Session.Restore(loaded);
                foreach (MemoryInstance pending in savedObserver.Personality.Memories)
                    map.LocalTime.TurnCounter = Math.Max(map.LocalTime.TurnCounter, pending.ResolveTurn);
                PersonalitySystem.ResolveDue(world.Game, map);
                Check.Equal(0, savedObserver.Personality.Memories.Count,
                    "saved pending memory resolves after load");
                Check.Equal(true, person.Memories[0].ResolvedTurn > 0,
                    "resolved memory stays in the relationship");
                Check.Equal("skill:MEDIC", person.Memories[0].OutcomeId,
                    "relationship records the outcome after load");
                BinarySaveStore.Save(path, Session.Get);
                Session reloaded = BinarySaveStore.Load<Session>(path);
                Map finalMap = reloaded.World[0, 0].EntryMap;
                finalMap.ReconstructAuxiliaryFields();
                Actor finalObserver = finalMap.GetActorAt(1, 1);
                Actor finalHelper = finalMap.GetActorAt(2, 1);
                RelationshipRecord finalPerson = finalObserver.Personality.Person(helperId);
                Check.Equal(2, finalPerson.Memories.Count,
                    "resolved personal memory survives a second save and load");
                Check.Equal("received_help", finalPerson.Memories[0].Id,
                    "resolved relationship retains event identity");
                Check.Equal("skill:MEDIC", finalPerson.Memories[0].OutcomeId,
                    "resolved relationship retains outcome");
                Session.Restore(reloaded);
                Check.Equal(57, PersonalitySystem.Attitude(finalObserver, finalHelper),
                    "loaded personal, group and faction relationship still changes behavior");
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
