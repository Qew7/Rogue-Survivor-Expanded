using System;

static partial class NpcSafetyBenchmarks
{
    static string[] Rows(string layout)
    {
        var rows = new string[7];
        for (int y = 0; y < rows.Length; y++)
        {
            var row = new char[13];
            for (int x = 0; x < 12; x++) row[x] = layout == "open" || layout == "corner" ? (x == 11 ? '#' : '.') : '#';
            row[12] = '.'; // player isolated from the encounter by x=11 wall
            if (layout != "open" && layout != "corner")
            {
                for (int x = 0; x <= 10; x++) if (y == 3) row[x] = '.';
                if (layout == "branch" && y >= 1 && y <= 2)
                    for (int x = 0; x <= 3; x++) row[x] = '.';
                if ((layout == "turn_exit" && y >= 1 && y <= 2) ||
                    (layout == "long_turn_exit" && y <= 2)) row[4] = '.';
            }
            if (layout == "corner" && y >= 1 && y <= 4) row[6] = '#';
            rows[y] = new string(row);
        }
        return rows;
    }

}
