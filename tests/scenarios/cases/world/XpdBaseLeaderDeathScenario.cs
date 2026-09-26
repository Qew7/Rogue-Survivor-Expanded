using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class XpdBaseLeaderDeathScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("xpd/base-leader-death", () => TownScenarioFactory.Arena(4414,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GameMode = GameMode.GM_XPD;
            Session.Get.UniqueActors.TheSewersThing = new UniqueActor();
            Actor player = SkillScenario.Actor(world);
            world.Place(player, 4, 1);
            world.SetPlayer(player);
            Actor leader = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "leader", false, false, 0);
            Actor follower = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "follower", false, false, 0);
            leader.Controller = new djack.RogueSurvivor.Gameplay.AI.CivilianAI();
            follower.Controller = new djack.RogueSurvivor.Gameplay.AI.CivilianAI();
            world.Map.PlaceActorAt(leader, new Point(1, 1));
            world.Map.PlaceActorAt(follower, new Point(2, 1));
            leader.AddFollower(follower);
            XpdBase claim = new XpdBase(leader, new[] { new Point(1, 1), new Point(2, 1) });
            world.Map.AddXpdBase(claim);
            Actor unrelated = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "unrelated", false, false, 0);
            Actor unrelatedFollower = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "other follower", false, false, 0);
            unrelated.AddFollower(unrelatedFollower);
            XpdBase otherBase = new XpdBase(unrelated, new[] { new Point(3, 1) });
            world.Map.AddXpdBase(otherBase);
            Check.Equal(true, claim.Owns(follower), "group owns base before leader death");

            world.Game.KillActor(null, leader, "scenario", false);
            Check.Equal(null, world.Map.XpdBaseAt(new Point(1, 1)),
                "leader death releases claimed territory");
            Check.Equal(true, world.Map.XpdBaseAt(new Point(3, 1)) == otherBase,
                "unrelated group base remains claimed");
            Check.Equal(false, follower.HasLeader, "follower is no longer led by dead actor");
            Actor newFollower = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "new follower", false, false, 0);
            follower.AddFollower(newFollower);
            world.Map.AddXpdBase(new XpdBase(follower, new[] { new Point(1, 1) }));
            Check.Equal(true, world.Map.XpdBaseAt(new Point(1, 1)) != null,
                "released territory can be claimed again");

            string path = Path.Combine(Path.GetTempPath(), "base-death-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, world.Map);
                Map loaded = (Map)BinarySaveStore.Load(path, null);
                loaded.ReconstructAuxiliaryFields();
                Check.Equal(2, CountBases(loaded), "only surviving claims remain after loading");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }

    static int CountBases(Map map)
    {
        int count = 0;
        foreach (XpdBase claim in map.XpdBases) count++;
        return count;
    }
}
