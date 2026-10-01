using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityOwnGroupHistoryScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-own-group-history", () => TownScenarioFactory.Arena(4580,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_XPD);
            Actor leader = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "leader", true, false, 0);
            Actor follower = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "follower", true, false, 0);
            Actor unrelated = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "unrelated", true, false, 0);
            foreach (Actor actor in new[] { leader, follower, unrelated }) actor.Personality = new PersonalityState();
            world.Place(leader, 1, 1);
            world.Place(follower, 2, 1);
            world.Place(unrelated, 3, 1);
            leader.AddFollower(follower);
            world.Map.AddXpdBase(new XpdBase(leader, new[] { new Point(1, 1) }));
            Check.Call(world.Game, "ReleaseGroupBases", new[] { typeof(Actor), typeof(XpdBase) }, leader, null);
            Check.Equal(null, world.Map.XpdBaseAt(new Point(1, 1)), "real action releases the group's base");
            Check.Equal(null, leader.Personality.Person(leader.PersonalityIdentity),
                "leader has no personal relationship with themselves");
            RelationshipRecord own = leader.Personality.Group(leader.PersonalityIdentity);
            Check.Equal("base_loss", own.Memories[0].Id, "leader retains their own group's loss history");
            Check.Equal(0, own.Feeling, "base loss does not change the group's reputation");
            Check.Equal(0, PersonalitySystem.Attitude(leader, follower), "history alone creates no hostility toward followers");
            Check.Equal(1, follower.Personality.Group(leader.PersonalityIdentity).Memories.Count,
                "follower retains the same group event in their own history");
            Check.Equal(0, unrelated.Personality.Memories.Count, "outside witness does not acquire another group's base loss");
            world.Map.LocalTime.TurnCounter = Math.Max(leader.Personality.Memories[0].ResolveTurn,
                follower.Personality.Memories[0].ResolveTurn);
            PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(0, leader.Personality.Memories.Count, "leader resolves their loss");
            Check.Equal(1, own.Memories.Count, "resolved group loss remains in history");
            string path = Path.Combine(Path.GetTempPath(), "own-group-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, world.Map);
                Map loaded = BinarySaveStore.LoadExact<Map>(path);
                loaded.ReconstructAuxiliaryFields();
                Actor savedLeader = loaded.GetActorAt(1, 1);
                RelationshipRecord saved = savedLeader.Personality.Group(leader.PersonalityIdentity);
                Check.Equal(1, saved.Memories.Count, "own group history survives save/load");
                Check.Equal(true, saved.Memories[0].ResolvedTurn >= 0, "saved group memory keeps its resolution");
                Check.Equal(null, savedLeader.Personality.Person(leader.PersonalityIdentity),
                    "loading does not introduce a personal self relationship");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
