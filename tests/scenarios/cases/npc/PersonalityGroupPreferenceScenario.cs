using System;
using System.Drawing;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class PersonalityGroupPreferenceScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-group-preference", () => TownScenarioFactory.Arena(4530,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor leader = SkillScenario.Actor(world);
            world.Map.PlaceActorAt(leader, new Point(2, 1));
            world.SetPlayer(leader);
            Actor sociable = SkillScenario.Actor(world);
            sociable.Personality = new PersonalityState();
            sociable.Personality.AddTrait(new TraitInstance("sociable"));
            world.Map.PlaceActorAt(sociable, new Point(1, 1));
            Actor solitary = SkillScenario.Actor(world);
            solitary.Personality = new PersonalityState();
            solitary.Personality.AddTrait(new TraitInstance("solitary"));
            world.Map.PlaceActorAt(solitary, new Point(3, 1));
            leader.AddFollower(sociable);
            leader.AddFollower(solitary);
            sociable.TrustInLeader = 0;
            solitary.TrustInLeader = 0;

            Type flags = typeof(RogueGame).GetNestedType("SimFlags", BindingFlags.NonPublic);
            Check.Call(world.Game, "NextMapTurn", new[] { typeof(Map), flags },
                world.Map, Enum.Parse(flags, "NOT_SIMULATING"));
            Check.Equal(true, sociable.TrustInLeader > Rules.TRUST_BASE_INCREASE,
                "sociable follower bonds with the group faster on a real turn");
            Check.Equal(true, solitary.TrustInLeader < Rules.TRUST_BASE_INCREASE,
                "solitary follower resists the group on a real turn");
            Check.Equal(true, sociable.TrustInLeader > solitary.TrustInLeader,
                "followers with the same leader develop different trust");

            GamePreset disabled = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            disabled.NpcPersonalitiesEnabled = false;
            Session.Get.GamePreset = disabled;
            sociable.TrustInLeader = 0;
            solitary.TrustInLeader = 0;
            Check.Call(world.Game, "NextMapTurn", new[] { typeof(Map), flags },
                world.Map, Enum.Parse(flags, "NOT_SIMULATING"));
            Check.Equal(Rules.TRUST_BASE_INCREASE, sociable.TrustInLeader,
                "disabled personalities restore ordinary trust gain for sociable follower");
            Check.Equal(Rules.TRUST_BASE_INCREASE, solitary.TrustInLeader,
                "disabled personalities restore ordinary trust gain for solitary follower");
        });
    }
}
