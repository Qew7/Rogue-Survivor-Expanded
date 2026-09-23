using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.UI;

namespace djack.RogueSurvivor.Gameplay.Generators
{
    partial class BaseTownGenerator
    {
        #region Rooms
        // alpha10.1 allow different x and y min size
        protected virtual void MakeRoomsPlan(Map map, ref List<Rectangle> list, Rectangle rect, int minRoomsXSize, int minRoomsYSize)
        {
            ////////////
            // 1. Split
            ////////////
            int splitX, splitY;
            Rectangle topLeft, topRight, bottomLeft, bottomRight;
            QuadSplit(rect, minRoomsXSize, minRoomsYSize, out splitX, out splitY, out topLeft, out topRight, out bottomLeft, out bottomRight);

            ///////////////////
            // 2. Termination?
            ///////////////////
            if (topRight.IsEmpty && bottomLeft.IsEmpty && bottomRight.IsEmpty)
            {
                list.Add(rect);
                return;
            }

            //////////////
            // 3. Recurse
            //////////////
            // always top left.
            MakeRoomsPlan(map, ref list, topLeft, minRoomsXSize, minRoomsYSize);
            // then recurse in non empty quads.
            // we shift and inflante the quads cause we want rooms walls and doors to overlap.
            if (!topRight.IsEmpty)
            {
                topRight.Offset(-1, 0);
                ++topRight.Width;
                MakeRoomsPlan(map, ref list, topRight, minRoomsXSize, minRoomsYSize);
            }
            if (!bottomLeft.IsEmpty)
            {
                bottomLeft.Offset(0, -1);
                ++bottomLeft.Height;
                MakeRoomsPlan(map, ref list, bottomLeft, minRoomsXSize, minRoomsYSize);
            }
            if (!bottomRight.IsEmpty)
            {
                bottomRight.Offset(-1, -1);
                ++bottomRight.Width;
                ++bottomRight.Height;
                MakeRoomsPlan(map, ref list, bottomRight, minRoomsXSize, minRoomsYSize);
            }
        }

        protected virtual void MakeHousingRoom(Map map, Rectangle roomRect, TileModel floor, TileModel wall)
        {
            ////////////////////
            // 1. Floor & Walls
            ////////////////////
            base.TileFill(map, floor, roomRect);
            base.TileRectangle(map, wall, roomRect.Left, roomRect.Top, roomRect.Width, roomRect.Height,
                (tile, prevmodel, x, y) =>
                {
                    // if we have a door there, don't put a wall!
                    if (map.GetMapObjectAt(x, y) != null)
                        map.SetTileModelAt(x, y, floor);
                });

            //////////////////////
            // 2. Doors & Windows
            //////////////////////
            int midX = roomRect.Left + roomRect.Width / 2;
            int midY = roomRect.Top + roomRect.Height / 2;
            const int outsideDoorChance = 25;

            PlaceIf(map, midX, roomRect.Top, floor,
                (x, y) => HasNoObjectAt(map, x, y) && IsAccessible(map, x, y) && CountAdjDoors(map, x, y) == 0,
                (x, y) => IsInside(map, x, y) || m_DiceRoller.RollChance(outsideDoorChance) ? MakeObjWoodenDoor() : MakeObjWindow());
            PlaceIf(map, midX, roomRect.Bottom - 1, floor,
                (x, y) => HasNoObjectAt(map, x, y) && IsAccessible(map, x, y) && CountAdjDoors(map, x, y) == 0,
                (x, y) => IsInside(map, x, y) || m_DiceRoller.RollChance(outsideDoorChance) ? MakeObjWoodenDoor() : MakeObjWindow());
            PlaceIf(map, roomRect.Left, midY, floor,
                (x, y) => HasNoObjectAt(map, x, y) && IsAccessible(map, x, y) && CountAdjDoors(map, x, y) == 0,
                (x, y) => IsInside(map, x, y) || m_DiceRoller.RollChance(outsideDoorChance) ? MakeObjWoodenDoor() : MakeObjWindow());
            PlaceIf(map, roomRect.Right - 1, midY, floor,
                (x, y) => HasNoObjectAt(map, x, y) && IsAccessible(map, x, y) && CountAdjDoors(map, x, y) == 0,
                (x, y) => IsInside(map, x, y) || m_DiceRoller.RollChance(outsideDoorChance) ? MakeObjWoodenDoor() : MakeObjWindow());
        }

        // alpha10.1 can force room role (optional param)
        // FIXME -- room role should be an enum and not hardcoded numbers -_-
        /// <summary>
        ///
        /// </summary>
        /// <param name="map"></param>
        /// <param name="roomRect"></param>
        /// <param name="role">-1 roll at random; 0-4 bedroom, 5-7 living room, 8-9 kitchen</param>
        protected virtual void FillHousingRoomContents(Map map, Rectangle roomRect, int role = -1)
        {
            Rectangle insideRoom = new Rectangle(roomRect.Left + 1, roomRect.Top + 1, roomRect.Width - 2, roomRect.Height - 2);

            // alpha10.1 roll room role if not set
            if (role == -1)
                role = m_DiceRoller.Roll(0, 10);

            // alpha10.1 added restriction to not place a mapobj if adj to at least 5 mapobj as to not cramp apartements

            switch (role)
            {
                // 1. Bedroom? 0-4 = 50%
                #region
                case 0:
                case 1:
                case 2:
                case 3:
                case 4:
                    {
                        #region
                        // beds with night tables.
                        int nbBeds = m_DiceRoller.Roll(1, 3);
                        for (int i = 0; i < nbBeds; i++)
                        {
                            MapObjectPlaceInGoodPosition(map, insideRoom,
                                (pt) => CountAdjWalls(map, pt.X, pt.Y) >= 3 && CountAdjDoors(map, pt.X, pt.Y) == 0 && CountAdjMapObjects(map, pt.X, pt.Y) < 5,  // alpha10.1 not cramped
                                m_DiceRoller,
                                (pt) =>
                                {
                                    // one night table around with item.
                                    Rectangle adjBedRect = new Rectangle(pt.X - 1, pt.Y - 1, 3, 3);
                                    adjBedRect.Intersect(insideRoom);
                                    MapObjectPlaceInGoodPosition(map, adjBedRect,
                                        (pt2) => pt2 != pt && CountAdjDoors(map, pt2.X, pt2.Y) == 0 && CountAdjWalls(map, pt2.X, pt2.Y) > 0 && CountAdjMapObjects(map, pt.X, pt.Y) < 5,  // alpha10.1 not cramped,
                                        m_DiceRoller,
                                        (pt2) =>
                                        {
                                            // item.
                                            Item it = MakeRandomBedroomItem();
                                            if (it != null)
                                                map.DropItemAt(it, pt2);

                                            // night table.
                                            return MakeObjNightTable(GameImages.OBJ_NIGHT_TABLE);
                                        });

                                    // bed.
                                    MapObject bed = MakeObjBed(GameImages.OBJ_BED);
                                    return bed;
                                });
                        }

                        // wardrobe/drawer with items
                        int nbWardrobeOrDrawer = m_DiceRoller.Roll(1, 4);
                        for (int i = 0; i < nbWardrobeOrDrawer; i++)
                        {
                            MapObjectPlaceInGoodPosition(map, insideRoom,
                                                (pt) => CountAdjWalls(map, pt.X, pt.Y) >= 2 && CountAdjDoors(map, pt.X, pt.Y) == 0 && CountAdjMapObjects(map, pt.X, pt.Y) < 5,  // alpha10.1 not cramped,
                                                m_DiceRoller,
                                                (pt) =>
                                                {
                                                    // item.
                                                    Item it = MakeRandomBedroomItem();
                                                    if (it != null)
                                                        map.DropItemAt(it, pt);

                                                    // wardrobe or drawer
                                                    if (m_DiceRoller.RollChance(50))
                                                        return MakeObjWardrobe(GameImages.OBJ_WARDROBE);
                                                    else
                                                        return MakeObjDrawer(GameImages.OBJ_DRAWER);
                                                });
                        }
                        break;
                        #endregion
                    }
                #endregion

                // 2. Living room? 5-6-7 = 30%
                #region
                case 5:
                case 6:
                case 7:
                    {
                        #region
                        // tables with chairs.
                        int nbTables = m_DiceRoller.Roll(1, 3);

                        for (int i = 0; i < nbTables; i++)
                        {
                            MapObjectPlaceInGoodPosition(map, insideRoom,
                                (pt) => CountAdjWalls(map, pt.X, pt.Y) == 0 && CountAdjDoors(map, pt.X, pt.Y) == 0 && CountAdjMapObjects(map, pt.X, pt.Y) < 5,  // alpha10.1 not cramped,
                                m_DiceRoller,
                                (pt) =>
                                {
                                    // items.
                                    for (int ii = 0; ii < HOUSE_LIVINGROOM_ITEMS_ON_TABLE; ii++)
                                    {
                                        Item it = MakeRandomKitchenItem();
                                        if (it != null)
                                            map.DropItemAt(it, pt);
                                    }

                                    // one chair around.
                                    Rectangle adjTableRect = new Rectangle(pt.X - 1, pt.Y - 1, 3, 3);
                                    adjTableRect.Intersect(insideRoom);
                                    MapObjectPlaceInGoodPosition(map, adjTableRect,
                                        (pt2) => pt2 != pt && CountAdjDoors(map, pt2.X, pt2.Y) == 0 && CountAdjMapObjects(map, pt.X, pt.Y) < 5,  // alpha10.1 not cramped,
                                        m_DiceRoller,
                                        (pt2) => MakeObjChair(GameImages.OBJ_CHAIR));

                                    // table.
                                    MapObject table = MakeObjTable(GameImages.OBJ_TABLE);
                                    return table;
                                });
                        }

                        // drawers.
                        int nbDrawers = m_DiceRoller.Roll(1, 3);
                        for (int i = 0; i < nbDrawers; i++)
                        {
                            MapObjectPlaceInGoodPosition(map, insideRoom,
                                                (pt) => CountAdjWalls(map, pt.X, pt.Y) >= 2 && CountAdjDoors(map, pt.X, pt.Y) == 0 && CountAdjMapObjects(map, pt.X, pt.Y) < 5,  // alpha10.1 not cramped,
                                                m_DiceRoller,
                                                (pt) => MakeObjDrawer(GameImages.OBJ_DRAWER));
                        }
                        break;
                        #endregion
                    }
                #endregion

                // 3. Kitchen? 8-9 = 20%
                #region
                case 8:
                case 9:
                    {
                        #region
                        // table with item & chair.
                        MapObjectPlaceInGoodPosition(map, insideRoom,
                            (pt) => CountAdjWalls(map, pt.X, pt.Y) == 0 && CountAdjDoors(map, pt.X, pt.Y) == 0 && CountAdjMapObjects(map, pt.X, pt.Y) < 5,  // alpha10.1 not cramped,
                            m_DiceRoller,
                            (pt) =>
                            {
                                // items.
                                for (int ii = 0; ii < HOUSE_KITCHEN_ITEMS_ON_TABLE; ii++)
                                {
                                    Item it = MakeRandomKitchenItem();
                                    if (it != null)
                                        map.DropItemAt(it, pt);
                                }

                                // one chair around.
                                Rectangle adjTableRect = new Rectangle(pt.X - 1, pt.Y - 1, 3, 3);
                                MapObjectPlaceInGoodPosition(map, adjTableRect,
                                    (pt2) => pt2 != pt && CountAdjDoors(map, pt2.X, pt2.Y) == 0,
                                    m_DiceRoller,
                                    (pt2) => MakeObjChair(GameImages.OBJ_CHAIR));

                                // table.
                                return MakeObjTable(GameImages.OBJ_TABLE);
                            });

                        // fridge with items
                        MapObjectPlaceInGoodPosition(map, insideRoom,
                                            (pt) => CountAdjWalls(map, pt.X, pt.Y) >= 2 && CountAdjDoors(map, pt.X, pt.Y) == 0 && CountAdjMapObjects(map, pt.X, pt.Y) < 5,  // alpha10.1 not cramped,
                                            m_DiceRoller,
                                            (pt) =>
                                            {
                                                // items.
                                                for (int ii = 0; ii < HOUSE_KITCHEN_ITEMS_IN_FRIDGE; ii++)
                                                {
                                                    Item it = MakeRandomKitchenItem();
                                                    if (it != null)
                                                        map.DropItemAt(it, pt);
                                                }

                                                // fridge
                                                return MakeObjFridge(GameImages.OBJ_FRIDGE);
                                            });
                        break;
                        #endregion
                    }

                default:
                    throw new ArgumentOutOfRangeException("unhandled roll");
                    #endregion
            }
        }

        #endregion
        #region Items
        protected Item MakeRandomShopItem(ShopType shop)
        {
            switch (shop)
            {
                case ShopType.CONSTRUCTION:
                    return MakeShopConstructionItem();
                case ShopType.GENERAL_STORE:
                    return MakeShopGeneralItem();
                case ShopType.GROCERY:
                    return MakeShopGroceryItem();
                case ShopType.GUNSHOP:
                    return MakeShopGunshopItem();
                case ShopType.PHARMACY:
                    return MakeShopPharmacyItem();
                case ShopType.SPORTSWEAR:
                    return MakeShopSportsWearItem();
                case ShopType.HUNTING:
                    return MakeHuntingShopItem();
                default:
                    throw new ArgumentOutOfRangeException("unhandled shoptype");

            }
        }

        public Item MakeShopGroceryItem()
        {
            if (m_DiceRoller.RollChance(50))
                return MakeItemCannedFood();
            else
                return MakeItemGroceries();
        }

        public Item MakeShopPharmacyItem()
        {
            int randomItem = m_DiceRoller.Roll(0, 6);
            switch (randomItem)
            {
                case 0: return MakeItemBandages();
                case 1: return MakeItemMedikit();
                case 2: return MakeItemPillsSLP();
                case 3: return MakeItemPillsSTA();
                case 4: return MakeItemPillsSAN();
                case 5: return MakeItemStenchKiller();

                default:
                    throw new ArgumentOutOfRangeException("unhandled roll");
            }
        }

        public Item MakeShopSportsWearItem()
        {
            int roll = m_DiceRoller.Roll(0, 10);

            switch (roll)
            {
                case 0:
                    if (m_DiceRoller.RollChance(30))
                        return MakeItemHuntingRifle();
                    else
                        return MakeItemLightRifleAmmo();
                case 1:
                    if (m_DiceRoller.RollChance(30))
                        return MakeItemHuntingCrossbow();
                    else
                        return MakeItemBoltsAmmo();
                case 2:
                case 3:
                case 4:
                case 5: return MakeItemBaseballBat();       // 40%

                case 6:
                case 7: return MakeItemIronGolfClub();      // 20%

                case 8:
                case 9: return MakeItemGolfClub();          // 20%
                default:
                    throw new ArgumentOutOfRangeException("unhandled roll");
            }
        }

        public Item MakeShopConstructionItem()
        {
            int roll = m_DiceRoller.Roll(0, 24);
            switch (roll)
            {
                case 0:
                case 1:
                case 2: return m_DiceRoller.RollChance(50) ? MakeItemShovel() : MakeItemShortShovel();

                case 3:
                case 4:
                case 5: return MakeItemCrowbar();

                case 6:
                case 7:
                case 8: return m_DiceRoller.RollChance(50) ? MakeItemHugeHammer() : MakeItemSmallHammer();

                case 9:
                case 10:
                case 11: return MakeItemWoodenPlank();

                case 12:
                case 13:
                case 14: return MakeItemFlashlight();

                case 15:
                case 16:
                case 17: return MakeItemBigFlashlight();

                case 18:
                case 19:
                case 20: return MakeItemSpikes();

                case 21:
                case 22:
                case 23: return MakeItemBarbedWire();

                default:
                    throw new ArgumentOutOfRangeException("unhandled roll");
            }
        }

        public Item MakeShopGunshopItem()
        {
            // Weapons (40%) vs Ammo (60%)
            if (m_DiceRoller.RollChance(40))
            {
                int roll = m_DiceRoller.Roll(0, 4);

                switch (roll)
                {
                    case 0: return MakeItemRandomPistol();
                    case 1: return MakeItemShotgun();
                    case 2: return MakeItemHuntingRifle();
                    case 3: return MakeItemHuntingCrossbow();

                    default:
                        return null;
                }
            }
            else
            {
                int roll = m_DiceRoller.Roll(0, 4);

                switch (roll)
                {
                    case 0: return MakeItemLightPistolAmmo();
                    case 1: return MakeItemShotgunAmmo();
                    case 2: return MakeItemLightRifleAmmo();
                    case 3: return MakeItemBoltsAmmo();

                    default:
                        return null;
                }
            }
        }

        public Item MakeHuntingShopItem()
        {
            // Weapons/Ammo (50%) Outfits&Traps (50%)
            if (m_DiceRoller.RollChance(50))
            {
                // Weapons(40) Ammo(60)
                if (m_DiceRoller.RollChance(40))
                {
                    int roll = m_DiceRoller.Roll(0, 2);

                    switch (roll)
                    {
                        case 0: return MakeItemHuntingRifle();
                        case 1: return MakeItemHuntingCrossbow();
                        default:
                            return null;
                    }
                }
                else
                {
                    int roll = m_DiceRoller.Roll(0, 2);

                    switch (roll)
                    {
                        case 0: return MakeItemLightRifleAmmo();
                        case 1: return MakeItemBoltsAmmo();
                        default:
                            return null;
                    }
                }
            }
            else
            {
                // Outfits&Traps
                int roll = m_DiceRoller.Roll(0, 2);
                switch (roll)
                {
                    case 0: return MakeItemHunterVest();
                    case 1: return MakeItemBearTrap();
                    default:
                        return null;
                }
            }
        }

        public Item MakeShopGeneralItem()
        {
            int roll = m_DiceRoller.Roll(0, 6);
            switch (roll)
            {
                case 0: return MakeShopPharmacyItem();
                case 1: return MakeShopSportsWearItem();
                case 2: return MakeShopConstructionItem();
                case 3: return MakeShopGroceryItem();
                case 4: return MakeHuntingShopItem();
                case 5: return MakeRandomBedroomItem();
                default:
                    throw new ArgumentOutOfRangeException("unhandled roll");
            }
        }

        public Item MakeHospitalItem()
        {
            int randomItem = m_DiceRoller.Roll(0, 7);
            switch (randomItem)
            {
                case 0: return MakeItemBandages();
                case 1: return MakeItemMedikit();
                case 2: return MakeItemPillsSLP();
                case 3: return MakeItemPillsSTA();
                case 4: return MakeItemPillsSAN();
                case 5: return MakeItemStenchKiller();
                case 6: return MakeItemPillsAntiviral();

                default:
                    throw new ArgumentOutOfRangeException("unhandled roll");
            }
        }

        public Item MakeRandomBedroomItem()
        {
            int randomItem = m_DiceRoller.Roll(0, 24);

            switch (randomItem)
            {
                case 0:
                case 1: return MakeItemBandages();
                case 2: return MakeItemPillsSTA();
                case 3: return MakeItemPillsSLP();
                case 4: return MakeItemPillsSAN();

                case 5:
                case 6:
                case 7:
                case 8: return MakeItemBaseballBat();

                case 9: return MakeItemRandomPistol();

                case 10: // rare fire weapon
                    if (m_DiceRoller.RollChance(30))
                    {
                        if (m_DiceRoller.RollChance(50))
                            return MakeItemShotgun();
                        else
                            return MakeItemHuntingRifle();
                    }
                    else
                    {
                        if (m_DiceRoller.RollChance(50))
                            return MakeItemShotgunAmmo();
                        else
                            return MakeItemLightRifleAmmo();
                    }
                case 11:
                case 12:
                case 13: return MakeItemCellPhone();

                case 14:
                case 15: return MakeItemFlashlight();

                case 16:
                case 17: return MakeItemLightPistolAmmo();

                case 18:
                case 19: return MakeItemStenchKiller();

                case 20: return MakeItemHunterVest();

                case 21:
                case 22:
                case 23:
                    if (m_DiceRoller.RollChance(50))
                        return MakeItemBook();
                    else
                        return MakeItemMagazines();

                default: throw new ArgumentOutOfRangeException("unhandled roll");
            }
        }

        public Item MakeRandomKitchenItem()
        {
            if (m_DiceRoller.RollChance(50))
                return MakeItemCannedFood();
            else
                return MakeItemGroceries();
        }

        public Item MakeRandomCHAROfficeItem()
        {
            int randomItem = m_DiceRoller.Roll(0, 10);
            switch (randomItem)
            {
                case 0:
                    // weapons:
                    // - grenade (rare).
                    // - shotgun/ammo
                    if (m_DiceRoller.RollChance(10))
                    {
                        // grenade!
                        return MakeItemGrenade();
                    }
                    else
                    {
                        // shotgun/ammo
                        if (m_DiceRoller.RollChance(30))
                            return MakeItemShotgun();
                        else
                            return MakeItemShotgunAmmo();
                    }

                case 1:
                case 2:
                    if (m_DiceRoller.RollChance(50))
                        return MakeItemBandages();
                    else
                        return MakeItemMedikit();

                case 3:
                    return MakeItemCannedFood();

                case 4: // rare tracker items
                    if (m_DiceRoller.RollChance(50))
                    {
                        if (m_DiceRoller.RollChance(50))
                            return MakeItemZTracker();
                        else
                            return MakeItemBlackOpsGPS();
                    }
                    else
                        return null;

                default: return null; // 50% chance to find nothing.
            }
        }

        public Item MakeRandomParkItem()
        {
            int randomItem = m_DiceRoller.Roll(0, 8);
            switch (randomItem)
            {
                case 0: return MakeItemSprayPaint();
                case 1: return MakeItemBaseballBat();
                case 2: return MakeItemPillsSLP();
                case 3: return MakeItemPillsSTA();
                case 4: return MakeItemPillsSAN();
                case 5: return MakeItemFlashlight();
                case 6: return MakeItemCellPhone();
                case 7: return MakeItemWoodenPlank();
                default: throw new ArgumentOutOfRangeException("unhandled item roll");
            }
        }
        #endregion
        #region Decorations

        static readonly string[] POSTERS = { GameImages.DECO_POSTERS1, GameImages.DECO_POSTERS2 };
        protected virtual void DecorateOutsideWallsWithPosters(Map map, Rectangle rect, int chancePerWall)
        {
            base.DecorateOutsideWalls(map, rect,
                (x, y) =>
                {
                    if (m_DiceRoller.RollChance(chancePerWall))
                    {
                        return POSTERS[m_DiceRoller.Roll(0, POSTERS.Length)];
                    }
                    else
                        return null;
                });
        }

        static readonly string[] TAGS = { GameImages.DECO_TAGS1, GameImages.DECO_TAGS2, GameImages.DECO_TAGS3, GameImages.DECO_TAGS4, GameImages.DECO_TAGS5, GameImages.DECO_TAGS6, GameImages.DECO_TAGS7 };

        protected virtual void DecorateOutsideWallsWithTags(Map map, Rectangle rect, int chancePerWall)
        {
            base.DecorateOutsideWalls(map, rect,
                (x, y) =>
                {
                    if (m_DiceRoller.RollChance(chancePerWall))
                    {
                        return TAGS[m_DiceRoller.Roll(0, TAGS.Length)];
                    }
                    else
                        return null;
                });
        }
        #endregion
        #region Populating buildings
        protected virtual void PopulateCHAROfficeBuilding(Map map, Block b)
        {
            //////////
            // Guards
            //////////
            for (int i = 0; i < MAX_CHAR_GUARDS_PER_OFFICE; i++)
            {
                Actor newGuard = CreateNewCHARGuard(0);
                ActorPlace(m_DiceRoller, 100, map, newGuard, b.InsideRect.Left, b.InsideRect.Top, b.InsideRect.Width, b.InsideRect.Height);
            }

        }
        #endregion
    }
}
