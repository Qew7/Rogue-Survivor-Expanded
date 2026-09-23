using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Drawing;

using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay
{
    partial class GameItems
    {
        #region Data loading

        #region Helpers
        void Notify(IRogueUI ui, string what, string stage)
        {
            ui.UI_Clear(Color.Black);
            ui.UI_DrawStringBold(Color.White, "Loading "+what+" data : " + stage, 0, 0);
            ui.UI_Repaint();
        }

        CSVLine FindLineForModel(CSVTable table, IDs modelID)
        {
            foreach (CSVLine l in table.Lines)
            {
                if (l[0].ParseText() == modelID.ToString())
                    return l;
            }

            return null;
        }

        _DATA_TYPE_ GetDataFromCSVTable<_DATA_TYPE_>(IRogueUI ui, CSVTable table, Func<CSVLine, _DATA_TYPE_> fn, IDs modelID)
        {
            // get line for model in table.
            CSVLine line = FindLineForModel(table, modelID);
            if (line == null)
                throw new InvalidOperationException(String.Format("model {0} not found", modelID.ToString()));

            // get data from line.
            _DATA_TYPE_ data;
            try
            {
                data = fn(line);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(String.Format("invalid data format for model {0}; exception : {1}", modelID.ToString(), e.ToString()));
            }

            // ok.
            return data;
        }

        bool LoadDataFromCSV<_DATA_TYPE_>(IRogueUI ui, string path, string kind, int fieldsCount, Func<CSVLine, _DATA_TYPE_> fn, IDs[] idsToRead, out _DATA_TYPE_[] data)
        {
            //////////////////////////
            // Read & parse csv file.
            //////////////////////////
            Notify(ui, kind, "loading file...");
            // read the whole file.
            List<string> allLines = new List<string>();
            bool ignoreHeader = true;
            using (StreamReader reader = File.OpenText(path))
            {
                while (!reader.EndOfStream)
                {
                    string inLine = reader.ReadLine();
                    if (ignoreHeader)
                    {
                        ignoreHeader = false;
                        continue;
                    }
                    allLines.Add(inLine);
                }
                reader.Close();
            }
            // parse all the lines read.
            Notify(ui, kind, "parsing CSV...");
            CSVParser parser = new CSVParser();
            CSVTable table = parser.ParseToTable(allLines.ToArray(), fieldsCount);

            /////////////
            // Set data.
            /////////////
            Notify(ui, kind, "reading data...");

            data = new _DATA_TYPE_[idsToRead.Length];
            for (int i = 0; i < idsToRead.Length; i++)
            {
                data[i] = GetDataFromCSVTable<_DATA_TYPE_>(ui, table, fn, idsToRead[i]);
            }

            //////////////
            // all fine.
            /////////////
            Notify(ui, kind, "done!");
            return true;
        }
        #endregion

        #region Medecine
        public bool LoadMedicineFromCSV(IRogueUI ui, string path)
        {
            MedecineData[] data;

            LoadDataFromCSV<MedecineData>(ui, path, "medicine items", MedecineData.COUNT_FIELDS, MedecineData.FromCSVLine,
                new IDs[] { IDs.MEDICINE_BANDAGES, IDs.MEDICINE_MEDIKIT, IDs.MEDICINE_PILLS_SLP, IDs.MEDICINE_PILLS_STA,  IDs.MEDICINE_PILLS_SAN,
                            IDs.MEDICINE_PILLS_ANTIVIRAL },
                out data);

            DATA_MEDICINE_BANDAGE = data[0];
            DATA_MEDICINE_MEDIKIT = data[1];
            DATA_MEDICINE_PILLS_SLP = data[2];
            DATA_MEDICINE_PILLS_STA = data[3];
            DATA_MEDICINE_PILLS_SAN = data[4];
            DATA_MEDICINE_PILLS_ANTIVIRAL = data[5];

            return true;
        }
        #endregion

        #region Food
        public bool LoadFoodFromCSV(IRogueUI ui, string path)
        {
            FoodData[] data;

            LoadDataFromCSV<FoodData>(ui, path, "food items", FoodData.COUNT_FIELDS, FoodData.FromCSVLine,
                new IDs[] { IDs.FOOD_ARMY_RATION, IDs.FOOD_CANNED_FOOD, IDs.FOOD_GROCERIES },
                out data);

            DATA_FOOD_ARMY_RATION = data[0];
            DATA_FOOD_CANNED_FOOD = data[1];
            DATA_FOOD_GROCERIES = data[2];

            return true;
        }
        #endregion

        #region Melee weapons
        public bool LoadMeleeWeaponsFromCSV(IRogueUI ui, string path)
        {
            MeleeWeaponData[] data;

            LoadDataFromCSV<MeleeWeaponData>(ui, path, "melee weapons items", MeleeWeaponData.COUNT_FIELDS, MeleeWeaponData.FromCSVLine,
                new IDs[] { IDs.MELEE_BASEBALLBAT, IDs.MELEE_COMBAT_KNIFE, IDs.MELEE_CROWBAR, IDs.MELEE_GOLFCLUB, IDs.MELEE_HUGE_HAMMER, IDs.MELEE_IRON_GOLFCLUB,
                            IDs.MELEE_SHOVEL, IDs.MELEE_SHORT_SHOVEL, IDs.MELEE_TRUNCHEON, IDs.UNIQUE_JASON_MYERS_AXE,
                            IDs.MELEE_IMPROVISED_CLUB, IDs.MELEE_IMPROVISED_SPEAR, IDs.MELEE_SMALL_HAMMER,
                            IDs.UNIQUE_FAMU_FATARU_KATANA, IDs.UNIQUE_BIGBEAR_BAT, IDs.UNIQUE_ROGUEDJACK_KEYBOARD },
                out data);

            DATA_MELEE_BASEBALLBAT = data[0];
            DATA_MELEE_COMBAT_KNIFE = data[1];
            DATA_MELEE_CROWBAR = data[2];
            DATA_MELEE_GOLFCLUB = data[3];
            DATA_MELEE_HUGE_HAMMER = data[4];
            DATA_MELEE_IRON_GOLFCLUB = data[5];
            DATA_MELEE_SHOVEL = data[6];
            DATA_MELEE_SHORT_SHOVEL = data[7];
            DATA_MELEE_TRUNCHEON = data[8];
            DATA_MELEE_UNIQUE_JASON_MYERS_AXE = data[9];
            DATA_MELEE_IMPROVISED_CLUB = data[10];
            DATA_MELEE_IMPROVISED_SPEAR = data[11];
            DATA_MELEE_SMALL_HAMMER = data[12];
            DATA_MELEE_UNIQUE_FAMU_FATARU_KATANA = data[13];
            DATA_MELEE_UNIQUE_BIGBEAR_BAT = data[14];
            DATA_MELEE_UNIQUE_ROGUEDJACK_KEYBOARD = data[15];

            return true;
        }
        #endregion

        #region Ranged weapons
        public bool LoadRangedWeaponsFromCSV(IRogueUI ui, string path)
        {
            RangedWeaponData[] data;

            LoadDataFromCSV<RangedWeaponData>(ui, path, "ranged weapons items", RangedWeaponData.COUNT_FIELDS, RangedWeaponData.FromCSVLine,
                new IDs[] { IDs.RANGED_ARMY_PISTOL, IDs.RANGED_ARMY_RIFLE, IDs.RANGED_HUNTING_CROSSBOW, IDs.RANGED_HUNTING_RIFLE, IDs.RANGED_KOLT_REVOLVER, IDs.RANGED_PISTOL, IDs.RANGED_PRECISION_RIFLE, IDs.RANGED_SHOTGUN,
                            IDs.UNIQUE_SANTAMAN_SHOTGUN, IDs.UNIQUE_HANS_VON_HANZ_PISTOL },
                out data);

            DATA_RANGED_ARMY_PISTOL = data[0];
            DATA_RANGED_ARMY_RIFLE = data[1];
            DATA_RANGED_HUNTING_CROSSBOW = data[2];
            DATA_RANGED_HUNTING_RIFLE = data[3];
            DATA_RANGED_KOLT_REVOLVER = data[4];
            DATA_RANGED_PISTOL = data[5];
            DATA_RANGED_PRECISION_RIFLE = data[6];
            DATA_RANGED_SHOTGUN = data[7];
            DATA_UNIQUE_SANTAMAN_SHOTGUN = data[8];
            DATA_UNIQUE_HANS_VON_HANZ_PISTOL = data[9];

            return true;
        }
        #endregion

        #region Explosives
        public bool LoadExplosivesFromCSV(IRogueUI ui, string path)
        {
            ExplosiveData[] data;

            LoadDataFromCSV<ExplosiveData>(ui, path, "explosives items", ExplosiveData.COUNT_FIELDS, ExplosiveData.FromCSVLine,
                new IDs[] { IDs.EXPLOSIVE_GRENADE },
                out data);

            DATA_EXPLOSIVE_GRENADE = data[0];

            return true;
        }
        #endregion

        #region Barricading material
        public bool LoadBarricadingMaterialFromCSV(IRogueUI ui, string path)
        {
            BarricadingMaterialData[] data;

            LoadDataFromCSV<BarricadingMaterialData>(ui, path, "barricading items", BarricadingMaterialData.COUNT_FIELDS, BarricadingMaterialData.FromCSVLine,
                new IDs[] { IDs.BAR_WOODEN_PLANK },
                out data);

            DATA_BAR_WOODEN_PLANK = data[0];

            return true;
        }
        #endregion

        #region Armors
        public bool LoadArmorsFromCSV(IRogueUI ui, string path)
        {
            ArmorData[] data;

            LoadDataFromCSV<ArmorData>(ui, path, "armors items", ArmorData.COUNT_FIELDS, ArmorData.FromCSVLine,
                new IDs[] { IDs.ARMOR_ARMY_BODYARMOR,IDs.ARMOR_CHAR_LIGHT_BODYARMOR,
                            IDs.ARMOR_HELLS_SOULS_JACKET, IDs.ARMOR_FREE_ANGELS_JACKET,
                            IDs.ARMOR_POLICE_JACKET, IDs.ARMOR_POLICE_RIOT,
                            IDs.ARMOR_HUNTER_VEST },
                out data);

            DATA_ARMOR_ARMY = data[0];
            DATA_ARMOR_CHAR = data[1];
            DATA_ARMOR_HELLS_SOULS_JACKET = data[2];
            DATA_ARMOR_FREE_ANGELS_JACKET = data[3];
            DATA_ARMOR_POLICE_JACKET = data[4];
            DATA_ARMOR_POLICE_RIOT = data[5];
            DATA_ARMOR_HUNTER_VEST = data[6];

            return true;
        }
        #endregion

        #region Trackers
        public bool LoadTrackersFromCSV(IRogueUI ui, string path)
        {
            TrackerData[] data;

            LoadDataFromCSV<TrackerData>(ui, path, "trackers items", TrackerData.COUNT_FIELDS, TrackerData.FromCSVLine,
                new IDs[] { IDs.TRACKER_BLACKOPS, IDs.TRACKER_CELL_PHONE, IDs.TRACKER_ZTRACKER, IDs.TRACKER_POLICE_RADIO },
                out data);

            DATA_TRACKER_BLACKOPS_GPS = data[0];
            DATA_TRACKER_CELL_PHONE = data[1];
            DATA_TRACKER_ZTRACKER = data[2];
            DATA_TRACKER_POLICE_RADIO = data[3];

            return true;
        }
        #endregion

        #region Spray paints
        public bool LoadSpraypaintsFromCSV(IRogueUI ui, string path)
        {
            SprayPaintData[] data;

            LoadDataFromCSV<SprayPaintData>(ui, path, "spraypaint items", SprayPaintData.COUNT_FIELDS, SprayPaintData.FromCSVLine,
                new IDs[] { IDs.SPRAY_PAINT1, IDs.SPRAY_PAINT2, IDs.SPRAY_PAINT3, IDs.SPRAY_PAINT4 },
                out data);

            DATA_SPRAY_PAINT1 = data[0];
            DATA_SPRAY_PAINT2 = data[1];
            DATA_SPRAY_PAINT3 = data[2];
            DATA_SPRAY_PAINT4 = data[3];

            return true;
        }
        #endregion

        #region Lights
        public bool LoadLightsFromCSV(IRogueUI ui, string path)
        {
            LightData[] data;

            LoadDataFromCSV<LightData>(ui, path, "lights items", LightData.COUNT_FIELDS, LightData.FromCSVLine,
                new IDs[] { IDs.LIGHT_FLASHLIGHT, IDs.LIGHT_BIG_FLASHLIGHT },
                out data);

            DATA_LIGHT_FLASHLIGHT = data[0];
            DATA_LIGHT_BIG_FLASHLIGHT = data[1];

            return true;
        }
        #endregion

        #region Scentsprays
        public bool LoadScentspraysFromCSV(IRogueUI ui, string path)
        {
            ScentSprayData[] data;

            LoadDataFromCSV<ScentSprayData>(ui, path, "scentsprays items", ScentSprayData.COUNT_FIELDS, ScentSprayData.FromCSVLine,
                new IDs[] { IDs.SCENT_SPRAY_STENCH_KILLER },
                out data);

            DATA_SCENT_SPRAY_STENCH_KILLER = data[0];

            return true;
        }
        #endregion

        #region Traps
        public bool LoadTrapsFromCSV(IRogueUI ui, string path)
        {
            TrapData[] data;

            LoadDataFromCSV<TrapData>(ui, path, "traps items", TrapData.COUNT_FIELDS, TrapData.FromCSVLine,
                new IDs[] { IDs.TRAP_EMPTY_CAN, IDs.TRAP_BEAR_TRAP, IDs.TRAP_SPIKES, IDs.TRAP_BARBED_WIRE },
                out data);

            DATA_TRAP_EMPTY_CAN = data[0];
            DATA_TRAP_BEAR_TRAP = data[1];
            DATA_TRAP_SPIKES = data[2];
            DATA_TRAP_BARBED_WIRE = data[3];

            return true;
        }
        #endregion

        #region Entertainment
        public bool LoadEntertainmentFromCSV(IRogueUI ui, string path)
        {
            EntData[] data;

            LoadDataFromCSV<EntData>(ui, path, "entertainment items", EntData.COUNT_FIELDS, EntData.FromCSVLine,
                new IDs[] { IDs.ENT_BOOK, IDs.ENT_MAGAZINE },
                out data);

            DATA_ENT_BOOK = data[0];
            DATA_ENT_MAGAZINE = data[1];

            return true;
        }
        #endregion
        #endregion
    }
}
