using System;
using System.Collections.Generic;

namespace djack.RogueSurvivor.Engine
{
    static class TextLayout
    {
        // Measure with the font used to draw the text, rather than counting characters.
        public static List<string> Wrap(string text, int maxWidth, Func<string, int> measure)
        {
            if (text == null) throw new ArgumentNullException("text");
            if (maxWidth <= 0) throw new ArgumentOutOfRangeException("maxWidth");
            List<string> lines = new List<string>();
            foreach (string paragraph in text.Replace("\r\n", "\n").Split('\n'))
            {
                string rest = paragraph;
                while (measure(rest) > maxWidth)
                {
                    int low = 1;
                    int high = rest.Length;
                    while (low < high)
                    {
                        int middle = low + (high - low + 1) / 2;
                        if (measure(rest.Substring(0, middle)) <= maxWidth) low = middle;
                        else high = middle - 1;
                    }
                    int split = rest.LastIndexOf(' ', Math.Max(0, low - 1), low);
                    if (split <= 0) split = low;
                    lines.Add(rest.Substring(0, split).TrimEnd());
                    rest = rest.Substring(split).TrimStart();
                }
                lines.Add(rest);
            }
            return lines;
        }
    }
}
