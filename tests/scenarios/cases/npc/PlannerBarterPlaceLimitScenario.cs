using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class PlannerBarterPlaceLimitScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/planner-barter-place-limit", () => TownScenarioFactory.Arena(4827,
            new string('.', 40), new string('.', 40), new string('.', 40)), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 39, 2);
            Actor buyer = NpcIntentSupport.Actor(world, "buyer", 1, 1, "solitary", "scavenger");
            Actor seller = NpcIntentSupport.Actor(world, "seller", 38, 1);
            buyer.Inventory.AddAll(new ItemMedicine(world.Game.GameItems.MEDIKIT));
            buyer.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            NpcIntent goal = NpcStorySystem.StartKnown(world.Game.NpcContent, buyer, new NpcKnownPerson { Id = buyer.PersonalityIdentity,
                Name = buyer.UnmodifiedName, Place = buyer.Location }, world.Game.NpcContent.Capability("obtain_food"));
            Check.Equal(true, goal != null, "hungry buyer has a real food acquisition goal");
            var domain = new NpcPlanDomain(world.Game, buyer, goal, new List<Actor>());
            NpcKnownPerson known = new NpcKnownPerson { Id = seller.PersonalityIdentity, Name = seller.UnmodifiedName, Place = seller.Location };
            buyer.Personality.Knowledge.People.Add(known);
            buyer.Personality.Knowledge.Learn(new NpcFact { EventId = 501, Kind = "food_offered",
                SubjectId = seller.PersonalityIdentity, OtherId = buyer.PersonalityIdentity, Confidence = 100,
                EventTurn = world.Map.LocalTime.TurnCounter });
            for (int x = 0; x < NpcFactLayout.MaximumPlaces; x++)
                domain.At(new Location(world.Map, new Point(x, 0)));
            Check.Equal(0UL, domain.At(seller.Location), "a new seller cannot enter the full place table");
            domain.Actions.Clear();
            FoodPlanOperators.Acquisition(domain);
            Check.Equal(false, domain.Actions.Exists(step => step.Action == NpcPlanAction.BarterFood),
                "full place table cannot create barter without a location precondition");
            var available = new NpcPlanDomain(world.Game, buyer, goal, new List<Actor>());
            Check.Equal(true, available.Actions.Exists(step => step.Action == NpcPlanAction.BarterFood),
                "known seller still offers barter while a location slot is available");
        });
    }
}
