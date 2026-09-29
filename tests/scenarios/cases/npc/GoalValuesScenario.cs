using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class GoalValuesScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/goal-values", () => TownScenarioFactory.Arena(4651, ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            Actor target = NpcIntentSupport.Actor(world, "target", 2, 1);
            Actor lawful = NpcIntentSupport.Actor(world, "lawful", 1, 1, "lawful");
            Actor timid = NpcIntentSupport.Actor(world, "timid", 3, 1, "timid");
            foreach (Actor observer in new[] { lawful, timid })
            {
                observer.Personality.Knowledge.See(target, 0);
                NpcKnownPerson state = observer.Personality.Knowledge.Person(target.PersonalityIdentity);
                state.Danger = state.Violation = state.ThreatConfidence = 100;
                NpcGoalGenerator.Refresh(world.Game, observer);
            }
            Check.Equal(NpcGoalValue.Justice, NpcIntentSystem.Select(lawful).Generated.Value, "lawful NPC values addressing wrongdoing");
            Check.Equal(NpcGoalValue.Safety, NpcIntentSystem.Select(timid).Generated.Value, "timid NPC values safety in identical known circumstances");
            Check.Equal(0, lawful.Personality.Events.Count, "the generator needs no scripted incident");
            int hp = target.HitPoints; NpcIntentSupport.Turn(world, lawful);
            Check.Equal(true, NpcIntentSupport.HasEvent(lawful, "confronted"), "justice goal performs a real warning");
            Check.Equal(hp, target.HitPoints, "communicating a boundary does not fabricate violence");
            int distance = world.Game.Rules.GridDistance(timid.Location.Position, target.Location.Position);
            NpcIntentSupport.Turn(world, timid);
            Check.Equal(true, world.Game.Rules.GridDistance(timid.Location.Position, target.Location.Position) > distance, "safety goal actually changes position");
        });
    }
}
