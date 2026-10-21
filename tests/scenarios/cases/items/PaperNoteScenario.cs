using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

static class PaperNoteScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("items/paper-note", () => TownScenarioFactory.Arena(4735,
            ".....", ".....", "....."), world =>
        {
            Actor author = SkillScenario.Actor(world);
            world.Place(author, 1, 1);
            world.SetPlayer(author);
            ItemNote blank = new ItemNote(world.Game.GameItems.NOTE);
            Check.Equal(false, world.Try(new ActionUseItem(author, world.Game, blank)),
                "unowned blank paper cannot be read");
            Check.Equal(false, world.Try(new ActionWriteNote(author, world.Game, blank, NoteTopic.Location)),
                "writing requires carried paper");
            author.Inventory.AddAll(blank);
            Check.Equal(false, world.Try(new ActionUseItem(author, world.Game, blank)),
                "blank paper cannot be read");
            int before = author.ActionPoints;
            Check.Equal(true, world.Try(new ActionWriteNote(author, world.Game, blank, NoteTopic.Location,
                signed: false)), "author can write a location claim");
            Check.Equal(before - Rules.BASE_ACTION_COST, author.ActionPoints, "writing spends a turn");
            Check.Equal(false, blank.IsBlank, "paper becomes written");
            Check.Equal(null, blank.Signature, "unsigned note hides author");
            Check.Equal(false, blank.Text.Contains(author.UnmodifiedName), "text does not expose hidden author");
            Check.Equal(false, world.Try(new ActionWriteNote(author, world.Game, blank, NoteTopic.Location)),
                "written paper cannot be overwritten");
            Check.Equal(true, world.Try(new ActionUseItem(author, world.Game, blank)),
                "written note can be read");

            StorageContainer fridge = new StorageContainer("fridge", "closed", "open", 240, 2);
            world.Map.PlaceMapObjectAt(fridge, new Point(2, 1));
            fridge.Contents.AddAll(new ItemFood(world.Game.GameItems.GROCERIES));
            ItemNote cacheNote = new ItemNote(world.Game.GameItems.NOTE);
            author.Inventory.AddAll(cacheNote);
            Check.Equal(false, world.Try(new ActionWriteNote(author, world.Game, cacheNote,
                NoteTopic.CacheSupplies, fridge)), "closed cache cannot be described");
            world.Try(new ActionUseStorageContainer(author, world.Game, fridge, true));
            Check.Equal(true, world.Try(new ActionWriteNote(author, world.Game, cacheNote,
                NoteTopic.CacheSupplies, fridge)), "visible supplies can be recorded");
            Check.Equal(fridge.Id, cacheNote.CacheId, "note names observed cache identity");
            Check.Equal("food", cacheNote.Resource, "note captures observed resource");
            Check.Equal(1, cacheNote.Units, "note captures observed quantity");
            Guid id = cacheNote.Id;
            string text = cacheNote.Text;
            string path = Path.Combine(Path.GetTempPath(), "paper-note-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, world.Map);
                Map loaded = BinarySaveStore.Load<Map>(path);
                loaded.ReconstructAuxiliaryFields();
                StorageContainer restoredFridge = (StorageContainer)loaded.GetMapObjectAt(2, 1);
                Actor restoredAuthor = loaded.GetActorAt(1, 1);
                ItemNote restoredNote = (ItemNote)restoredAuthor.Inventory.GetFirstMatching(it =>
                    it is ItemNote && ((ItemNote)it).CacheId == restoredFridge.Id);
                Check.Equal(id, restoredNote.Id, "note identity survives save");
                Check.Equal(text, restoredNote.Text, "written text survives save unchanged");
                Check.Equal(restoredFridge.Id, restoredNote.CacheId, "cache reference stays physical ID");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
