using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

static class StoryPersistenceScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/story-persistence", () => TownScenarioFactory.Arena(4629, "........", "........", "........"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 7, 2);
            Actor leader = NpcIntentSupport.Actor(world, "leader", 1, 1, "loyal", "organized");
            Actor hungry = NpcIntentSupport.Actor(world, "hungry", 2, 1, "sociable");
            Actor collector = NpcIntentSupport.Actor(world, "collector", 1, 2, "generous");
            leader.AddFollower(hungry); leader.AddFollower(collector); hungry.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            NpcIntentSupport.Turn(world, hungry);
            world.Map.DropItemAt(new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 3 }, new Point(2, 2));
            NpcIntentSupport.Turn(world, leader); NpcIntentSupport.Turn(world, collector);
            NpcIntent goal = NpcIntentSupport.Intent(collector, "gather_group_supplies");
            Check.Equal(1, goal.Progress, "save occurs after real acquisition and before delivery");
            Check.Equal(true, Session.Get.NpcDirector.OfferDue(world.Map, 0), "source map receives one planning slot");
            Check.Equal(false, Session.Get.NpcDirector.OfferDue(world.Map, 0), "another offer must wait");
            string path = Path.Combine(Path.GetTempPath(), "story-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get); Session loaded = BinarySaveStore.LoadExact<Session>(path);
                ScenarioWorld restored = NpcIntentSupport.Restore(world, loaded);
                Actor owner = NpcIntentSupport.Find(restored.Map, collector.PersonalityIdentity);
                Actor boss = NpcIntentSupport.Find(restored.Map, leader.PersonalityIdentity);
                Actor recipient = NpcIntentSupport.Find(restored.Map, hungry.PersonalityIdentity);
                NpcIntent saved = NpcIntentSupport.Intent(owner, "gather_group_supplies");
                Check.Same(owner.SocialGroup, boss.SocialGroup, "loaded members share one stable group");
                Check.Equal(goal.GroupId, owner.SocialGroup.Identity, "group identity is retained");
                Check.Equal(1, saved.Progress, "collector resumes the delivery stage");
                Check.Same(restored.Map, saved.Destination.Map, "known destination shares the loaded map");
                Check.Same(restored.Map, loaded.NpcDirector.Find(saved.StoryId).Resource.Map, "resource reservation shares the same loaded map");
                Check.Equal(true, boss.Personality.Knowledge.Facts.Count > 0, "known facts and provenance survive");
                Check.Equal(false, loaded.NpcDirector.OfferDue(restored.Map, 0), "loading preserves pacing and rebuilds its reference-keyed map lookup");
                Check.Equal(true, loaded.NpcDirector.OfferDue(restored.Map, 15), "the saved admission interval expires normally");
                NpcFact need = boss.Personality.Knowledge.Facts.Find(f => f.Kind == "requested_food");
                Check.Equal(hungry.PersonalityIdentity, need.SubjectId, "fact participants retain permanent identity");
                Check.Equal(NpcKnowledgeSource.Participant, need.Source, "loading retains participation separately from a report");
                Check.Equal(100, need.Confidence, "direct evidence retains confidence");
                for (int i = 0; i < 8 && !saved.Finished; i++) { restored.Map.LocalTime.TurnCounter = i + 1; NpcIntentSupport.Turn(restored, owner); }
                Check.Equal(1, NpcIntentSupport.FoodUnits(recipient), "loading does not duplicate acquisition or delivery");
                Check.Equal("completed", owner.SocialGroup.Plan.Stage, "restored group plan completes");
                loaded.WorldTime.TurnCounter = restored.Map.LocalTime.TurnCounter;
                BinarySaveStore.Save(path, loaded); RecordsSave archive = RecordsReader.Load(path);
                Check.Equal(true, String.Join(" ", new System.Collections.Generic.List<string>(RecordsReader.Lines(archive, null, null, saved.StoryId,
                    RecordsEventFilter.Intentions)).ToArray()).Contains("gather supplies for the group came to an end successfully"), "archive-only reader can search the multi-actor episode");
            }
            finally { if (File.Exists(path)) File.Delete(path); if (File.Exists(path + ".bak")) File.Delete(path + ".bak"); }
        });
    }
}
