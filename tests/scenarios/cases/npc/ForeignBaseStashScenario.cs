using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay.AI.Sensors;
using djack.RogueSurvivor.Gameplay.Personality;

static class ForeignBaseStashScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/stashes-in-foreign-base", () => TownScenarioFactory.Arena(4768,
            "........", "........", "........"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Map.Lighting = Lighting.LIT;
            Actor petr = NpcIntentSupport.Actor(world, "Petr", 1, 1,
                "hoarder", "brave", "rebellious");
            Actor andrey = NpcIntentSupport.Actor(world, "Andrey", 5, 1);
            NpcIntentSupport.Player(world, 7, 2);
            Point at = new Point(2, 1);
            StorageContainer fridge = new StorageContainer("fridge", "closed", "open", 80, 3);
            fridge.SetState(StorageContainer.OPEN);
            world.Map.PlaceMapObjectAt(fridge, at);
            world.Map.AddXpdBase(new XpdBase(andrey, new[] { at }));
            ItemFood store = NpcIntentSupport.Food(world, petr, 1);
            petr.Inventory.AddAll(new ItemFood(world.Game.GameItems.GROCERIES));
            var sensor = new LOSSensor(LOSSensor.SensingFilter.ACTORS);
            NpcKnowledgeSystem.Perceive(world.Game, petr, sensor.Sense(world.Game, petr), sensor.FOV);
            Check.Equal(0, petr.Personality.Knowledge.Caches[0].Units,
                "Petr knows the foreign base has an empty fridge");
            bool wanted = false;
            foreach (NpcGoalCandidate candidate in NpcGoalGenerator.Evaluate(world.Game, petr))
                if (candidate.Capability.Id == "stash_food") wanted = true;
            Check.Equal(true, wanted, "brave hoarder may desire a stash in another group's base");
            for (int i = 0; i < 16 && !fridge.Contents.Contains(store); i++)
            {
                petr.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(petr);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(true, fridge.Contents.Contains(store),
                "NPC executes the stash goal through movement and drop actions");
            Check.Equal(petr.PersonalityIdentity, store.StorageClaimantId,
                "Petr retains the personal claim in Andrey's base");
            Check.Equal(false, NpcIntentSupport.HasEvent(andrey, "base_theft"),
                "placing food is not a theft");
        });
    }
}
