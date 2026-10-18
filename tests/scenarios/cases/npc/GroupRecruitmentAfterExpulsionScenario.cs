using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.Personality;

static class GroupRecruitmentAfterExpulsionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/group-recruitment-after-expulsion", () => TownScenarioFactory.Arena(4817,
            "........", "........", "........"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 7, 2);
            Actor leader = NpcIntentSupport.Actor(world, "leader", 1, 1, "lawful", "brave");
            Actor recruit = NpcIntentSupport.Actor(world, "recruit", 2, 1);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 5, 1);
            world.Game.SkillUpgrade(leader, Skills.IDs.LEADERSHIP);
            world.Game.SkillUpgrade(recruit, Skills.IDs.LEADERSHIP);
            leader.AddFollower(recruit);
            PersonalitySystem.Report(world.Game, new SignificantEvent("base_theft", recruit, owner,
                world.Map, recruit.Location.Position, 0));

            NpcKnownPerson known = leader.Personality.Knowledge.Person(recruit.PersonalityIdentity);
            Check.Equal(100, known.Violation, "witnessed theft marks a known violation");
            Check.Equal(true, PersonalitySystem.Attitude(leader, recruit) <= -30,
                "serious trusted misconduct changes effective attitude");
            known.ViolationConfidence = 79;
            Check.Equal(true, PersonalitySystem.Attitude(leader, recruit) > -30,
                "uncertain misconduct does not block recruitment");
            known.ViolationConfidence = 100;

            NpcIntentSupport.Turn(world, leader);
            Check.Equal(null, recruit.Leader, "leader actually expels the thief");
            string reason;
            Check.Equal(false, world.Game.Rules.CanActorTakeLead(leader, recruit, out reason),
                "former leader refuses to recruit a known offender");
            Check.Equal("does not want this follower", reason, "recruiter's attitude causes refusal");
            Check.Equal(false, world.Game.Rules.CanActorTakeLead(recruit, leader, out reason),
                "offender cannot immediately reverse the group leadership");
            Check.Equal("does not trust this leader", reason, "former leader rejects the invitation");
            Check.Equal(false, world.Try(new ActionTakeLead(recruit, world.Game, leader)),
                "the real recruitment action cannot undo expulsion");

            recruit.Personality.AddTrait(new TraitInstance("lawful"));
            recruit.Personality.AddTrait(new TraitInstance("kind"));
            recruit.Personality.ChangeFrustration(world.Map.LocalTime.TurnCounter, -100);
            NpcIntentSupport.Turn(world, leader);
            Check.Equal(true, NpcIntentSupport.HasEvent(recruit, "confronted"), "leader gives an actual warning");
            Check.Equal(100, known.Violation, "warning alone does not resolve the concern");
            NpcIntentSupport.Turn(world, recruit);
            Check.Equal(true, NpcIntentSupport.HasEvent(leader, "boundary_accepted"), "former follower answers the warning");
            Check.Equal(0, known.Violation, "accepted boundary clears the known violation");
            Check.Equal(true, PersonalitySystem.Attitude(leader, recruit) > -30,
                "relationship can recover when the violation is cleared");
            Check.Equal(true, world.Try(new ActionTakeLead(leader, world.Game, recruit)),
                "ordinary recruitment remains available after the concern is addressed");
            Check.Same(leader, recruit.Leader, "the action restores actual group membership");
        });
    }
}
