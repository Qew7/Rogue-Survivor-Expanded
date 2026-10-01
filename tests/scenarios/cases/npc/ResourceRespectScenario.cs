using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class ResourceRespectScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/resource-respect", () => TownScenarioFactory.Arena(4674, ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            Actor holder = NpcIntentSupport.Actor(world, "holder", 2, 1, "selfish", "loyal");
            Actor seeker = NpcIntentSupport.Actor(world, "seeker", 1, 1, "solitary", "lawful");
            seeker.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            Location cache = new Location(world.Map, new Point(1, 2));
            var food = new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 2 }; world.Map.DropItemAt(food, cache.Position);
            NpcIntent reservation = NpcStorySystem.StartKnown(world.Game.NpcContent, holder, new NpcKnownPerson { Id = seeker.PersonalityIdentity, Name = seeker.UnmodifiedName,
                Place = seeker.Location }, world.Game.NpcContent.Capability("seek_companion"));
            Session.Get.NpcDirector.Reserve(reservation.StoryId, cache);
            NpcIntentSupport.Turn(world, seeker); NpcIntentSupport.Turn(world, holder);
            NpcIntent goal = NpcIntentSupport.Intent(seeker, "obtain_food");
            var domain = new NpcPlanDomain(world.Game, seeker, goal, new[] { holder });
            Check.Equal(false, domain.Actions.Exists(a => a.Action == NpcPlanAction.PickupFood && a.Place == cache), "lawful values remove refused supplies from candidate methods");
            Check.Equal(true, domain.Actions.Exists(a => a.Action == NpcPlanAction.AskFood), "the same need retains a peaceful alternative");
            Check.Equal(0, NpcIntentSupport.FoodUnits(seeker), "respecting refusal does not fabricate a successful outcome");
            Check.Equal(2, food.Quantity, "refused supplies remain intact");
            Check.Equal(false, NpcIntentSupport.HasEvent(holder, "contested_taken"), "planning an alternative creates no theft memory");
        });
    }
}
