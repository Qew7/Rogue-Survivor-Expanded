using System;
using System.IO;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class ContentModulePersistenceScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/content-module-persistence", () => TownScenarioFactory.Arena(4691, ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 6, 2);
            world.Game.NpcContent = PersonalityContent.Create(new NpcRestContent()).Content;
            Actor owner = NpcIntentSupport.Actor(world, "resting", 1, 1, "restful"); owner.StaminaPoints = Rules.STAMINA_MIN_FOR_ACTIVITY;
            NpcGoalGenerator.Refresh(world.Game, owner); NpcIntent goal = NpcIntentSupport.Intent(owner, "take_breath");
            owner.ActionPoints = Rules.BASE_ACTION_COST;
            var action = NpcPlanRuntime.Choose(world.Game, owner, goal, new[] { owner }, null);
            Check.Equal(true, action.IsLegal(), "extension plan is bound before saving");
            Check.Equal("rest", goal.Plan.Current.OperatorId, "plan stores a stable operator ID");
            string path = Path.Combine(Path.GetTempPath(), "npc-module-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get); Session loaded = BinarySaveStore.LoadExact<Session>(path);
                ScenarioWorld restored = NpcIntentSupport.Restore(world, loaded); Actor actor = NpcIntentSupport.Find(restored.Map, owner.PersonalityIdentity);
                restored.Game.NpcContent = PersonalityContent.Create(new NpcRestContent()).Content;
                NpcIntent saved = NpcIntentSupport.Intent(actor, "take_breath");
                Check.Equal(goal.Generated.Key, saved.Generated.Key, "value ID and subject survive serialization");
                Check.Equal(true, saved.Plan.Current.Extension.Adds.Extended, "extended symbolic masks survive serialization");
                Check.Same(restored.Map, saved.Plan.Current.Place.Map, "extension shares world map identity");
                NpcIntentSupport.Turn(restored, actor); Check.Equal(NpcIntentStatus.Completed, saved.Status, "saved extension resolves its runtime executor");
                BinarySaveStore.Save(path, loaded);
                RecordsSave archive = RecordsReader.Load(path); string lines = String.Join(" ", RecordsReader.Lines(archive, null, null, "", RecordsEventFilter.Life));
                Check.Equal(true, lines.Contains("regained stamina by taking a breath"), "archive-only reader uses persisted extension prose and categories");
                Check.Equal(1, archive.Records.Residents.Single(r => r.Identity == actor.PersonalityIdentity).Entries.Count(e => e.Kind == "regained_stamina"), "one physical action produces one archived event");
                Check.Equal(true, archive.Records.Residents.Single(r => r.Identity == actor.PersonalityIdentity).Entries.Any(e => e.Kind == "goal_plan" && e.Text.Contains("rest")), "archived plans name the registered custom operator");
            }
            finally { if (File.Exists(path)) File.Delete(path); if (File.Exists(path + ".bak")) File.Delete(path + ".bak"); }
        });
    }
}
