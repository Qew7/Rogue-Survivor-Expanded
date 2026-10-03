using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;

static class ContestedSuccessionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/contested-succession", () => TownScenarioFactory.Arena(4694,
            "..........", "..........", ".........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 9, 2);
            Actor leader = NpcIntentSupport.Actor(world, "leader", 1, 1);
            Actor successor = NpcIntentSupport.Actor(world, "successor", 2, 1, "loyal", "organized");
            Actor supporter = NpcIntentSupport.Actor(world, "supporter", 3, 1, "loyal");
            Actor rival = NpcIntentSupport.Actor(world, "rival", 1, 2, "solitary");
            Actor ally = NpcIntentSupport.Actor(world, "ally", 2, 2, "solitary");
            leader.AddFollower(successor); leader.AddFollower(supporter); leader.AddFollower(rival); leader.AddFollower(ally);
            SocialGroup original = leader.SocialGroup;
            rival.SetTrustIn(successor, -100); ally.SetTrustIn(successor, -100);
            world.Game.KillActor(null, leader, "scenario", false);
            Check.Same(original, successor.SocialGroup, "successor retains the original group");
            Check.Same(successor, supporter.Leader, "supporter remains with successor");
            Check.Equal(false, original.Members.Contains(rival.PersonalityIdentity), "dissenter leaves old membership");
            Check.Equal(true, rival.SocialGroup != original && ally.SocialGroup == rival.SocialGroup,
                "dissenters form a separate group with its own identity");
            Check.Equal(true, NpcIntentSupport.HasEvent(supporter, "group_split"), "split is witnessed by the old group");
            Check.Equal(true, rival.Personality.Memories.Any(m => m.Id == "left_after_succession"),
                "rival retains a memory of the split");
            Check.Equal(true, ally.Personality.Memories.Any(m => m.Id == "left_after_succession"),
                "each dissenter retains a memory of leaving");
            NpcFact report = supporter.Personality.Knowledge.Facts.Find(f => f.Kind == "group_split");
            Check.Equal(true, report != null, "witness retains a reportable fact");
            Actor listener = NpcIntentSupport.Actor(world, "listener", 4, 1);
            Check.Equal(true, world.Try(new ActionNpcTell(supporter, world.Game, listener, report)), "witness can tell the split to someone else");
            Check.Equal(true, listener.Personality.Knowledge.Facts.Exists(f => f.Kind == "group_split"), "listener learns the split as hearsay");
        });
    }
}
