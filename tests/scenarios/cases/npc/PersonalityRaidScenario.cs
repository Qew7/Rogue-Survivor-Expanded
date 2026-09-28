using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityRaidScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-raid", () => TownScenarioFactory.Arena(4529,
            "...#...", "...#...", "...#..."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor witness = SkillScenario.Actor(world);
            witness.Personality = new PersonalityState();
            witness.Personality.AddTrait(new TraitInstance("vigilant"));
            world.Map.PlaceActorAt(witness, new Point(2, 1));
            Actor hidden = SkillScenario.Actor(world);
            hidden.Personality = new PersonalityState();
            world.Map.PlaceActorAt(hidden, new Point(5, 1));

            Check.Call(world.Game, "NotifyOrderablesAI",
                new[] { typeof(Map), typeof(RaidType), typeof(Point) },
                world.Map, RaidType.BIKERS, new Point(1, 1));
            Check.Equal(1, witness.Personality.Memories.Count,
                "real raid notification creates a memory for a visible survivor");
            Check.Equal("bikers_raid", witness.Personality.Memories[0].Id,
                "raid notification uses its memory definition");
            Check.Equal(0, hidden.Personality.Memories.Count,
                "raid behind a wall is not witnessed");
            world.Map.LocalTime.TurnCounter = witness.Personality.Memories[0].ResolveTurn;
            PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(true, witness.Personality.HasTrait("roadside_vigilance"),
                "vigilant survivor develops a distinct biker raid trait");
        });
    }
}
