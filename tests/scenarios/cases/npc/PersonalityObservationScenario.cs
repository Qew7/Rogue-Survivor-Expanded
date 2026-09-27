using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityObservationScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-observation", () => TownScenarioFactory.Arena(4519,
            "...#...", "...#...", "...#..."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor subject = SkillScenario.Actor(world);
            subject.Personality = new PersonalityState();
            subject.IsSleeping = true;
            world.Map.PlaceActorAt(subject, new Point(1, 1));
            Actor awake = SkillScenario.Actor(world);
            awake.Personality = new PersonalityState();
            world.Map.PlaceActorAt(awake, new Point(2, 1));
            Actor asleep = SkillScenario.Actor(world);
            asleep.Personality = new PersonalityState();
            asleep.IsSleeping = true;
            world.Map.PlaceActorAt(asleep, new Point(0, 1));
            Actor hidden = SkillScenario.Actor(world);
            hidden.Personality = new PersonalityState();
            world.Map.PlaceActorAt(hidden, new Point(5, 1));

            SignificantEvent eventAtSubject = new SignificantEvent("murder", subject, null,
                world.Map, subject.Location.Position, world.Map.LocalTime.TurnCounter);
            PersonalitySystem.Report(world.Game, eventAtSubject);
            Check.Equal(1, subject.Personality.Events.Count,
                "direct participant remembers an event even while asleep");
            Check.Equal(true, subject.Personality.Events[0].Direct, "direct role is recorded");
            Check.Equal(1, awake.Personality.Memories.Count,
                "awake visible witness gains a memory");
            Check.Equal(0, asleep.Personality.Events.Count,
                "sleeping bystander does not witness event");
            Check.Equal(0, hidden.Personality.Events.Count,
                "wall blocks witness journal as well as memory");

            GamePreset disabled = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            disabled.NpcPersonalitiesEnabled = false;
            Session.Get.GamePreset = disabled;
            PersonalitySystem.Report(world.Game, new SignificantEvent("raid", null, null,
                world.Map, subject.Location.Position, world.Map.LocalTime.TurnCounter + 1));
            Check.Equal(1, awake.Personality.Events.Count,
                "disabled system records no further observations");
            Check.Equal(1, awake.Personality.Memories.Count,
                "disabled system adds no memories");
            world.Map.LocalTime.TurnCounter = awake.Personality.Memories[0].ResolveTurn;
            PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(1, awake.Personality.Memories.Count,
                "disabled system pauses pending resolution");

            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(0, awake.Personality.Memories.Count,
                "pending memory resumes resolution when option is enabled again");
            for (int turn = 1; turn <= 40; turn++)
                PersonalitySystem.Report(world.Game, new SignificantEvent("raid", null, null,
                    world.Map, subject.Location.Position, turn));
            Check.Equal(32, awake.Personality.Events.Count,
                "only the latest significant observations are retained");
            Check.Equal(9, awake.Personality.Events[0].Turn,
                "oldest observations are evicted first");
            PersonalitySystem.Report(world.Game, new SignificantEvent("raid", null, null,
                world.Map, subject.Location.Position, 40));
            Check.Equal(32, awake.Personality.Events.Count,
                "reporting the same event twice does not grow the journal");
        });
    }
}
