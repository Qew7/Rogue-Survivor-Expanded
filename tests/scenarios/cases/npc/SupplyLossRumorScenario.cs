using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class SupplyLossRumorScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/supply-loss-rumor", () => TownScenarioFactory.Arena(4903,
            "...#.........", "...#.........", "...#........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor witness = NpcIntentSupport.Actor(world, "witness", 0, 1);
            Actor owner = NpcIntentSupport.Actor(world, "Peter Steel", 1, 1);
            Actor thief = NpcIntentSupport.Actor(world, "Vasily", 2, 1);
            thief.Faction = world.Game.GameFactions.TheBikers;
            Actor listener = NpcIntentSupport.Actor(world, "listener", 5, 1);
            Actor player = NpcIntentSupport.Player(world, 6, 1);
            witness.Personality.Opinion(owner.PersonalityIdentity, owner.UnmodifiedName);
            var claim = new XpdBase(owner, new[] { new Point(2, 1) });
            claim.SetFoodRoom(new Rectangle(2, 1, 1, 1)); world.Map.AddXpdBase(claim);
            var food = new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 2 };
            world.Map.DropItemAt(food, new Point(2, 1));
            world.Game.DoTakeItem(thief, new Point(2, 1), food);
            NpcFact fact = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "supplies_lost");
            Check.Equal(true, fact != null, "actual storage theft creates a reportable supply loss");
            Check.Equal("Peter Steel", fact.ReportSubject, "known base owner is named");
            Check.Equal("a biker", fact.ReportOther, "unknown thief is described by faction");
            Check.Equal("food", fact.Resource, "the stolen resource is retained");
            Check.Equal(2, fact.Units, "the actual quantity is retained");
            Check.Equal(false, listener.Personality.Knowledge.Facts.Exists(f => f.EventId == fact.EventId),
                "wall blocks firsthand knowledge");

            world.Place(witness, 4, 0); world.Place(listener, 5, 0); world.Place(player, 6, 0);
            var tell = new ActionNpcTell(witness, world.Game, listener, fact);
            Check.Equal(true, tell.IsLegal(), "witness can tell the storage loss");
            tell.Perform();
            NpcFact heard = listener.Personality.Knowledge.Facts.Find(f => f.EventId == fact.EventId);
            Check.Equal(NpcKnowledgeSource.Told, heard.Source, "listener learns hearsay");
            Check.Equal("a biker", heard.ReportOther, "hearsay keeps the witness's description");
            Check.Equal(true, player.Personality.HeardJournal[0].Text.Contains("Peter Steel's base lost 2 units of food from storage"),
                "unidentified thief stays anonymous while the actual supply loss is reported");
        });
    }
}
