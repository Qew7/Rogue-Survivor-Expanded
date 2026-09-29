using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

static class RestitutionDemandScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/restitution-demand", () => TownScenarioFactory.Arena(4679, ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 4, 1, "lawful", "frugal", "organized");
            Actor taker = NpcIntentSupport.Actor(world, "taker", 2, 1, "selfish", "rebellious");
            var claim = new XpdBase(owner, new[] { new Point(3, 1) }); claim.SetFoodRoom(new Rectangle(3, 1, 1, 1)); world.Map.AddXpdBase(claim);
            var food = new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 3 }; world.Map.DropItemAt(food, new Point(3, 1));
            world.Game.DoTakeItem(taker, new Point(3, 1), food);
            taker.RemoveAggressorOf(owner); owner.RemoveSelfDefenceFrom(taker);
            world.Map.RemoveActor(owner); world.Place(owner, 3, 0);
            NpcIntentSupport.Turn(world, owner);
            Check.Equal(true, NpcIntentSupport.HasEvent(taker, "restitution_requested"), "resource-conscious claimant selects and speaks a compensation demand");
            Check.Equal(0, NpcIntentSupport.FoodUnits(owner), "spoken demand invents no returned resources");
            NpcIntentSupport.Turn(world, taker);
            Check.Equal(true, NpcIntentSupport.HasEvent(owner, "restitution_refused"), "recipient's own values decide the real response");
            Check.Equal(3, NpcIntentSupport.FoodUnits(taker), "refusal transfers no item");
            Check.Equal(true, owner.Personality.Person(taker.PersonalityIdentity).Memories.Count >= 2, "original loss and refusal both persist in the relationship");
        });
    }
}
