using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class GroupPlanParticipantExitScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/group-plan-participant-exit", () => TownScenarioFactory.Arena(4826,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 8, 2);
            Actor leader = NpcIntentSupport.Actor(world, "leader", 1, 1, "loyal", "protective");
            Actor helper = NpcIntentSupport.Actor(world, "helper", 2, 1, "loyal", "protective");
            Actor target = NpcIntentSupport.Actor(world, "target", 4, 1);
            leader.AddFollower(helper);
            var known = new NpcKnownPerson { Id = target.PersonalityIdentity, Name = target.UnmodifiedName, Place = target.Location };
            NpcIntent first = NpcStorySystem.StartKnown(world.Game.NpcContent, leader, known, world.Game.NpcContent.Capability("seek_companion"), storyId: "shared-exit");
            NpcIntent second = NpcStorySystem.StartKnown(world.Game.NpcContent, helper, known, world.Game.NpcContent.Capability("seek_companion"), storyId: "shared-exit");
            Check.Equal(true, first != null && second != null, "two real goals share one director episode");
            Location destination = new Location(world.Map, new Point(6, 1));
            var plan = new NpcGroupPlan { StoryId = "shared-exit", Kind = "group_shelter", Stage = "seeking", Destination = destination };
            leader.SocialGroup.Plan = plan;
            NpcIntentSystem.Finish(world.Game.NpcContent, helper, second, NpcIntentStatus.Abandoned, "motivation changed");
            Check.Equal(false, Session.Get.NpcDirector.Find(plan.StoryId).Finished,
                "one withdrawn participant does not end the shared episode");
            Check.Equal("seeking", plan.Stage, "the remaining participant keeps the active group plan");
            Check.Equal(destination, plan.Destination, "the shared destination survives a participant's withdrawal");
            NpcIntentSystem.Finish(world.Game.NpcContent, leader, first, NpcIntentStatus.Abandoned, "motivation changed");
            Check.Equal("failed", Session.Get.NpcDirector.Find(plan.StoryId).Stage,
                "director reaches a terminal result when every role has ended");
            Check.Equal("failed", plan.Stage, "the group plan follows the director's terminal result");
            Check.Equal(null, plan.Destination.Map, "only the terminal outcome releases the destination");
        });
    }
}
