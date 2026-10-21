using System.Windows.Forms;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;

static class PaperNoteInputScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("items/paper-note-input", () => TownScenarioFactory.Arena(4736,
            ".....", ".....", "....."), world =>
        {
            Actor player = SkillScenario.Actor(world);
            world.Place(player, 1, 1);
            world.SetPlayer(player);
            ItemNote note = new ItemNote(world.Game.GameItems.NOTE);
            player.Inventory.AddAll(note);
            ScenarioUI ui = (ScenarioUI)world.Game.UI;
            ui.QueueWaitKey(Keys.Escape);
            Check.Equal(false, Check.Call(world.Game, "OnLMBItem",
                new[] { typeof(Inventory), typeof(Item) }, player.Inventory, note),
                "cancel leaves writing turn unused");
            Check.Equal(true, note.IsBlank, "cancel leaves paper blank");
            ui.QueueWaitKey(Keys.D3);
            Check.Equal(true, Check.Call(world.Game, "OnLMBItem",
                new[] { typeof(Inventory), typeof(Item) }, player.Inventory, note),
                "anonymous location topic writes through inventory");
            Check.Equal(null, note.Signature, "anonymous selection hides signature");
            Check.Equal(false, note.IsBlank, "selected topic writes note");
        });
    }
}
