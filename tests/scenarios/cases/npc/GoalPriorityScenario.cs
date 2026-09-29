using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class GoalPriorityScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/goal-priority", () => TownScenarioFactory.Arena(4658, ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1, "kind");
            for (int i = 0; i < 4; i++)
            {
                Actor person = NpcIntentSupport.Actor(world, "person" + i, 2 + i, 1);
                owner.Personality.Knowledge.See(person, 0); NpcKnownPerson known = owner.Personality.Knowledge.Person(person.PersonalityIdentity);
                known.FoodNeed = known.FoodConfidence = 100;
            }
            NpcGoalGenerator.Refresh(world.Game, owner);
            Check.Equal(4, owner.Personality.Intents.Count, "four lower-urgency care goals occupy the actor's budget");
            owner.FoodPoints = Session.Get.GamePreset.HungerPoints - 1; NpcIntentSupport.Turn(world, owner);
            NpcIntent ownNeed = NpcIntentSupport.Intent(owner, "request_food");
            Check.Equal(NpcGoalValue.Nutrition, ownNeed.Generated.Value, "own urgent need displaces a weaker generated desire");
            Check.Equal(true, ownNeed.Announced, "new priority performs its actual request");
            int active = 0, abandoned = 0;
            foreach (NpcIntent intent in owner.Personality.Intents)
            { if (!intent.Finished) active++; if (intent.Status == NpcIntentStatus.Abandoned) abandoned++; }
            Check.Equal(4, active, "reprioritization preserves the active-goal budget");
            Check.Equal(1, abandoned, "only the necessary lower-utility goal is replaced");
            Check.Equal(0, NpcIntentSupport.FoodUnits(owner), "changing priority creates no predicted supplies");
        });
    }
}
