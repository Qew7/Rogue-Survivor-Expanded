using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;

static class PersonalityItemChoiceScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-item-choice", () => TownScenarioFactory.Arena(4514,
            ".....", ".....", ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor civilian = world.Game.GameActors.MaleCivilian.CreateNumberedName(
                world.Game.GameFactions.TheCivilians, 0);
            civilian.Personality = new PersonalityState();
            world.Place(civilian, 2, 2);
            ItemEntertainment magazine = new ItemEntertainment(world.Game.GameItems.MAGAZINE);
            world.Map.DropItemAt(magazine, new Point(2, 2));
            BaseAI ai = (BaseAI)civilian.Controller;

            civilian.Personality.AddTrait(new TraitInstance("dislikes_items", magazine.Model.ID));
            Check.Equal(BaseAI.ItemRating.JUNK, ai.RateItem(world.Game, magazine, false),
                "disliked magazine is rejected by real AI valuation");
            Check.Equal(true, world.NpcTurn(civilian), "NPC makes a legal decision without a wanted item");
            Check.Equal(false, civilian.Inventory.Contains(magazine),
                "NPC leaves disliked magazine on the ground");

            civilian.Personality = new PersonalityState();
            civilian.Personality.AddTrait(new TraitInstance("likes_items", magazine.Model.ID));
            civilian.Controller = new CivilianAI();
            ai = (BaseAI)civilian.Controller;
            civilian.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(BaseAI.ItemRating.NEED, ai.RateItem(world.Game, magazine, false),
                "liked magazine becomes a need");
            for (int turn = 0; turn < 6 && !civilian.Inventory.Contains(magazine); turn++)
            {
                civilian.ActionPoints = Rules.BASE_ACTION_COST;
                Check.Equal(true, world.NpcTurn(civilian), "NPC makes a legal decision with a wanted item");
            }
            Check.Equal(true, civilian.Inventory.Contains(magazine),
                "NPC picks up preferred magazine from its tile");
        });
    }
}
