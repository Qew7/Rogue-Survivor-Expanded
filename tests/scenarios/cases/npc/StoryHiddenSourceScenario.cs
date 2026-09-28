using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class StoryHiddenSourceScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/story-hidden-source", () => TownScenarioFactory.Arena(4631, "...#...", "...#...", "...#..."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 6, 2);
            Actor witness = NpcIntentSupport.Actor(world, "witness", 0, 1, "lawful");
            Actor victim = NpcIntentSupport.Actor(world, "victim", 1, 1);
            Actor hidden = NpcIntentSupport.Actor(world, "hidden aggressor", 5, 1);
            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", victim, hidden, world.Map, victim.Location.Position, 0));
            NpcFact fact = witness.Personality.Knowledge.Facts[0];
            Check.Equal(victim.PersonalityIdentity, fact.SubjectId, "visible victim is identified");
            Check.Equal(Guid.Empty, fact.OtherId, "witness does not identify an unseen aggressor through event metadata");
            Check.Equal(null, witness.Personality.Knowledge.Person(hidden.PersonalityIdentity), "hidden location remains unknown");
            NpcIntentSupport.Turn(world, witness);
            Check.Equal(null, NpcIntentSupport.Intent(witness, "confront_reported_aggressor"), "lawful trait cannot accuse a globally known but personally unidentified actor");
        });
    }
}
