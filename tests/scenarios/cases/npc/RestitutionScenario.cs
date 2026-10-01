using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

static class RestitutionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/restitution", () => TownScenarioFactory.Arena(4672, ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 3, 1, "lawful");
            Actor taker = NpcIntentSupport.Actor(world, "taker", 1, 1, "kind", "honest");
            var claim = new XpdBase(owner, new[] { new Point(2, 1) }); claim.SetFoodRoom(new Rectangle(2, 1, 1, 1)); world.Map.AddXpdBase(claim);
            var stolen = new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 3 }; world.Map.DropItemAt(stolen, new Point(2, 1));
            world.Game.DoTakeItem(taker, new Point(2, 1), stolen);
            Check.Equal(3, taker.Personality.Knowledge.Person(owner.PersonalityIdentity).LossUnits, "perceived wrongdoing retains actual lost quantity");
            taker.RemoveAggressorOf(owner); owner.RemoveSelfDefenceFrom(taker);
            world.Map.RemoveActor(taker); world.Place(taker, 2, 0);
            NpcIntentSupport.Turn(world, taker);
            NpcIntent goal = NpcIntentSupport.Intent(taker, "restore_property");
            Check.Equal(1, NpcIntentSupport.FoodUnits(owner), "law and compassion motivate real compensation");
            Check.Equal(NpcIntentStatus.Active, goal.Status, "one unit cannot falsely complete a three-unit loss");
            NpcIntentSupport.Food(world, taker, 1);
            world.Map.LocalTime.TurnCounter = 1; NpcIntentSupport.Turn(world, taker);
            world.Map.LocalTime.TurnCounter = 2; NpcIntentSupport.Turn(world, taker);
            Check.Equal(3, NpcIntentSupport.FoodUnits(owner), "all lost units are physically replaced");
            Check.Equal(0, taker.Personality.Knowledge.Person(owner.PersonalityIdentity).LossUnits, "known outstanding loss reaches zero");
            Check.Equal(NpcIntentStatus.Completed, goal.Status, "restoration completes only after actual compensation");
            Check.Equal(true, NpcIntentSupport.HasEvent(owner, "restitution_given"), "owner remembers the real restitution");
            Check.Equal(true, owner.Personality.Person(taker.PersonalityIdentity).Memories.Count >= 2, "theft and later repair coexist in personal history");
            var weapon = new ItemMeleeWeapon(world.Game.GameItems.BASEBALLBAT);
            world.Map.DropItemAt(weapon, new Point(2, 1)); world.Game.DoTakeItem(taker, new Point(2, 1), weapon);
            Check.Equal(0, taker.Personality.Knowledge.Person(owner.PersonalityIdentity).LossUnits, "weapon theft cannot be misclassified as a food compensation debt");
        });
    }
}
