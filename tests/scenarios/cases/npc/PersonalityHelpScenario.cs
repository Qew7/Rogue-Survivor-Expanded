using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

static class PersonalityHelpScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-help", () => TownScenarioFactory.Arena(4527,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor giver = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "giver", false, false, 0);
            giver.Controller = new PlayerController();
            Actor recipient = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "recipient", false, false, 0);
            recipient.Personality = new PersonalityState();
            world.Map.PlaceActorAt(giver, new Point(1, 1));
            world.Map.PlaceActorAt(recipient, new Point(2, 1));
            world.SetPlayer(giver);

            recipient.FoodPoints = Session.Get.GamePreset.HungerPoints + 1;
            ItemFood ordinaryGift = new ItemFood(world.Game.GameItems.GROCERIES);
            Check.Equal(true, giver.Inventory.AddAll(ordinaryGift), "giver carries first food item");
            world.Game.DoGiveItemTo(giver, recipient, ordinaryGift);
            Check.Equal(true, recipient.Inventory.Contains(ordinaryGift),
                "ordinary gift is transferred by the real action");
            Check.Equal(0, recipient.Personality.Memories.Count,
                "ordinary gift does not create a significant memory");

            giver.ActionPoints = Rules.BASE_ACTION_COST;
            recipient.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            ItemFood neededGift = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            Check.Equal(true, giver.Inventory.AddAll(neededGift), "giver carries needed food item");
            world.Game.DoGiveItemTo(giver, recipient, neededGift);
            Check.Equal(true, recipient.Inventory.Contains(neededGift),
                "needed gift reaches hungry recipient");
            Check.Equal(1, recipient.Personality.Memories.Count,
                "needed food creates one help memory");
            Check.Equal("received_help", recipient.Personality.Memories[0].Id,
                "significant aid uses its memory definition");
            Check.Equal(true, recipient.Personality.Events[0].Direct,
                "recipient directly experiences the help");
        });
    }
}
