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
    partial class GameItems : ItemModelDB
    {
        #region IDs
        public enum IDs
        {
            _FIRST = 0,

            MEDICINE_BANDAGES = _FIRST,
            MEDICINE_MEDIKIT,
            MEDICINE_PILLS_STA,
            MEDICINE_PILLS_SLP,
            MEDICINE_PILLS_SAN,
            MEDICINE_PILLS_ANTIVIRAL,

            FOOD_ARMY_RATION,
            FOOD_GROCERIES,
            FOOD_CANNED_FOOD,

            MELEE_BASEBALLBAT,
            MELEE_COMBAT_KNIFE,
            MELEE_CROWBAR,
            UNIQUE_JASON_MYERS_AXE,
            MELEE_HUGE_HAMMER,
            MELEE_SMALL_HAMMER,
            MELEE_GOLFCLUB,
            MELEE_IRON_GOLFCLUB,
            MELEE_SHOVEL,
            MELEE_SHORT_SHOVEL,
            MELEE_TRUNCHEON,
            MELEE_IMPROVISED_CLUB,
            MELEE_IMPROVISED_SPEAR,

            RANGED_ARMY_PISTOL,
            RANGED_ARMY_RIFLE,
            RANGED_HUNTING_CROSSBOW,
            RANGED_HUNTING_RIFLE,
            RANGED_PISTOL,
            RANGED_KOLT_REVOLVER,
            RANGED_PRECISION_RIFLE,
            RANGED_SHOTGUN,

            EXPLOSIVE_GRENADE,
            EXPLOSIVE_GRENADE_PRIMED,

            BAR_WOODEN_PLANK,

            ARMOR_ARMY_BODYARMOR,
            ARMOR_CHAR_LIGHT_BODYARMOR,
            ARMOR_HELLS_SOULS_JACKET,
            ARMOR_FREE_ANGELS_JACKET,
            ARMOR_POLICE_JACKET,
            ARMOR_POLICE_RIOT,
            ARMOR_HUNTER_VEST,

            TRACKER_BLACKOPS,
            TRACKER_CELL_PHONE,
            TRACKER_ZTRACKER,
            TRACKER_POLICE_RADIO,

            SPRAY_PAINT1,
            SPRAY_PAINT2,
            SPRAY_PAINT3,
            SPRAY_PAINT4,

            SCENT_SPRAY_STENCH_KILLER,

            LIGHT_FLASHLIGHT,
            LIGHT_BIG_FLASHLIGHT,

            AMMO_LIGHT_PISTOL,
            AMMO_HEAVY_PISTOL,
            AMMO_LIGHT_RIFLE,
            AMMO_HEAVY_RIFLE,
            AMMO_SHOTGUN,
            AMMO_BOLTS,

            TRAP_EMPTY_CAN,
            TRAP_BEAR_TRAP,
            TRAP_SPIKES,
            TRAP_BARBED_WIRE,

            ENT_BOOK,
            ENT_MAGAZINE,

            UNIQUE_SUBWAY_BADGE,
            UNIQUE_FAMU_FATARU_KATANA,
            UNIQUE_BIGBEAR_BAT,
            UNIQUE_ROGUEDJACK_KEYBOARD,
            UNIQUE_SANTAMAN_SHOTGUN,
            UNIQUE_HANS_VON_HANZ_PISTOL,

            _COUNT
        }
        #endregion

        #region Fields
        ItemModel[] m_Models = new ItemModel[(int)IDs._COUNT];
        #endregion

        #region Properties
        public override ItemModel this[int id]
        {
            get { return m_Models[id]; }
        }

        public ItemModel this[IDs id]
        {
            get { return this[(int)id]; }
            private set
            {
                m_Models[(int)id] = value;
                m_Models[(int)id].ID = (int)id;
            }
        }

        #region Medicine
        struct MedecineData
        {
            public const int COUNT_FIELDS = 10;

            public string NAME { get; set; }
            public string PLURAL { get; set; }
            public int STACKINGLIMIT { get; set; }
            public int HEALING { get; set; }
            public int STAMINABOOST { get; set; }
            public int SLEEPBOOST { get; set; }
            public int INFECTIONCURE { get; set; }
            public int SANITYCURE { get; set; }
            public string FLAVOR { get; set; }

            public static MedecineData FromCSVLine(CSVLine line)
            {
                return new MedecineData()
                {
                    NAME = line[1].ParseText(),
                    PLURAL = line[2].ParseText(),
                    HEALING = line[3].ParseInt(),
                    STAMINABOOST = line[4].ParseInt(),
                    SLEEPBOOST = line[5].ParseInt(),
                    INFECTIONCURE = line[6].ParseInt(),
                    SANITYCURE = line[7].ParseInt(),
                    STACKINGLIMIT = line[8].ParseInt(),
                    FLAVOR = line[9].ParseText()
                };
            }
        }

        MedecineData DATA_MEDICINE_BANDAGE;
        public ItemMedicineModel BANDAGE { get { return this[IDs.MEDICINE_BANDAGES] as ItemMedicineModel; } }
        MedecineData DATA_MEDICINE_MEDIKIT;
        public ItemMedicineModel MEDIKIT { get { return this[IDs.MEDICINE_MEDIKIT] as ItemMedicineModel; } }
        MedecineData DATA_MEDICINE_PILLS_STA;
        public ItemMedicineModel PILLS_STA { get { return this[IDs.MEDICINE_PILLS_STA] as ItemMedicineModel; } }
        MedecineData DATA_MEDICINE_PILLS_SLP;
        public ItemMedicineModel PILLS_SLP { get { return this[IDs.MEDICINE_PILLS_SLP] as ItemMedicineModel; } }
        MedecineData DATA_MEDICINE_PILLS_SAN;
        public ItemMedicineModel PILLS_SAN { get { return this[IDs.MEDICINE_PILLS_SAN] as ItemMedicineModel; } }
        MedecineData DATA_MEDICINE_PILLS_ANTIVIRAL;
        public ItemMedicineModel PILLS_ANTIVIRAL { get { return this[IDs.MEDICINE_PILLS_ANTIVIRAL] as ItemMedicineModel; } }
        #endregion

        #region Food
        struct FoodData
        {
            public const int COUNT_FIELDS = 7;

            public string NAME { get; set; }
            public string PLURAL { get; set; }
            public int NUTRITION { get; set; }
            public int BESTBEFORE { get; set; }
            public int STACKINGLIMIT { get; set; }
            public string FLAVOR { get; set; }

            public static FoodData FromCSVLine(CSVLine line)
            {
                return new FoodData()
                {
                    NAME = line[1].ParseText(),
                    PLURAL = line[2].ParseText(),
                    NUTRITION = (int)(Rules.FOOD_BASE_POINTS * line[3].ParseFloat()),
                    BESTBEFORE = line[4].ParseInt(),
                    STACKINGLIMIT = line[5].ParseInt(),
                    FLAVOR = line[6].ParseText()
                };
            }
        }

        FoodData DATA_FOOD_ARMY_RATION;
        public ItemFoodModel ARMY_RATION { get { return this[IDs.FOOD_ARMY_RATION] as ItemFoodModel; } }
        FoodData DATA_FOOD_GROCERIES;
        public ItemFoodModel GROCERIES { get { return this[IDs.FOOD_GROCERIES] as ItemFoodModel; } }
        FoodData DATA_FOOD_CANNED_FOOD;
        public ItemFoodModel CANNED_FOOD { get { return this[IDs.FOOD_CANNED_FOOD] as ItemFoodModel; } }
        #endregion

        #region Melee weapons
        struct MeleeWeaponData
        {
            public const int COUNT_FIELDS = 12;  // alpha10

            public string NAME { get; set; }
            public string PLURAL { get; set; }
            public int ATK { get; set; }
            public int DMG { get; set; }
            public int STA { get; set; }
            public int DISARM { get; set; }  // alpha10
            public int TOOLBASHDMGBONUS { get; set; }  // alpha10
            public float TOOLBUILDBONUS { get; set; } // alpha10
            public int STACKINGLIMIT { get; set; }
            public bool ISFRAGILE { get; set; }
            public string FLAVOR { get; set; }

            public static MeleeWeaponData FromCSVLine(CSVLine line)
            {
                return new MeleeWeaponData()
                {
                    NAME = line[1].ParseText(),
                    PLURAL = line[2].ParseText(),
                    ATK = line[3].ParseInt(),
                    DMG = line[4].ParseInt(),
                    STA = line[5].ParseInt(),
                    DISARM = line[6].ParseInt(),  // alpha10
                    TOOLBASHDMGBONUS = line[7].ParseInt(), // alpha10
                    TOOLBUILDBONUS = line[8].ParseFloat(),  // alpha10
                    STACKINGLIMIT = line[9].ParseInt(),
                    ISFRAGILE = line[10].ParseBool(),
                    FLAVOR = line[11].ParseText()
                };
            }
        }

        MeleeWeaponData DATA_MELEE_CROWBAR;
        public ItemMeleeWeaponModel CROWBAR { get { return this[IDs.MELEE_CROWBAR] as ItemMeleeWeaponModel; } }
        MeleeWeaponData DATA_MELEE_BASEBALLBAT;
        public ItemMeleeWeaponModel BASEBALLBAT { get { return this[IDs.MELEE_BASEBALLBAT] as ItemMeleeWeaponModel; } }
        MeleeWeaponData DATA_MELEE_COMBAT_KNIFE;
        public ItemMeleeWeaponModel COMBAT_KNIFE { get { return this[IDs.MELEE_COMBAT_KNIFE] as ItemMeleeWeaponModel; } }
        MeleeWeaponData DATA_MELEE_UNIQUE_JASON_MYERS_AXE;
        public ItemMeleeWeaponModel UNIQUE_JASON_MYERS_AXE { get { return this[IDs.UNIQUE_JASON_MYERS_AXE] as ItemMeleeWeaponModel; } }
        MeleeWeaponData DATA_MELEE_GOLFCLUB;
        public ItemMeleeWeaponModel GOLFCLUB { get { return this[IDs.MELEE_GOLFCLUB] as ItemMeleeWeaponModel; } }
        MeleeWeaponData DATA_MELEE_HUGE_HAMMER;
        public ItemMeleeWeaponModel HUGE_HAMMER { get { return this[IDs.MELEE_HUGE_HAMMER] as ItemMeleeWeaponModel; } }
        MeleeWeaponData DATA_MELEE_SMALL_HAMMER;
        public ItemMeleeWeaponModel SMALL_HAMMER { get { return this[IDs.MELEE_SMALL_HAMMER] as ItemMeleeWeaponModel; } }
        MeleeWeaponData DATA_MELEE_IRON_GOLFCLUB;
        public ItemMeleeWeaponModel IRON_GOLFCLUB { get { return this[IDs.MELEE_IRON_GOLFCLUB] as ItemMeleeWeaponModel; } }
        MeleeWeaponData DATA_MELEE_SHOVEL;
        public ItemMeleeWeaponModel SHOVEL { get { return this[IDs.MELEE_SHOVEL] as ItemMeleeWeaponModel; } }
        MeleeWeaponData DATA_MELEE_SHORT_SHOVEL;
        public ItemMeleeWeaponModel SHORT_SHOVEL { get { return this[IDs.MELEE_SHORT_SHOVEL] as ItemMeleeWeaponModel; } }
        MeleeWeaponData DATA_MELEE_TRUNCHEON;
        public ItemMeleeWeaponModel TRUNCHEON { get { return this[IDs.MELEE_TRUNCHEON] as ItemMeleeWeaponModel; } }
        MeleeWeaponData DATA_MELEE_IMPROVISED_CLUB;
        public ItemMeleeWeaponModel IMPROVISED_CLUB { get { return this[IDs.MELEE_IMPROVISED_CLUB] as ItemMeleeWeaponModel; } }
        MeleeWeaponData DATA_MELEE_IMPROVISED_SPEAR;
        public ItemMeleeWeaponModel IMPROVISED_SPEAR { get { return this[IDs.MELEE_IMPROVISED_SPEAR] as ItemMeleeWeaponModel; } }
        MeleeWeaponData DATA_MELEE_UNIQUE_FAMU_FATARU_KATANA;
        public ItemMeleeWeaponModel UNIQUE_FAMU_FATARU_KATANA { get { return this[IDs.UNIQUE_FAMU_FATARU_KATANA] as ItemMeleeWeaponModel; } }
        MeleeWeaponData DATA_MELEE_UNIQUE_BIGBEAR_BAT;
        public ItemMeleeWeaponModel UNIQUE_BIGBEAR_BAT { get { return this[IDs.UNIQUE_BIGBEAR_BAT] as ItemMeleeWeaponModel; } }
        MeleeWeaponData DATA_MELEE_UNIQUE_ROGUEDJACK_KEYBOARD;
        public ItemMeleeWeaponModel UNIQUE_ROGUEDJACK_KEYBOARD { get { return this[IDs.UNIQUE_ROGUEDJACK_KEYBOARD] as ItemMeleeWeaponModel; } }

        #endregion

        #region Ranged weapons
        struct RangedWeaponData
        {
            public const int COUNT_FIELDS = 10; // alpha10

            public string NAME { get; set; }
            public string PLURAL { get; set; }
            public int ATK { get; set; }
            public int RAPID1 { get; set; } // alpha10
            public int RAPID2 { get; set; } // alpha10
            public int DMG { get; set; }
            public int RANGE { get; set; }
            public int MAXAMMO { get; set; }
            public string FLAVOR { get; set; }

            public static RangedWeaponData FromCSVLine(CSVLine line)
            {
                return new RangedWeaponData()
                {
                    NAME = line[1].ParseText(),
                    PLURAL = line[2].ParseText(),
                    ATK = line[3].ParseInt(),
                    RAPID1 = line[4].ParseInt(),
                    RAPID2 = line[5].ParseInt(),
                    DMG = line[6].ParseInt(),
                    RANGE = line[7].ParseInt(),
                    MAXAMMO = line[8].ParseInt(),
                    FLAVOR = line[9].ParseText()
                };
            }
        }

        RangedWeaponData DATA_RANGED_ARMY_PISTOL;
        public ItemRangedWeaponModel ARMY_PISTOL { get { return this[IDs.RANGED_ARMY_PISTOL] as ItemRangedWeaponModel; } }
        RangedWeaponData DATA_RANGED_ARMY_RIFLE;
        public ItemRangedWeaponModel ARMY_RIFLE { get { return this[IDs.RANGED_ARMY_RIFLE] as ItemRangedWeaponModel; } }
        RangedWeaponData DATA_RANGED_HUNTING_CROSSBOW;
        public ItemRangedWeaponModel HUNTING_CROSSBOW { get { return this[IDs.RANGED_HUNTING_CROSSBOW] as ItemRangedWeaponModel; } }
        RangedWeaponData DATA_RANGED_HUNTING_RIFLE;
        public ItemRangedWeaponModel HUNTING_RIFLE { get { return this[IDs.RANGED_HUNTING_RIFLE] as ItemRangedWeaponModel; } }
        RangedWeaponData DATA_RANGED_KOLT_REVOLVER;
        public ItemRangedWeaponModel KOLT_REVOLVER { get { return this[IDs.RANGED_KOLT_REVOLVER] as ItemRangedWeaponModel; } }
        RangedWeaponData DATA_RANGED_PISTOL;
        public ItemRangedWeaponModel PISTOL { get { return this[IDs.RANGED_PISTOL] as ItemRangedWeaponModel; } }
        RangedWeaponData DATA_RANGED_PRECISION_RIFLE;
        public ItemRangedWeaponModel PRECISION_RIFLE { get { return this[IDs.RANGED_PRECISION_RIFLE] as ItemRangedWeaponModel; } }
        RangedWeaponData DATA_RANGED_SHOTGUN;
        public ItemRangedWeaponModel SHOTGUN { get { return this[IDs.RANGED_SHOTGUN] as ItemRangedWeaponModel; } }
        RangedWeaponData DATA_UNIQUE_SANTAMAN_SHOTGUN;
        public ItemRangedWeaponModel UNIQUE_SANTAMAN_SHOTGUN { get { return this[IDs.UNIQUE_SANTAMAN_SHOTGUN] as ItemRangedWeaponModel; } }
        RangedWeaponData DATA_UNIQUE_HANS_VON_HANZ_PISTOL;
        public ItemRangedWeaponModel UNIQUE_HANS_VON_HANZ_PISTOL { get { return this[IDs.UNIQUE_HANS_VON_HANZ_PISTOL] as ItemRangedWeaponModel; } }
        #endregion

        #region Ammos
        public ItemAmmoModel AMMO_LIGHT_PISTOL { get { return this[IDs.AMMO_LIGHT_PISTOL] as ItemAmmoModel; } }
        public ItemAmmoModel AMMO_HEAVY_PISTOL { get { return this[IDs.AMMO_HEAVY_PISTOL] as ItemAmmoModel; } }
        public ItemAmmoModel AMMO_LIGHT_RIFLE { get { return this[IDs.AMMO_LIGHT_RIFLE] as ItemAmmoModel; } }
        public ItemAmmoModel AMMO_HEAVY_RIFLE { get { return this[IDs.AMMO_HEAVY_RIFLE] as ItemAmmoModel; } }
        public ItemAmmoModel AMMO_SHOTGUN { get { return this[IDs.AMMO_SHOTGUN] as ItemAmmoModel; } }
        public ItemAmmoModel AMMO_BOLTS { get { return this[IDs.AMMO_BOLTS] as ItemAmmoModel; } }
        #endregion

        #region Explosives
        struct ExplosiveData
        {
            public const int COUNT_FIELDS = 14;

            public string NAME { get; set; }
            public string PLURAL { get; set; }
            public int FUSE { get; set; }
            public int MAXTHROW { get; set; }
            public int STACKLINGLIMIT { get; set; }
            public int RADIUS { get; set; }
            public int[] DMG { get; set; }
            public string FLAVOR { get; set; }

            public static ExplosiveData FromCSVLine(CSVLine line)
            {
                return new ExplosiveData()
                {
                    NAME = line[1].ParseText(),
                    PLURAL = line[2].ParseText(),
                    FUSE = line[3].ParseInt(),
                    MAXTHROW = line[4].ParseInt(),
                    STACKLINGLIMIT = line[5].ParseInt(),
                    RADIUS = line[6].ParseInt(),
                    DMG = new int[6] {
                        line[7].ParseInt(),
                        line[8].ParseInt(),
                        line[9].ParseInt(),
                        line[10].ParseInt(),
                        line[11].ParseInt(),
                        line[12].ParseInt() },
                    FLAVOR = line[13].ParseText()
                };
            }
        }

        ExplosiveData DATA_EXPLOSIVE_GRENADE;
        public ItemGrenadeModel GRENADE { get { return this[IDs.EXPLOSIVE_GRENADE] as ItemGrenadeModel; } }
        public ItemGrenadePrimedModel GRENADE_PRIMED { get { return this[IDs.EXPLOSIVE_GRENADE_PRIMED] as ItemGrenadePrimedModel; } }
        #endregion

        #region Barricades
        struct BarricadingMaterialData
        {
            public const int COUNT_FIELDS = 6;

            public string NAME { get; set; }
            public string PLURAL { get; set; }
            public int VALUE { get; set; }
            public int STACKINGLIMIT { get; set; }
            public string FLAVOR { get; set; }

            public static BarricadingMaterialData FromCSVLine(CSVLine line)
            {
                return new BarricadingMaterialData()
                {
                    NAME = line[1].ParseText(),
                    PLURAL = line[2].ParseText(),
                    VALUE = line[3].ParseInt(),
                    STACKINGLIMIT = line[4].ParseInt(),
                    FLAVOR = line[5].ParseText()
                };
            }
        }

        BarricadingMaterialData DATA_BAR_WOODEN_PLANK;
        public ItemBarricadeMaterialModel WOODENPLANK { get { return this[IDs.BAR_WOODEN_PLANK] as ItemBarricadeMaterialModel; } }
        #endregion

        #region Body Armors
        struct ArmorData
        {
            public const int COUNT_FIELDS = 8;

            public string NAME { get; set; }
            public string PLURAL { get; set; }
            public int PRO_HIT { get; set; }
            public int PRO_SHOT { get; set; }
            public int ENC { get; set; }
            public int WEIGHT { get; set; }
            public string FLAVOR { get; set; }

            public static ArmorData FromCSVLine(CSVLine line)
            {
                return new ArmorData()
                {
                    NAME = line[1].ParseText(),
                    PLURAL = line[2].ParseText(),
                    PRO_HIT = line[3].ParseInt(),
                    PRO_SHOT = line[4].ParseInt(),
                    ENC = line[5].ParseInt(),
                    WEIGHT = line[6].ParseInt(),
                    FLAVOR = line[7].ParseText()
                };
            }
        }

        ArmorData DATA_ARMOR_ARMY;
        public ItemBodyArmorModel ARMY_BODYARMOR { get { return this[IDs.ARMOR_ARMY_BODYARMOR] as ItemBodyArmorModel; } }
        ArmorData DATA_ARMOR_CHAR;
        public ItemBodyArmorModel CHAR_LT_BODYARMOR { get { return this[IDs.ARMOR_CHAR_LIGHT_BODYARMOR] as ItemBodyArmorModel; } }
        ArmorData DATA_ARMOR_HELLS_SOULS_JACKET;
        public ItemBodyArmorModel HELLS_SOULS_JACKET { get { return this[IDs.ARMOR_HELLS_SOULS_JACKET] as ItemBodyArmorModel; } }
        ArmorData DATA_ARMOR_FREE_ANGELS_JACKET;
        public ItemBodyArmorModel FREE_ANGELS_JACKET { get { return this[IDs.ARMOR_FREE_ANGELS_JACKET] as ItemBodyArmorModel; } }
        ArmorData DATA_ARMOR_POLICE_JACKET;
        public ItemBodyArmorModel POLICE_JACKET { get { return this[IDs.ARMOR_POLICE_JACKET] as ItemBodyArmorModel; } }
        ArmorData DATA_ARMOR_POLICE_RIOT;
        public ItemBodyArmorModel POLICE_RIOT { get { return this[IDs.ARMOR_POLICE_RIOT] as ItemBodyArmorModel; } }
        ArmorData DATA_ARMOR_HUNTER_VEST;
        public ItemBodyArmorModel HUNTER_VEST { get { return this[IDs.ARMOR_HUNTER_VEST] as ItemBodyArmorModel; } }
        #endregion

        #region Trackers
        struct TrackerData
        {
            public const int COUNT_FIELDS = 6;

            public string NAME { get; set; }
            public string PLURAL { get; set; }
            public int BATTERIES { get; set; }
            public bool HASCLOCK { get; set; }  // alpha10
            public string FLAVOR { get; set; }

            public static TrackerData FromCSVLine(CSVLine line)
            {
                return new TrackerData()
                {
                    NAME = line[1].ParseText(),
                    PLURAL = line[2].ParseText(),
                    BATTERIES = line[3].ParseInt(),
                    HASCLOCK = line[4].ParseBool(),  // alpha10
                    FLAVOR = line[5].ParseText()
                };
            }
        }

        TrackerData DATA_TRACKER_BLACKOPS_GPS;
        public ItemTrackerModel BLACKOPS_GPS { get { return this[IDs.TRACKER_BLACKOPS] as ItemTrackerModel; } }
        TrackerData DATA_TRACKER_CELL_PHONE;
        public ItemTrackerModel CELL_PHONE { get { return this[IDs.TRACKER_CELL_PHONE] as ItemTrackerModel; } }
        TrackerData DATA_TRACKER_ZTRACKER;
        public ItemTrackerModel ZTRACKER { get { return this[IDs.TRACKER_ZTRACKER] as ItemTrackerModel; } }
        TrackerData DATA_TRACKER_POLICE_RADIO;
        public ItemTrackerModel POLICE_RADIO { get { return this[IDs.TRACKER_POLICE_RADIO] as ItemTrackerModel; } }
        #endregion

        #region Spray Paint
        struct SprayPaintData
        {
            public const int COUNT_FIELDS = 5;

            public string NAME { get; set; }
            public string PLURAL { get; set; }
            public int QUANTITY { get; set; }
            public string FLAVOR { get; set; }

            public static SprayPaintData FromCSVLine(CSVLine line)
            {
                return new SprayPaintData()
                {
                    NAME = line[1].ParseText(),
                    PLURAL = line[2].ParseText(),
                    QUANTITY = line[3].ParseInt(),
                    FLAVOR = line[4].ParseText()
                };
            }
        }

        SprayPaintData DATA_SPRAY_PAINT1;
        public ItemSprayPaintModel SPRAY_PAINT1 { get { return this[IDs.SPRAY_PAINT1] as ItemSprayPaintModel; } }
        SprayPaintData DATA_SPRAY_PAINT2;
        public ItemSprayPaintModel SPRAY_PAINT2 { get { return this[IDs.SPRAY_PAINT2] as ItemSprayPaintModel; } }
        SprayPaintData DATA_SPRAY_PAINT3;
        public ItemSprayPaintModel SPRAY_PAINT3 { get { return this[IDs.SPRAY_PAINT3] as ItemSprayPaintModel; } }
        SprayPaintData DATA_SPRAY_PAINT4;
        public ItemSprayPaintModel SPRAY_PAINT4 { get { return this[IDs.SPRAY_PAINT4] as ItemSprayPaintModel; } }
        #endregion

        #region Lights
        struct LightData
        {
            public const int COUNT_FIELDS = 6;

            public string NAME { get; set; }
            public string PLURAL { get; set; }
            public int FOV { get; set; }
            public int BATTERIES { get; set; }
            public string FLAVOR { get; set; }

            public static LightData FromCSVLine(CSVLine line)
            {
                return new LightData()
                {
                    NAME = line[1].ParseText(),
                    PLURAL = line[2].ParseText(),
                    FOV = line[3].ParseInt(),
                    BATTERIES = line[4].ParseInt(),
                    FLAVOR = line[5].ParseText()
                };
            }
        }

        LightData DATA_LIGHT_FLASHLIGHT;
        public ItemLightModel FLASHLIGHT { get { return this[IDs.LIGHT_FLASHLIGHT] as ItemLightModel; } }
        LightData DATA_LIGHT_BIG_FLASHLIGHT;
        public ItemLightModel BIG_FLASHLIGHT { get { return this[IDs.LIGHT_BIG_FLASHLIGHT] as ItemLightModel; } }
        #endregion

        #region Scent Sprays
        struct ScentSprayData
        {
            public const int COUNT_FIELDS = 6;

            public string NAME { get; set; }
            public string PLURAL { get; set; }
            public int QUANTITY { get; set; }
            public int STRENGTH { get; set; }
            public string FLAVOR { get; set; }

            public static ScentSprayData FromCSVLine(CSVLine line)
            {
                return new ScentSprayData()
                {
                    NAME = line[1].ParseText(),
                    PLURAL = line[2].ParseText(),
                    QUANTITY = line[3].ParseInt(),
                    STRENGTH = line[4].ParseInt(),
                    FLAVOR = line[5].ParseText()
                };
            }
        }

        ScentSprayData DATA_SCENT_SPRAY_STENCH_KILLER;
        public ItemModel STENCH_KILLER { get { return this[IDs.SCENT_SPRAY_STENCH_KILLER]; } }
        #endregion

        #region Traps
        struct TrapData
        {
            public const int COUNT_FIELDS = 16;

            public string NAME { get; set; }
            public string PLURAL { get; set; }
            public int STACKING { get; set; }
            public bool USE_ACTIVATE { get; set; }
            public int CHANCE { get; set; }
            public int DAMAGE { get; set; }
            public bool DROP_ACTIVATE { get; set; }
            public bool IS_ONE_TIME { get; set; }
            public int BREAK_CHANCE { get; set; }
            public int BLOCK_CHANCE { get; set; }
            public int BREAK_CHANCE_ESCAPE { get; set; }
            public bool IS_NOISY { get; set; }
            public string NOISE_NAME { get; set; }
            public bool IS_FLAMMABLE { get; set; }
            public string FLAVOR { get; set; }

            public static TrapData FromCSVLine(CSVLine line)
            {
                return new TrapData()
                {
                    NAME = line[1].ParseText(),
                    PLURAL = line[2].ParseText(),
                    STACKING = line[3].ParseInt(),
                    DROP_ACTIVATE = line[4].ParseBool(),
                    USE_ACTIVATE = line[5].ParseBool(),
                    CHANCE = line[6].ParseInt(),
                    DAMAGE = line[7].ParseInt(),
                    IS_ONE_TIME = line[8].ParseBool(),
                    BREAK_CHANCE = line[9].ParseInt(),
                    BLOCK_CHANCE = line[10].ParseInt(),
                    BREAK_CHANCE_ESCAPE = line[11].ParseInt(),
                    IS_NOISY = line[12].ParseBool(),
                    NOISE_NAME = line[13].ParseText(),
                    IS_FLAMMABLE = line[14].ParseBool(),
                    FLAVOR = line[15].ParseText()
                };
            }
        }

        TrapData DATA_TRAP_EMPTY_CAN;
        public ItemModel EMPTY_CAN { get { return this[IDs.TRAP_EMPTY_CAN]; } }
        TrapData DATA_TRAP_BEAR_TRAP;
        public ItemModel BEAR_TRAP { get { return this[IDs.TRAP_BEAR_TRAP]; } }
        TrapData DATA_TRAP_SPIKES;
        public ItemModel SPIKES { get { return this[IDs.TRAP_SPIKES]; } }
        TrapData DATA_TRAP_BARBED_WIRE;
        public ItemModel BARBED_WIRE { get { return this[IDs.TRAP_BARBED_WIRE]; } }

        #endregion

        #region Entertainment
        struct EntData
        {
            public const int COUNT_FIELDS = 7;

            public string NAME { get; set; }
            public string PLURAL { get; set; }
            public int STACKING { get; set; }
            public int VALUE { get; set; }
            public int BORECHANCE { get; set; }
            public string FLAVOR { get; set; }

            public static EntData FromCSVLine(CSVLine line)
            {
                return new EntData()
                {
                    NAME = line[1].ParseText(),
                    PLURAL = line[2].ParseText(),
                    STACKING = line[3].ParseInt(),
                    VALUE = line[4].ParseInt(),
                    BORECHANCE = line[5].ParseInt(),
                    FLAVOR = line[6].ParseText()
                };
            }
        }

        EntData DATA_ENT_BOOK;
        public ItemModel BOOK { get { return this[IDs.ENT_BOOK]; } }
        EntData DATA_ENT_MAGAZINE;
        public ItemModel MAGAZINE { get { return this[IDs.ENT_MAGAZINE]; } }
        #endregion

        #region Special Uniques
        public ItemModel UNIQUE_SUBWAY_BADGE { get { return this[IDs.UNIQUE_SUBWAY_BADGE]; } }
        #endregion

        #endregion


    }
}
