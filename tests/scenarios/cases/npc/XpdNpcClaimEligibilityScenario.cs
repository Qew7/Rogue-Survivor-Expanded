using System.Drawing;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.AI;

static class XpdNpcClaimEligibilityScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("xpd/npc-claim-eligibility", () => TownScenarioFactory.Arena(4433,
            ".......", ".#####.", ".#...#.", ".#...#.", ".#...#.",
            ".#####.", "......."), world =>
        {
            Session.Get.GameMode = GameMode.GM_XPD;
            for (int y = 2; y <= 4; y++)
                for (int x = 2; x <= 4; x++) world.Map.GetTileAt(x, y).IsInside = true;
            Actor spectator = SkillScenario.Actor(world);
            spectator.Controller = new PlayerController();
            world.SetPlayer(spectator);
            Actor leader = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "leader", false, false, 0);
            leader.Controller = new CivilianAI();
            world.Map.PlaceActorAt(leader, new Point(3, 3));
            world.Map.LocalTime.TurnCounter = 24; // 24 + 3 + 3 is a claim turn.
            string reason;
            Check.Equal(false, world.Game.TryClaimXpdBase(leader, out reason),
                "solitary NPC cannot claim directly");
            MethodInfo handle = typeof(RogueGame).GetMethod("HandleAiActor",
                BindingFlags.Instance | BindingFlags.NonPublic);
            handle.Invoke(world.Game, new object[] { leader });
            Check.Equal(null, world.Map.XpdBaseAt(new Point(3, 3)),
                "solitary NPC skips automatic claim on eligible turn");

            world.Map.PlaceActorAt(leader, new Point(3, 3));
            Actor follower = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "follower", false, false, 0);
            follower.Controller = new CivilianAI();
            world.Map.PlaceActorAt(follower, new Point(4, 3));
            leader.AddFollower(follower);
            leader.ActionPoints = Rules.BASE_ACTION_COST;
            handle.Invoke(world.Game, new object[] { leader });
            XpdBase claim = world.Map.XpdBaseAt(new Point(3, 3));
            Check.Equal(true, claim != null, "NPC leader claims automatically with follower");
            Check.Same(leader, claim.GroupLeader, "claim is owned by leader");
            Check.Equal(true, claim.Owns(follower), "follower owns group base");
            Actor outsider = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "outsider", false, false, 0);
            Check.Equal(false, claim.Owns(outsider),
                "same faction without group membership does not own base");
            leader.RemoveFollower(follower);
            Check.Equal(false, claim.Owns(follower),
                "former follower loses access after leaving group");
            Check.Equal(true, claim.Owns(leader),
                "leader keeps existing claim after last follower leaves");
        });
    }
}
