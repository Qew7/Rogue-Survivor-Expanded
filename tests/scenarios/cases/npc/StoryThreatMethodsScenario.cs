using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class StoryThreatMethodsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/story-threat-methods", () => TownScenarioFactory.Arena(4621, ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 8, 2);
            Actor victim = NpcIntentSupport.Actor(world, "victim", 1, 0);
            Actor attacker = NpcIntentSupport.Actor(world, "attacker", 2, 0);
            Actor lawful = NpcIntentSupport.Actor(world, "lawful", 1, 1, "lawful");
            Actor timid = NpcIntentSupport.Actor(world, "timid", 2, 1, "timid");
            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", victim, attacker, world.Map, victim.Location.Position, 0));
            int hp = attacker.HitPoints;
            NpcIntentSupport.Turn(world, lawful);
            Check.Equal(NpcIntentStatus.Completed, NpcIntentSupport.Intent(lawful, "confront_reported_aggressor").Status, "lawful witness performs a warning");
            Check.Equal(true, NpcIntentSupport.HasEvent(lawful, "confronted"), "warning is an actual speech event");
            Check.Equal(hp, attacker.HitPoints, "an accusation does not fabricate a fight");
            world.Map.LocalTime.TurnCounter = 15;
            int distance = world.Game.Rules.GridDistance(timid.Location.Position, attacker.Location.Position);
            NpcIntentSupport.Turn(world, timid);
            Check.Equal(true, NpcIntentSupport.Intent(timid, "avoid_reported_threat") != null, "same fact produces a different trait-driven goal");
            Check.Equal(true, world.Game.Rules.GridDistance(timid.Location.Position, attacker.Location.Position) > distance, "timid actor actually retreats");
            Check.Equal(true, timid.Personality.Person(attacker.PersonalityIdentity).Fear > 0, "fear coexists with remembered feeling");
        });
    }
}
