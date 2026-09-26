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
            Actor npcFollower = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "npc follower", false, false, 0);
            Actor unrelatedCivilian = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "unrelated", false, false, 0);
            leader.Controller = new PlayerController();
            leader.AddFollower(follower);
            XpdBase playerBase = new XpdBase(leader, new[] { new Point(1, 1) });
            Check.Equal(true, playerBase.Owns(leader), "player is group leader");
            Check.Equal(true, playerBase.Owns(follower), "follower is part of player's group");
            Check.Equal(false, playerBase.Owns(otherCivilian),
                "unrelated civilian does not share player base");
            otherCivilian.AddFollower(npcFollower);
            XpdBase npcBase = new XpdBase(otherCivilian, new[] { new Point(2, 1) });
            Check.Equal(true, npcBase.Owns(otherCivilian), "NPC leader owns group base");
            Check.Equal(true, npcBase.Owns(npcFollower), "NPC follower owns group base");
            Check.Equal(false, npcBase.Owns(unrelatedCivilian),
                "same-faction outsider does not own NPC base");
            Check.Equal(false, npcBase.Owns(follower),
                "player follower does not join unrelated NPC group base");
            Actor undead = SkillScenario.Actor(world, true);
            Check.Equal(false, npcBase.Owns(undead), "undead cannot own living base");
        });
    }
}
