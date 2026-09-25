using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace djack.RogueSurvivor.Engine
{
    [DataContract]
    class ModInfo
    {
        public string Name { get; internal set; }
        public string DirectoryPath { get; internal set; }
        [DataMember(Name = "author")]
        public string Author { get; set; }
        [DataMember(Name = "authors")]
        public string[] Authors { get; set; }
        [DataMember(Name = "website")]
        public string Website { get; set; }
        [DataMember(Name = "websites")]
        public string[] Websites { get; set; }
        [DataMember(Name = "description")]
        public string Description { get; set; }
        [DataMember(Name = "version")]
        public string Version { get; set; }
        [DataMember(Name = "game_version")]
        public string GameVersion { get; set; }

        public bool SupportsCurrentGame
        {
            get { return String.IsNullOrWhiteSpace(GameVersion) ||
                SetupConfig.SupportsGameVersion(GameVersion.Trim()); }
        }

        public string[] GetAuthors() { return Values(Authors, Author); }
        public string[] GetWebsites() { return Values(Websites, Website); }

        static string[] Values(string[] multiple, string single)
        {
            List<string> values = new List<string>();
            if (multiple != null)
                foreach (string value in multiple)
                    if (!String.IsNullOrWhiteSpace(value)) values.Add(value.Trim());
            if (values.Count == 0 && !String.IsNullOrWhiteSpace(single))
                values.Add(single.Trim());
            return values.ToArray();
        }
    }

    // Stable identity stored in saves; paths are deliberately machine-specific and omitted.
    [DataContract]
    class ModStamp
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }
        [DataMember(Name = "version")]
        public string Version { get; set; }
    }

    // The menu's default profile is independent of the temporary set used by a save.
    static class ModProfileStore
    {
        public static ModInfo[] Load(string path, ModInfo[] available)
        {
            if (!File.Exists(path)) return new ModInfo[0];
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    ModStamp[] stamps = (ModStamp[])new DataContractJsonSerializer(
                        typeof(ModStamp[])).ReadObject(stream);
                    List<ModInfo> chosen = new List<ModInfo>();
                    foreach (ModStamp stamp in stamps)
                    {
                        ModInfo mod = Array.Find(available, candidate =>
                            String.Equals(candidate.Name, stamp.Name,
                                StringComparison.OrdinalIgnoreCase));
                        if (mod != null && mod.SupportsCurrentGame && !chosen.Contains(mod))
                            chosen.Add(mod);
                    }
                    return chosen.ToArray();
                }
            }
            catch (Exception) { return new ModInfo[0]; }
        }

        public static void Save(string path, ModInfo[] selected)
        {
            string temporary = path + ".tmp";
            try
            {
                using (FileStream stream = File.Create(temporary))
                    new DataContractJsonSerializer(typeof(ModStamp[])).WriteObject(stream,
                        ModCatalog.Stamps(selected));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    static class ModCatalog
    {
        static ModInfo[] selected = new ModInfo[0];
        public static ModInfo Active { get { return selected.Length == 0 ? null : selected[0]; } }
        public static ModInfo[] Selected { get { return (ModInfo[])selected.Clone(); } }

        public static ModInfo[] Discover(string root)
        {
            List<ModInfo> mods = new List<ModInfo>();
            if (!Directory.Exists(root)) return mods.ToArray();
            foreach (string directory in Directory.GetDirectories(root))
            {
                if (Path.GetFileName(directory).StartsWith(".", StringComparison.Ordinal)) continue;
                ModInfo mod = new ModInfo {
                    Name = Path.GetFileName(directory), DirectoryPath = directory
                };
                string metadata = Path.Combine(directory, "authors.json");
                if (File.Exists(metadata))
                {
                    try
                    {
                        using (FileStream stream = File.OpenRead(metadata))
                        {
                            ModInfo details = (ModInfo)new DataContractJsonSerializer(typeof(ModInfo))
                                .ReadObject(stream);
                            mod.Author = details.Author;
                            mod.Authors = details.Authors;
                            mod.Website = details.Website;
                            mod.Websites = details.Websites;
                            mod.Description = details.Description;
                            mod.Version = details.Version;
                            mod.GameVersion = details.GameVersion;
                        }
                    }
                    catch (Exception)
                    {
                        mod.Description = "Cannot read authors.json; mod files can still be used.";
                    }
                }
                mods.Add(mod);
            }
            mods.Sort((a, b) => String.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return mods.ToArray();
        }

        // Mods are ordered from highest to lowest priority.
        public static void Select(params ModInfo[] mods)
        {
            selected = mods == null ? new ModInfo[0] : (ModInfo[])mods.Clone();
        }

        public static ModStamp[] Stamps(ModInfo[] mods)
        {
            ModStamp[] stamps = new ModStamp[mods.Length];
            for (int i = 0; i < mods.Length; i++)
                stamps[i] = new ModStamp { Name = mods[i].Name,
                    Version = mods[i].Version ?? String.Empty };
            return stamps;
        }

        public static ModInfo[] Match(ModStamp[] stamps, ModInfo[] available,
            out ModStamp[] missing)
        {
            List<ModInfo> found = new List<ModInfo>();
            List<ModStamp> absent = new List<ModStamp>();
            foreach (ModStamp stamp in stamps)
            {
                ModInfo mod = Array.Find(available, candidate =>
                    String.Equals(candidate.Name, stamp.Name, StringComparison.OrdinalIgnoreCase) &&
                    candidate.SupportsCurrentGame &&
                    (String.IsNullOrEmpty(stamp.Version) ||
                     String.Equals(candidate.Version ?? String.Empty, stamp.Version,
                         StringComparison.OrdinalIgnoreCase)));
                if (mod == null) absent.Add(stamp);
                else found.Add(mod);
            }
            missing = absent.ToArray();
            return found.ToArray();
        }

        public static string Resolve(string category, string relativeName)
        {
            string name = relativeName.Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
            foreach (ModInfo mod in selected)
            {
                string overridePath = Path.Combine(mod.DirectoryPath, category, name);
                if (File.Exists(overridePath)) return overridePath;
            }
            return Path.Combine("Resources", category, name);
        }
    }

    // Keeps enabled mods at the front, in priority order, followed by disabled mods.
    class ModLoadOrder
    {
        readonly List<ModInfo> mods;
        int enabledCount;

        public ModLoadOrder(ModInfo[] discovered) : this(discovered, new ModInfo[0]) { }

        public ModLoadOrder(ModInfo[] discovered, ModInfo[] selected)
        {
            mods = new List<ModInfo>(discovered);
            foreach (ModInfo active in selected)
            {
                int index = mods.FindIndex(mod => String.Equals(mod.DirectoryPath,
                    active.DirectoryPath, StringComparison.OrdinalIgnoreCase));
                if (index < 0) continue;
                ModInfo modInfo = mods[index];
                mods.RemoveAt(index);
                mods.Insert(enabledCount++, modInfo);
            }
        }
        public int Count { get { return mods.Count; } }
        public int EnabledCount { get { return enabledCount; } }
        public ModInfo this[int index] { get { return mods[index]; } }
        public bool IsEnabled(int index) { return index < enabledCount; }

        public int Toggle(int index)
        {
            ModInfo mod = mods[index];
            mods.RemoveAt(index);
            if (index < enabledCount)
            {
                enabledCount--;
                mods.Add(mod);
                return mods.Count - 1;
            }
            if (!mod.SupportsCurrentGame)
            {
                mods.Insert(index, mod);
                return index;
            }
            mods.Insert(enabledCount, mod);
            return enabledCount++;
        }

        public int Move(int index, int direction)
        {
            int destination = index + direction;
            if (index >= enabledCount || destination < 0 || destination >= enabledCount)
                return index;
            ModInfo mod = mods[index];
            mods[index] = mods[destination];
            mods[destination] = mod;
            return destination;
        }

        public ModInfo[] Selected()
        {
            return mods.GetRange(0, enabledCount).ToArray();
        }
    }
}
