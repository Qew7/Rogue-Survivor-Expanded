using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityGroupScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-group", () => TownScenarioFactory.Arena(4522,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor leader = SkillScenario.Actor(world);
            world.Map.PlaceActorAt(leader, new Point(1, 1));
            world.SetPlayer(leader);
            Actor recruit = SkillScenario.Actor(world);
            recruit.Personality = new PersonalityState();
            recruit.Personality.AddTrait(new TraitInstance("generous"));
            recruit.Personality.AddTrait(new TraitInstance("skeptic"));
            world.Map.PlaceActorAt(recruit, new Point(2, 1));
            world.Game.SkillUpgrade(leader, Skills.IDs.LEADERSHIP);

            Check.Equal(true, world.Try(new ActionTakeLead(leader, world.Game, recruit)),
                "real recruitment action succeeds");
            Check.Same(leader, recruit.Leader, "recruit joins leader");
            Check.Equal(1, recruit.Personality.Memories.Count,
                "joining group creates a memory");
            Check.Equal("new_group", recruit.Personality.Memories[0].Id,
                "recruitment uses the group memory definition");

            leader.ActionPoints = Rules.BASE_ACTION_COST;
            world.Game.DoCancelLead(leader, recruit);
            Check.Equal(false, recruit.HasLeader, "recruit leaves group");
            Check.Equal(2, recruit.Personality.Memories.Count,
                "dismissal creates a separate abandonment memory");
            int due = 0;
            foreach (MemoryInstance memory in recruit.Personality.Memories)
                due = System.Math.Max(due, memory.ResolveTurn);
            world.Map.LocalTime.TurnCounter = due;
            PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(true, recruit.Personality.HasTrait("selfless"),
                "generous recruit resolves group experience into selflessness");
            Check.Equal(true, recruit.Personality.HasTrait("mistrustful"),
                "skeptical recruit resolves abandonment into mistrust");
            Check.Equal(0, recruit.Personality.Memories.Count,
                "both group memories are resolved");
        });
    }
}
