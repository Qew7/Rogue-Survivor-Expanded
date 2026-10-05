using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using djack.RogueSurvivor.Data;

sealed class SaveTileAudit
{
    static readonly FieldInfo Model = typeof(Tile).GetField("m_ModelID", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo Flags = typeof(Tile).GetField("m_Flags", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo Decorations = typeof(Tile).GetField("m_Decorations", BindingFlags.Instance | BindingFlags.NonPublic);
    readonly Dictionary<string, int> patterns = new Dictionary<string, int>(StringComparer.Ordinal);
    readonly Dictionary<string, int> decorationPatterns = new Dictionary<string, int>(StringComparer.Ordinal);
    readonly Dictionary<object, int> decorationIds = new Dictionary<object, int>(new SaveGraphAudit.References());
    readonly Dictionary<string, int> terrains = new Dictionary<string, int>();
    public long Cells, Runs, DecoratedCells, PaletteBytes;
    public int Patterns { get { return patterns.Count; } }
    public int DecorationPatterns { get { return decorationPatterns.Count; } }
    public int EqualTerrainGroups, MapsInEqualTerrainGroups;

    public void Add(Map map)
    {
        using (MemoryStream terrain = new MemoryStream())
        {
            BinaryWriter writer = new BinaryWriter(terrain);
            writer.Write(map.Width); writer.Write(map.Height);
            int previous = -1;
            for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                {
                    Tile tile = map.GetTileAt(x, y);
                    if (tile == null) throw new InvalidDataException("Missing tile in " + map.Name);
                    int model = (int)Model.GetValue(tile), flags = Convert.ToInt32(Flags.GetValue(tile));
                    object decorations = Decorations.GetValue(tile);
                    int decoration = DecorationId((List<string>)decorations);
                    if (decorations != null) DecoratedCells++;
                    string key = model + "/" + flags + "/" + decoration;
                    int id;
                    if (!patterns.TryGetValue(key, out id))
                    {
                        id = patterns.Count; patterns.Add(key, id); PaletteBytes += 12;
                    }
                    writer.Write(id); Cells++;
                    if (id != previous) Runs++;
                    previous = id;
                }
            terrain.Position = 0;
            using (SHA256 hash = SHA256.Create())
            {
                string digest = Convert.ToBase64String(hash.ComputeHash(terrain));
                int count; terrains.TryGetValue(digest, out count); terrains[digest] = count + 1;
            }
        }
    }
    int DecorationId(List<string> decorations)
    {
        if (decorations == null) return -1;
        int id;
        if (decorationIds.TryGetValue(decorations, out id)) return id;
        using (MemoryStream stream = new MemoryStream())
        {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(decorations.Count);
            foreach (string text in decorations)
            {
                writer.Write(text != null);
                if (text != null) writer.Write(text);
            }
            string key = Convert.ToBase64String(stream.ToArray());
            if (!decorationPatterns.TryGetValue(key, out id))
            {
                id = decorationPatterns.Count; decorationPatterns.Add(key, id);
                PaletteBytes += stream.Length;
            }
        }
        decorationIds.Add(decorations, id);
        return id;
    }
    public void Finish()
    {
        EqualTerrainGroups = MapsInEqualTerrainGroups = 0;
        foreach (int count in terrains.Values)
            if (count > 1) { EqualTerrainGroups++; MapsInEqualTerrainGroups += count; }
    }
}
