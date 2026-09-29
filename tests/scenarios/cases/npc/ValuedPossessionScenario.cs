using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class ValuedPossessionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/valued-possession", () => TownScenarioFactory.Arena(4667, ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1, "solitary");
            var treasured = new ItemMeleeWeapon(world.Game.GameItems.BASEBALLBAT);
            owner.Personality.AddTrait(new TraitInstance("likes_items", treasured.Model.ID)); owner.Inventory.AddAll(treasured);
            NpcGoalGenerator.Refresh(world.Game, owner);
            Check.Equal(0, owner.Personality.Intents.Count, "owned valued possession creates no deficit");
            owner.Inventory.RemoveAllQuantity(treasured); world.Map.DropItemAt(treasured, new Point(2, 1));
            var namesake = new ItemMeleeWeapon(treasured.Model); world.Map.DropItemAt(namesake, new Point(1, 2));
            var substitute = new ItemMeleeWeapon(treasured.Model); owner.Inventory.AddAll(substitute); world.Game.DoEquipItem(owner, substitute);
            NpcIntentSupport.Turn(world, owner);
            Check.Equal(true, owner.Inventory.Contains(treasured), "actor recovers the specific remembered possession");
            Check.Equal(false, owner.Inventory.Contains(namesake), "another item of the same model does not satisfy the attachment");
            NpcIntent goal = NpcIntentSupport.Intent(owner, "recover_valued_item");
            Check.Equal(treasured.StoryIdentity, goal.Generated.ItemId, "goal carries stable object identity");
            Check.Equal(true, NpcIntentSupport.HasEvent(owner, "valued_item_acquired"), "successful recovery has an actual event");
        });
    }
}
