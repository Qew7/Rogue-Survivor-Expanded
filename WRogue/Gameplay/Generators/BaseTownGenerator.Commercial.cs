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
        #region Concrete buildings
        protected bool IsThereASpecialBuilding(Map map, Rectangle rect)
        {
            // must not be a special building.
            List<Zone> zonesUpThere = map.GetZonesAt(rect.Left, rect.Top);
            if (zonesUpThere != null)
            {
                bool special = false;
                foreach (Zone z in zonesUpThere)
                    if (z.Name.Contains(RogueGame.NAME_SEWERS_MAINTENANCE) || z.Name.Contains(RogueGame.NAME_SUBWAY_STATION) || z.Name.Contains("office") || z.Name.Contains("shop"))
                    {
                        special = true;
                        break;
                    }
                if (special)
                    return true;
            }

            // must not have an exit.
            if (map.HasAnExitIn(rect))
                return true;

            // all clear.
            return false;
        }


        protected virtual bool MakeShopBuilding(Map map, Block b)
        {
            ////////////////////////
            // 0. Check suitability
            ////////////////////////
            if (b.InsideRect.Width < 5 || b.InsideRect.Height < 5)
                return false;

            /////////////////////////////
            // 1. Walkway, floor & walls
            /////////////////////////////
            base.TileRectangle(map, m_Game.GameTiles.FLOOR_WALKWAY, b.Rectangle);
            base.TileRectangle(map, m_Game.GameTiles.WALL_STONE, b.BuildingRect);
            base.TileFill(map, m_Game.GameTiles.FLOOR_TILES, b.InsideRect, (tile, prevmodel, x, y) => tile.IsInside = true);

            ///////////////////////
            // 2. Decide shop type
            ///////////////////////
            ShopType shopType = (ShopType)m_DiceRoller.Roll((int)ShopType._FIRST, (int)ShopType._COUNT);

            //////////////////////////////////////////
            // 3. Make sections alleys with displays.
            //////////////////////////////////////////
            #region
            int alleysStartX = b.InsideRect.Left;
            int alleysStartY = b.InsideRect.Top;
            int alleysEndX = b.InsideRect.Right;
            int alleysEndY = b.InsideRect.Bottom;
            bool horizontalAlleys = b.Rectangle.Width >= b.Rectangle.Height;
            int centralAlley;

            if (horizontalAlleys)
            {
                ++alleysStartX;
                --alleysEndX;
                centralAlley = b.InsideRect.Left + b.InsideRect.Width / 2;
            }
            else
            {
                ++alleysStartY;
                --alleysEndY;
                centralAlley = b.InsideRect.Top + b.InsideRect.Height / 2;
            }
            Rectangle alleysRect = Rectangle.FromLTRB(alleysStartX, alleysStartY, alleysEndX, alleysEndY);

            base.MapObjectFill(map, alleysRect,
                (pt) =>
                {
                    bool addShelf;

                    if (horizontalAlleys)
                        addShelf = ((pt.Y - alleysRect.Top) % 2 == 1) && pt.X != centralAlley;
                    else
                        addShelf = ((pt.X - alleysRect.Left) % 2 == 1) && pt.Y != centralAlley;

                    if (addShelf)
                        return base.MakeObjShelf(GameImages.OBJ_SHOP_SHELF);
                    else
                        return null;
                });
            #endregion

            ///////////////////////////////
            // 4. Entry door with shop ids
            //    Might add window(s).
            ///////////////////////////////
            #region
            int midX = b.Rectangle.Left + b.Rectangle.Width / 2;
            int midY = b.Rectangle.Top + b.Rectangle.Height / 2;

            // make doors on one side.
            if (horizontalAlleys)
            {
                bool west = m_DiceRoller.RollChance(50);

                if (west)
                {

                    // west
                    PlaceDoor(map, b.BuildingRect.Left, midY, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                    if (b.InsideRect.Height >= 8)
                    {
                        PlaceDoor(map, b.BuildingRect.Left, midY - 1, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                        if (b.InsideRect.Height >= 12)
                            PlaceDoor(map, b.BuildingRect.Left, midY + 1, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                    }
                }
                else
                {
                    // east
                    PlaceDoor(map, b.BuildingRect.Right - 1, midY, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                    if (b.InsideRect.Height >= 8)
                    {
                        PlaceDoor(map, b.BuildingRect.Right - 1, midY - 1, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                        if (b.InsideRect.Height >= 12)
                            PlaceDoor(map, b.BuildingRect.Right - 1, midY + 1, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                    }
                }
            }
            else
            {
                bool north = m_DiceRoller.RollChance(50);

                if (north)
                {
                    // north
                    PlaceDoor(map, midX, b.BuildingRect.Top, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                    if (b.InsideRect.Width >= 8)
                    {
                        PlaceDoor(map, midX - 1, b.BuildingRect.Top, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                        if (b.InsideRect.Width >= 12)
                            PlaceDoor(map, midX + 1, b.BuildingRect.Top, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                    }
                }
                else
                {
                    // south
                    PlaceDoor(map, midX, b.BuildingRect.Bottom - 1, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                    if (b.InsideRect.Width >= 8)
                    {
                        PlaceDoor(map, midX - 1, b.BuildingRect.Bottom - 1, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                        if (b.InsideRect.Width >= 12)
                            PlaceDoor(map, midX + 1, b.BuildingRect.Bottom - 1, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                    }
                }
            }

            // add shop image next to doors.
            string shopImage;
            string shopName;
            switch (shopType)
            {
                case ShopType.CONSTRUCTION:
                    shopImage = GameImages.DECO_SHOP_CONSTRUCTION;
                    shopName = "Construction";
                    break;
                case ShopType.GENERAL_STORE:
                    shopImage = GameImages.DECO_SHOP_GENERAL_STORE;
                    shopName = "GeneralStore";
                    break;
                case ShopType.GROCERY:
                    shopImage = GameImages.DECO_SHOP_GROCERY;
                    shopName = "Grocery";
                    break;
                case ShopType.GUNSHOP:
                    shopImage = GameImages.DECO_SHOP_GUNSHOP;
                    shopName = "Gunshop";
                    break;
                case ShopType.PHARMACY:
                    shopImage = GameImages.DECO_SHOP_PHARMACY;
                    shopName = "Pharmacy";
                    break;
                case ShopType.SPORTSWEAR:
                    shopImage = GameImages.DECO_SHOP_SPORTSWEAR;
                    shopName = "Sportswear";
                    break;
                case ShopType.HUNTING:
                    shopImage = GameImages.DECO_SHOP_HUNTING;
                    shopName = "Hunting Shop";
                    break;
                default:
                    throw new ArgumentOutOfRangeException("unhandled shoptype");
            }
            DecorateOutsideWalls(map, b.BuildingRect, (x, y) => map.GetMapObjectAt(x, y) == null && CountAdjDoors(map, x, y) >= 1 ? shopImage : null);

            // window?
            if (m_DiceRoller.RollChance(SHOP_WINDOW_CHANCE))
            {
                // pick a random side.
                int side = m_DiceRoller.Roll(0, 4);
                int wx, wy;
                switch (side)
                {
                    case 0: wx = b.BuildingRect.Left + b.BuildingRect.Width / 2; wy = b.BuildingRect.Top; break;
                    case 1: wx = b.BuildingRect.Left + b.BuildingRect.Width / 2; wy = b.BuildingRect.Bottom - 1; break;
                    case 2: wx = b.BuildingRect.Left; wy = b.BuildingRect.Top + b.BuildingRect.Height / 2; break;
                    case 3: wx = b.BuildingRect.Right - 1; wy = b.BuildingRect.Top + b.BuildingRect.Height / 2; break;
                    default: throw new ArgumentOutOfRangeException("unhandled side");
                }
                // check it is ok to make a window there.
                bool isGoodWindowPos = true;
                if (map.GetTileAt(wx, wy).Model.IsWalkable) isGoodWindowPos = false;
                // do it?
                if (isGoodWindowPos)
                {
                    PlaceDoor(map, wx, wy, m_Game.GameTiles.FLOOR_TILES, base.MakeObjWindow());
                }
            }

            // barricade certain shops types.
            if (shopType == ShopType.GUNSHOP)
            {
                BarricadeDoors(map, b.BuildingRect, Rules.BARRICADING_MAX);
            }

            #endregion

            ///////////////////////////
            // 5. Add items to shelves.
            ///////////////////////////
            #region
            base.ItemsDrop(map, b.InsideRect,
                (pt) =>
                {
                    MapObject mapObj = map.GetMapObjectAt(pt);
                    if (mapObj == null)
                        return false;
                    return mapObj.ImageID == GameImages.OBJ_SHOP_SHELF &&
                        m_DiceRoller.RollChance(m_Params.ItemInShopShelfChance);
                },
                (pt) => MakeRandomShopItem(shopType));
            #endregion

            ///////////
            // 6. Zone
            ///////////
            // shop building.
            map.AddZone(MakeUniqueZone(shopName, b.BuildingRect));
            // walkway zones.
            MakeWalkwayZones(map, b);

            ////////////////
            // 7. Basement?
            ////////////////
            #region
            if (m_DiceRoller.RollChance(SHOP_BASEMENT_CHANCE))
            {
                // shop basement map:
                // - a single dark room.
                // - some shop items.

                // - a single dark room.
                Map shopBasement = new Map((map.Seed << 1) ^ shopName.GetHashCode(), "basement-" + shopName, b.BuildingRect.Width, b.BuildingRect.Height)
                {
                    Lighting = Lighting.DARKNESS
                };
                DoForEachTile(shopBasement, shopBasement.Rect, (pt) => shopBasement.GetTileAt(pt).IsInside = true);
                TileFill(shopBasement, m_Game.GameTiles.FLOOR_CONCRETE);
                TileRectangle(shopBasement, m_Game.GameTiles.WALL_BRICK, shopBasement.Rect);
                shopBasement.AddZone(MakeUniqueZone("basement", shopBasement.Rect));

                // - some shelves with shop items.
                // - some rats.
                DoForEachTile(shopBasement, shopBasement.Rect,
                    (pt) =>
                    {
                        if (!shopBasement.IsWalkable(pt.X, pt.Y))
                            return;
                        if (shopBasement.GetExitAt(pt) != null)
                            return;

                        if (m_DiceRoller.RollChance(SHOP_BASEMENT_SHELF_CHANCE_PER_TILE))
                        {
                            shopBasement.PlaceMapObjectAt(MakeObjShelf(GameImages.OBJ_SHOP_SHELF), pt);
                            if (m_DiceRoller.RollChance(SHOP_BASEMENT_ITEM_CHANCE_PER_SHELF))
                            {
                                Item it = MakeRandomShopItem(shopType);
                                if (it != null)
                                    shopBasement.DropItemAt(it, pt);
                            }
                        }

                        if (Rules.HasZombiesInBasements(m_Game.Session.GameMode))
                        {
                            if (m_DiceRoller.RollChance(SHOP_BASEMENT_ZOMBIE_RAT_CHANCE))
                                shopBasement.PlaceActorAt(CreateNewBasementRatZombie(0), pt);
                        }
                    });

                // alpha10 music
                shopBasement.BgMusic = GameMusics.SEWERS;

                // link maps, stairs in one corner.
                Point basementCorner = new Point();
                basementCorner.X = m_DiceRoller.RollChance(50) ? 1 : shopBasement.Width - 2;
                basementCorner.Y = m_DiceRoller.RollChance(50) ? 1 : shopBasement.Height - 2;
                Point shopCorner = new Point(basementCorner.X - 1 + b.InsideRect.Left, basementCorner.Y - 1 + b.InsideRect.Top);
                AddExit(shopBasement, basementCorner, map, shopCorner, GameImages.DECO_STAIRS_UP, true);
                AddExit(map, shopCorner, shopBasement, basementCorner, GameImages.DECO_STAIRS_DOWN, true);

                // remove any blocking object in the shop.
                MapObject blocker = map.GetMapObjectAt(shopCorner);
                if (blocker != null)
                    map.RemoveMapObjectAt(shopCorner.X, shopCorner.Y);

                // add map.
                m_Params.District.AddUniqueMap(shopBasement);
            }
            #endregion

            // Done
            return true;
        }

        /// <summary>
        /// Either an Office (for large enough buildings) or an Agency (for small buildings).
        /// </summary>
        /// <param name="map"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        protected virtual CHARBuildingType MakeCHARBuilding(Map map, Block b)
        {
            ///////////////////////////////
            // Offices are large buildings.
            // Agency are small ones.
            ///////////////////////////////
            if (b.InsideRect.Width < 8 || b.InsideRect.Height < 8)
            {
                // small, make it an Agency.
                if (MakeCHARAgency(map, b))
                    return CHARBuildingType.AGENCY;
                else
                    return CHARBuildingType.NONE;
            }
            else
            {
                if (MakeCHAROffice(map, b))
                    return CHARBuildingType.OFFICE;
                else
                    return CHARBuildingType.NONE;
            }
        }

        static string[] CHAR_POSTERS = { GameImages.DECO_CHAR_POSTER1, GameImages.DECO_CHAR_POSTER2, GameImages.DECO_CHAR_POSTER3 };

        protected virtual bool MakeCHARAgency(Map map, Block b)
        {
            /////////////////////////////
            // 1. Walkway, floor & walls
            /////////////////////////////
            base.TileRectangle(map, m_Game.GameTiles.FLOOR_WALKWAY, b.Rectangle);
            base.TileRectangle(map, m_Game.GameTiles.WALL_CHAR_OFFICE, b.BuildingRect);
            base.TileFill(map, m_Game.GameTiles.FLOOR_OFFICE, b.InsideRect,
                (tile, prevmodel, x, y) =>
                {
                    tile.IsInside = true;
                    tile.AddDecoration(GameImages.DECO_CHAR_FLOOR_LOGO);
                });

            //////////////////////////
            // 2. Decide orientation.
            //////////////////////////
            bool horizontalCorridor = (b.InsideRect.Width >= b.InsideRect.Height);

            /////////////////
            // 3. Entry door
            /////////////////
            #region
            int midX = b.Rectangle.Left + b.Rectangle.Width / 2;
            int midY = b.Rectangle.Top + b.Rectangle.Height / 2;

            // make doors on one side.
            #region
            if (horizontalCorridor)
            {
                bool west = m_DiceRoller.RollChance(50);

                if (west)
                {
                    // west
                    PlaceDoor(map, b.BuildingRect.Left, midY, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                    if (b.InsideRect.Height >= 8)
                    {
                        PlaceDoor(map, b.BuildingRect.Left, midY - 1, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                        if (b.InsideRect.Height >= 12)
                            PlaceDoor(map, b.BuildingRect.Left, midY + 1, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                    }
                }
                else
                {
                    // east
                    PlaceDoor(map, b.BuildingRect.Right - 1, midY, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                    if (b.InsideRect.Height >= 8)
                    {
                        PlaceDoor(map, b.BuildingRect.Right - 1, midY - 1, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                        if (b.InsideRect.Height >= 12)
                            PlaceDoor(map, b.BuildingRect.Right - 1, midY + 1, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                    }
                }
            }
            else
            {
                bool north = m_DiceRoller.RollChance(50);

                if (north)
                {
                    // north
                    PlaceDoor(map, midX, b.BuildingRect.Top, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                    if (b.InsideRect.Width >= 8)
                    {
                        PlaceDoor(map, midX - 1, b.BuildingRect.Top, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                        if (b.InsideRect.Width >= 12)
                            PlaceDoor(map, midX + 1, b.BuildingRect.Top, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                    }
                }
                else
                {
                    // south
                    PlaceDoor(map, midX, b.BuildingRect.Bottom - 1, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                    if (b.InsideRect.Width >= 8)
                    {
                        PlaceDoor(map, midX - 1, b.BuildingRect.Bottom - 1, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                        if (b.InsideRect.Width >= 12)
                            PlaceDoor(map, midX + 1, b.BuildingRect.Bottom - 1, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                    }
                }
            }
            #endregion

            // add office image next to doors.
            string officeImage = GameImages.DECO_CHAR_OFFICE;
            DecorateOutsideWalls(map, b.BuildingRect, (x, y) => map.GetMapObjectAt(x, y) == null && CountAdjDoors(map, x, y) >= 1 ? officeImage : null);
            #endregion

            ////////////////
            // 4. Furniture
            ////////////////
            #region
            // chairs on the sides.
            // alpha10.1 chance to add book/magazines
            MapObjectFill(map, b.InsideRect,
                (pt) =>
                {
                    if (CountAdjWalls(map, pt.X, pt.Y) < 3)
                        return null;

                    // alpha10.1 book/magazines on chair?
                    if (m_DiceRoller.RollChance(25))
                        map.DropItemAt(m_DiceRoller.RollChance(20) ? MakeItemBook() : MakeItemMagazines(), pt);

                    return MakeObjChair(GameImages.OBJ_CHAR_CHAIR);
                });
            // walls/pilars in the middle.
            TileFill(map, m_Game.GameTiles.WALL_CHAR_OFFICE, new Rectangle(b.InsideRect.Left + b.InsideRect.Width / 2 - 1, b.InsideRect.Top + b.InsideRect.Height / 2 - 1, 3, 2),
                (tile, model, x, y) =>
                {
                    tile.AddDecoration(CHAR_POSTERS[m_DiceRoller.Roll(0, CHAR_POSTERS.Length)]);
                });
            #endregion

            //////////////
            // 5. Posters
            //////////////
            #region
            // outside.
            DecorateOutsideWalls(map, b.BuildingRect,
                (x, y) =>
                {
                    if (CountAdjDoors(map, x, y) > 0)
                        return null;
                    else
                    {
                        if (m_DiceRoller.RollChance(25))
                            return CHAR_POSTERS[m_DiceRoller.Roll(0, CHAR_POSTERS.Length)];
                        else
                            return null;
                    }
                });
            #endregion

            ////////////
            // 6. Zones.
            ////////////
            map.AddZone(MakeUniqueZone("CHAR Agency", b.BuildingRect));
            MakeWalkwayZones(map, b);

            // Done
            return true;
        }

        protected virtual bool MakeCHAROffice(Map map, Block b)
        {

            /////////////////////////////
            // 1. Walkway, floor & walls
            /////////////////////////////
            base.TileRectangle(map, m_Game.GameTiles.FLOOR_WALKWAY, b.Rectangle);
            base.TileRectangle(map, m_Game.GameTiles.WALL_CHAR_OFFICE, b.BuildingRect);
            base.TileFill(map, m_Game.GameTiles.FLOOR_OFFICE, b.InsideRect, (tile, prevmodel, x, y) => tile.IsInside = true);

            //////////////////////////
            // 2. Decide orientation.
            //////////////////////////
            bool horizontalCorridor = (b.InsideRect.Width >= b.InsideRect.Height);

            /////////////////
            // 3. Entry door
            /////////////////
            #region
            int midX = b.Rectangle.Left + b.Rectangle.Width / 2;
            int midY = b.Rectangle.Top + b.Rectangle.Height / 2;
            Direction doorSide;

            // make doors on one side.
            #region
            if (horizontalCorridor)
            {
                bool west = m_DiceRoller.RollChance(50);

                if (west)
                {
                    doorSide = Direction.W;
                    // west
                    PlaceDoor(map, b.BuildingRect.Left, midY, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                    if (b.InsideRect.Height >= 8)
                    {
                        PlaceDoor(map, b.BuildingRect.Left, midY - 1, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                        if (b.InsideRect.Height >= 12)
                            PlaceDoor(map, b.BuildingRect.Left, midY + 1, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                    }
                }
                else
                {
                    doorSide = Direction.E;
                    // east
                    PlaceDoor(map, b.BuildingRect.Right - 1, midY, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                    if (b.InsideRect.Height >= 8)
                    {
                        PlaceDoor(map, b.BuildingRect.Right - 1, midY - 1, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                        if (b.InsideRect.Height >= 12)
                            PlaceDoor(map, b.BuildingRect.Right - 1, midY + 1, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                    }
                }
            }
            else
            {
                bool north = m_DiceRoller.RollChance(50);

                if (north)
                {
                    doorSide = Direction.N;
                    // north
                    PlaceDoor(map, midX, b.BuildingRect.Top, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                    if (b.InsideRect.Width >= 8)
                    {
                        PlaceDoor(map, midX - 1, b.BuildingRect.Top, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                        if (b.InsideRect.Width >= 12)
                            PlaceDoor(map, midX + 1, b.BuildingRect.Top, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                    }
                }
                else
                {
                    doorSide = Direction.S;
                    // south
                    PlaceDoor(map, midX, b.BuildingRect.Bottom - 1, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                    if (b.InsideRect.Width >= 8)
                    {
                        PlaceDoor(map, midX - 1, b.BuildingRect.Bottom - 1, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                        if (b.InsideRect.Width >= 12)
                            PlaceDoor(map, midX + 1, b.BuildingRect.Bottom - 1, m_Game.GameTiles.FLOOR_WALKWAY, base.MakeObjGlassDoor());
                    }
                }
            }
            #endregion

            // add office image next to doors.
            string officeImage = GameImages.DECO_CHAR_OFFICE;
            DecorateOutsideWalls(map, b.BuildingRect, (x, y) => map.GetMapObjectAt(x, y) == null && CountAdjDoors(map, x, y) >= 1 ? officeImage : null);

            // barricade entry doors.
            BarricadeDoors(map, b.BuildingRect, Rules.BARRICADING_MAX);
            #endregion

            ///////////////////////
            // 4. Make entry hall.
            ///////////////////////
            #region
            const int hallDepth = 3;
            if (doorSide == Direction.N)
            {
                base.TileHLine(map, m_Game.GameTiles.WALL_CHAR_OFFICE, b.InsideRect.Left, b.InsideRect.Top + hallDepth, b.InsideRect.Width);
            }
            else if (doorSide == Direction.S)
            {
                base.TileHLine(map, m_Game.GameTiles.WALL_CHAR_OFFICE, b.InsideRect.Left, b.InsideRect.Bottom - 1 - hallDepth, b.InsideRect.Width);
            }
            else if (doorSide == Direction.E)
            {
                base.TileVLine(map, m_Game.GameTiles.WALL_CHAR_OFFICE, b.InsideRect.Right - 1 - hallDepth, b.InsideRect.Top, b.InsideRect.Height);
            }
            else if (doorSide == Direction.W)
            {
                base.TileVLine(map, m_Game.GameTiles.WALL_CHAR_OFFICE, b.InsideRect.Left + hallDepth, b.InsideRect.Top, b.InsideRect.Height);
            }
            else
                throw new InvalidOperationException("unhandled door side");
            #endregion

            /////////////////////////////////////
            // 5. Make central corridor & wings
            /////////////////////////////////////
            #region
            Rectangle corridorRect;
            Point corridorDoor;
            if (doorSide == Direction.N)
            {
                corridorRect = new Rectangle(midX - 1, b.InsideRect.Top + hallDepth, 3, b.BuildingRect.Height - 1 - hallDepth);
                corridorDoor = new Point(corridorRect.Left + 1, corridorRect.Top);
            }
            else if (doorSide == Direction.S)
            {
                corridorRect = new Rectangle(midX - 1, b.BuildingRect.Top, 3, b.BuildingRect.Height - 1 - hallDepth);
                corridorDoor = new Point(corridorRect.Left + 1, corridorRect.Bottom - 1);
            }
            else if (doorSide == Direction.E)
            {
                corridorRect = new Rectangle(b.BuildingRect.Left, midY - 1, b.BuildingRect.Width - 1 - hallDepth, 3);
                corridorDoor = new Point(corridorRect.Right - 1, corridorRect.Top + 1);
            }
            else if (doorSide == Direction.W)
            {
                corridorRect = new Rectangle(b.InsideRect.Left + hallDepth, midY - 1, b.BuildingRect.Width - 1 - hallDepth, 3);
                corridorDoor = new Point(corridorRect.Left, corridorRect.Top + 1);
            }
            else
                throw new InvalidOperationException("unhandled door side");

            base.TileRectangle(map, m_Game.GameTiles.WALL_CHAR_OFFICE, corridorRect);
            PlaceDoor(map, corridorDoor.X, corridorDoor.Y, m_Game.GameTiles.FLOOR_OFFICE, base.MakeObjCharDoor());
            #endregion

            /////////////////////////
            // 6. Make office rooms.
            /////////////////////////
            #region
            // make wings.
            Rectangle wingOne;
            Rectangle wingTwo;
            if (horizontalCorridor)
            {
                // top side.
                wingOne = new Rectangle(corridorRect.Left, b.BuildingRect.Top, corridorRect.Width, 1 + corridorRect.Top - b.BuildingRect.Top);
                // bottom side.
                wingTwo = new Rectangle(corridorRect.Left, corridorRect.Bottom - 1, corridorRect.Width, 1 + b.BuildingRect.Bottom - corridorRect.Bottom);
            }
            else
            {
                // left side
                wingOne = new Rectangle(b.BuildingRect.Left, corridorRect.Top, 1 + corridorRect.Left - b.BuildingRect.Left, corridorRect.Height);
                // right side
                wingTwo = new Rectangle(corridorRect.Right - 1, corridorRect.Top, 1 + b.BuildingRect.Right - corridorRect.Right, corridorRect.Height);
            }

            // make rooms in each wing with doors leaving toward corridor.
            const int officeRoomsSize = 4;

            List<Rectangle> officesOne = new List<Rectangle>();
            MakeRoomsPlan(map, ref officesOne, wingOne, officeRoomsSize, officeRoomsSize);

            List<Rectangle> officesTwo = new List<Rectangle>();
            MakeRoomsPlan(map, ref officesTwo, wingTwo, officeRoomsSize, officeRoomsSize);

            List<Rectangle> allOffices = new List<Rectangle>(officesOne.Count + officesTwo.Count);
            allOffices.AddRange(officesOne);
            allOffices.AddRange(officesTwo);

            foreach (Rectangle roomRect in officesOne)
            {
                base.TileRectangle(map, m_Game.GameTiles.WALL_CHAR_OFFICE, roomRect);
                map.AddZone(MakeUniqueZone("Office room", roomRect));
            }
            foreach (Rectangle roomRect in officesTwo)
            {
                base.TileRectangle(map, m_Game.GameTiles.WALL_CHAR_OFFICE, roomRect);
                map.AddZone(MakeUniqueZone("Office room", roomRect));
            }

            foreach (Rectangle roomRect in officesOne)
            {
                if (horizontalCorridor)
                {
                    PlaceDoor(map, roomRect.Left + roomRect.Width / 2, roomRect.Bottom - 1, m_Game.GameTiles.FLOOR_OFFICE, base.MakeObjCharDoor());
                }
                else
                {
                    PlaceDoor(map, roomRect.Right - 1, roomRect.Top + roomRect.Height / 2, m_Game.GameTiles.FLOOR_OFFICE, base.MakeObjCharDoor());
                }
            }
            foreach (Rectangle roomRect in officesTwo)
            {
                if (horizontalCorridor)
                {
                    PlaceDoor(map, roomRect.Left + roomRect.Width / 2, roomRect.Top, m_Game.GameTiles.FLOOR_OFFICE, base.MakeObjCharDoor());
                }
                else
                {
                    PlaceDoor(map, roomRect.Left, roomRect.Top + roomRect.Height / 2, m_Game.GameTiles.FLOOR_OFFICE, base.MakeObjCharDoor());
                }
            }

            // tables with chairs.
            foreach (Rectangle roomRect in allOffices)
            {
                // table.
                Point tablePos = new Point(roomRect.Left + roomRect.Width / 2, roomRect.Top + roomRect.Height / 2);
                map.PlaceMapObjectAt(base.MakeObjTable(GameImages.OBJ_CHAR_TABLE), tablePos);

                // try to put chairs around.
                int nbChairs = 2;
                Rectangle insideRoom = new Rectangle(roomRect.Left + 1, roomRect.Top + 1, roomRect.Width - 2, roomRect.Height - 2);
                if (!insideRoom.IsEmpty)
                {
                    for (int i = 0; i < nbChairs; i++)
                    {
                        Rectangle adjTableRect = new Rectangle(tablePos.X - 1, tablePos.Y - 1, 3, 3);
                        adjTableRect.Intersect(insideRoom);
                        MapObjectPlaceInGoodPosition(map, adjTableRect,
                            (pt) => pt != tablePos,
                            m_DiceRoller,
                            (pt) => MakeObjChair(GameImages.OBJ_CHAR_CHAIR));
                    }
                }
            }
            #endregion

            ////////////////
            // 7. Add items.
            ////////////////
            #region
            // drop goodies in rooms.
            foreach (Rectangle roomRect in allOffices)
            {
                base.ItemsDrop(map, roomRect,
                    (pt) =>
                    {
                        Tile tile = map.GetTileAt(pt.X, pt.Y);
                        if (tile.Model != m_Game.GameTiles.FLOOR_OFFICE)
                            return false;
                        MapObject mapObj = map.GetMapObjectAt(pt);
                        if (mapObj != null)
                            return false;
                        return true;
                    },
                    (pt) => MakeRandomCHAROfficeItem());
            }
            #endregion

            ///////////
            // 8. Zone
            ///////////
            Zone zone = base.MakeUniqueZone("CHAR Office", b.BuildingRect);
            zone.SetGameAttribute<bool>(ZoneAttributes.IS_CHAR_OFFICE, true);
            map.AddZone(zone);
            MakeWalkwayZones(map, b);

            // Done
            return true;
        }

        protected virtual bool MakeParkBuilding(Map map, Block b)
        {
            ////////////////////////
            // 0. Check suitability
            ////////////////////////
            if (b.InsideRect.Width < 3 || b.InsideRect.Height < 3)
                return false;

            /////////////////////////////
            // 1. Grass, walkway & fence
            /////////////////////////////
            base.TileRectangle(map, m_Game.GameTiles.FLOOR_WALKWAY, b.Rectangle);
            base.TileFill(map, m_Game.GameTiles.FLOOR_GRASS, b.InsideRect);
            base.MapObjectFill(map, b.BuildingRect,
                (pt) =>
                {
                    bool placeFence = (pt.X == b.BuildingRect.Left || pt.X == b.BuildingRect.Right - 1 || pt.Y == b.BuildingRect.Top || pt.Y == b.BuildingRect.Bottom - 1);
                    if (placeFence)
                        return base.MakeObjFence(GameImages.OBJ_FENCE);
                    else
                        return null;
                });

            ///////////////////////////////
            // 2. Random trees and benches
            ///////////////////////////////
            base.MapObjectFill(map, b.InsideRect,
                (pt) =>
                {
                    bool placeTree = m_DiceRoller.RollChance(PARK_TREE_CHANCE);
                    if (placeTree)
                        return base.MakeObjTree(GameImages.OBJ_TREE);
                    else
                        return null;
                });

            base.MapObjectFill(map, b.InsideRect,
                (pt) =>
                {
                    bool placeBench = m_DiceRoller.RollChance(PARK_BENCH_CHANCE);
                    if (placeBench)
                        return base.MakeObjBench(GameImages.OBJ_BENCH);
                    else
                        return null;
                });

            ///////////////
            // 3. Entrance
            ///////////////
            int entranceFace = m_DiceRoller.Roll(0, 4);
            int ex, ey;
            switch (entranceFace)
            {
                case 0: // west
                    ex = b.BuildingRect.Left;
                    ey = b.BuildingRect.Top + b.BuildingRect.Height / 2;
                    break;
                case 1: // east
                    ex = b.BuildingRect.Right - 1;
                    ey = b.BuildingRect.Top + b.BuildingRect.Height / 2;
                    break;
                case 3: // north
                    ex = b.BuildingRect.Left + b.BuildingRect.Width / 2;
                    ey = b.BuildingRect.Top;
                    break;
                default: // south
                    ex = b.BuildingRect.Left + b.BuildingRect.Width / 2;
                    ey = b.BuildingRect.Bottom - 1;
                    break;
            }
            map.RemoveMapObjectAt(ex, ey);
            map.SetTileModelAt(ex, ey, m_Game.GameTiles.FLOOR_WALKWAY);

            ////////////
            // 4. Items
            ////////////
            base.ItemsDrop(map, b.InsideRect,
                (pt) => map.GetMapObjectAt(pt) == null && m_DiceRoller.RollChance(PARK_ITEM_CHANCE),
                (pt) => MakeRandomParkItem());

            ///////////
            // 5. Zone
            ///////////
            Zone parkZone = MakeUniqueZone("Park", b.BuildingRect);
            map.AddZone(parkZone);
            MakeWalkwayZones(map, b);

            // alpha10
            ////////////
            // 5. Shed?
            ////////////
            if (b.InsideRect.Width > PARK_SHED_WIDTH + 2 && b.InsideRect.Height > PARK_SHED_HEIGHT + 2)
            {
                if (m_DiceRoller.RollChance(PARK_SHED_CHANCE))
                {
                    // roll shed pos - dont put next to park fences!
                    int shedX = m_DiceRoller.Roll(b.InsideRect.Left + 1, b.InsideRect.Right - PARK_SHED_WIDTH);
                    int shedY = m_DiceRoller.Roll(b.InsideRect.Top + 1, b.InsideRect.Bottom - PARK_SHED_HEIGHT);
                    Rectangle shedRect = new Rectangle(shedX, shedY, PARK_SHED_WIDTH, PARK_SHED_HEIGHT);
                    Rectangle shedInsideRect = new Rectangle(shedX + 1, shedY + 1, PARK_SHED_WIDTH - 2, PARK_SHED_HEIGHT - 2);

                    // clear everything but zones in shed location
                    ClearRectangle(map, shedRect, false);

                    // build it
                    MakeParkShedBuilding(map, "Shed", shedRect);
                }
            }

            // Done.
            return true;
        }

        protected virtual void MakeParkShedBuilding(Map map, string baseZoneName, Rectangle shedBuildingRect)
        {
            Rectangle shedInsideRect = new Rectangle(shedBuildingRect.X + 1, shedBuildingRect.Y + 1, shedBuildingRect.Width - 2, shedBuildingRect.Height - 2);

            // build building & zone
            TileRectangle(map, m_Game.GameTiles.WALL_BRICK, shedBuildingRect);
            TileFill(map, m_Game.GameTiles.FLOOR_PLANKS, shedInsideRect, (tile, prevTileModel, x, y) => tile.IsInside = true);
            map.AddZone(MakeUniqueZone(baseZoneName, shedBuildingRect));

            // place shed door and make sure door front is cleared of objects (trees).
            int doorDir = m_DiceRoller.Roll(0, 4);
            int doorX, doorY;
            int doorFrontX, doorFrontY;
            switch (doorDir)
            {
                case 0: // west
                    doorX = shedBuildingRect.Left;
                    doorY = shedBuildingRect.Top + shedBuildingRect.Height / 2;
                    doorFrontX = doorX - 1;
                    doorFrontY = doorY;
                    break;
                case 1: // east
                    doorX = shedBuildingRect.Right - 1;
                    doorY = shedBuildingRect.Top + shedBuildingRect.Height / 2;
                    doorFrontX = doorX + 1;
                    doorFrontY = doorY;
                    break;
                case 3: // north
                    doorX = shedBuildingRect.Left + shedBuildingRect.Width / 2;
                    doorY = shedBuildingRect.Top;
                    doorFrontX = doorX;
                    doorFrontY = doorY - 1;
                    break;
                default: // south
                    doorX = shedBuildingRect.Left + shedBuildingRect.Width / 2;
                    doorY = shedBuildingRect.Bottom - 1;
                    doorFrontX = doorX;
                    doorFrontY = doorY + 1;
                    break;
            }
            PlaceDoor(map, doorX, doorY, m_Game.GameTiles.FLOOR_TILES, MakeObjWoodenDoor());
            map.RemoveMapObjectAt(doorFrontX, doorFrontY);

            // mark as inside and add shelves with tools
            DoForEachTile(map, shedInsideRect, (pt) =>
            {
                if (!map.IsWalkable(pt))
                    return;

                if (CountAdjDoors(map, pt.X, pt.Y) > 0)
                    return;

                if (CountAdjWalls(map, pt.X, pt.Y) == 0)
                    return;

                // shelf.
                map.PlaceMapObjectAt(MakeObjShelf(GameImages.OBJ_SHOP_SHELF), pt);

                // construction item (tools, lights)
                Item it = MakeShopConstructionItem();
                if (it.Model.IsStackable)
                    it.Quantity = it.Model.StackingLimit;
                map.DropItemAt(it, pt);
            });
        }

        // alpha10.1 makes apartements or vanilla house
        #endregion
    }
}
