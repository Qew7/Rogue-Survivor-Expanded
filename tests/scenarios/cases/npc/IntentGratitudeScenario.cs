using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class IntentGratitudeScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/intent-gratitude", () => TownScenarioFactory.Arena(4601, ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = NpcIntentSupport.Player(world, 2, 1);
            Actor generous = NpcIntentSupport.Actor(world, "generous", 1, 1, "generous");
            Actor selfish = NpcIntentSupport.Actor(world, "selfish", 3, 1, "selfish");
            generous.FoodPoints = selfish.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            var first = NpcIntentSupport.Food(world, player, 1); world.Game.DoGiveItemTo(player, generous, first);
            var second = NpcIntentSupport.Food(world, player, 1); world.Game.DoGiveItemTo(player, selfish, second);
            NpcIntent repayment = NpcIntentSupport.Intent(generous, "repay_aid");
            Check.Equal(true, repayment != null, "generosity turns actual help into an intention");
            Check.Equal(null, NpcIntentSupport.Intent(selfish, "repay_aid"), "selfishness chooses a different reaction to the same help");
            Check.Equal(player.PersonalityIdentity, repayment.TargetId, "gratitude is tied to the actual helper");
            generous.FoodPoints = selfish.FoodPoints = world.Game.Rules.ActorMaxFood(generous);
            NpcIntentSupport.Food(world, generous, 2);
            long total = generous.Inventory.TotalReceived;
            NpcIntentSupport.Turn(world, generous);
            Check.Equal(true, NpcIntentSupport.HasEvent(generous, "aid_acknowledged"), "thanks is a performed action");
            var ground = new djack.RogueSurvivor.Engine.Items.ItemFood(world.Game.GameItems.CANNED_FOOD);
            ground.Quantity = 3; world.Map.DropItemAt(ground, generous.Location.Position);
            int playerAP = player.ActionPoints;
            NpcIntentSupport.Turn(world, generous);
            Check.Equal(1, NpcIntentSupport.FoodUnits(player), "repayment reaches the helper");
            Check.Equal(2, NpcIntentSupport.FoodUnits(generous), "NPC keeps a food reserve");
            Check.Equal(total, generous.Inventory.TotalReceived, "splitting a gift does not invent acquisitions");
            Check.Equal(3, ground.Quantity, "existing ground stack is untouched by the transfer");
            Check.Equal(playerAP, player.ActionPoints, "passive recipient does not spend an action");
            Check.Equal(NpcIntentStatus.Completed, repayment.Status, "completion follows actual transfer");
            Check.Equal(true, NpcIntentSupport.HasEvent(generous, "shared_food"), "gift is observable and recorded");
            NpcIntentSupport.Turn(world, selfish);
            Check.Equal(1, NpcIntentSupport.FoodUnits(selfish), "selfish NPC acknowledges without giving its food away");
            Check.Equal(1, NpcIntentSupport.FoodUnits(player), "no duplicate transfer after another reaction");
        });
    }
}
