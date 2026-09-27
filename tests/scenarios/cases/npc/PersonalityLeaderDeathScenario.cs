using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class PersonalityLeaderDeathScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-leader-death", () => TownScenarioFactory.Arena(4515,
            "...#...", "...#...", "...#..."), world =>
        {
            Session.Get.GameMode = GameMode.GM_XPD;
            Session.Get.UniqueActors.TheSewersThing = new UniqueActor();
            Actor player = SkillScenario.Actor(world);
            world.SetPlayer(player);
            Actor leader = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "leader", false, false, 0);
            Actor follower = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "follower", false, false, 0);
            Actor hidden = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "hidden", false, false, 0);
            follower.Personality = new PersonalityState();
            hidden.Personality = new PersonalityState();
            world.Place(leader, 1, 1);
            world.Place(follower, 2, 1);
            world.Place(hidden, 5, 1);
            leader.AddFollower(follower);
            world.Map.AddXpdBase(new XpdBase(leader, new[] { new Point(1, 1), new Point(2, 1) }));

            world.Game.KillActor(null, leader, "scenario", false);
            Check.Equal(false, follower.HasLeader, "leader relationship ends after death");
            Check.Equal(null, world.Map.XpdBaseAt(new Point(1, 1)), "base is released");
            Check.Equal(2, follower.Personality.Memories.Count,
                "real death and base release create distinct memories for follower");
            Check.Equal(0, hidden.Personality.Memories.Count,
                "unrelated NPC behind wall learns neither event");
        });
    }
}
