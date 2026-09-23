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
        #region CHAR Underground Facility
        // alpha10 added entry pos
        public Map GenerateUniqueMap_CHARUnderground(Map surfaceMap, Zone officeZone, out Point baseEntryPos)
        {
            /////////////////////////
            // 1. Create basic secret map.
            // 2. Link to office.
            // 3. Create rooms.
            // 4. Furniture & Items.
            // 5. Posters & Blood.
            // 6. Populate.
            // 7. Add uniques.
            // alpha10
            // 8. Music
            /////////////////////////

            // 1. Create basic secret map.
            #region
            // huge map.
            Map underground = new Map((surfaceMap.Seed << 3) ^ surfaceMap.Seed, "CHAR Underground Facility", RogueGame.MAP_MAX_WIDTH, RogueGame.MAP_MAX_HEIGHT)
            {
                Lighting = Lighting.DARKNESS,
                IsSecret = true
            };
            // fill & enclose.
            TileFill(underground, m_Game.GameTiles.FLOOR_OFFICE, (tile, model, x, y) => tile.IsInside = true);
            TileRectangle(underground, m_Game.GameTiles.WALL_CHAR_OFFICE, new Rectangle(0, 0, underground.Width, underground.Height));
            #endregion

            // 2. Link to office.
            #region
            // find surface point in office:
            // - in a random office room.
            // - set exit somewhere walkable inside.
            // - iron door, barricade the door.
            Zone roomZone = null;
            Point surfaceExit = new Point();
            while (true)    // loop until found.
            {
                // find a random room.
                do
                {
                    int x = m_DiceRoller.Roll(officeZone.Bounds.Left, officeZone.Bounds.Right);
                    int y = m_DiceRoller.Roll(officeZone.Bounds.Top, officeZone.Bounds.Bottom);
                    List<Zone> zonesHere = surfaceMap.GetZonesAt(x, y);
                    if (zonesHere == null || zonesHere.Count == 0)
                        continue;
                    foreach (Zone z in zonesHere)
                        if (z.Name.Contains("room"))
                        {
                            roomZone = z;
                            break;
                        }
                }
                while (roomZone == null);

                // find somewhere walkable inside.
                bool foundSurfaceExit = false;
                int attempts = 0;
                do
                {
                    surfaceExit.X = m_DiceRoller.Roll(roomZone.Bounds.Left, roomZone.Bounds.Right);
                    surfaceExit.Y = m_DiceRoller.Roll(roomZone.Bounds.Top, roomZone.Bounds.Bottom);
                    foundSurfaceExit = surfaceMap.IsWalkable(surfaceExit.X, surfaceExit.Y);
                    ++attempts;
                }
                while (attempts < 100 && !foundSurfaceExit);

                // failed?
                if (foundSurfaceExit == false)
                    continue;

                // found everything, good!
                break;
            }

            // alpha10
            // remember position
            baseEntryPos = surfaceExit;

            // barricade the rooms door.
            DoForEachTile(surfaceMap, roomZone.Bounds,
                (pt) =>
                {
                    DoorWindow door = surfaceMap.GetMapObjectAt(pt) as DoorWindow;
                    if (door == null)
                        return;
                    surfaceMap.RemoveMapObjectAt(pt.X, pt.Y);
                    door = MakeObjIronDoor();
                    door.BarricadePoints = Rules.BARRICADING_MAX;
                    surfaceMap.PlaceMapObjectAt(door, pt);
                });

            // stairs.
            // underground : in the middle of the map.
            Point undergroundStairs = new Point(underground.Width / 2, underground.Height / 2);
            underground.SetExitAt(undergroundStairs, new Exit(surfaceMap, surfaceExit));
            underground.GetTileAt(undergroundStairs.X, undergroundStairs.Y).AddDecoration(GameImages.DECO_STAIRS_UP);
            surfaceMap.SetExitAt(surfaceExit, new Exit(underground, undergroundStairs));
            surfaceMap.GetTileAt(surfaceExit.X, surfaceExit.Y).AddDecoration(GameImages.DECO_STAIRS_DOWN);
            // floor logo.
            ForEachAdjacent(underground, undergroundStairs.X, undergroundStairs.Y, (pt) => underground.GetTileAt(pt).AddDecoration(GameImages.DECO_CHAR_FLOOR_LOGO));
            #endregion

            // 3. Create floorplan & rooms.
            #region
            // make 4 quarters, splitted by a crossed corridor.
            const int corridorHalfWidth = 1;
            Rectangle qTopLeft = Rectangle.FromLTRB(0, 0, underground.Width / 2 - corridorHalfWidth, underground.Height / 2 - corridorHalfWidth);
            Rectangle qTopRight = Rectangle.FromLTRB(underground.Width / 2 + 1 + corridorHalfWidth, 0, underground.Width, qTopLeft.Bottom);
            Rectangle qBotLeft = Rectangle.FromLTRB(0, underground.Height / 2 + 1 + corridorHalfWidth, qTopLeft.Right, underground.Height);
            Rectangle qBotRight = Rectangle.FromLTRB(qTopRight.Left, qBotLeft.Top, underground.Width, underground.Height);

            // split all the map in rooms.
            const int minRoomSize = 6;
            List<Rectangle> roomsList = new List<Rectangle>();
            MakeRoomsPlan(underground, ref roomsList, qBotLeft, minRoomSize, minRoomSize);
            MakeRoomsPlan(underground, ref roomsList, qBotRight, minRoomSize, minRoomSize);
            MakeRoomsPlan(underground, ref roomsList, qTopLeft, minRoomSize, minRoomSize);
            MakeRoomsPlan(underground, ref roomsList, qTopRight, minRoomSize, minRoomSize);

            // make the rooms walls.
            foreach (Rectangle roomRect in roomsList)
            {
                TileRectangle(underground, m_Game.GameTiles.WALL_CHAR_OFFICE, roomRect);
            }

            // add room doors.
            // quarters have door side preferences to lead toward the central corridors.
            foreach (Rectangle roomRect in roomsList)
            {
                Point westEastDoorPos = roomRect.Left < underground.Width / 2 ?
                    new Point(roomRect.Right - 1, roomRect.Top + roomRect.Height / 2) :
                    new Point(roomRect.Left, roomRect.Top + roomRect.Height / 2);
                if (underground.GetMapObjectAt(westEastDoorPos) == null)
                {
                    DoorWindow door = MakeObjCharDoor();
                    PlaceDoorIfAccessibleAndNotAdjacent(underground, westEastDoorPos.X, westEastDoorPos.Y, m_Game.GameTiles.FLOOR_OFFICE, 6, door);
                }

                Point northSouthDoorPos = roomRect.Top < underground.Height / 2 ?
                    new Point(roomRect.Left + roomRect.Width / 2, roomRect.Bottom - 1) :
                    new Point(roomRect.Left + roomRect.Width / 2, roomRect.Top);
                if (underground.GetMapObjectAt(northSouthDoorPos) == null)
                {
                    DoorWindow door = MakeObjCharDoor();
                    PlaceDoorIfAccessibleAndNotAdjacent(underground, northSouthDoorPos.X, northSouthDoorPos.Y, m_Game.GameTiles.FLOOR_OFFICE, 6, door);
                }
            }

            // add iron doors closing each corridor.
            for (int x = qTopLeft.Right; x < qBotRight.Left; x++)
            {
                PlaceDoor(underground, x, qTopLeft.Bottom - 1, m_Game.GameTiles.FLOOR_OFFICE, MakeObjIronDoor());
                PlaceDoor(underground, x, qBotLeft.Top, m_Game.GameTiles.FLOOR_OFFICE, MakeObjIronDoor());
            }
            for (int y = qTopLeft.Bottom; y < qBotLeft.Top; y++)
            {
                PlaceDoor(underground, qTopLeft.Right - 1, y, m_Game.GameTiles.FLOOR_OFFICE, MakeObjIronDoor());
                PlaceDoor(underground, qTopRight.Left, y, m_Game.GameTiles.FLOOR_OFFICE, MakeObjIronDoor());
            }
            #endregion

            // 4. Rooms, furniture & items.
            #region
            // furniture + items in rooms.
            // room roles with zones:
            // - corners room : Power Room.
            // - top left quarter : armory.
            // - top right quarter : storage.
            // - bottom left quarter : living.
            // - bottom right quarter : pharmacy.
            foreach (Rectangle roomRect in roomsList)
            {
                Rectangle insideRoomRect = new Rectangle(roomRect.Left + 1, roomRect.Top + 1, roomRect.Width - 2, roomRect.Height - 2);
                string roomName = "<noname>";

                // special room?
                // one power room in each corner.
                bool isPowerRoom = (roomRect.Left == 0 && roomRect.Top == 0) ||
                    (roomRect.Left == 0 && roomRect.Bottom == underground.Height) ||
                    (roomRect.Right == underground.Width && roomRect.Top == 0) ||
                    (roomRect.Right == underground.Width && roomRect.Bottom == underground.Height);
                if (isPowerRoom)
                {
                    roomName = "Power Room";
                    MakeCHARPowerRoom(underground, roomRect, insideRoomRect);
                }
                else
                {
                    // common room.
                    int roomRole = (roomRect.Left < underground.Width / 2 && roomRect.Top < underground.Height / 2) ? 0 :
                        (roomRect.Left >= underground.Width / 2 && roomRect.Top < underground.Height / 2) ? 1 :
                        (roomRect.Left < underground.Width / 2 && roomRect.Top >= underground.Height / 2) ? 2 :
                        3;
                    switch (roomRole)
                    {
                        case 0: // armory room.
                            {
                                roomName = "Armory";
                                MakeCHARArmoryRoom(underground, insideRoomRect);
                                break;
                            }
                        case 1: // storage room.
                            {
                                roomName = "Storage";
                                MakeCHARStorageRoom(underground, insideRoomRect);
                                break;
                            }
                        case 2: // living room.
                            {
                                roomName = "Living";
                                MakeCHARLivingRoom(underground, insideRoomRect);
                                break;
                            }
                        case 3: // pharmacy.
                            {
                                roomName = "Pharmacy";
                                MakeCHARPharmacyRoom(underground, insideRoomRect);
                                break;
                            }
                        default:
                            throw new ArgumentOutOfRangeException("unhandled role");
                    }
                }

                underground.AddZone(MakeUniqueZone(roomName, insideRoomRect));
            }
            #endregion

            // 5. Posters & Blood.
            #region
            // char propaganda posters & blood almost everywhere.
            for (int x = 0; x < underground.Width; x++)
                for (int y = 0; y < underground.Height; y++)
                {
                    // poster on wall?
                    if (m_DiceRoller.RollChance(25))
                    {
                        Tile tile = underground.GetTileAt(x, y);
                        if (tile.Model.IsWalkable)
                            continue;
                        tile.AddDecoration(CHAR_POSTERS[m_DiceRoller.Roll(0, CHAR_POSTERS.Length)]);
                    }

                    // blood?
                    if (m_DiceRoller.RollChance(20))
                    {
                        Tile tile = underground.GetTileAt(x, y);
                        if (tile.Model.IsWalkable)
                            tile.AddDecoration(GameImages.DECO_BLOODIED_FLOOR);
                        else
                            tile.AddDecoration(GameImages.DECO_BLOODIED_WALL);
                    }
                }
            #endregion

            // 6. Populate.
            // don't block exits!
            #region
            // leveled up undeads!
            int nbZombies = underground.Width;  // 100 for 100.
            for (int i = 0; i < nbZombies; i++)
            {
                Actor undead = CreateNewUndead(0);
                for (; ; )
                {
                    GameActors.IDs upID = m_Game.NextUndeadEvolution((GameActors.IDs)undead.Model.ID);
                    if (upID == (GameActors.IDs)undead.Model.ID)
                        break;
                    undead.Model = m_Game.GameActors[upID];
                }
                ActorPlace(m_DiceRoller, underground.Width * underground.Height, underground, undead, (pt) => underground.GetExitAt(pt) == null);
            }

            // CHAR Guards.
            int nbGuards = underground.Width / 10; // 10 for 100.
            for (int i = 0; i < nbGuards; i++)
            {
                Actor guard = CreateNewCHARGuard(0);
                ActorPlace(m_DiceRoller, underground.Width * underground.Height, underground, guard, (pt) => underground.GetExitAt(pt) == null);
            }
            #endregion

            // 7. Add uniques.
            // TODO...
            #region
            #endregion

            // alpha10
            // 8. Music
            underground.BgMusic = GameMusics.CHAR_UNDERGROUND_FACILITY;

            // done.
            return underground;
        }

        void MakeCHARArmoryRoom(Map map, Rectangle roomRect)
        {
            // Shelves with weapons/ammo along walls.
            MapObjectFill(map, roomRect,
                (pt) =>
                {
                    if (CountAdjWalls(map, pt.X, pt.Y) < 3)
                        return null;
                    // dont block exits!
                    if (map.GetExitAt(pt) != null)
                        return null;

                    // table + tracker/armor/weapon.
                    if (m_DiceRoller.RollChance(20))
                    {
                        Item it;
                        if (m_DiceRoller.RollChance(20))
                            it = MakeItemCHARLightBodyArmor();
                        else if (m_DiceRoller.RollChance(20))
                        {
                            it = m_DiceRoller.RollChance(50) ? MakeItemZTracker() : MakeItemBlackOpsGPS();
                        }
                        else
                        {
                            // rare grenades.
                            if (m_DiceRoller.RollChance(20))
                            {
                                it = MakeItemGrenade();
                            }
                            else
                            {
                                // weapon vs ammo.
                                if (m_DiceRoller.RollChance(30))
                                {
                                    it = m_DiceRoller.RollChance(50) ? MakeItemShotgun() : MakeItemHuntingRifle();
                                }
                                else
                                {
                                    it = m_DiceRoller.RollChance(50) ? MakeItemShotgunAmmo() : MakeItemLightRifleAmmo();
                                }
                            }
                        }
                        map.DropItemAt(it, pt);

                        MapObject shelf = MakeObjShelf(GameImages.OBJ_SHOP_SHELF);
                        return shelf;
                    }
                    else
                        return null;
                });
        }

        void MakeCHARStorageRoom(Map map, Rectangle roomRect)
        {
            // Replace floor with concrete.
            TileFill(map, m_Game.GameTiles.FLOOR_CONCRETE, roomRect);

            // Objects.
            // Barrels & Junk in the middle of the room.
            MapObjectFill(map, roomRect,
                (pt) =>
                {
                    if (CountAdjWalls(map, pt.X, pt.Y) > 0)
                        return null;
                    // dont block exits!
                    if (map.GetExitAt(pt) != null)
                        return null;

                    // barrels/junk?
                    if (m_DiceRoller.RollChance(50))
                        return m_DiceRoller.RollChance(50) ? MakeObjJunk(GameImages.OBJ_JUNK) : MakeObjBarrels(GameImages.OBJ_BARRELS);
                    else
                        return null;
                });

            // Items.
            // Construction items in this mess.
            for (int x = roomRect.Left; x < roomRect.Right; x++)
                for (int y = roomRect.Top; y < roomRect.Bottom; y++)
                {
                    if (CountAdjWalls(map, x, y) > 0)
                        continue;
                    if (map.GetMapObjectAt(x, y) != null)
                        continue;

                    map.DropItemAt(MakeShopConstructionItem(), x, y);
                }
        }

        void MakeCHARLivingRoom(Map map, Rectangle roomRect)
        {
            // Replace floor with wood with painted logo.
            TileFill(map, m_Game.GameTiles.FLOOR_PLANKS, roomRect, (tile, model, x, y) => tile.AddDecoration(GameImages.DECO_CHAR_FLOOR_LOGO));

            // Objects.
            // Beds/Fridges along walls.
            MapObjectFill(map, roomRect,
                (pt) =>
                {
                    if (CountAdjWalls(map, pt.X, pt.Y) < 3)
                        return null;
                    // dont block exits!
                    if (map.GetExitAt(pt) != null)
                        return null;

                    // bed/fridge?
                    if (m_DiceRoller.RollChance(30))
                    {
                        if (m_DiceRoller.RollChance(50))
                            return MakeObjBed(GameImages.OBJ_BED);
                        else
                            return MakeObjFridge(GameImages.OBJ_FRIDGE);
                    }
                    else
                        return null;
                });
            // Tables(with canned food) & Chairs in the middle.
            MapObjectFill(map, roomRect,
                (pt) =>
                {
                    if (CountAdjWalls(map, pt.X, pt.Y) > 0)
                        return null;
                    // dont block exits!
                    if (map.GetExitAt(pt) != null)
                        return null;

                    // tables/chairs.
                    if (m_DiceRoller.RollChance(30))
                    {
                        if (m_DiceRoller.RollChance(30))
                        {
                            MapObject table = MakeObjTable(GameImages.OBJ_CHAR_TABLE);
                            map.DropItemAt(MakeItemCannedFood(), pt);
                            return table;
                        }
                        else
                            return MakeObjChair(GameImages.OBJ_CHAR_CHAIR);
                    }
                    else
                        return null;
                });
        }

        void MakeCHARPharmacyRoom(Map map, Rectangle roomRect)
        {
            // Shelves with medicine along walls.
            MapObjectFill(map, roomRect,
                (pt) =>
                {
                    if (CountAdjWalls(map, pt.X, pt.Y) < 3)
                        return null;
                    // dont block exits!
                    if (map.GetExitAt(pt) != null)
                        return null;

                    // table + meds.
                    if (m_DiceRoller.RollChance(20))
                    {
                        Item it = MakeHospitalItem();
                        map.DropItemAt(it, pt);

                        MapObject shelf = MakeObjShelf(GameImages.OBJ_SHOP_SHELF);
                        return shelf;
                    }
                    else
                        return null;
                });
        }

        void MakeCHARPowerRoom(Map map, Rectangle wallsRect, Rectangle roomRect)
        {
            // Replace floor with concrete.
            TileFill(map, m_Game.GameTiles.FLOOR_CONCRETE, roomRect);

            // add deco power sign next to doors.
            DoForEachTile(map, wallsRect,
                (pt) =>
                {
                    if (!(map.GetMapObjectAt(pt) is DoorWindow))
                        return;
                    DoForEachAdjacentInMap(map, pt, (
                        ptAdj) =>
                        {
                            Tile tile = map.GetTileAt(ptAdj);
                            if (tile.Model.IsWalkable)
                                return;
                            tile.RemoveAllDecorations();
                            tile.AddDecoration(GameImages.DECO_POWER_SIGN_BIG);
                        });
                });

            // add power generators along walls.
            DoForEachTile(map, roomRect,
                (pt) =>
                {
                    if (!map.GetTileAt(pt).Model.IsWalkable)
                        return;
                    if (map.GetExitAt(pt) != null)
                        return;
                    if (CountAdjWalls(map, pt.X, pt.Y) < 3)
                        return;

                    PowerGenerator powGen = MakeObjPowerGenerator(GameImages.OBJ_POWERGEN_OFF, GameImages.OBJ_POWERGEN_ON);
                    map.PlaceMapObjectAt(powGen, pt);
                });
        }
        #endregion
    }
}
