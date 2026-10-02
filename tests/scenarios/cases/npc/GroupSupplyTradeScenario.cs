using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class GroupSupplyTradeScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/group-supply-trade", () => TownScenarioFactory.Arena(4700,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 8, 2);
            Actor buyer = NpcIntentSupport.Actor(world, "buyer", 1, 1, "sociable", "generous");
            Actor seller = NpcIntentSupport.Actor(world, "seller", 2, 1, "sociable", "honest", "humble");
            Actor member = NpcIntentSupport.Actor(world, "member", 3, 1, "loyal");
            seller.AddFollower(member); seller.SocialGroup.SupplyRule = 30;
            buyer.HitPoints = 1; NpcIntentSupport.Food(world, buyer, 3);
            seller.Inventory.AddAll(new ItemMedicine(world.Game.GameItems.MEDIKIT));
            NpcIntentSupport.Turn(world, buyer); NpcIntentSupport.Turn(world, seller);
            world.Map.LocalTime.TurnCounter = 1; NpcIntentSupport.Turn(world, buyer);
            Check.Equal(true, NpcIntentSupport.HasEvent(seller, "bartered_medicine"),
                "real medicine exchange is observed by the leader");
            Check.Equal(true, seller.SocialGroup.SupplyRule < 30, "completed exchange relaxes the shared supply rule");
            Check.Equal(true, NpcIntentSupport.HasEvent(member, "group_supply_rule_open"),
                "present member sees the announcement");
            Check.Equal(0, member.Personality.KnownSupplyRule(seller.SocialGroup.Identity),
                "member records the current open rule");
        });
    }
}
