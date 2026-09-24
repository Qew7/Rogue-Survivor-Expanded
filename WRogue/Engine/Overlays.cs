using System.Drawing;

namespace djack.RogueSurvivor.Engine
{
    #region Overlays
    abstract class Overlay
    {
        public abstract void Draw(IRogueUI ui);
    }

    class OverlayImage : Overlay
    {
        public Point ScreenPosition { get; set; }
        public string ImageID { get; set; }

        public OverlayImage(Point screenPosition, string imageID)
        {
            this.ScreenPosition = screenPosition;
            this.ImageID = imageID;
        }

        public override void Draw(IRogueUI ui)
        {
            ui.UI_DrawImage(ImageID, ScreenPosition.X, ScreenPosition.Y);
        }
    }

    class OverlayTransparentImage : Overlay
    {
        public float Alpha { get; set; }
        public Point ScreenPosition { get; set; }
        public string ImageID { get; set; }

        public OverlayTransparentImage(float alpha, Point screenPosition, string imageID)
        {
            this.Alpha = alpha;
            this.ScreenPosition = screenPosition;
            this.ImageID = imageID;
        }

        public override void Draw(IRogueUI ui)
        {
            ui.UI_DrawTransparentImage(Alpha, ImageID, ScreenPosition.X, ScreenPosition.Y);
        }
    }

    class OverlayText : Overlay
    {
        public Point ScreenPosition { get; set; }
        public string Text { get; set; }
        public Color Color { get; set; }
        public Color? ShadowColor { get; set; }

        public OverlayText(Point screenPosition, Color color, string text)
            : this(screenPosition, color, text, null)
        {
        }

        public OverlayText(Point screenPosition, Color color, string text, Color? shadowColor)
        {
            this.ScreenPosition = screenPosition;
            this.Color = color;
            this.ShadowColor = shadowColor;
            this.Text = text;
        }

        public override void Draw(IRogueUI ui)
        {
            if (ShadowColor.HasValue)
                ui.UI_DrawString(ShadowColor.Value, Text, ScreenPosition.X + 1, ScreenPosition.Y + 1);
            ui.UI_DrawString(Color, Text, ScreenPosition.X, ScreenPosition.Y);
        }
    }

    class OverlayLine : Overlay
    {
        public Point ScreenFrom { get; set; }
        public Point ScreenTo { get; set; }
        public Color Color { get; set; }

        public OverlayLine(Point screenFrom, Color color, Point screenTo)
        {
            ScreenFrom = screenFrom;
            ScreenTo = screenTo;
            Color = color;
        }

        public override void Draw(IRogueUI ui)
        {
            ui.UI_DrawLine(Color, ScreenFrom.X, ScreenFrom.Y, ScreenTo.X, ScreenTo.Y);
        }
    }

    class OverlayRect : Overlay
    {
        public Rectangle Rectangle { get; set; }
        public Color Color { get; set; }

        public OverlayRect(Color color, Rectangle rect)
        {
            this.Rectangle = rect;
            this.Color = color;
        }

        public override void Draw(IRogueUI ui)
        {
            ui.UI_DrawRect(this.Color, this.Rectangle);
        }
    }

    class OverlayPopup : Overlay
    {
        public Point ScreenPosition { get; set; }
        public Color TextColor { get; set; }
        public Color BoxBorderColor { get; set; }
        public Color BoxFillColor { get; set; }
        public string[] Lines { get; set; }

        /// <summary>
        ///
        /// </summary>
        /// <param name="lines">can be null if want to set text property later</param>
        /// <param name="textColor"></param>
        /// <param name="boxBorderColor"></param>
        /// <param name="boxFillColor"></param>
        /// <param name="screenPos"></param>
        public OverlayPopup(string[] lines, Color textColor, Color boxBorderColor, Color boxFillColor, Point screenPos)
        {
            this.ScreenPosition = screenPos;
            this.TextColor = textColor;
            this.BoxBorderColor = boxBorderColor;
            this.BoxFillColor = boxFillColor;
            this.Lines = lines;
        }

        public override void Draw(IRogueUI ui)
        {
            ui.UI_DrawPopup(Lines, TextColor, BoxBorderColor, BoxFillColor, ScreenPosition.X, ScreenPosition.Y);
        }
    }

    // alpha10

    class OverlayPopupTitle : Overlay
    {
        public Point ScreenPosition { get; set; }
        public string Title { get; set; }
        public Color TitleColor { get; set; }
        public string[] Lines { get; set; }
        public Color TextColor { get; set; }
        public Color BoxBorderColor { get; set; }
        public Color BoxFillColor { get; set; }

        public OverlayPopupTitle(string title, Color titleColor, string[] lines, Color textColor, Color boxBorderColor, Color boxFillColor, Point screenPos)
        {
            this.ScreenPosition = screenPos;
            this.Title = title;
            this.TitleColor = titleColor;
            this.TextColor = textColor;
            this.BoxBorderColor = boxBorderColor;
            this.BoxFillColor = boxFillColor;
            this.Lines = lines;
        }

        public override void Draw(IRogueUI ui)
        {
            ui.UI_DrawPopupTitle(Title, TitleColor, Lines, TextColor, BoxBorderColor, BoxFillColor, ScreenPosition.X, ScreenPosition.Y);
        }
    }

    class OverlayPopupTitleColors : Overlay
    {
        public Point ScreenPosition { get; set; }
        public string Title { get; set; }
        public Color TitleColor { get; set; }
        public string[] Lines { get; set; }
        public Color[] Colors { get; set; }
        public Color BoxBorderColor { get; set; }
        public Color BoxFillColor { get; set; }

        public OverlayPopupTitleColors(string title, Color titleColor, string[] lines, Color[] colors, Color boxBorderColor, Color boxFillColor, Point screenPos)
        {
            this.ScreenPosition = screenPos;
            this.Title = title;
            this.TitleColor = titleColor;
            this.Colors = colors;
            this.BoxBorderColor = boxBorderColor;
            this.BoxFillColor = boxFillColor;
            this.Lines = lines;
        }

        public override void Draw(IRogueUI ui)
        {
            ui.UI_DrawPopupTitleColors(Title, TitleColor, Lines, Colors, BoxBorderColor, BoxFillColor, ScreenPosition.X, ScreenPosition.Y);
        }
    }
    #endregion

}
