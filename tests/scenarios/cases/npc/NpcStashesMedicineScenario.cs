using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay.AI.Sensors;
using djack.RogueSurvivor.Gameplay.Personality;

static class NpcStashesMedicineScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/stashes-medicine", () => TownScenarioFactory.Arena(4781,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Map.Lighting = Lighting.LIT;
            Actor keeper = NpcIntentSupport.Actor(world, "keeper", 1, 1, "hoarder");
            NpcIntentSupport.Player(world, 6, 2);
            var fridge = new StorageContainer("fridge", "closed", "open", 80, 3);
            fridge.SetState(StorageContainer.OPEN);
            world.Map.PlaceMapObjectAt(fridge, new Point(2, 1));
            ItemMedicine spare = new ItemMedicine(world.Game.GameItems.BANDAGE);
            keeper.Inventory.AddAll(spare);
            ItemMedicine retained = new ItemMedicine(world.Game.GameItems.MEDIKIT);
            keeper.Inventory.AddAll(retained);
            var sensor = new LOSSensor(LOSSensor.SensingFilter.ACTORS);
            NpcKnowledgeSystem.Perceive(world.Game, keeper,
                sensor.Sense(world.Game, keeper), sensor.FOV);
            keeper.HitPoints = 1;
            bool woundedStash = false;
            foreach (NpcGoalCandidate candidate in NpcGoalGenerator.Evaluate(world.Game, keeper))
                if (candidate.Capability.Id == "stash_food") woundedStash = true;
            Check.Equal(false, woundedStash, "badly wounded NPC keeps medicine at hand");
            keeper.HitPoints = world.Game.Rules.ActorMaxHPs(keeper);
            bool healthyStash = false;
            foreach (NpcGoalCandidate candidate in NpcGoalGenerator.Evaluate(world.Game, keeper))
                if (candidate.Capability.Id == "stash_food") healthyStash = true;
            Check.Equal(true, healthyStash, "healthy NPC may protect surplus medicine");
            for (int i = 0; i < 16 && !fridge.Contents.Contains(spare); i++)
            {
                keeper.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(keeper);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(true, fridge.Contents.Contains(spare), "NPC physically stores spare bandage");
            Check.Equal(true, keeper.Inventory.Contains(retained),
                "NPC keeps one medicine item in inventory");
            Check.Equal(keeper.PersonalityIdentity, spare.StorageClaimantId,
                "stored medicine retains its personal claimant");
        });
    }
}
