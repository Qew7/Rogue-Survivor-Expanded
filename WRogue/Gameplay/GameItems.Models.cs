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
        #region Init
        public GameItems()
        {
            // bind
            Models.Items = this;
        }

        #region Grammar Helpers
        bool StartsWithVowel(string name)
        {
            return name[0] == 'a' || name[0] == 'A' ||
                name[0] == 'e' || name[0] == 'E' ||
                name[0] == 'i' || name[0] == 'I' ||
                name[0] == 'y' || name[0] == 'Y';
        }

        bool CheckPlural(string name, string plural)
        {
            return name == plural;
        }
        #endregion

        /// <summary>
        /// FIXME: clean up the code (sometimes uses temp local var, sometimes use explicit model)
        /// </summary>
        public void CreateModels()
        {
            #region Medicine
            this[IDs.MEDICINE_BANDAGES] = new ItemMedicineModel(DATA_MEDICINE_BANDAGE.NAME, DATA_MEDICINE_BANDAGE.PLURAL, GameImages.ITEM_BANDAGES,
                DATA_MEDICINE_BANDAGE.HEALING, DATA_MEDICINE_BANDAGE.STAMINABOOST, DATA_MEDICINE_BANDAGE.SLEEPBOOST, DATA_MEDICINE_BANDAGE.INFECTIONCURE, DATA_MEDICINE_BANDAGE.SANITYCURE)
            {
                IsPlural = true,
                IsStackable = true,
                StackingLimit = DATA_MEDICINE_BANDAGE.STACKINGLIMIT,
                FlavorDescription = DATA_MEDICINE_BANDAGE.FLAVOR
            };

            this[IDs.MEDICINE_MEDIKIT] = new ItemMedicineModel(DATA_MEDICINE_MEDIKIT.NAME, DATA_MEDICINE_MEDIKIT.PLURAL, GameImages.ITEM_MEDIKIT,
                DATA_MEDICINE_MEDIKIT.HEALING, DATA_MEDICINE_MEDIKIT.STAMINABOOST, DATA_MEDICINE_MEDIKIT.SLEEPBOOST, DATA_MEDICINE_MEDIKIT.INFECTIONCURE, DATA_MEDICINE_MEDIKIT.SANITYCURE)
            {
                FlavorDescription = DATA_MEDICINE_MEDIKIT.FLAVOR
            };

            this[IDs.MEDICINE_PILLS_STA] = new ItemMedicineModel(DATA_MEDICINE_PILLS_STA.NAME, DATA_MEDICINE_PILLS_STA.PLURAL, GameImages.ITEM_PILLS_GREEN,
                DATA_MEDICINE_PILLS_STA.HEALING, DATA_MEDICINE_PILLS_STA.STAMINABOOST, DATA_MEDICINE_PILLS_STA.SLEEPBOOST, DATA_MEDICINE_PILLS_STA.INFECTIONCURE, DATA_MEDICINE_PILLS_STA.SANITYCURE)
            {
                IsPlural = true,
                IsStackable = true,
                StackingLimit = DATA_MEDICINE_PILLS_STA.STACKINGLIMIT,
                FlavorDescription = DATA_MEDICINE_PILLS_STA.FLAVOR
            };

            this[IDs.MEDICINE_PILLS_SLP] = new ItemMedicineModel(DATA_MEDICINE_PILLS_SLP.NAME, DATA_MEDICINE_PILLS_SLP.PLURAL, GameImages.ITEM_PILLS_BLUE,
                DATA_MEDICINE_PILLS_SLP.HEALING, DATA_MEDICINE_PILLS_SLP.STAMINABOOST, DATA_MEDICINE_PILLS_SLP.SLEEPBOOST, DATA_MEDICINE_PILLS_SLP.INFECTIONCURE, DATA_MEDICINE_PILLS_SLP.SANITYCURE)
            {
                IsPlural = true,
                IsStackable = true,
                StackingLimit = DATA_MEDICINE_PILLS_SLP.STACKINGLIMIT,
                FlavorDescription = DATA_MEDICINE_PILLS_SLP.FLAVOR
            };
            this[IDs.MEDICINE_PILLS_SAN] = new ItemMedicineModel(DATA_MEDICINE_PILLS_SAN.NAME, DATA_MEDICINE_PILLS_SAN.PLURAL, GameImages.ITEM_PILLS_SAN,
                DATA_MEDICINE_PILLS_SAN.HEALING, DATA_MEDICINE_PILLS_SAN.STAMINABOOST, DATA_MEDICINE_PILLS_SAN.SLEEPBOOST, DATA_MEDICINE_PILLS_SAN.INFECTIONCURE, DATA_MEDICINE_PILLS_SAN.SANITYCURE)
            {
                IsPlural = true,
                IsStackable = true,
                StackingLimit = DATA_MEDICINE_PILLS_SAN.STACKINGLIMIT,
                FlavorDescription = DATA_MEDICINE_PILLS_SAN.FLAVOR
            };
            this[IDs.MEDICINE_PILLS_ANTIVIRAL] = new ItemMedicineModel(DATA_MEDICINE_PILLS_ANTIVIRAL.NAME, DATA_MEDICINE_PILLS_ANTIVIRAL.PLURAL, GameImages.ITEM_PILLS_ANTIVIRAL,
                DATA_MEDICINE_PILLS_ANTIVIRAL.HEALING, DATA_MEDICINE_PILLS_ANTIVIRAL.STAMINABOOST, DATA_MEDICINE_PILLS_ANTIVIRAL.SLEEPBOOST, DATA_MEDICINE_PILLS_ANTIVIRAL.INFECTIONCURE, DATA_MEDICINE_PILLS_ANTIVIRAL.SANITYCURE)
            {
                IsPlural = true,
                IsStackable = true,
                StackingLimit = DATA_MEDICINE_PILLS_ANTIVIRAL.STACKINGLIMIT,
                FlavorDescription = DATA_MEDICINE_PILLS_ANTIVIRAL.FLAVOR
            };

            #endregion

            #region Food
            this[IDs.FOOD_ARMY_RATION] = new ItemFoodModel(DATA_FOOD_ARMY_RATION.NAME, DATA_FOOD_ARMY_RATION.PLURAL, GameImages.ITEM_ARMY_RATION, DATA_FOOD_ARMY_RATION.NUTRITION, DATA_FOOD_ARMY_RATION.BESTBEFORE)
            {
                IsAn = StartsWithVowel(DATA_FOOD_ARMY_RATION.NAME),
                IsPlural = CheckPlural(DATA_FOOD_ARMY_RATION.NAME, DATA_FOOD_ARMY_RATION.PLURAL),
                StackingLimit = DATA_FOOD_ARMY_RATION.STACKINGLIMIT,
                FlavorDescription = DATA_FOOD_ARMY_RATION.FLAVOR
            };

            this[IDs.FOOD_GROCERIES] = new ItemFoodModel(DATA_FOOD_GROCERIES.NAME, DATA_FOOD_GROCERIES.PLURAL, GameImages.ITEM_GROCERIES, DATA_FOOD_GROCERIES.NUTRITION, DATA_FOOD_GROCERIES.BESTBEFORE)
            {
                IsAn = StartsWithVowel(DATA_FOOD_GROCERIES.NAME),
                IsPlural = CheckPlural(DATA_FOOD_GROCERIES.NAME, DATA_FOOD_GROCERIES.PLURAL),
                StackingLimit = DATA_FOOD_GROCERIES.STACKINGLIMIT,
                FlavorDescription = DATA_FOOD_GROCERIES.FLAVOR
            };

            this[IDs.FOOD_CANNED_FOOD] = new ItemFoodModel(DATA_FOOD_CANNED_FOOD.NAME, DATA_FOOD_CANNED_FOOD.PLURAL, GameImages.ITEM_CANNED_FOOD, DATA_FOOD_CANNED_FOOD.NUTRITION, DATA_FOOD_CANNED_FOOD.BESTBEFORE)
            {
                IsAn = StartsWithVowel(DATA_FOOD_CANNED_FOOD.NAME),
                IsPlural = CheckPlural(DATA_FOOD_CANNED_FOOD.NAME, DATA_FOOD_CANNED_FOOD.PLURAL),
                StackingLimit = DATA_FOOD_CANNED_FOOD.STACKINGLIMIT,
                IsStackable = true,
                FlavorDescription = DATA_FOOD_CANNED_FOOD.FLAVOR
            };
            #endregion

            #region Melee weapons
            // alpha10 disarm chance added to attack, tool bonuses added to properties
            MeleeWeaponData mwdata;

            mwdata = DATA_MELEE_BASEBALLBAT;
            this[IDs.MELEE_BASEBALLBAT] = new ItemMeleeWeaponModel(mwdata.NAME, mwdata.PLURAL, GameImages.ITEM_BASEBALL_BAT,
                Attack.MeleeAttack(new Verb("smash", "smashes"), mwdata.ATK, mwdata.DMG, mwdata.STA, mwdata.DISARM))
            {
                EquipmentPart = DollPart.RIGHT_HAND,
                IsStackable = (mwdata.STACKINGLIMIT > 1),
                StackingLimit = mwdata.STACKINGLIMIT,
                FlavorDescription = mwdata.FLAVOR,
                IsFragile = mwdata.ISFRAGILE,
                ToolBashDamageBonus = mwdata.TOOLBASHDMGBONUS, // alpha10
                ToolBuildBonus = mwdata.TOOLBUILDBONUS  // alpha10
            };

            mwdata = DATA_MELEE_COMBAT_KNIFE;
            this[IDs.MELEE_COMBAT_KNIFE] = new ItemMeleeWeaponModel(mwdata.NAME, mwdata.PLURAL, GameImages.ITEM_COMBAT_KNIFE,
                Attack.MeleeAttack(new Verb("stab", "stabs"), mwdata.ATK, mwdata.DMG, mwdata.STA, mwdata.DISARM))
            {
                EquipmentPart = DollPart.RIGHT_HAND,
                IsStackable = true,
                StackingLimit = mwdata.STACKINGLIMIT,
                FlavorDescription = mwdata.FLAVOR,
                IsFragile = mwdata.ISFRAGILE,
                ToolBashDamageBonus = mwdata.TOOLBASHDMGBONUS, // alpha10
                ToolBuildBonus = mwdata.TOOLBUILDBONUS  // alpha10
            };

            mwdata = DATA_MELEE_CROWBAR;
            this[IDs.MELEE_CROWBAR] = new ItemMeleeWeaponModel(mwdata.NAME, mwdata.PLURAL, GameImages.ITEM_CROWBAR,
                Attack.MeleeAttack(new Verb("strike"), mwdata.ATK, mwdata.DMG, mwdata.STA, mwdata.DISARM))
            {
                EquipmentPart = DollPart.RIGHT_HAND,
                IsStackable = true,
                StackingLimit = mwdata.STACKINGLIMIT,
                FlavorDescription = mwdata.FLAVOR,
                IsFragile = mwdata.ISFRAGILE,
                ToolBashDamageBonus = mwdata.TOOLBASHDMGBONUS, // alpha10
                ToolBuildBonus = mwdata.TOOLBUILDBONUS  // alpha10
            };

            mwdata = DATA_MELEE_UNIQUE_JASON_MYERS_AXE;
            this[IDs.UNIQUE_JASON_MYERS_AXE] = new ItemMeleeWeaponModel(mwdata.NAME, mwdata.PLURAL, GameImages.ITEM_JASON_MYERS_AXE,
                Attack.MeleeAttack(new Verb("slash", "slashes"), mwdata.ATK, mwdata.DMG, mwdata.STA, mwdata.DISARM))
            {
                EquipmentPart = DollPart.RIGHT_HAND,
                IsProper = true,
                FlavorDescription = mwdata.FLAVOR,
                IsUnbreakable = true,
                ToolBashDamageBonus = mwdata.TOOLBASHDMGBONUS, // alpha10
                ToolBuildBonus = mwdata.TOOLBUILDBONUS  // alpha10
            };

            mwdata = DATA_MELEE_GOLFCLUB;
            this[IDs.MELEE_GOLFCLUB] = new ItemMeleeWeaponModel(mwdata.NAME, mwdata.PLURAL, GameImages.ITEM_GOLF_CLUB,
                Attack.MeleeAttack(new Verb("strike"), mwdata.ATK, mwdata.DMG, mwdata.STA, mwdata.DISARM))
            {
                EquipmentPart = DollPart.RIGHT_HAND,
                IsStackable = (mwdata.STACKINGLIMIT > 1),
                StackingLimit = mwdata.STACKINGLIMIT,
                FlavorDescription = mwdata.FLAVOR,
                IsFragile = mwdata.ISFRAGILE,
                ToolBashDamageBonus = mwdata.TOOLBASHDMGBONUS, // alpha10
                ToolBuildBonus = mwdata.TOOLBUILDBONUS  // alpha10
            };

            mwdata = DATA_MELEE_IRON_GOLFCLUB;
            this[IDs.MELEE_IRON_GOLFCLUB] = new ItemMeleeWeaponModel(mwdata.NAME, mwdata.PLURAL, GameImages.ITEM_IRON_GOLF_CLUB,
                Attack.MeleeAttack(new Verb("strike"), mwdata.ATK, mwdata.DMG, mwdata.STA, mwdata.DISARM))
            {
                EquipmentPart = DollPart.RIGHT_HAND,
                IsStackable = (mwdata.STACKINGLIMIT > 1),
                StackingLimit = mwdata.STACKINGLIMIT,
                FlavorDescription = mwdata.FLAVOR,
                IsFragile = mwdata.ISFRAGILE,
                ToolBashDamageBonus = mwdata.TOOLBASHDMGBONUS, // alpha10
                ToolBuildBonus = mwdata.TOOLBUILDBONUS  // alpha10
            };

            mwdata = DATA_MELEE_HUGE_HAMMER;
            this[IDs.MELEE_HUGE_HAMMER] = new ItemMeleeWeaponModel(mwdata.NAME, mwdata.PLURAL, GameImages.ITEM_HUGE_HAMMER,
                Attack.MeleeAttack(new Verb("smash", "smashes"), mwdata.ATK, mwdata.DMG, mwdata.STA, mwdata.DISARM))
            {
                EquipmentPart = DollPart.RIGHT_HAND,
                IsStackable = (mwdata.STACKINGLIMIT > 1),
                StackingLimit = mwdata.STACKINGLIMIT,
                FlavorDescription = mwdata.FLAVOR,
                IsFragile = mwdata.ISFRAGILE,
                ToolBashDamageBonus = mwdata.TOOLBASHDMGBONUS, // alpha10
                ToolBuildBonus = mwdata.TOOLBUILDBONUS  // alpha10
            };

            mwdata = DATA_MELEE_SHOVEL;
            this[IDs.MELEE_SHOVEL] = new ItemMeleeWeaponModel(mwdata.NAME, mwdata.PLURAL, GameImages.ITEM_SHOVEL,
                Attack.MeleeAttack(new Verb("strike"), mwdata.ATK, mwdata.DMG, mwdata.STA, mwdata.DISARM))
            {
                EquipmentPart = DollPart.RIGHT_HAND,
                IsStackable = (mwdata.STACKINGLIMIT > 1),
                StackingLimit = mwdata.STACKINGLIMIT,
                FlavorDescription = mwdata.FLAVOR,
                IsFragile = mwdata.ISFRAGILE,
                ToolBashDamageBonus = mwdata.TOOLBASHDMGBONUS, // alpha10
                ToolBuildBonus = mwdata.TOOLBUILDBONUS  // alpha10
            };

            mwdata = DATA_MELEE_SHORT_SHOVEL;
            this[IDs.MELEE_SHORT_SHOVEL] = new ItemMeleeWeaponModel(mwdata.NAME, mwdata.PLURAL, GameImages.ITEM_SHORT_SHOVEL,
                 Attack.MeleeAttack(new Verb("strike"), mwdata.ATK, mwdata.DMG, mwdata.STA, mwdata.DISARM))
            {
                EquipmentPart = DollPart.RIGHT_HAND,
                IsStackable = (mwdata.STACKINGLIMIT > 1),
                StackingLimit = mwdata.STACKINGLIMIT,
                FlavorDescription = mwdata.FLAVOR,
                IsFragile = mwdata.ISFRAGILE,
                ToolBashDamageBonus = mwdata.TOOLBASHDMGBONUS, // alpha10
                ToolBuildBonus = mwdata.TOOLBUILDBONUS  // alpha10
            };

            mwdata = DATA_MELEE_TRUNCHEON;
            this[IDs.MELEE_TRUNCHEON] = new ItemMeleeWeaponModel(mwdata.NAME, mwdata.PLURAL, GameImages.ITEM_TRUNCHEON,
                Attack.MeleeAttack(new Verb("strike"), mwdata.ATK, mwdata.DMG, mwdata.STA, mwdata.DISARM))
            {
                EquipmentPart = DollPart.RIGHT_HAND,
                IsStackable = true,
                StackingLimit = mwdata.STACKINGLIMIT,
                FlavorDescription = mwdata.FLAVOR,
                IsFragile = mwdata.ISFRAGILE,
                ToolBashDamageBonus = mwdata.TOOLBASHDMGBONUS, // alpha10
                ToolBuildBonus = mwdata.TOOLBUILDBONUS  // alpha10
            };

            mwdata = DATA_MELEE_IMPROVISED_CLUB;
            this[IDs.MELEE_IMPROVISED_CLUB] = new ItemMeleeWeaponModel(mwdata.NAME, mwdata.PLURAL, GameImages.ITEM_IMPROVISED_CLUB,
                Attack.MeleeAttack(new Verb("strike"), mwdata.ATK, mwdata.DMG, mwdata.STA, mwdata.DISARM))
            {
                EquipmentPart = DollPart.RIGHT_HAND,
                IsStackable = (mwdata.STACKINGLIMIT > 1),
                StackingLimit = mwdata.STACKINGLIMIT,
                FlavorDescription = mwdata.FLAVOR,
                IsFragile = mwdata.ISFRAGILE,
                ToolBashDamageBonus = mwdata.TOOLBASHDMGBONUS, // alpha10
                ToolBuildBonus = mwdata.TOOLBUILDBONUS  // alpha10
            };

            mwdata = DATA_MELEE_IMPROVISED_SPEAR;
            this[IDs.MELEE_IMPROVISED_SPEAR] = new ItemMeleeWeaponModel(mwdata.NAME, mwdata.PLURAL, GameImages.ITEM_IMPROVISED_SPEAR,
                Attack.MeleeAttack(new Verb("pierce"), mwdata.ATK, mwdata.DMG, mwdata.STA, mwdata.DISARM))
            {
                EquipmentPart = DollPart.RIGHT_HAND,
                IsStackable = (mwdata.STACKINGLIMIT > 1),
                StackingLimit = mwdata.STACKINGLIMIT,
                FlavorDescription = mwdata.FLAVOR,
                IsFragile = mwdata.ISFRAGILE,
                ToolBashDamageBonus = mwdata.TOOLBASHDMGBONUS, // alpha10
                ToolBuildBonus = mwdata.TOOLBUILDBONUS  // alpha10
            };

            mwdata = DATA_MELEE_SMALL_HAMMER;
            this[IDs.MELEE_SMALL_HAMMER] = new ItemMeleeWeaponModel(mwdata.NAME, mwdata.PLURAL, GameImages.ITEM_SMALL_HAMMER,
                Attack.MeleeAttack(new Verb("smash"), mwdata.ATK, mwdata.DMG, mwdata.STA, mwdata.DISARM))
            {
                EquipmentPart = DollPart.RIGHT_HAND,
                IsStackable = (mwdata.STACKINGLIMIT > 1),
                StackingLimit = mwdata.STACKINGLIMIT,
                FlavorDescription = mwdata.FLAVOR,
                IsFragile = mwdata.ISFRAGILE,
                ToolBashDamageBonus = mwdata.TOOLBASHDMGBONUS, // alpha10
                ToolBuildBonus = mwdata.TOOLBUILDBONUS  // alpha10
            };

            mwdata = DATA_MELEE_UNIQUE_FAMU_FATARU_KATANA;
            this[IDs.UNIQUE_FAMU_FATARU_KATANA] = new ItemMeleeWeaponModel(mwdata.NAME, mwdata.PLURAL, GameImages.ITEM_FAMU_FATARU_KATANA,
                Attack.MeleeAttack(new Verb("slash", "slashes"), mwdata.ATK, mwdata.DMG, mwdata.STA, mwdata.DISARM))
            {
                EquipmentPart = DollPart.RIGHT_HAND,
                FlavorDescription = mwdata.FLAVOR,
                IsProper = true,
                IsUnbreakable = true,
                ToolBashDamageBonus = mwdata.TOOLBASHDMGBONUS, // alpha10
                ToolBuildBonus = mwdata.TOOLBUILDBONUS  // alpha10
            };

            mwdata = DATA_MELEE_UNIQUE_BIGBEAR_BAT;
            this[IDs.UNIQUE_BIGBEAR_BAT] = new ItemMeleeWeaponModel(mwdata.NAME, mwdata.PLURAL, GameImages.ITEM_BIGBEAR_BAT,
                Attack.MeleeAttack(new Verb("smash", "smashes"), mwdata.ATK, mwdata.DMG, mwdata.STA, mwdata.DISARM))
            {
                EquipmentPart = DollPart.RIGHT_HAND,
                FlavorDescription = mwdata.FLAVOR,
                IsProper = true,
                IsUnbreakable = true,
                ToolBashDamageBonus = mwdata.TOOLBASHDMGBONUS, // alpha10
                ToolBuildBonus = mwdata.TOOLBUILDBONUS  // alpha10
            };

            mwdata = DATA_MELEE_UNIQUE_ROGUEDJACK_KEYBOARD;
            this[IDs.UNIQUE_ROGUEDJACK_KEYBOARD] = new ItemMeleeWeaponModel(mwdata.NAME, mwdata.PLURAL, GameImages.ITEM_ROGUEDJACK_KEYBOARD,
                Attack.MeleeAttack(new Verb("bash", "bashes"), mwdata.ATK, mwdata.DMG, mwdata.STA, mwdata.DISARM))
            {
                EquipmentPart = DollPart.RIGHT_HAND,
                FlavorDescription = mwdata.FLAVOR,
                IsProper = true,
                IsUnbreakable = true,
                ToolBashDamageBonus = mwdata.TOOLBASHDMGBONUS, // alpha10
                ToolBuildBonus = mwdata.TOOLBUILDBONUS  // alpha10
            };
            #endregion

            #region Ranged weapons
            // alpha10 rapid fire property
            RangedWeaponData rwp;

            rwp = DATA_RANGED_ARMY_PISTOL;
            this[IDs.RANGED_ARMY_PISTOL] = new ItemRangedWeaponModel(rwp.NAME, rwp.FLAVOR, GameImages.ITEM_ARMY_PISTOL,
                Attack.RangedAttack(AttackKind.FIREARM, new Verb("shoot"), rwp.ATK, rwp.RAPID1, rwp.RAPID2, rwp.DMG, rwp.RANGE),
                    rwp.MAXAMMO, AmmoType.HEAVY_PISTOL)
            {
                EquipmentPart = DollPart.RIGHT_HAND,
                FlavorDescription = rwp.FLAVOR,
                IsAn = true
            };

            rwp = DATA_RANGED_ARMY_RIFLE;
            this[IDs.RANGED_ARMY_RIFLE] = new ItemRangedWeaponModel(rwp.NAME, rwp.FLAVOR, GameImages.ITEM_ARMY_RIFLE,
                Attack.RangedAttack(AttackKind.FIREARM, new Verb("fire a salvo at", "fires a salvo at"), rwp.ATK, rwp.RAPID1, rwp.RAPID2, rwp.DMG, rwp.RANGE),
                     rwp.MAXAMMO, AmmoType.HEAVY_RIFLE)
            {
                EquipmentPart = DollPart.RIGHT_HAND,
                FlavorDescription = rwp.FLAVOR,
                IsAn = true
            };

            rwp = DATA_RANGED_HUNTING_CROSSBOW;
            this[IDs.RANGED_HUNTING_CROSSBOW] = new ItemRangedWeaponModel(rwp.NAME, rwp.FLAVOR, GameImages.ITEM_HUNTING_CROSSBOW,
                Attack.RangedAttack(AttackKind.BOW, new Verb("shoot"), rwp.ATK, rwp.RAPID1, rwp.RAPID2, rwp.DMG, rwp.RANGE),
                    rwp.MAXAMMO, AmmoType.BOLT)
            {
                EquipmentPart = DollPart.RIGHT_HAND,
                FlavorDescription = rwp.FLAVOR
            };

            rwp = DATA_RANGED_HUNTING_RIFLE;
            this[IDs.RANGED_HUNTING_RIFLE] = new ItemRangedWeaponModel(rwp.NAME, rwp.FLAVOR, GameImages.ITEM_HUNTING_RIFLE,
                Attack.RangedAttack(AttackKind.FIREARM, new Verb("shoot"), rwp.ATK, rwp.RAPID1, rwp.RAPID2, rwp.DMG, rwp.RANGE),
                    rwp.MAXAMMO, AmmoType.LIGHT_RIFLE)
                {
                    EquipmentPart = DollPart.RIGHT_HAND,
                    FlavorDescription = rwp.FLAVOR
                };

            rwp = DATA_RANGED_PISTOL;
            this[IDs.RANGED_PISTOL] = new ItemRangedWeaponModel(rwp.NAME, rwp.FLAVOR, GameImages.ITEM_PISTOL,
                Attack.RangedAttack(AttackKind.FIREARM, new Verb("shoot"), rwp.ATK, rwp.RAPID1, rwp.RAPID2, rwp.DMG, rwp.RANGE),
                    rwp.MAXAMMO, AmmoType.LIGHT_PISTOL)
                {
                    EquipmentPart = DollPart.RIGHT_HAND,
                    FlavorDescription =rwp.FLAVOR
                };

            rwp = DATA_RANGED_KOLT_REVOLVER;
            this[IDs.RANGED_KOLT_REVOLVER] = new ItemRangedWeaponModel(rwp.NAME, rwp.FLAVOR, GameImages.ITEM_KOLT_REVOLVER,
                Attack.RangedAttack(AttackKind.FIREARM, new Verb("shoot"), rwp.ATK, rwp.RAPID1, rwp.RAPID2, rwp.DMG, rwp.RANGE),
                    rwp.MAXAMMO, AmmoType.LIGHT_PISTOL)
            {
                EquipmentPart = DollPart.RIGHT_HAND,
                FlavorDescription = rwp.FLAVOR
            };

            rwp = DATA_RANGED_PRECISION_RIFLE;
            this[IDs.RANGED_PRECISION_RIFLE] = new ItemRangedWeaponModel(rwp.NAME, rwp.FLAVOR, GameImages.ITEM_PRECISION_RIFLE,
                Attack.RangedAttack(AttackKind.FIREARM, new Verb("shoot"), rwp.ATK, rwp.RAPID1, rwp.RAPID2, rwp.DMG, rwp.RANGE),
                    rwp.MAXAMMO, AmmoType.HEAVY_RIFLE)
            {
                EquipmentPart = DollPart.RIGHT_HAND,
                FlavorDescription = rwp.FLAVOR
            };

            rwp = DATA_RANGED_SHOTGUN;
            this[IDs.RANGED_SHOTGUN] = new ItemRangedWeaponModel(rwp.NAME, rwp.FLAVOR, GameImages.ITEM_SHOTGUN,
                Attack.RangedAttack(AttackKind.FIREARM, new Verb("shoot"), rwp.ATK, rwp.RAPID1, rwp.RAPID2, rwp.DMG, rwp.RANGE),
                    rwp.MAXAMMO, AmmoType.SHOTGUN)
                {
                    EquipmentPart = DollPart.RIGHT_HAND,
                    FlavorDescription = rwp.FLAVOR
                };

            rwp = DATA_UNIQUE_SANTAMAN_SHOTGUN;
            this[IDs.UNIQUE_SANTAMAN_SHOTGUN] = new ItemRangedWeaponModel(rwp.NAME, rwp.FLAVOR, GameImages.ITEM_SANTAMAN_SHOTGUN,
                Attack.RangedAttack(AttackKind.FIREARM, new Verb("shoot"), rwp.ATK, rwp.RAPID1, rwp.RAPID2, rwp.DMG, rwp.RANGE),
                    rwp.MAXAMMO, AmmoType.SHOTGUN)
            {
                EquipmentPart = DollPart.RIGHT_HAND,
                FlavorDescription = rwp.FLAVOR,
                IsProper = true,
                IsUnbreakable = true
            };

            rwp = DATA_UNIQUE_HANS_VON_HANZ_PISTOL;
            this[IDs.UNIQUE_HANS_VON_HANZ_PISTOL] = new ItemRangedWeaponModel(rwp.NAME, rwp.FLAVOR, GameImages.ITEM_HANS_VON_HANZ_PISTOL,
                Attack.RangedAttack(AttackKind.FIREARM, new Verb("shoot"), rwp.ATK, rwp.RAPID1, rwp.RAPID2, rwp.DMG, rwp.RANGE),
                    rwp.MAXAMMO, AmmoType.LIGHT_PISTOL)
            {
                EquipmentPart = DollPart.RIGHT_HAND,
                FlavorDescription = rwp.FLAVOR,
                IsProper = true,
                IsUnbreakable = true
            };
            #endregion

            #region Ammos
            this[IDs.AMMO_LIGHT_PISTOL] = new ItemAmmoModel("light pistol bullets", "light pistol bullets", GameImages.ITEM_AMMO_LIGHT_PISTOL,
                AmmoType.LIGHT_PISTOL, 20)
                {
                    IsPlural = true,
                    FlavorDescription = ""
                };

            this[IDs.AMMO_HEAVY_PISTOL] = new ItemAmmoModel("heavy pistol bullets", "heavy pistol bullets", GameImages.ITEM_AMMO_HEAVY_PISTOL,
                AmmoType.HEAVY_PISTOL, 12)
            {
                IsPlural = true,
                FlavorDescription = ""
            };

            this[IDs.AMMO_LIGHT_RIFLE] = new ItemAmmoModel("light rifle bullets", "light rifle bullets", GameImages.ITEM_AMMO_LIGHT_RIFLE,
                AmmoType.LIGHT_RIFLE, 14)
            {
                IsPlural = true,
                FlavorDescription = ""
            };

            this[IDs.AMMO_HEAVY_RIFLE] = new ItemAmmoModel("heavy rifle bullets", "heavy rifle bullets", GameImages.ITEM_AMMO_HEAVY_RIFLE,
                AmmoType.HEAVY_RIFLE, 20)
            {
                IsPlural = true,
                FlavorDescription = ""
            };

            this[IDs.AMMO_SHOTGUN] = new ItemAmmoModel("shotgun shells", "shotgun shells", GameImages.ITEM_AMMO_SHOTGUN,
                AmmoType.SHOTGUN, 10)
            {
                IsPlural = true,
                FlavorDescription = ""
            };

            this[IDs.AMMO_BOLTS] = new ItemAmmoModel("crossbow bolts", "crossbow bolts", GameImages.ITEM_AMMO_BOLTS,
                AmmoType.BOLT, 30)
            {
                IsPlural = true,
                FlavorDescription = ""
            };
            #endregion

            #region Explosives
            ExplosiveData exData;
            int[] exArray;

            exData = DATA_EXPLOSIVE_GRENADE;
            exArray = new int[exData.RADIUS + 1];
            for (int i = 0; i < exData.RADIUS + 1; i++)
                exArray[i] = exData.DMG[i];
            this[IDs.EXPLOSIVE_GRENADE] = new ItemGrenadeModel(exData.NAME, exData.PLURAL, GameImages.ITEM_GRENADE,
                exData.FUSE, new BlastAttack(exData.RADIUS, exArray, true, false), GameImages.ICON_BLAST, exData.MAXTHROW)
                {
                    EquipmentPart = DollPart.RIGHT_HAND,
                    IsStackable = true,
                    StackingLimit =exData.STACKLINGLIMIT,
                    FlavorDescription = exData.FLAVOR
                };

            this[IDs.EXPLOSIVE_GRENADE_PRIMED] = new ItemGrenadePrimedModel("primed " +exData.NAME, "primed "+exData.PLURAL, GameImages.ITEM_GRENADE_PRIMED, this[IDs.EXPLOSIVE_GRENADE] as ItemGrenadeModel)
            {
                EquipmentPart = DollPart.RIGHT_HAND
            };
            #endregion

            #region Barricade material
            BarricadingMaterialData barData;

            barData = DATA_BAR_WOODEN_PLANK;
            this[IDs.BAR_WOODEN_PLANK] = new ItemBarricadeMaterialModel(barData.NAME, barData.PLURAL, GameImages.ITEM_WOODEN_PLANK, barData.VALUE)
            {
                IsStackable = (barData.STACKINGLIMIT > 1),
                StackingLimit = barData.STACKINGLIMIT,
                FlavorDescription = barData.FLAVOR
            };
            #endregion

            #region Bodyarmors
            ArmorData armData;

            armData = DATA_ARMOR_ARMY;
            this[IDs.ARMOR_ARMY_BODYARMOR] = new ItemBodyArmorModel(armData.NAME, armData.PLURAL, GameImages.ITEM_ARMY_BODYARMOR, armData.PRO_HIT, armData.PRO_SHOT, armData.ENC, armData.WEIGHT)
            {
                EquipmentPart = DollPart.TORSO,
                FlavorDescription = armData.FLAVOR,
                IsAn = StartsWithVowel(armData.NAME)
            };

            armData = DATA_ARMOR_CHAR;
            this[IDs.ARMOR_CHAR_LIGHT_BODYARMOR] = new ItemBodyArmorModel(armData.NAME, armData.PLURAL, GameImages.ITEM_CHAR_LIGHT_BODYARMOR, armData.PRO_HIT, armData.PRO_SHOT, armData.ENC, armData.WEIGHT)
            {
                EquipmentPart = DollPart.TORSO,
                FlavorDescription = armData.FLAVOR,
                IsAn = StartsWithVowel(armData.NAME)
            };

            armData = DATA_ARMOR_HELLS_SOULS_JACKET;
            this[IDs.ARMOR_HELLS_SOULS_JACKET] = new ItemBodyArmorModel(armData.NAME, armData.PLURAL, GameImages.ITEM_HELLS_SOULS_JACKET, armData.PRO_HIT, armData.PRO_SHOT, armData.ENC, armData.WEIGHT)
            {
                EquipmentPart = DollPart.TORSO,
                FlavorDescription = armData.FLAVOR,
                IsAn = StartsWithVowel(armData.NAME)
            };

            armData = DATA_ARMOR_FREE_ANGELS_JACKET;
            this[IDs.ARMOR_FREE_ANGELS_JACKET] = new ItemBodyArmorModel(armData.NAME, armData.PLURAL, GameImages.ITEM_FREE_ANGELS_JACKET, armData.PRO_HIT, armData.PRO_SHOT, armData.ENC, armData.WEIGHT)
            {
                EquipmentPart = DollPart.TORSO,
                FlavorDescription = armData.FLAVOR,
                IsAn = StartsWithVowel(armData.NAME)
            };

            armData = DATA_ARMOR_POLICE_JACKET;
            this[IDs.ARMOR_POLICE_JACKET] = new ItemBodyArmorModel(armData.NAME, armData.PLURAL, GameImages.ITEM_POLICE_JACKET, armData.PRO_HIT, armData.PRO_SHOT, armData.ENC, armData.WEIGHT)
            {
                EquipmentPart = DollPart.TORSO,
                FlavorDescription = armData.FLAVOR,
                IsAn = StartsWithVowel(armData.NAME)
            };

            armData = DATA_ARMOR_POLICE_RIOT;
            this[IDs.ARMOR_POLICE_RIOT] = new ItemBodyArmorModel(armData.NAME, armData.PLURAL, GameImages.ITEM_POLICE_RIOT_ARMOR, armData.PRO_HIT, armData.PRO_SHOT, armData.ENC, armData.WEIGHT)
            {
                EquipmentPart = DollPart.TORSO,
                FlavorDescription = armData.FLAVOR,
                IsAn = StartsWithVowel(armData.NAME)
            };

            armData = DATA_ARMOR_HUNTER_VEST;
            this[IDs.ARMOR_HUNTER_VEST] = new ItemBodyArmorModel(armData.NAME, armData.PLURAL, GameImages.ITEM_HUNTER_VEST, armData.PRO_HIT, armData.PRO_SHOT, armData.ENC, armData.WEIGHT)
            {
                EquipmentPart = DollPart.TORSO,
                FlavorDescription = armData.FLAVOR,
                IsAn = StartsWithVowel(armData.NAME)
            };


            #endregion

            #region Trackers
            // alpha10 added clock prop to trackers

            TrackerData traData;

            traData = DATA_TRACKER_CELL_PHONE;
            this[IDs.TRACKER_CELL_PHONE] = new ItemTrackerModel(traData.NAME, traData.PLURAL, GameImages.ITEM_CELL_PHONE,
                ItemTrackerModel.TrackingFlags.FOLLOWER_AND_LEADER,
                traData.BATTERIES * WorldTime.TURNS_PER_HOUR,
                traData.HASCLOCK)
            {
                EquipmentPart = DollPart.LEFT_HAND,
                FlavorDescription = traData.FLAVOR
            };

            traData = DATA_TRACKER_ZTRACKER;
            this[IDs.TRACKER_ZTRACKER] = new ItemTrackerModel(traData.NAME, traData.PLURAL, GameImages.ITEM_ZTRACKER,
                ItemTrackerModel.TrackingFlags.UNDEADS,
                traData.BATTERIES * WorldTime.TURNS_PER_HOUR,
                traData.HASCLOCK)
                {
                    EquipmentPart = DollPart.LEFT_HAND,
                    FlavorDescription = traData.FLAVOR
                };

            traData = DATA_TRACKER_BLACKOPS_GPS;
            this[IDs.TRACKER_BLACKOPS] = new ItemTrackerModel(traData.NAME, traData.PLURAL, GameImages.ITEM_BLACKOPS_GPS,
                ItemTrackerModel.TrackingFlags.BLACKOPS_FACTION,
                traData.BATTERIES * WorldTime.TURNS_PER_HOUR,
                traData.HASCLOCK)
            {
                EquipmentPart = DollPart.LEFT_HAND,
                FlavorDescription = traData.FLAVOR
            };

            traData = DATA_TRACKER_POLICE_RADIO;
            this[IDs.TRACKER_POLICE_RADIO] = new ItemTrackerModel(traData.NAME, traData.PLURAL, GameImages.ITEM_POLICE_RADIO,
                ItemTrackerModel.TrackingFlags.POLICE_FACTION,
                traData.BATTERIES * WorldTime.TURNS_PER_HOUR,
                traData.HASCLOCK)
            {
                EquipmentPart = DollPart.LEFT_HAND,
                FlavorDescription = traData.FLAVOR
            };
            #endregion

            #region Spray Paint
            SprayPaintData spData;

            spData = DATA_SPRAY_PAINT1;
            this[IDs.SPRAY_PAINT1] = new ItemSprayPaintModel(spData.NAME, spData.PLURAL, GameImages.ITEM_SPRAYPAINT, spData.QUANTITY, GameImages.DECO_PLAYER_TAG1)
            {
                EquipmentPart = DollPart.LEFT_HAND,
                FlavorDescription = spData.FLAVOR
            };

            spData = DATA_SPRAY_PAINT2;
            this[IDs.SPRAY_PAINT2] = new ItemSprayPaintModel(spData.NAME, spData.PLURAL, GameImages.ITEM_SPRAYPAINT2, spData.QUANTITY, GameImages.DECO_PLAYER_TAG2)
            {
                EquipmentPart = DollPart.LEFT_HAND,
                FlavorDescription = spData.FLAVOR
            };

            spData = DATA_SPRAY_PAINT3;
            this[IDs.SPRAY_PAINT3] = new ItemSprayPaintModel(spData.NAME, spData.PLURAL, GameImages.ITEM_SPRAYPAINT3, spData.QUANTITY, GameImages.DECO_PLAYER_TAG3)
            {
                EquipmentPart = DollPart.LEFT_HAND,
                FlavorDescription = spData.FLAVOR
            };

            spData = DATA_SPRAY_PAINT4;
            this[IDs.SPRAY_PAINT4] = new ItemSprayPaintModel(spData.NAME, spData.PLURAL, GameImages.ITEM_SPRAYPAINT4, spData.QUANTITY, GameImages.DECO_PLAYER_TAG4)
            {
                EquipmentPart = DollPart.LEFT_HAND,
                FlavorDescription = spData.FLAVOR
            };
            #endregion

            #region Lights
            LightData ltData;

            ltData = DATA_LIGHT_FLASHLIGHT;
            this[IDs.LIGHT_FLASHLIGHT] = new ItemLightModel(ltData.NAME, ltData.PLURAL, GameImages.ITEM_FLASHLIGHT, ltData.FOV, ltData.BATTERIES * WorldTime.TURNS_PER_HOUR, GameImages.ITEM_FLASHLIGHT_OUT)
            {
                EquipmentPart = DollPart.LEFT_HAND,
                FlavorDescription = ltData.FLAVOR
            };

            ltData = DATA_LIGHT_BIG_FLASHLIGHT;
            this[IDs.LIGHT_BIG_FLASHLIGHT] = new ItemLightModel(ltData.NAME, ltData.PLURAL, GameImages.ITEM_BIG_FLASHLIGHT, ltData.FOV, ltData.BATTERIES * WorldTime.TURNS_PER_HOUR, GameImages.ITEM_BIG_FLASHLIGHT_OUT)
            {
                EquipmentPart = DollPart.LEFT_HAND,
                FlavorDescription = ltData.FLAVOR
            };

            #endregion

            #region Scent sprays
            ScentSprayData sspData;

            // alpha10 new way of using stench killer
            sspData = DATA_SCENT_SPRAY_STENCH_KILLER;
            this[IDs.SCENT_SPRAY_STENCH_KILLER] = new ItemSprayScentModel(sspData.NAME, sspData.PLURAL, GameImages.ITEM_STENCH_KILLER,
                sspData.QUANTITY, Odor.SUPPRESSOR, sspData.STRENGTH * WorldTime.TURNS_PER_HOUR)
            {
                EquipmentPart = DollPart.LEFT_HAND,
                FlavorDescription = sspData.FLAVOR
            };
            #endregion

            #region Traps
            TrapData trpData;

            trpData = DATA_TRAP_EMPTY_CAN;
            this[IDs.TRAP_EMPTY_CAN] = new ItemTrapModel(trpData.NAME, trpData.PLURAL, GameImages.ITEM_EMPTY_CAN,
                trpData.STACKING, trpData.CHANCE, trpData.DAMAGE,
                trpData.DROP_ACTIVATE, trpData.USE_ACTIVATE, trpData.IS_ONE_TIME,
                trpData.BREAK_CHANCE, trpData.BLOCK_CHANCE, trpData.BREAK_CHANCE_ESCAPE,
                trpData.IS_NOISY, trpData.NOISE_NAME, trpData.IS_FLAMMABLE)
            {
                FlavorDescription = trpData.FLAVOR
            };

            trpData = DATA_TRAP_BEAR_TRAP;
            this[IDs.TRAP_BEAR_TRAP] = new ItemTrapModel(trpData.NAME, trpData.PLURAL, GameImages.ITEM_BEAR_TRAP,
                trpData.STACKING, trpData.CHANCE, trpData.DAMAGE,
                trpData.DROP_ACTIVATE, trpData.USE_ACTIVATE, trpData.IS_ONE_TIME,
                trpData.BREAK_CHANCE, trpData.BLOCK_CHANCE, trpData.BREAK_CHANCE_ESCAPE,
                trpData.IS_NOISY, trpData.NOISE_NAME, trpData.IS_FLAMMABLE)
            {
                FlavorDescription = trpData.FLAVOR
            };

            trpData = DATA_TRAP_SPIKES;
            this[IDs.TRAP_SPIKES] = new ItemTrapModel(trpData.NAME, trpData.PLURAL, GameImages.ITEM_SPIKES,
                trpData.STACKING, trpData.CHANCE, trpData.DAMAGE,
                trpData.DROP_ACTIVATE, trpData.USE_ACTIVATE, trpData.IS_ONE_TIME,
                trpData.BREAK_CHANCE, trpData.BLOCK_CHANCE, trpData.BREAK_CHANCE_ESCAPE,
                trpData.IS_NOISY, trpData.NOISE_NAME, trpData.IS_FLAMMABLE)
            {
                FlavorDescription = trpData.FLAVOR
            };

            trpData = DATA_TRAP_BARBED_WIRE;
            this[IDs.TRAP_BARBED_WIRE] = new ItemTrapModel(trpData.NAME, trpData.PLURAL, GameImages.ITEM_BARBED_WIRE,
                trpData.STACKING, trpData.CHANCE, trpData.DAMAGE,
                trpData.DROP_ACTIVATE, trpData.USE_ACTIVATE, trpData.IS_ONE_TIME,
                trpData.BREAK_CHANCE, trpData.BLOCK_CHANCE, trpData.BREAK_CHANCE_ESCAPE,
                trpData.IS_NOISY, trpData.NOISE_NAME, trpData.IS_FLAMMABLE)
            {
                FlavorDescription = trpData.FLAVOR
            };

            #endregion

            #region Entertainment
            EntData entData;

            entData = DATA_ENT_BOOK;
            this[IDs.ENT_BOOK] = new ItemEntertainmentModel(entData.NAME, entData.PLURAL, GameImages.ITEM_BOOK, entData.VALUE, entData.BORECHANCE)
            {
                StackingLimit = entData.STACKING,
                FlavorDescription = entData.FLAVOR
            };

            entData = DATA_ENT_MAGAZINE;
            this[IDs.ENT_MAGAZINE] = new ItemEntertainmentModel(entData.NAME, entData.PLURAL, GameImages.ITEM_MAGAZINE, entData.VALUE, entData.BORECHANCE)
            {
                StackingLimit = entData.STACKING,
                FlavorDescription = entData.FLAVOR
            };
            #endregion

            #region Uniques
            this[IDs.UNIQUE_SUBWAY_BADGE] = new ItemModel("Subway Worker Badge", "Subways Worker Badges", GameImages.ITEM_SUBWAY_BADGE)
            {
                DontAutoEquip = true,
                EquipmentPart = DollPart.LEFT_HAND,
                FlavorDescription = "You got yourself a new job!"
            };
            #endregion

            #region Fixes/Post processing
            for (int i = (int)IDs._FIRST; i < (int)IDs._COUNT; i++)
            {
                ItemModel model = this[i];

                // grammar.
                model.IsAn = StartsWithVowel(model.SingleName);

                // IsStackable
                model.IsStackable = model.StackingLimit > 1;
            }
            #endregion
        }

        #endregion
    }
}
