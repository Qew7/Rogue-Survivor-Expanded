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
        protected virtual bool MakeHousingBuilding(Map map, Block b)
        {
            // alpha10.1 decide floorplan
            // apartment?
            if (m_DiceRoller.RollChance(HOUSE_IS_APARTMENTS_CHANCE))
                if (MakeApartmentsBuilding(map, b))
                    return true;

            // vanilla house?
            return MakeVanillaHousingBuilding(map, b);
        }

        // alpha10.1 apartment houses
        protected virtual bool MakeApartmentsBuilding(Map map, Block b)
        {
            ////////////////////////
            // 0. Check suitability
            ////////////////////////
            if (b.InsideRect.Width < 9 || b.InsideRect.Height < 9)
                return false;
            if (b.InsideRect.Width > 17 || b.InsideRect.Height > 17)
                return false;

            // I pretty much copied and edited the char office algorithm. lame but i'm lazy.

            /////////////////////////////
            // 1. Walkway, floor & walls
            /////////////////////////////
            base.TileRectangle(map, m_Game.GameTiles.FLOOR_WALKWAY, b.Rectangle);
            base.TileRectangle(map, m_Game.GameTiles.WALL_BRICK, b.BuildingRect);
            base.TileFill(map, m_Game.GameTiles.FLOOR_PLANKS, b.InsideRect, (tile, prevmodel, x, y) => tile.IsInside = true);

            //////////////////////////
            // 2. Decide orientation.
            //////////////////////////
            bool horizontalCorridor = (b.InsideRect.Width >= b.InsideRect.Height);

            /////////////////////////////////////
            // 3. Entry door and opposite window
            /////////////////////////////////////
            #region
            int midX = b.Rectangle.Left + b.Rectangle.Width / 2;
            int midY = b.Rectangle.Top + b.Rectangle.Height / 2;
            Direction doorSide;

            if (horizontalCorridor)
            {
                bool west = m_DiceRoller.RollChance(50);

                if (west)
                {
                    doorSide = Direction.W;
                    // west
                    PlaceDoor(map, b.BuildingRect.Left, midY, m_Game.GameTiles.FLOOR_PLANKS, base.MakeObjWoodenDoor());
                    PlaceDoor(map, b.BuildingRect.Right - 1, midY, m_Game.GameTiles.FLOOR_PLANKS, base.MakeObjWindow());
                }
                else
                {
                    doorSide = Direction.E;
                    // east
                    PlaceDoor(map, b.BuildingRect.Right - 1, midY, m_Game.GameTiles.FLOOR_PLANKS, base.MakeObjWoodenDoor());
                    PlaceDoor(map, b.BuildingRect.Left, midY, m_Game.GameTiles.FLOOR_PLANKS, base.MakeObjWindow());
                }
            }
            else
            {
                bool north = m_DiceRoller.RollChance(50);

                if (north)
                {
                    doorSide = Direction.N;
                    // north
                    PlaceDoor(map, midX, b.BuildingRect.Top, m_Game.GameTiles.FLOOR_PLANKS, base.MakeObjWoodenDoor());
                    PlaceDoor(map, midX, b.BuildingRect.Bottom - 1, m_Game.GameTiles.FLOOR_PLANKS, base.MakeObjWindow());
                }
                else
                {
                    doorSide = Direction.S;
                    // south
                    PlaceDoor(map, midX, b.BuildingRect.Bottom - 1, m_Game.GameTiles.FLOOR_PLANKS, base.MakeObjWoodenDoor());
                    PlaceDoor(map, midX, b.BuildingRect.Top, m_Game.GameTiles.FLOOR_PLANKS, base.MakeObjWindow());
                }
            }
            #endregion

            //////////////////////////////////////////////
            // 4. Make central corridor & side apartments
            //////////////////////////////////////////////
            #region
            Rectangle corridorRect;
            if (doorSide == Direction.N)
                corridorRect = new Rectangle(midX, b.InsideRect.Top, 1, b.BuildingRect.Height - 1);
            else if (doorSide == Direction.S)
                corridorRect = new Rectangle(midX, b.BuildingRect.Top, 1, b.BuildingRect.Height - 1);
            else if (doorSide == Direction.E)
                corridorRect = new Rectangle(b.BuildingRect.Left, midY, b.BuildingRect.Width - 1, 1);
            else if (doorSide == Direction.W)
                corridorRect = new Rectangle(b.InsideRect.Left, midY, b.BuildingRect.Width - 1, 1);
            else
                throw new InvalidOperationException("apartment: unhandled door side");
            #endregion

            //////////////////////
            // 5. Make apartments
            //////////////////////
            #region
            // make wings.
            Rectangle wingOne;
            Rectangle wingTwo;
            if (horizontalCorridor)
            {
                // top side.
                wingOne = Rectangle.FromLTRB(b.BuildingRect.Left, b.BuildingRect.Top, b.BuildingRect.Right, corridorRect.Top);
                // bottom side.
                wingTwo = Rectangle.FromLTRB(b.BuildingRect.Left, corridorRect.Bottom, b.BuildingRect.Right, b.BuildingRect.Bottom);
            }
            else
            {
                // left side
                wingOne = Rectangle.FromLTRB(b.BuildingRect.Left, b.BuildingRect.Top, corridorRect.Left, b.BuildingRect.Bottom);
                // right side
                wingTwo = Rectangle.FromLTRB(corridorRect.Right, b.BuildingRect.Top, b.BuildingRect.Right, b.BuildingRect.Bottom);
            }

            // make apartements in each wing with doors leaving toward corridor and windows to the outside
            // pick sizes so the apartements are not cut into multiple rooms by MakeRoomsPlan
            int apartmentMinXSize, apartmentMinYSize;
            if (horizontalCorridor)
            {
                apartmentMinXSize = 4;
                apartmentMinYSize = b.BuildingRect.Height / 2;
            }
            else
            {
                apartmentMinXSize = b.BuildingRect.Width / 2;
                apartmentMinYSize = 4;
            }

            List<Rectangle> apartementsWingOne = new List<Rectangle>();
            MakeRoomsPlan(map, ref apartementsWingOne, wingOne, apartmentMinXSize, apartmentMinYSize);
            List<Rectangle> apartementsWingTwo = new List<Rectangle>();
            MakeRoomsPlan(map, ref apartementsWingTwo, wingTwo, apartmentMinXSize, apartmentMinYSize);

            List<Rectangle> allApartments = new List<Rectangle>(apartementsWingOne.Count + apartementsWingTwo.Count);
            allApartments.AddRange(apartementsWingOne);
            allApartments.AddRange(apartementsWingTwo);

            foreach (Rectangle apartRect in apartementsWingOne)
                base.TileRectangle(map, m_Game.GameTiles.WALL_BRICK, apartRect);
            foreach (Rectangle roomRect in apartementsWingTwo)
                base.TileRectangle(map, m_Game.GameTiles.WALL_BRICK, roomRect);

            // put door leading to corridor; and an opposite window if outer wall / a door if inside
            foreach (Rectangle apartRect in apartementsWingOne)
            {
                if (horizontalCorridor)
                {
                    PlaceDoor(map, apartRect.Left + apartRect.Width / 2, apartRect.Bottom - 1, m_Game.GameTiles.FLOOR_PLANKS, base.MakeObjWoodenDoor());
                    PlaceDoor(map, apartRect.Left + apartRect.Width / 2, apartRect.Top, m_Game.GameTiles.FLOOR_PLANKS, base.MakeObjWindow());
                }
                else
                {
                    PlaceDoor(map, apartRect.Right - 1, apartRect.Top + apartRect.Height / 2, m_Game.GameTiles.FLOOR_PLANKS, base.MakeObjWoodenDoor());
                    PlaceDoor(map, apartRect.Left, apartRect.Top + apartRect.Height / 2, m_Game.GameTiles.FLOOR_PLANKS, base.MakeObjWindow());
                }
            }
            foreach (Rectangle apartRect in apartementsWingTwo)
            {
                if (horizontalCorridor)
                {
                    PlaceDoor(map, apartRect.Left + apartRect.Width / 2, apartRect.Top, m_Game.GameTiles.FLOOR_PLANKS, base.MakeObjWoodenDoor());
                    PlaceDoor(map, apartRect.Left + apartRect.Width / 2, apartRect.Bottom - 1, m_Game.GameTiles.FLOOR_PLANKS, base.MakeObjWindow());
                }
                else
                {
                    PlaceDoor(map, apartRect.Left, apartRect.Top + apartRect.Height / 2, m_Game.GameTiles.FLOOR_PLANKS, base.MakeObjWoodenDoor());
                    PlaceDoor(map, apartRect.Right - 1, apartRect.Top + apartRect.Height / 2, m_Game.GameTiles.FLOOR_PLANKS, base.MakeObjWindow());
                }
            }

            // fill appartements with furniture and items
            // an "apartement" is one big room that fits all the housing roles: bedroom, kitchen and living room.
            foreach (Rectangle apartRect in allApartments)
            {
                // bedroom
                FillHousingRoomContents(map, apartRect, 0);
                // kitchen
                FillHousingRoomContents(map, apartRect, 8);
                // living room
                FillHousingRoomContents(map, apartRect, 5);
            }
            #endregion

            ///////////
            // 6. Zone
            ///////////
            Zone zone = base.MakeUniqueZone("Apartements", b.BuildingRect);
            map.AddZone(zone);
            MakeWalkwayZones(map, b);

            // done
            return true;
        }

        // alpha10.1 pre alpha10.1 regular houses
        protected virtual bool MakeVanillaHousingBuilding(Map map, Block b)
        {
            ////////////////////////
            // 0. Check suitability
            ////////////////////////
            if (b.InsideRect.Width < 4 || b.InsideRect.Height < 4)
                return false;

            /////////////////////////////
            // 1. Walkway, floor & walls
            /////////////////////////////
            base.TileRectangle(map, m_Game.GameTiles.FLOOR_WALKWAY, b.Rectangle);
            base.TileRectangle(map, m_Game.GameTiles.WALL_BRICK, b.BuildingRect);
            base.TileFill(map, m_Game.GameTiles.FLOOR_PLANKS, b.InsideRect, (tile, prevmodel, x, y) => tile.IsInside = true);

            ///////////////////////
            // 2. Rooms floor plan
            ///////////////////////
            List<Rectangle> roomsList = new List<Rectangle>();
            MakeRoomsPlan(map, ref roomsList, b.BuildingRect, 5, 5);

            /////////////////
            // 3. Make rooms
            /////////////////
            // alpha10 make some housings floor plan non rectangular by randomly chosing not to place one border room
            // and replace it with a special "outside" room : a garden, a parking lot.

            int iOutsideRoom = -1;
            HouseOutsideRoomType outsideRoom = HouseOutsideRoomType._FIRST;
            if (roomsList.Count >= HOUSE_OUTSIDE_ROOM_NEED_MIN_ROOMS && m_DiceRoller.RollChance(HOUSE_OUTSIDE_ROOM_CHANCE))
            {
                for (; ; )
                {
                    iOutsideRoom = m_DiceRoller.Roll(0, roomsList.Count);
                    Rectangle r = roomsList[iOutsideRoom];
                    if (r.Left == b.BuildingRect.Left || r.Right == b.BuildingRect.Right || r.Top == b.BuildingRect.Top || r.Bottom == b.BuildingRect.Bottom)
                        break;
                }
                outsideRoom = (HouseOutsideRoomType)m_DiceRoller.Roll((int)HouseOutsideRoomType._FIRST, (int)HouseOutsideRoomType._COUNT);
            }

            for (int i = 0; i < roomsList.Count; i++)
            {
                Rectangle roomRect = roomsList[i];
                if (iOutsideRoom == i)
                {
                    // make sure all tiles are marked as outside
                    base.DoForEachTile(map, roomRect, (pt) => map.GetTileAt(pt).IsInside = false);

                    // then shrink it properly so we dont overlap with tiles from other rooms and mess things up.
                    if (roomRect.Left != b.BuildingRect.Left)
                    {
                        roomRect.X++;
                        roomRect.Width--;
                    }
                    if (roomRect.Right != b.BuildingRect.Right)
                    {
                        roomRect.Width--;
                    }
                    if (roomRect.Top != b.BuildingRect.Top)
                    {
                        roomRect.Y++;
                        roomRect.Height--;
                    }
                    if (roomRect.Bottom != b.BuildingRect.Bottom)
                    {
                        roomRect.Height--;
                    }

                    // then fill the outside room
                    switch (outsideRoom)
                    {
                        case HouseOutsideRoomType.GARDEN:
                            base.TileFill(map, m_Game.GameTiles.FLOOR_GRASS, roomRect);
                            base.DoForEachTile(map, roomRect,
                                (pos) =>
                                {
                                    if (map.GetTileAt(pos).Model == m_Game.GameTiles.FLOOR_GRASS && m_DiceRoller.RollChance(HOUSE_GARDEN_TREE_CHANCE))
                                        map.PlaceMapObjectAt(MakeObjTree(GameImages.OBJ_TREE), pos);
                                });
                            break;

                        case HouseOutsideRoomType.PARKING_LOT:
                            base.TileFill(map, m_Game.GameTiles.FLOOR_ASPHALT, roomRect);
                            base.DoForEachTile(map, roomRect,
                                (pos) =>
                                {
                                    if (map.GetTileAt(pos).Model == m_Game.GameTiles.FLOOR_ASPHALT && m_DiceRoller.RollChance(HOUSE_PARKING_LOT_CAR_CHANCE))
                                        map.PlaceMapObjectAt(MakeObjWreckedCar(m_DiceRoller), pos);
                                });
                            break;
                    }
                }
                else
                {
                    MakeHousingRoom(map, roomRect, m_Game.GameTiles.FLOOR_PLANKS, m_Game.GameTiles.WALL_BRICK);
                    FillHousingRoomContents(map, roomRect);
                }
            }

            // once all rooms are done, enclose the outside room
            if (iOutsideRoom != -1)
            {
                Rectangle roomRect = roomsList[iOutsideRoom];
                switch (outsideRoom)
                {
                    case HouseOutsideRoomType.GARDEN:
                        base.DoForEachTile(map, roomRect,
                            (pos) =>
                            {
                                if ((pos.X == roomRect.Left || pos.X == roomRect.Right - 1 || pos.Y == roomRect.Top || pos.Y == roomRect.Bottom - 1) && map.GetTileAt(pos).Model == m_Game.GameTiles.FLOOR_GRASS)
                                {
                                    map.RemoveMapObjectAt(pos.X, pos.Y); // make sure trees are removed
                                    map.PlaceMapObjectAt(MakeObjFence(GameImages.OBJ_GARDEN_FENCE, MapObject.Fire.BURNABLE, DoorWindow.BASE_HITPOINTS / 2), pos);
                                }
                            });
                        break;

                    case HouseOutsideRoomType.PARKING_LOT:
                        base.DoForEachTile(map, roomRect,
                            (pos) =>
                            {
                                bool isLotEntry = (pos.X == roomRect.Left + roomRect.Width / 2) || (pos.Y == roomRect.Top + roomRect.Height / 2);
                                if (!isLotEntry && ((pos.X == roomRect.Left || pos.X == roomRect.Right - 1 || pos.Y == roomRect.Top || pos.Y == roomRect.Bottom - 1) && map.GetTileAt(pos).Model == m_Game.GameTiles.FLOOR_ASPHALT))
                                {
                                    map.RemoveMapObjectAt(pos.X, pos.Y); // make sure cars are removed
                                    map.PlaceMapObjectAt(MakeObjWireFence(GameImages.OBJ_WIRE_FENCE), pos);
                                }
                            });
                        break;
                }
            }

            ///////////////////////////////////////
            // 5. Fix buildings with no door exits
            ///////////////////////////////////////
            #region
            bool hasOutsideDoor = false;
            for (int x = b.BuildingRect.Left; x < b.BuildingRect.Right && !hasOutsideDoor; x++)
                for (int y = b.BuildingRect.Top; y < b.BuildingRect.Bottom && !hasOutsideDoor; y++)
                {
                    if (!map.GetTileAt(x, y).IsInside)
                    {
                        DoorWindow door = map.GetMapObjectAt(x, y) as DoorWindow;
                        if (door != null && !door.IsWindow)
                            hasOutsideDoor = true;
                    }
                }
            if (!hasOutsideDoor)
            {
                // replace a random window with a door.
                // alpha10 list all the exit windows, pick one and replace with a door.

                // list all exit windows
                List<Point> buildingExits = new List<Point>(8);
                for (int x = b.BuildingRect.Left; x < b.BuildingRect.Right; x++)
                    for (int y = b.BuildingRect.Top; y < b.BuildingRect.Bottom; y++)
                    {
                        if (!map.GetTileAt(x, y).IsInside)
                        {
                            DoorWindow window = map.GetMapObjectAt(x, y) as DoorWindow;
                            if (window != null && window.IsWindow)
                            {
                                buildingExits.Add(new Point(x, y));
                            }
                        }
                    }

                // replace an exit window with a door
                if (buildingExits.Count > 0)
                {
                    Point newDoorPos = buildingExits[m_DiceRoller.Roll(0, buildingExits.Count)];
                    map.RemoveMapObjectAt(newDoorPos.X, newDoorPos.Y);
                    map.PlaceMapObjectAt(MakeObjWoodenDoor(), newDoorPos);
                    hasOutsideDoor = true;
                }

                // if we did not found an exit window to replace this is a bug, it should never happen.
                // i'm lazy and assume this never happens and throw an exception.
                if (hasOutsideDoor == false)
                {
                    Logger.WriteLine(Logger.Stage.RUN_MAIN, "ERROR: house has no exit, should never happen; sector@" + map.District.WorldPosition + " house@" + b.BuildingRect);
                    throw new Exception("house has not exit, should never happen. read the log.");
                }
            }
            #endregion

            ////////////////
            // 6. Basement?
            ////////////////
            #region
            if (m_DiceRoller.RollChance(HOUSE_BASEMENT_CHANCE))
            {
                Map basementMap = GenerateHouseBasementMap(map, b);
                m_Params.District.AddUniqueMap(basementMap);
            }
            #endregion

            ///////////
            // 7. Zone
            ///////////
            #region
            map.AddZone(MakeUniqueZone("Housing", b.BuildingRect));
            MakeWalkwayZones(map, b);
            #endregion

            // Done
            return true;
        }

        protected virtual void MakeSewersMaintenanceBuilding(Map map, bool isSurface, Block b, Map linkedMap, Point exitPosition)
        {
            ///////////////
            // Outer walls.
            ///////////////
            // if sewers dig room.
            if (!isSurface)
                TileFill(map, m_Game.GameTiles.FLOOR_CONCRETE, b.InsideRect);
            // outer walls.
            TileRectangle(map, m_Game.GameTiles.WALL_SEWER, b.BuildingRect);
            // make sure its marked as inside (in case we replace a park for instance)
            for (int x = b.InsideRect.Left; x < b.InsideRect.Right; x++)
                for (int y = b.InsideRect.Top; y < b.InsideRect.Bottom; y++)
                    map.GetTileAt(x, y).IsInside = true;

            //////////////////
            // Entrance door.
            //////////////////
            // pick door side and put tags.
            #region
            int doorX, doorY;
            Direction digDirection;
            int sideRoll = m_DiceRoller.Roll(0, 4);
            switch (sideRoll)
            {
                case 0: // north.
                    digDirection = Direction.N;
                    doorX = b.BuildingRect.Left + b.BuildingRect.Width / 2;
                    doorY = b.BuildingRect.Top;

                    map.GetTileAt(doorX - 1, doorY).AddDecoration(GameImages.DECO_SEWERS_BUILDING);
                    map.GetTileAt(doorX + 1, doorY).AddDecoration(GameImages.DECO_SEWERS_BUILDING);
                    break;

                case 1: // south.
                    digDirection = Direction.S;
                    doorX = b.BuildingRect.Left + b.BuildingRect.Width / 2;
                    doorY = b.BuildingRect.Bottom - 1;

                    map.GetTileAt(doorX - 1, doorY).AddDecoration(GameImages.DECO_SEWERS_BUILDING);
                    map.GetTileAt(doorX + 1, doorY).AddDecoration(GameImages.DECO_SEWERS_BUILDING);
                    break;

                case 2: // west.
                    digDirection = Direction.W;
                    doorX = b.BuildingRect.Left;
                    doorY = b.BuildingRect.Top + b.BuildingRect.Height / 2;


                    map.GetTileAt(doorX, doorY - 1).AddDecoration(GameImages.DECO_SEWERS_BUILDING);
                    map.GetTileAt(doorX, doorY + 1).AddDecoration(GameImages.DECO_SEWERS_BUILDING);
                    break;

                case 3: // east.
                    digDirection = Direction.E;
                    doorX = b.BuildingRect.Right - 1;
                    doorY = b.BuildingRect.Top + b.BuildingRect.Height / 2;


                    map.GetTileAt(doorX, doorY - 1).AddDecoration(GameImages.DECO_SEWERS_BUILDING);
                    map.GetTileAt(doorX, doorY + 1).AddDecoration(GameImages.DECO_SEWERS_BUILDING);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("unhandled roll");
            }
            // add the door.
            PlaceDoor(map, doorX, doorY, m_Game.GameTiles.FLOOR_CONCRETE, MakeObjIronDoor());
            BarricadeDoors(map, b.BuildingRect, Rules.BARRICADING_MAX);
            #endregion

            /////////////////////////////////
            // Hole/Ladder to sewers/surface.
            /////////////////////////////////
            // add exit.
            map.GetTileAt(exitPosition.X, exitPosition.Y).AddDecoration(isSurface ? GameImages.DECO_SEWER_HOLE : GameImages.DECO_SEWER_LADDER);
            map.SetExitAt(exitPosition, new Exit(linkedMap, exitPosition) { IsAnAIExit = true });

            ///////////////////////////////////////////////////
            // If sewers, dig corridor until we reach a tunnel.
            ///////////////////////////////////////////////////
            if (!isSurface)
            {
                Point digPos = new Point(doorX, doorY) + digDirection;
                while (map.IsInBounds(digPos) && !map.GetTileAt(digPos.X, digPos.Y).Model.IsWalkable)
                {
                    // corridor.
                    map.SetTileModelAt(digPos.X, digPos.Y, m_Game.GameTiles.FLOOR_CONCRETE);
                    // continue digging.
                    digPos += digDirection;
                }
            }

            /////////////////////
            // Furniture & Items.
            /////////////////////
            // bunch of tables near walls with construction items on them.
            int nbTables = m_DiceRoller.Roll(Math.Max(b.InsideRect.Width, b.InsideRect.Height), 2 * Math.Max(b.InsideRect.Width, b.InsideRect.Height));
            for (int i = 0; i < nbTables; i++)
            {
                MapObjectPlaceInGoodPosition(map, b.InsideRect,
                    (pt) => CountAdjWalls(map, pt.X, pt.Y) >= 3 && CountAdjDoors(map, pt.X, pt.Y) == 0,
                    m_DiceRoller,
                    (pt) =>
                    {
                        // add item.
                        map.DropItemAt(MakeShopConstructionItem(), pt);

                        // add table.
                        return MakeObjTable(GameImages.OBJ_TABLE);
                    });
            }
            // a bed and a fridge with food if lucky.
            if (m_DiceRoller.RollChance(33))
            {
                // bed.
                MapObjectPlaceInGoodPosition(map, b.InsideRect,
                    (pt) => CountAdjWalls(map, pt.X, pt.Y) >= 3 && CountAdjDoors(map, pt.X, pt.Y) == 0,
                    m_DiceRoller,
                    (pt) => MakeObjBed(GameImages.OBJ_BED));

                // fridge + food.
                MapObjectPlaceInGoodPosition(map, b.InsideRect,
                    (pt) => CountAdjWalls(map, pt.X, pt.Y) >= 3 && CountAdjDoors(map, pt.X, pt.Y) == 0,
                    m_DiceRoller,
                    (pt) =>
                    {
                        // add food.
                        map.DropItemAt(MakeItemCannedFood(), pt);

                        // add fridge.
                        return MakeObjFridge(GameImages.OBJ_FRIDGE);
                    });
            }

            ////////////////////////////////////
            // Add the poor maintenance guy/gal.
            ////////////////////////////////////
            Actor poorGuy = CreateNewCivilian(0, 3, 1);
            ActorPlace(m_DiceRoller, b.Rectangle.Width * b.Rectangle.Height, map, poorGuy, b.InsideRect.Left, b.InsideRect.Top, b.InsideRect.Width, b.InsideRect.Height);

            //////////////
            // Make zone.
            //////////////
            map.AddZone(MakeUniqueZone(RogueGame.NAME_SEWERS_MAINTENANCE, b.BuildingRect));

            // Done...
        }

        protected virtual void MakeSubwayStationBuilding(Map map, bool isSurface, Block b, Map linkedMap, Point exitPosition)
        {
            ///////////////
            // Outer walls.
            ///////////////
            #region
            // if sewers dig room.
            if (!isSurface)
                TileFill(map, m_Game.GameTiles.FLOOR_CONCRETE, b.InsideRect);
            // outer walls.
            TileRectangle(map, m_Game.GameTiles.WALL_SUBWAY, b.BuildingRect);
            // make sure its marked as inside (in case we replace a park for instance)
            for (int x = b.InsideRect.Left; x < b.InsideRect.Right; x++)
                for (int y = b.InsideRect.Top; y < b.InsideRect.Bottom; y++)
                    map.GetTileAt(x, y).IsInside = true;
            #endregion

            ////////////
            // Entrance
            ////////////
            #region
            // pick door/corridor side and put tags.
            // if not surface, we must dig toward the rails.
            int entryFenceX, entryFenceY;
            Direction digDirection;
            int sideRoll;
            if (isSurface)
                sideRoll = m_DiceRoller.Roll(0, 4);
            else
                sideRoll = b.Rectangle.Bottom < map.Width / 2 ? 1 : 0;
            switch (sideRoll)
            {
                case 0: // north.
                    digDirection = Direction.N;
                    entryFenceX = b.BuildingRect.Left + b.BuildingRect.Width / 2;
                    entryFenceY = b.BuildingRect.Top;

                    if (isSurface)
                    {
                        map.GetTileAt(entryFenceX - 1, entryFenceY).AddDecoration(GameImages.DECO_SUBWAY_BUILDING);
                        map.GetTileAt(entryFenceX + 1, entryFenceY).AddDecoration(GameImages.DECO_SUBWAY_BUILDING);
                    }
                    break;

                case 1: // south.
                    digDirection = Direction.S;
                    entryFenceX = b.BuildingRect.Left + b.BuildingRect.Width / 2;
                    entryFenceY = b.BuildingRect.Bottom - 1;

                    if (isSurface)
                    {
                        map.GetTileAt(entryFenceX - 1, entryFenceY).AddDecoration(GameImages.DECO_SUBWAY_BUILDING);
                        map.GetTileAt(entryFenceX + 1, entryFenceY).AddDecoration(GameImages.DECO_SUBWAY_BUILDING);
                    }
                    break;

                case 2: // west.
                    digDirection = Direction.W;
                    entryFenceX = b.BuildingRect.Left;
                    entryFenceY = b.BuildingRect.Top + b.BuildingRect.Height / 2;

                    if (isSurface)
                    {
                        map.GetTileAt(entryFenceX, entryFenceY - 1).AddDecoration(GameImages.DECO_SUBWAY_BUILDING);
                        map.GetTileAt(entryFenceX, entryFenceY + 1).AddDecoration(GameImages.DECO_SUBWAY_BUILDING);
                    }
                    break;

                case 3: // east.
                    digDirection = Direction.E;
                    entryFenceX = b.BuildingRect.Right - 1;
                    entryFenceY = b.BuildingRect.Top + b.BuildingRect.Height / 2;

                    if (isSurface)
                    {
                        map.GetTileAt(entryFenceX, entryFenceY - 1).AddDecoration(GameImages.DECO_SUBWAY_BUILDING);
                        map.GetTileAt(entryFenceX, entryFenceY + 1).AddDecoration(GameImages.DECO_SUBWAY_BUILDING);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException("unhandled roll");
            }
            // add door if surface.
            if (isSurface)
            {
                map.SetTileModelAt(entryFenceX, entryFenceY, m_Game.GameTiles.FLOOR_CONCRETE);
                map.PlaceMapObjectAt(MakeObjGlassDoor(), new Point(entryFenceX, entryFenceY));
            }
            #endregion

            ///////////////////////////
            // Stairs to the other map.
            ///////////////////////////
            #region
            // add exits.
            for (int ex = exitPosition.X - 1; ex <= exitPosition.X + 1; ex++)
            {
                Point thisExitPos = new Point(ex, exitPosition.Y);
                map.GetTileAt(thisExitPos.X, thisExitPos.Y).AddDecoration(isSurface ? GameImages.DECO_STAIRS_DOWN : GameImages.DECO_STAIRS_UP);
                map.SetExitAt(thisExitPos, new Exit(linkedMap, thisExitPos) { IsAnAIExit = true });
            }
            #endregion

            ///////////////////////////////////////////////////
            // If subway :
            // - dig corridor until we reach the rails.
            // - dig platform and make corridor zone.
            // - add closed iron fences between corridor and platform.
            // - make power room.
            ///////////////////////////////////////////////////
            #region
            if (!isSurface)
            {
                // - dig corridor until we reach the rails.
                #region
                map.SetTileModelAt(entryFenceX, entryFenceY, m_Game.GameTiles.FLOOR_CONCRETE);
                map.SetTileModelAt(entryFenceX + 1, entryFenceY, m_Game.GameTiles.FLOOR_CONCRETE);
                map.SetTileModelAt(entryFenceX - 1, entryFenceY, m_Game.GameTiles.FLOOR_CONCRETE);
                map.SetTileModelAt(entryFenceX - 2, entryFenceY, m_Game.GameTiles.WALL_STONE);
                map.SetTileModelAt(entryFenceX + 2, entryFenceY, m_Game.GameTiles.WALL_STONE);

                Point digPos = new Point(entryFenceX, entryFenceY) + digDirection;
                while (map.IsInBounds(digPos) && !map.GetTileAt(digPos.X, digPos.Y).Model.IsWalkable)
                {
                    // corridor.
                    map.SetTileModelAt(digPos.X, digPos.Y, m_Game.GameTiles.FLOOR_CONCRETE);
                    map.SetTileModelAt(digPos.X - 1, digPos.Y, m_Game.GameTiles.FLOOR_CONCRETE);
                    map.SetTileModelAt(digPos.X + 1, digPos.Y, m_Game.GameTiles.FLOOR_CONCRETE);
                    map.SetTileModelAt(digPos.X - 2, digPos.Y, m_Game.GameTiles.WALL_STONE);
                    map.SetTileModelAt(digPos.X + 2, digPos.Y, m_Game.GameTiles.WALL_STONE);

                    // continue digging.
                    digPos += digDirection;
                }
                #endregion

                // - dig platform and make corridor zone.
                #region
                const int platformExtend = 10;
                const int platformWidth = 3;
                Rectangle platformRect;
                int platformLeft = Math.Max(0, b.BuildingRect.Left - platformExtend);
                int platformRight = Math.Min(map.Width - 1, b.BuildingRect.Right + platformExtend);
                int benchesLine;
                if (digDirection == Direction.S)
                {
                    platformRect = Rectangle.FromLTRB(platformLeft, digPos.Y - platformWidth, platformRight, digPos.Y);
                    benchesLine = platformRect.Top;
                    map.AddZone(MakeUniqueZone("corridor", Rectangle.FromLTRB(entryFenceX - 1, entryFenceY, entryFenceX + 1 + 1, platformRect.Top)));
                }
                else
                {
                    platformRect = Rectangle.FromLTRB(platformLeft, digPos.Y + 1, platformRight, digPos.Y + 1 + platformWidth);
                    benchesLine = platformRect.Bottom - 1;
                    map.AddZone(MakeUniqueZone("corridor", Rectangle.FromLTRB(entryFenceX - 1, platformRect.Bottom, entryFenceX + 1 + 1, entryFenceY + 1)));
                }
                TileFill(map, m_Game.GameTiles.FLOOR_CONCRETE, platformRect);

                // - iron benches in platform.
                for (int bx = platformRect.Left; bx < platformRect.Right; bx++)
                {
                    if (CountAdjWalls(map, bx, benchesLine) < 3)
                        continue;
                    map.PlaceMapObjectAt(MakeObjIronBench(GameImages.OBJ_IRON_BENCH), new Point(bx, benchesLine));
                }

                // - platform zone.
                map.AddZone(MakeUniqueZone("platform", platformRect));
                #endregion

                // - add closed iron gates between corridor and platform.
                #region
                Point ironFencePos;
                if (digDirection == Direction.S)
                    ironFencePos = new Point(entryFenceX, platformRect.Top - 1);
                else
                    ironFencePos = new Point(entryFenceX, platformRect.Bottom);
                map.PlaceMapObjectAt(MakeObjIronGate(GameImages.OBJ_GATE_CLOSED), new Point(ironFencePos.X, ironFencePos.Y));
                map.PlaceMapObjectAt(MakeObjIronGate(GameImages.OBJ_GATE_CLOSED), new Point(ironFencePos.X + 1, ironFencePos.Y));
                map.PlaceMapObjectAt(MakeObjIronGate(GameImages.OBJ_GATE_CLOSED), new Point(ironFencePos.X - 1, ironFencePos.Y));
                #endregion

                // - make power room.
                #region
                // access in the corridor, going toward the center of the map.
                Point powerRoomEntry;
                Rectangle powerRoomRect;
                const int powerRoomWidth = 4;
                const int powerRoomHalfHeight = 2;
                if (entryFenceX > map.Width / 2)
                {
                    // west.
                    powerRoomEntry = new Point(entryFenceX - 2, entryFenceY + powerRoomHalfHeight * digDirection.Vector.Y);
                    powerRoomRect = Rectangle.FromLTRB(powerRoomEntry.X - powerRoomWidth, powerRoomEntry.Y - powerRoomHalfHeight, powerRoomEntry.X + 1, powerRoomEntry.Y + powerRoomHalfHeight + 1);
                }
                else
                {
                    // east.
                    powerRoomEntry = new Point(entryFenceX + 2, entryFenceY + powerRoomHalfHeight * digDirection.Vector.Y);
                    powerRoomRect = Rectangle.FromLTRB(powerRoomEntry.X, powerRoomEntry.Y - powerRoomHalfHeight, powerRoomEntry.X + powerRoomWidth, powerRoomEntry.Y + powerRoomHalfHeight + 1);
                }

                // carve power room.
                TileFill(map, m_Game.GameTiles.FLOOR_CONCRETE, powerRoomRect);
                TileRectangle(map, m_Game.GameTiles.WALL_STONE, powerRoomRect);

                // add door with signs.
                PlaceDoor(map, powerRoomEntry.X, powerRoomEntry.Y, m_Game.GameTiles.FLOOR_CONCRETE, MakeObjIronDoor());
                map.GetTileAt(powerRoomEntry.X, powerRoomEntry.Y - 1).AddDecoration(GameImages.DECO_POWER_SIGN_BIG);
                map.GetTileAt(powerRoomEntry.X, powerRoomEntry.Y + 1).AddDecoration(GameImages.DECO_POWER_SIGN_BIG);

                // add power generators along wall.
                MapObjectFill(map, powerRoomRect,
                    (pt) =>
                    {
                        if (!map.GetTileAt(pt).Model.IsWalkable)
                            return null;
                        if (CountAdjWalls(map, pt.X, pt.Y) < 3 || CountAdjDoors(map, pt.X, pt.Y) > 0)
                            return null;
                        return MakeObjPowerGenerator(GameImages.OBJ_POWERGEN_OFF, GameImages.OBJ_POWERGEN_ON);
                    });

                #endregion
            }
            #endregion

            /////////////////////
            // Furniture & Items.
            /////////////////////
            // iron benches in station.
            #region
            for (int bx = b.InsideRect.Left; bx < b.InsideRect.Right; bx++)
                for (int by = b.InsideRect.Top + 1; by < b.InsideRect.Bottom - 1; by++)
                {
                    // next to walls and no doors.
                    if (CountAdjWalls(map, bx, by) < 2 || CountAdjDoors(map, bx, by) > 0)
                        continue;

                    // not next to stairs.
                    if (m_Game.Rules.GridDistance(new Point(bx, by), new Point(entryFenceX, entryFenceY)) < 2)
                        continue;

                    // bench.
                    map.PlaceMapObjectAt(MakeObjIronBench(GameImages.OBJ_IRON_BENCH), new Point(bx, by));
                }
            #endregion

            /////////////////////////////////////
            // Add subway police guy on surface.
            /////////////////////////////////////
            if (isSurface)
            {
                Actor policeMan = CreateNewPoliceman(0);
                ActorPlace(m_DiceRoller, b.Rectangle.Width * b.Rectangle.Height, map, policeMan, b.InsideRect.Left, b.InsideRect.Top, b.InsideRect.Width, b.InsideRect.Height);
            }

            //////////////
            // Make zone.
            //////////////
            map.AddZone(MakeUniqueZone(RogueGame.NAME_SUBWAY_STATION, b.BuildingRect));
        }

        #endregion
    }
}
