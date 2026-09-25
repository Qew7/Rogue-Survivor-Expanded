using System;
using System.Collections.Generic;
using System.IO;

namespace djack.RogueSurvivor.Engine
{
    [Serializable]
    sealed class GamePreset
    {
        public string Name;
        public bool Bases;
        public bool ImmediateZombification;
        public bool Infection;
        public bool Corpses;
        public bool Evolution;
        public bool Skeletons;
        public bool Shamblers;
        public bool ZombieMasters;
        public bool Zombified;
        public bool RatZombies;
        public bool ZombiesInBasements;
        public bool ZombiesInSewers;
        public int HungerThreshold;
        public int SleepThreshold;
        public int SanityThreshold;
        public int RotThreshold;
        public int CorpseDecayPercent;
        public int CorpseRiseChance;
        public int CorpseBaseRiseChance;
        public int InfectionRatePercent;
        public int RotDecayPercent;
        public int InfectionWeakThreshold;
        public int InfectionTiredThreshold;
        public int InfectionVomitThreshold;
        public int InfectionBleedThreshold;
        public int InfectionDeathThreshold;
        public int InfectionEffectRatePercent;
        [System.Runtime.Serialization.OptionalField]
        public GameOptions Options;
        [System.Runtime.Serialization.OptionalField]
        public bool HasOptions;

        public int HungerPoints { get { return Rules.FOOD_BASE_POINTS * HungerThreshold / 100; } }
        public int SleepPoints { get { return Rules.SLEEP_BASE_POINTS * SleepThreshold / 100; } }
        public int SanityPoints { get { return Rules.SANITY_BASE_POINTS * SanityThreshold / 100; } }
        public int RotPoints { get { return Rules.ROT_BASE_POINTS * RotThreshold / 100; } }

        public GamePreset Copy()
        {
            return (GamePreset)MemberwiseClone();
        }

        public static GamePreset BuiltIn(GameMode mode)
        {
            GamePreset preset = new GamePreset {
                Name = Session.DescGameMode(mode),
                Bases = mode == GameMode.GM_XPD,
                ImmediateZombification = mode == GameMode.GM_STANDARD,
                Infection = mode != GameMode.GM_STANDARD,
                Corpses = mode != GameMode.GM_STANDARD,
                Evolution = mode != GameMode.GM_VINTAGE,
                Skeletons = mode != GameMode.GM_VINTAGE,
                Shamblers = mode != GameMode.GM_VINTAGE,
                ZombieMasters = mode != GameMode.GM_VINTAGE,
                Zombified = mode == GameMode.GM_VINTAGE,
                RatZombies = mode != GameMode.GM_VINTAGE,
                ZombiesInBasements = mode != GameMode.GM_VINTAGE,
                ZombiesInSewers = mode != GameMode.GM_VINTAGE,
                HungerThreshold = 50,
                SleepThreshold = 50,
                SanityThreshold = 50,
                RotThreshold = 50,
                CorpseDecayPercent = 100,
                CorpseRiseChance = 100,
                CorpseBaseRiseChance = 0,
                InfectionRatePercent = 100,
                RotDecayPercent = 100,
                InfectionWeakThreshold = 10,
                InfectionTiredThreshold = 30,
                InfectionVomitThreshold = 50,
                InfectionBleedThreshold = 75,
                InfectionDeathThreshold = 100,
                InfectionEffectRatePercent = 100
            };
            return preset;
        }

        public void Validate()
        {
            if (String.IsNullOrWhiteSpace(Name) || Name.Length > 40)
                throw new ArgumentException("Preset name must contain 1 to 40 characters.");
            if (!Skeletons && !Shamblers && !ZombieMasters && !Zombified && !RatZombies)
                throw new ArgumentException("At least one zombie type must be enabled.");
            if (HasOptions && !Zombified && !RatZombies &&
                (!Skeletons || Options.SpawnSkeletonChance == 0) &&
                (!Shamblers || Options.SpawnZombieChance == 0) &&
                (!ZombieMasters || Options.SpawnZombieMasterChance == 0))
                throw new ArgumentException("At least one enabled zombie needs a positive spawn weight.");
            if (HungerThreshold < 0 || HungerThreshold > 100 || SleepThreshold < 0 || SleepThreshold > 100 ||
                SanityThreshold < 0 || SanityThreshold > 100 || RotThreshold < 0 || RotThreshold > 100 ||
                CorpseDecayPercent < 0 || CorpseDecayPercent > 500 ||
                CorpseRiseChance < 0 || CorpseRiseChance > 500 ||
                CorpseBaseRiseChance < 0 || CorpseBaseRiseChance > 100 ||
                InfectionRatePercent < 0 || InfectionRatePercent > 500 ||
                RotDecayPercent < 0 || RotDecayPercent > 500 ||
                InfectionEffectRatePercent < 0 || InfectionEffectRatePercent > 500 ||
                InfectionWeakThreshold < 0 || InfectionDeathThreshold > 100 ||
                InfectionWeakThreshold > InfectionTiredThreshold ||
                InfectionTiredThreshold > InfectionVomitThreshold ||
                InfectionVomitThreshold > InfectionBleedThreshold ||
                InfectionBleedThreshold > InfectionDeathThreshold)
                throw new ArgumentException("Preset value is out of range.");
        }
    }

    [Serializable]
    sealed class GamePresetCollection
    {
        public List<GamePreset> Presets = new List<GamePreset>();

        public static GamePresetCollection Load(string path)
        {
            if (!File.Exists(path)) return new GamePresetCollection();
            GamePresetCollection collection = (GamePresetCollection)BinarySaveStore.Load(path, null);
            if (collection == null || collection.Presets == null) return new GamePresetCollection();
            foreach (GamePreset preset in collection.Presets) preset.Validate();
            return collection;
        }

        public void Save(string path)
        {
            foreach (GamePreset preset in Presets) preset.Validate();
            BinarySaveStore.Save(path, this);
        }

        public void AddOrReplace(GamePreset preset)
        {
            preset.Validate();
            foreach (GameMode mode in new[] { GameMode.GM_STANDARD, GameMode.GM_CORPSES_INFECTION,
                GameMode.GM_VINTAGE, GameMode.GM_XPD })
                if (String.Equals(preset.Name, GamePreset.BuiltIn(mode).Name, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Built-in preset names cannot be replaced.");
            for (int i = 0; i < Presets.Count; i++)
                if (String.Equals(Presets[i].Name, preset.Name, StringComparison.OrdinalIgnoreCase))
                {
                    Presets[i] = preset.Copy();
                    return;
                }
            Presets.Add(preset.Copy());
        }
    }
}
