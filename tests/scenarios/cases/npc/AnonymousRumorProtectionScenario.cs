using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class AnonymousRumorProtectionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/anonymous-rumor-protection", () => TownScenarioFactory.Arena(4913,
            "...#.........", "...#.........", "...#........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor victim = NpcIntentSupport.Actor(world, "victim", 1, 1);
            Actor attacker = NpcIntentSupport.Actor(world, "attacker", 2, 1);
            attacker.Faction = world.Game.GameFactions.TheBikers;
            Actor witness = NpcIntentSupport.Actor(world, "witness", 0, 1);
            witness.Personality.Opinion(victim.PersonalityIdentity, victim.UnmodifiedName);
            Actor leader = NpcIntentSupport.Actor(world, "leader", 6, 1, "loyal", "brave");
            Actor guard = NpcIntentSupport.Actor(world, "guard", 7, 1, "protective", "brave");
            leader.AddFollower(victim); leader.AddFollower(guard);
            // They met earlier, but the witness still cannot say which biker attacked.
            NpcKnownPerson knownAttacker = leader.Personality.Knowledge.See(attacker, -1);
            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", victim, attacker,
                world.Map, victim.Location.Position, 0));
            NpcFact anonymous = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "attack");
            Check.Equal("a biker", anonymous.ReportOther, "witness only identifies the attacker's faction");
            Check.Equal(true, NpcKnowledgeSystem.Hear(world.Game, leader, witness, anonymous),
                "leader hears the faction-only account");
            Check.Equal(0, knownAttacker.Danger, "rumor does not mark an acquaintance as the attacker");
            Check.Equal(null, NpcStorySystem.ProposeGroupPlan(world.Game, leader, new List<Actor> { guard, victim, attacker }),
                "group cannot target a specific known biker from an anonymous rumor");
        });
    }
}
