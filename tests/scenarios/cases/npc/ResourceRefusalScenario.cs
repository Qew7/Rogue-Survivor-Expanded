using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class ResourceRefusalScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/resource-refusal", () => TownScenarioFactory.Arena(4665, ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            Actor holder = NpcIntentSupport.Actor(world, "holder", 2, 1, "selfish", "loyal");
            Actor taker = NpcIntentSupport.Actor(world, "taker", 1, 1, "solitary", "rebellious");
            taker.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            Location cache = new Location(world.Map, new Point(1, 2));
            world.Map.DropItemAt(new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 2 }, cache.Position);
            NpcIntent reservation = NpcStorySystem.StartKnown(holder, new NpcKnownPerson { Id = taker.PersonalityIdentity, Name = taker.UnmodifiedName,
                Place = taker.Location }, world.Game.NpcContent.Capability("seek_companion"));
            Session.Get.NpcDirector.Reserve(reservation.StoryId, cache);
            NpcIntentSupport.Turn(world, taker); NpcIntentSupport.Turn(world, holder);
            Check.Equal(true, NpcIntentSupport.HasEvent(taker, "resource_refused"), "holder's values produce a real refusal");
            world.Map.LocalTime.TurnCounter = 1; NpcIntentSupport.Turn(world, taker);
            Check.Equal(2, NpcIntentSupport.FoodUnits(taker), "rebellious contender takes the physically available resource");
            Check.Equal(true, NpcIntentSupport.HasEvent(holder, "contested_taken"), "taking after refusal causes a witnessed consequence");
            Check.Equal(true, holder.Personality.Person(taker.PersonalityIdentity).Feeling < 0, "rivalry changes subsequent attitudes");
            Check.Equal(true, holder.Personality.Memories.Count > 0, "scarcity produces personal memories");
        });
    }
}
