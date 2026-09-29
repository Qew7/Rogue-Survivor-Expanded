using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class ResourceYieldScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/resource-yield", () => TownScenarioFactory.Arena(4664, ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            Actor holder = NpcIntentSupport.Actor(world, "holder", 2, 1, "kind", "loyal");
            Actor hungry = NpcIntentSupport.Actor(world, "hungry", 1, 1, "solitary", "lawful");
            hungry.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            Location cache = new Location(world.Map, new Point(1, 2));
            var food = new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 2 }; world.Map.DropItemAt(food, cache.Position);
            NpcIntent reservation = NpcStorySystem.StartKnown(holder, new NpcKnownPerson { Id = hungry.PersonalityIdentity, Name = hungry.UnmodifiedName,
                Place = hungry.Location }, NpcIntentContent.Seek);
            Check.Equal(true, Session.Get.NpcDirector.Reserve(reservation.StoryId, cache), "another actual participant has an active competing plan");
            NpcIntentSupport.Turn(world, hungry);
            Check.Equal(0, NpcIntentSupport.FoodUnits(hungry), "contestation is a conversation, not acquisition");
            Check.Equal(true, NpcIntentSupport.HasEvent(holder, "resource_contested"), "actual participants observe the dispute");
            NpcIntentSupport.Turn(world, holder);
            Check.Equal(true, NpcIntentSupport.HasEvent(hungry, "resource_yielded"), "compassionate holder actually yields");
            world.Map.LocalTime.TurnCounter = 1; NpcIntentSupport.Turn(world, hungry);
            Check.Equal(2, NpcIntentSupport.FoodUnits(hungry), "concession permits real acquisition next turn");
            Check.Equal(null, world.Map.GetItemsAt(cache.Position), "resource is not duplicated");
            Check.Equal(true, hungry.Personality.Person(holder.PersonalityIdentity).Memories.Count > 0, "concession persists in the relationship history");
        });
    }
}
