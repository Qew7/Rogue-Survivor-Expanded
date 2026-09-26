using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using djack.RogueSurvivor.Engine;

// UI calls are inert in headless scenario setup; unexpected input fails fast.
sealed class ScenarioUI : IRogueUI
{
    readonly Queue<KeyEventArgs> keys = new Queue<KeyEventArgs>();
    readonly Queue<MouseButtons?> buttons = new Queue<MouseButtons?>();
    public readonly Dictionary<Point, Color> MinimapColors = new Dictionary<Point, Color>();
    public readonly List<string> DrawnImages = new List<string>();
    public readonly List<string> DrawnStrings = new List<string>();
    public int MinimapTileWrites { get; private set; }
    public Point MousePosition { get; set; }
    public void QueueKey(Keys key) { keys.Enqueue(null); keys.Enqueue(new KeyEventArgs(key)); }
    public void QueueClick(MouseButtons button) { buttons.Enqueue(button); }
    public KeyEventArgs UI_WaitKey() { throw new InvalidOperationException("Scenario requested keyboard input"); }
    public KeyEventArgs UI_PeekKey() { return keys.Count == 0 ? null : keys.Dequeue(); }
    public void UI_PostKey(KeyEventArgs e) { }
    public Point UI_GetMousePosition() { return MousePosition; }
    public MouseButtons? UI_PeekMouseButtons() { return buttons.Count == 0 ? null : buttons.Dequeue(); }
    public void UI_PostMouseButtons(MouseButtons buttons) { }
    public void UI_SetCursor(Cursor cursor) { }
    public void UI_Wait(int msecs) { }
    public void UI_Repaint() { }
    public void UI_Clear(Color color) { }
    public void UI_DrawImage(string id, int x, int y) { DrawnImages.Add(id); }
    public void UI_DrawImage(string id, int x, int y, Color tint) { }
    public void UI_DrawImageTransform(string id, int x, int y, float rotation, float scale) { }
    public void UI_DrawGrayLevelImage(string id, int x, int y) { }
    public void UI_DrawTransparentImage(float alpha, string id, int x, int y) { }
    public void UI_DrawPoint(Color color, int x, int y) { }
    public void UI_DrawLine(Color color, int x1, int y1, int x2, int y2) { }
    public void UI_DrawRect(Color color, Rectangle rect) { }
    public void UI_FillRect(Color color, Rectangle rect) { }
    public void UI_DrawString(Color color, string text, int x, int y, Color? shadow = null) { }
    public void UI_DrawStringBold(Color color, string text, int x, int y, Color? shadow = null)
    { DrawnStrings.Add(text); }
    public void UI_DrawPopup(string[] lines, Color text, Color border, Color fill, int x, int y) { }
    public void UI_DrawPopupTitle(string title, Color titleColor, string[] lines, Color text, Color border, Color fill, int x, int y) { }
    public void UI_DrawPopupTitleColors(string title, Color titleColor, string[] lines, Color[] colors, Color border, Color fill, int x, int y) { }
    public void UI_ClearMinimap(Color color) { MinimapColors.Clear(); MinimapTileWrites = 0; }
    public void UI_SetMinimapColor(int x, int y, Color color) { MinimapColors[new Point(x, y)] = color; MinimapTileWrites++; }
    public void UI_DrawMinimap(int x, int y) { }
    public float UI_GetCanvasScaleX() { return 1; }
    public float UI_GetCanvasScaleY() { return 1; }
    public string UI_SaveScreenshot(string path) { throw new InvalidOperationException("Scenario requested screenshot"); }
    public string UI_ScreenshotExtension() { return "png"; }
    public void UI_DoQuit() { throw new InvalidOperationException("Scenario requested quit"); }
}
