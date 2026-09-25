using djack.RogueSurvivor.Engine;

static class OverlayCollectionTests
{
    sealed class CountingOverlay : Overlay
    {
        public int Draws;
        public System.Action OnDraw;
        public override void Draw(IRogueUI ui)
        {
            Draws++;
            if (OnDraw != null) OnDraw();
        }
    }

    public static void Run()
    {
        OverlayCollection collection = new OverlayCollection();
        CountingOverlay first = new CountingOverlay();
        CountingOverlay addedDuringDraw = new CountingOverlay();
        first.OnDraw = () => { collection.Add(addedDuringDraw); first.OnDraw = null; };
        collection.Add(first);
        collection.Draw(new ScenarioUI());
        Check.Equal(1, first.Draws, "existing overlay drawn");
        Check.Equal(0, addedDuringDraw.Draws, "mutation waits for next snapshot");
        collection.Draw(new ScenarioUI());
        Check.Equal(1, addedDuringDraw.Draws, "new overlay drawn next frame");
        collection.Remove(first);
        collection.Draw(new ScenarioUI());
        Check.Equal(2, first.Draws, "removed overlay stays removed");
        collection.Clear();
        collection.Draw(new ScenarioUI());
        Check.Equal(2, addedDuringDraw.Draws, "clear drops overlays");
    }
}
