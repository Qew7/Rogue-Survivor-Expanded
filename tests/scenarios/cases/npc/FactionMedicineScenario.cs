using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.Personality;
static class FactionMedicineScenario
{
    static Actor Police(ScenarioWorld world, string name, int x, params string[] traits)
    {
        Actor actor = new Actor(world.Game.GameActors.MaleCivilian, world.Game.GameFactions.ThePolice, name, true, false, 0);
        actor.Controller = new CivilianAI(); actor.Personality = new PersonalityState();
        foreach (string trait in traits) actor.Personality.AddTrait(new TraitInstance(trait));
        actor.FoodPoints = world.Game.Rules.ActorMaxFood(actor); actor.SleepPoints = world.Game.Rules.ActorMaxSleep(actor);
        actor.StaminaPoints = world.Game.Rules.ActorMaxSTA(actor); world.Place(actor, x, 1); return actor;
    }
    public static void Register()
    {
        ScenarioRunner.Add("npc/faction-medicine", () => TownScenarioFactory.Arena(4814,
            "..........", "..........", ".........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 9, 2);
            Actor coordinator = Police(world, "coordinator", 1, "kind");
            Actor runner = Police(world, "runner", 2, "healer");
            Actor patient = Police(world, "patient", 3); patient.HitPoints = 1;
            Actor stranger = NpcIntentSupport.Actor(world, "stranger", 8, 1); stranger.HitPoints = 1;
            Check.Equal(null, coordinator.SocialGroup, "faction cooperation requires no follower group");
            NpcIntentSupport.Turn(world, patient);
            world.Map.DropItemAt(new ItemMedicine(world.Game.GameItems.MEDIKIT) { Quantity = 2 }, new Point(4, 1));
            NpcIntentSupport.Turn(world, coordinator);
            NpcGroupPlan plan = coordinator.Personality.FactionPlan;
            Check.Equal(true, plan != null && plan.Kind == "faction_medicine", "same-faction injury produces a collective task");
            Check.Equal(true, NpcIntentSupport.HasEvent(runner, "faction_medicine_requested"), "task is actually spoken to visible ally");
            NpcIntent assignment = NpcIntentSupport.Intent(runner, "gather_faction_medicine");
            Check.Equal(true, assignment != null && assignment.GroupId == Guid.Empty, "independent ally accepts without joining a group");
            Guid leaderId = coordinator.PersonalityIdentity, runnerId = runner.PersonalityIdentity, patientId = patient.PersonalityIdentity, strangerId = stranger.PersonalityIdentity;
            string path = Path.Combine(Path.GetTempPath(), "faction-task-" + Guid.NewGuid().ToString("N") + ".dat");
            try
            {
                BinarySaveStore.Save(path, Session.Get); Session loaded = BinarySaveStore.Load<Session>(path);
                ScenarioWorld restored = NpcIntentSupport.Restore(world, loaded);
                coordinator = NpcIntentSupport.Find(restored.Map, leaderId); runner = NpcIntentSupport.Find(restored.Map, runnerId);
                patient = NpcIntentSupport.Find(restored.Map, patientId); stranger = NpcIntentSupport.Find(restored.Map, strangerId);
                Check.Equal(plan.StoryId, coordinator.Personality.FactionPlan.StoryId, "collective faction plan survives save and load");
                assignment = NpcIntentSupport.Intent(runner, "gather_faction_medicine");
                for (int t = 1; t < 18 && !assignment.Finished; t++) { restored.Map.LocalTime.TurnCounter = t; NpcIntentSupport.Turn(restored, runner); }
                Check.Equal(NpcIntentStatus.Completed, assignment.Status, "restored ally actually delivers and reports");
                Check.Equal(1, patient.Inventory.CountItems, "the faction patient receives one real kit");
                Check.Equal(0, stranger.Inventory.CountItems, "unrelated faction receives no invisible aid");
                Check.Equal("completed", coordinator.Personality.FactionPlan.Stage, "saved collective plan reaches a real outcome");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        });
    }
}
