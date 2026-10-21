using System;
using System.Security.Cryptography;
using System.Text;

static class ModelIdTests
{
    public static void Run()
    {
        CheckCatalog("Gameplay.GameItems+IDs", "ecf2217c12619c5b1fdcc3cfdebcbd7c6db57ed71d6dad63e9fdbf3f0d477798");
        CheckCatalog("Gameplay.GameActors+IDs", "6b6eb9bbcec7c8145bdc191511a2b0515bde6d19aea8c58233659ac7809e0876");
        CheckCatalog("Gameplay.GameTiles+IDs", "a0b745cbe47a8c9d8c49033cae06b5fdaf7a1cf13a48cd8a9fa10e0d3d50d780");
    }

    static void CheckCatalog(string name, string expected)
    {
        Type type = Check.Type(name);
        string[] names = Enum.GetNames(type);
        Array.Sort(names, StringComparer.Ordinal);
        StringBuilder data = new StringBuilder();
        foreach (string entry in names)
            data.Append(entry).Append('=').Append(Convert.ToInt32(Enum.Parse(type, entry))).Append('\n');
        byte[] hash = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(data.ToString()));
        string actual = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        Check.Equal(expected, actual, name + " saved model IDs changed; add a save migration before updating this snapshot");
    }
}
