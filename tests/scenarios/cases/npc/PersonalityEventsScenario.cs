using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityEventsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-events", () => TownScenarioFactory.Arena(4512,
            "...#...", "...#...", "...#..."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor victim = SkillScenario.Actor(world);
            world.Map.PlaceActorAt(victim, new Point(1, 1));
            Actor witness = SkillScenario.Actor(world);
            world.Map.PlaceActorAt(witness, new Point(2, 1));
            Actor hidden = SkillScenario.Actor(world);
            world.Map.PlaceActorAt(hidden, new Point(5, 1));
            Actor neutral = SkillScenario.Actor(world);
            world.Map.PlaceActorAt(neutral, new Point(0, 1));
            Actor evolving = SkillScenario.Actor(world);
            world.Map.PlaceActorAt(evolving, new Point(1, 0));
            witness.Personality = new PersonalityState();
            hidden.Personality = new PersonalityState();
            neutral.Personality = new PersonalityState();
            evolving.Personality = new PersonalityState();
            witness.Personality.AddTrait(new TraitInstance("lawful"));
            hidden.Personality.AddTrait(new TraitInstance("lawful"));

            SignificantEvent murder = new SignificantEvent("murder", victim, null,
                world.Map, victim.Location.Position, world.Map.LocalTime.TurnCounter);
            PersonalitySystem.Report(world.Game, murder);
            Check.Equal(1, witness.Personality.Memories.Count, "visible murder creates a memory");
            Check.Equal(0, hidden.Personality.Memories.Count, "wall blocks witnessing");
            Check.Equal(1, neutral.Personality.Memories.Count,
                "another visible witness also remembers the murder");
            Check.Equal(1, evolving.Personality.Memories.Count,
                "witness without a starting prerequisite still forms the memory");
            PersonalitySystem.Report(world.Game, murder);
            Check.Equal(1, witness.Personality.Memories.Count, "same event is not duplicated");

            evolving.Personality.AddTrait(new TraitInstance("lawful"));

            int due = System.Math.Max(witness.Personality.Memories[0].ResolveTurn,
                neutral.Personality.Memories[0].ResolveTurn);
            due = System.Math.Max(due, evolving.Personality.Memories[0].ResolveTurn);
            world.Map.LocalTime.TurnCounter = due;
            PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(true, witness.Personality.HasTrait("zealot"),
                "lawful witness resolves murder into advanced justice trait");
            Check.Equal(0, witness.Personality.Memories.Count, "resolved memory removed");
            Check.Equal(1, neutral.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.STRONG_PSYCHE),
                "witness without a matching trait gains the fallback skill");
            Check.Equal(true, evolving.Personality.HasTrait("zealot"),
                "trait gained while memory is pending changes its outcome");
        });
    }
}
