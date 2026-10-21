using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay.Personality;

static class CacheEventNoteScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/cache-event-note", () => TownScenarioFactory.Arena(4871,
            "....#...", "....#...", "....#..."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "Petr", 1, 1, "kind");
            owner.Personality.AddMemory(new MemoryInstance("received_help", 0, 1, "real help"));
            Actor thief = NpcIntentSupport.Actor(world, "Vasily", 3, 1);
            owner.Personality.Opinion(thief.PersonalityIdentity, thief.UnmodifiedName);
            Actor reader = NpcIntentSupport.Actor(world, "reader", 5, 1);
            NpcIntentSupport.Player(world, 7, 2);
            StorageContainer cache = new StorageContainer("drawer", "closed", "open", 40, 3);
            cache.IsMaterialTransparent = true;
            world.Map.PlaceMapObjectAt(cache, new Point(2, 1));
            world.Try(new ActionUseStorageContainer(owner, world.Game, cache, true));
            world.Map.PlaceActorAt(owner, cache.Location.Position);
            world.Try(new ActionDropItem(owner, world.Game, NpcIntentSupport.Food(world, owner, 1)));
            world.Map.PlaceActorAt(owner, new Point(1, 1));
            world.Try(new ActionGetFromContainer(thief, world.Game, cache.Location.Position));
            NpcFact witnessed = owner.Personality.Knowledge.Facts.Find(f => f.Kind == "cache_theft");
            Check.Equal(true, witnessed != null && witnessed.NamesSubject,
                "real theft supplies the note's evidence");
            ItemNote note = new ItemNote(world.Game.GameItems.NOTE);
            owner.Inventory.AddAll(note);
            Check.Equal(false, world.Try(new ActionWriteNote(owner, world.Game, note,
                NoteTopic.Confession, null, true, witnessed)),
                "witness cannot confess to someone else's theft");
            Check.Equal(true, world.Try(new ActionWriteNote(owner, world.Game, note,
                NoteTopic.Warning, null, true, witnessed)), "witness writes a warning");
            Check.Equal(true, note.Text.Contains("Vasily") && note.Text.Contains("helped me once") &&
                note.Text.Contains("To the Civilians community,"), "real witness, memory and faction shape text");
            foreach (TraitDefinition trait in PersonalitySystem.Registry.StartingTraits)
                Check.Equal(true, NpcNoteTemplates.HasTraitTemplate(trait.Id),
                    "every spawnable trait has a note phrase: " + trait.Id);
            Check.Equal(witnessed.EventId, note.Claim.EventId, "note keeps the real cause identity");
            owner.Inventory.RemoveAllQuantity(note);
            reader.Inventory.AddAll(note);
            world.Try(new ActionUseItem(reader, world.Game, note));
            NpcFact learned = reader.Personality.Knowledge.Facts.Find(f => f.EventId == witnessed.EventId);
            Check.Equal(NpcKnowledgeSource.Told, learned.Source, "paper is reported evidence");
            Check.Equal(false, learned.Confidence == 100, "reader does not gain eyewitness certainty");
            ItemNote retold = new ItemNote(world.Game.GameItems.NOTE);
            reader.Inventory.AddAll(retold);
            Check.Equal(true, world.Try(new ActionWriteNote(reader, world.Game, retold,
                NoteTopic.Warning, null, true, learned)), "reader can write a warning from the report");
            Check.Equal(true, retold.Text.Contains("I heard that ") &&
                !retold.Text.Contains("I saw that "), "retelling keeps hearsay wording");
            string text = note.Text;
            string path = Path.Combine(Path.GetTempPath(), "cache-event-note-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, world.Map);
                Map restored = BinarySaveStore.Load<Map>(path);
                restored.ReconstructAuxiliaryFields();
                Actor savedReader = NpcIntentSupport.Find(restored, reader.PersonalityIdentity);
                ItemNote savedNote = (ItemNote)savedReader.Inventory.GetFirstMatching(i =>
                    i is ItemNote && ((ItemNote)i).Id == note.Id);
                Check.Equal(text, savedNote.Text, "composed text survives save without recomposition");
                Check.Equal(witnessed.EventId, savedNote.Claim.EventId, "source survives save");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
