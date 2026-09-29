using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class GoalBoundariesScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/goal-boundaries", () => TownScenarioFactory.Arena(4656, "...#...", "...#...", "...#..."), world =>
        {
            GamePreset preset = GamePreset.BuiltIn(GameMode.GM_STANDARD); preset.NpcPersonalitiesEnabled = false; Session.Get.GamePreset = preset;
            NpcIntentSupport.Player(world, 2, 2); Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1, "kind");
            owner.FoodPoints = Session.Get.GamePreset.HungerPoints - 1; NpcGoalGenerator.Refresh(world.Game, owner);
            Check.Equal(0, owner.Personality.Intents.Count, "disabled preset creates no state goals");
            Session.Get.GamePreset.NpcPersonalitiesEnabled = true; owner.IsSleeping = true; NpcGoalGenerator.Refresh(world.Game, owner);
            Check.Equal(0, owner.Personality.Intents.Count, "sleeping actor starts no new goals");
            owner.IsSleeping = false; owner.FoodPoints = world.Game.Rules.ActorMaxFood(owner);
            Actor target = NpcIntentSupport.Actor(world, "target", 5, 1);
            NpcGoalGenerator.Refresh(world.Game, owner);
            Check.Equal(0, owner.Personality.Intents.Count, "an unknown hidden person's real state is never scanned");
            owner.Personality.Knowledge.LearnPerson(new NpcKnownPerson { Id = target.PersonalityIdentity,
                Name = target.UnmodifiedName, Place = target.Location, Confidence = 10, FoodNeed = 100, FoodConfidence = 10 });
            NpcGoalGenerator.Refresh(world.Game, owner);
            Check.Equal(0, owner.Personality.Intents.Count, "uncertain belief discounts the expected benefit below admission");
            NpcKnownPerson known = owner.Personality.Knowledge.Person(target.PersonalityIdentity); known.FoodConfidence = 100; known.Hostile = true;
            NpcGoalGenerator.Refresh(world.Game, owner);
            Check.Equal(0, owner.Personality.Intents.Count, "care does not target a known enemy");
            known.Hostile = false; known.Dead = true; NpcGoalGenerator.Refresh(world.Game, owner);
            Check.Equal(0, owner.Personality.Intents.Count, "known death invalidates the desired aid state");
            for (int i = 0; i < 80; i++) owner.Personality.Knowledge.LearnPerson(new NpcKnownPerson { Id = Guid.NewGuid(), Name = "known",
                Place = owner.Location, Confidence = 100, FoodNeed = 100, FoodConfidence = 100 });
            NpcGoalGenerator.Refresh(world.Game, owner);
            Check.Equal(true, NpcGoalGenerator.Evaluate(world.Game, owner).Count <= 192, "candidate evaluation has bounded local inputs");
            Check.Equal(4, owner.Personality.Intents.Count, "only four current desires become executable goals");
            foreach (NpcIntent goal in owner.Personality.Intents) Check.Equal(true, goal.Generated != null, "admitted goals retain their state explanation");
        });
    }
}
