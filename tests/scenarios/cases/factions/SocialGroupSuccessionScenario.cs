using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class SocialGroupSuccessionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("factions/social-group-succession", () => TownScenarioFactory.Arena(4624, "......", "......", "......"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.UniqueActors.TheSewersThing = new UniqueActor();
            Actor observer = NpcIntentSupport.Player(world, 0, 2);
            Actor leader = NpcIntentSupport.Actor(world, "leader", 1, 1);
            Actor successor = NpcIntentSupport.Actor(world, "successor", 2, 1, "loyal", "organized");
            Actor member = NpcIntentSupport.Actor(world, "member", 3, 1, "loyal");
            leader.AddFollower(successor); leader.AddFollower(member);
            SocialGroup group = leader.SocialGroup;
            observer.Personality.RememberGroup(group.Identity, leader.UnmodifiedName, new MemoryInstance("received_help", 0, 10, "leader"), 15);
            world.Game.KillActor(null, leader, "scenario", false);
            Check.Same(successor, member.Leader, "actual leader death selects an eligible successor");
            Check.Same(group, successor.SocialGroup, "shared group object survives succession");
            Check.Same(group, member.SocialGroup, "members share the original group");
            Check.Equal(successor.PersonalityIdentity, group.LeaderId, "display leader changes separately from identity");
            Check.Equal(false, group.Identity == successor.PersonalityIdentity, "identity does not become the new leader ID");
            Check.Equal(15, PersonalitySystem.Attitude(observer, member), "existing group relationship follows the surviving group");
            Check.Equal(false, group.Members.Contains(leader.PersonalityIdentity), "dead leader leaves membership");
            bool rejected = false;
            try { successor.TransferSocialGroupTo(observer); } catch (ArgumentException) { rejected = true; }
            Check.Equal(true, rejected, "nonmember cannot take over group state");
            successor.RemoveFollower(member);
            Check.Equal(null, member.SocialGroup, "departing NPC loses current membership");
            Check.Equal(true, observer.Personality.Group(group.Identity) != null, "relationship to former group remains");
            successor.AddFollower(member); member.AddFollower(observer);
            Check.Same(successor.SocialGroup, observer.SocialGroup, "nested followers share the surviving group's identity");
            bool cycle = false;
            try { observer.AddFollower(successor); } catch (ArgumentException) { cycle = true; }
            Check.Equal(true, cycle, "membership cannot form a cycle");
            successor.RemoveFollower(member);
            Check.Equal(false, member.SocialGroup.Identity == group.Identity, "departing household forms a distinct group");
            Check.Same(member.SocialGroup, observer.SocialGroup, "descendants follow the household's new identity");
        });
    }
}
