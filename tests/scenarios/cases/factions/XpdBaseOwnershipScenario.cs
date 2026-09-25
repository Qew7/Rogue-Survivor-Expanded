using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class XpdBaseOwnershipScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("xpd/base-ownership", () => TownScenarioFactory.Arena(4404,
            ".....", ".....", "....."), world =>
        {
            Actor leader = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "leader", false, false, 0);
            Actor follower = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "follower", false, false, 0);
            Actor otherCivilian = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "other", false, false, 0);
            leader.Controller = new PlayerController();
            leader.AddFollower(follower);
            XpdBase playerBase = new XpdBase(leader, new[] { new Point(1, 1) });
            Check.Equal(true, playerBase.Owns(leader), "player is group leader");
            Check.Equal(true, playerBase.Owns(follower), "follower is part of player's group");
            Check.Equal(false, playerBase.Owns(otherCivilian),
                "unrelated civilian does not share player base");
            XpdBase factionBase = new XpdBase(otherCivilian, new[] { new Point(2, 1) });
            Check.Equal(true, factionBase.Owns(otherCivilian), "NPC claims for faction");
            Check.Equal(false, factionBase.Owns(follower),
                "player follower does not join unrelated NPC faction base");
            Actor undead = SkillScenario.Actor(world, true);
            Check.Equal(false, factionBase.Owns(undead), "undead cannot own living base");
        });
    }
}
