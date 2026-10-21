using System;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;

static class PaperNoteTooltipWrapScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("items/paper-note-tooltip-wrap", () => TownScenarioFactory.Arena(4880,
            ".....", ".....", "....."), world =>
        {
            Actor author = NpcIntentSupport.Actor(world, "Brent Gonzales", 1, 1, "kind");
            world.SetPlayer(author);
            author.Personality.AddMemory(new MemoryInstance("heard_a_report", 0, 1,
                "Cop Armando Ramirez"));
            ItemNote note = new ItemNote(world.Game.GameItems.NOTE);
            author.Inventory.AddAll(note);
            MethodInfo describe = typeof(RogueGame).GetMethod("DescribeItemLong",
                BindingFlags.Instance | BindingFlags.NonPublic);
            string[] blank = (string[])describe.Invoke(world.Game, new object[] { note, true, 0 });
            Check.Equal(true, Array.IndexOf(blank, "Blank paper.") >= 0,
                "blank paper keeps its ordinary tooltip");
            Check.Equal(true, world.Try(new ActionWriteNote(author, world.Game, note,
                NoteTopic.Location, signed: true)), "author writes a real note");
            string original = note.Text;
            string[] lines = (string[])describe.Invoke(world.Game, new object[] { note, true, 0 });
            int first = Array.IndexOf(lines, "To the Civilians community,");
            int last = Array.IndexOf(lines, author.UnmodifiedName);
            Check.Equal(true, first >= 0 && last - first > 3,
                "letter has an addressee, paragraphs and a signature");
            for (int i = first; i < last; i++)
                Check.Equal(true, lines[i].Length <= 45,
                    "each letter line fits the inventory popup");
            Check.Equal(true, Array.IndexOf(lines, "Written note:") < 0 &&
                Array.IndexOf(lines, "Signed: " + author.UnmodifiedName) < 0 &&
                Array.IndexOf(lines, "A loose sheet of paper.") < 0,
                "written letter has no duplicate item metadata");
            Check.Equal(true, original.Contains("Cop Armando Ramirez told me what happened") &&
                !original.Contains("Was told a report about events"),
                "reported memory reads as a sentence from the author");
            Check.Equal(original, note.Text, "display does not edit the physical note");
        });
    }
}
