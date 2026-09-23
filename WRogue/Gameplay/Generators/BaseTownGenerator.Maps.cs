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
        #region Entry Map (Surface)
        public override Map Generate(int seed)
        {
            m_DiceRoller = new DiceRoller(seed);
            Map map = new Map(seed, "Base City", m_Params.MapWidth, m_Params.MapHeight);

            ///////////////////
            // Init with grass
            ///////////////////
            base.TileFill(map, m_Game.GameTiles.FLOOR_GRASS);

            ///////////////
            // Cut blocks
            ///////////////
            List<Block> blocks = new List<Block>();
            Rectangle cityRectangle = new Rectangle(0, 0, map.Width, map.Height);
            MakeBlocks(map, true, ref blocks, cityRectangle);

            ///////////////////////////////////////
            // Make concrete buildings from blocks
            ///////////////////////////////////////
            List<Block> emptyBlocks = new List<Block>(blocks);
            List<Block> completedBlocks = new List<Block>(emptyBlocks.Count);

            // remember blocks.
            m_SurfaceBlocks = new List<Block>(blocks.Count);
            foreach (Block b in blocks)
                m_SurfaceBlocks.Add(new Block(b));

            // Special buildings.
            #region
            // Police Station?
            if (m_Params.GeneratePoliceStation)
            {
                Block policeBlock;
                MakePoliceStation(map, blocks, out policeBlock);
                emptyBlocks.Remove(policeBlock);
            }
            // Hospital?
            if (m_Params.GenerateHospital)
            {
                Block hospitalBlock;
                MakeHospital(map, blocks, out hospitalBlock);
                emptyBlocks.Remove(hospitalBlock);
            }
            #endregion

            // shops.
            completedBlocks.Clear();
            foreach (Block b in emptyBlocks)
            {
                if (m_DiceRoller.RollChance(m_Params.ShopBuildingChance) &&
                    MakeShopBuilding(map, b))
                    completedBlocks.Add(b);
            }
            foreach (Block b in completedBlocks)
                emptyBlocks.Remove(b);

            // CHAR buildings..
            completedBlocks.Clear();
            int charOfficesCount = 0;
            foreach (Block b in emptyBlocks)
            {
                if ((m_Params.District.Kind == DistrictKind.BUSINESS && charOfficesCount == 0) || m_DiceRoller.RollChance(m_Params.CHARBuildingChance))
                {
                    CHARBuildingType btype = MakeCHARBuilding(map, b);
                    if (btype == CHARBuildingType.OFFICE)
                    {
                        ++charOfficesCount;
                        PopulateCHAROfficeBuilding(map, b);
                    }
                    if (btype != CHARBuildingType.NONE)
                        completedBlocks.Add(b);
                }
            }
            foreach (Block b in completedBlocks)
                emptyBlocks.Remove(b);

            // parks.
            completedBlocks.Clear();
            foreach (Block b in emptyBlocks)
            {
                if (m_DiceRoller.RollChance(m_Params.ParkBuildingChance) &&
                    MakeParkBuilding(map, b))
                    completedBlocks.Add(b);
            }
            foreach (Block b in completedBlocks)
                emptyBlocks.Remove(b);

            // all the rest is housings.
            completedBlocks.Clear();
            foreach (Block b in emptyBlocks)
            {
                MakeHousingBuilding(map, b);
                completedBlocks.Add(b);
            }
            foreach (Block b in completedBlocks)
                emptyBlocks.Remove(b);

            ////////////
            // Decorate
            ////////////
            AddWreckedCarsOutside(map, cityRectangle);
            DecorateOutsideWallsWithPosters(map, cityRectangle, m_Params.PostersChance);
            DecorateOutsideWallsWithTags(map, cityRectangle, m_Params.TagsChance);

            // alpha10
            /////////
            // Music
            /////////
            map.BgMusic = GameMusics.SURFACE;

            ////////
            // Done
            ////////
            return map;
        }
        #endregion
        #region Sewers Map
        public virtual Map GenerateSewersMap(int seed, District district)
        {
            // Create.
            m_DiceRoller = new DiceRoller(seed);
            Map sewers = new Map(seed, "sewers", district.EntryMap.Width, district.EntryMap.Height)
            {
                Lighting = Lighting.DARKNESS
            };
            sewers.AddZone(MakeUniqueZone("sewers", sewers.Rect));
            TileFill(sewers, m_Game.GameTiles.WALL_SEWER);

            ///////////////////////////////////////////////////
            // 1. Make blocks.
            // 2. Make tunnels.
            // 3. Link with surface.
            // 4. Additional jobs.
            // 5. Sewers Maintenance Room & Building(surface).
            // 6. Some rooms.
            // 7. Objects.
            // 8. Items.
            // 9. Tags.
            // alpha10
            // 10. Music.
            ///////////////////////////////////////////////////
            Map surface = district.EntryMap;

            // 1. Make blocks.
            List<Block> blocks = new List<Block>(m_SurfaceBlocks.Count);
            MakeBlocks(sewers, false, ref blocks, new Rectangle(0, 0, sewers.Width, sewers.Height));

            // 2. Make tunnels.
            #region
            // Carve tunnels.
            foreach (Block b in blocks)
            {
                TileRectangle(sewers, m_Game.GameTiles.FLOOR_SEWER_WATER, b.Rectangle);
            }
            // Iron Fences blocking some tunnels.
            foreach (Block b in blocks)
            {
                // chance?
                if (!m_DiceRoller.RollChance(SEWERS_IRON_FENCE_PER_BLOCK_CHANCE))
                    continue;

                // fences on a side.
                int fx1, fy1, fx2, fy2;
                bool goodFencePos = false;
                do
                {
                    // roll side.
                    int sideRoll = m_DiceRoller.Roll(0, 4);
                    switch (sideRoll)
                    {
                        case 0: // north.
                        case 1: // south.
                            fx1 = m_DiceRoller.Roll(b.Rectangle.Left, b.Rectangle.Right - 1);
                            fy1 = (sideRoll == 0 ? b.Rectangle.Top : b.Rectangle.Bottom - 1);

                            fx2 = fx1;
                            fy2 = (sideRoll == 0 ? fy1 - 1 : fy1 + 1);
                            break;
                        case 2: // east.
                        case 3: // west.
                            fx1 = (sideRoll == 2 ? b.Rectangle.Left : b.Rectangle.Right - 1);
                            fy1 = m_DiceRoller.Roll(b.Rectangle.Top, b.Rectangle.Bottom - 1);

                            fx2 = (sideRoll == 2 ? fx1 - 1 : fx1 + 1);
                            fy2 = fy1;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException("unhandled roll");
                    }

                    // never on border.
                    if (sewers.IsOnMapBorder(fx1, fy1) || sewers.IsOnMapBorder(fx2, fy2))
                        continue;

                    // must have walls.
                    if (CountAdjWalls(sewers, fx1, fy1) != 3)
                        continue;
                    if (CountAdjWalls(sewers, fx2, fy2) != 3)
                        continue;

                    // found!
                    goodFencePos = true;
                }
                while (!goodFencePos);

                // add (both of them)
                MapObjectPlace(sewers, fx1, fy1, MakeObjIronFence(GameImages.OBJ_IRON_FENCE));
                MapObjectPlace(sewers, fx2, fy2, MakeObjIronFence(GameImages.OBJ_IRON_FENCE));
            }
            #endregion

            // 3. Link with surface.
            #region
            // loop until we got at least one link.
            int countLinks = 0;
            do
            {
                for (int x = 0; x < sewers.Width; x++)
                    for (int y = 0; y < sewers.Height; y++)
                    {
                        // link? roll chance. 3%
                        bool doLink = m_DiceRoller.RollChance(3);
                        if (!doLink)
                            continue;

                        // both surface and sewer tile must be walkable.
                        Tile tileSewer = sewers.GetTileAt(x, y);
                        if (!tileSewer.Model.IsWalkable)
                            continue;
                        Tile tileSurface = surface.GetTileAt(x, y);
                        if (!tileSurface.Model.IsWalkable)
                            continue;

                        // no blocking object.
                        if (sewers.GetMapObjectAt(x, y) != null)
                            continue;

                        // surface tile must be outside.
                        if (tileSurface.IsInside)
                            continue;
                        // surface tile must be walkway or grass.
                        if (tileSurface.Model != m_Game.GameTiles.FLOOR_WALKWAY && tileSurface.Model != m_Game.GameTiles.FLOOR_GRASS)
                            continue;
                        // surface tile must not be obstructed by an object.
                        if (surface.GetMapObjectAt(x, y) != null)
                            continue;

                        // must not be adjacent to another exit.
                        Point pt = new Point(x, y);
                        if (sewers.HasAnyAdjacentInMap(pt, (p) => sewers.GetExitAt(p) != null))
                            continue;
                        if (surface.HasAnyAdjacentInMap(pt, (p) => surface.GetExitAt(p) != null))
                            continue;

                        // link with ladder and sewer hole.
                        AddExit(sewers, pt, surface, pt, GameImages.DECO_SEWER_LADDER, true);
                        AddExit(surface, pt, sewers, pt, GameImages.DECO_SEWER_HOLE, true);

                        // - one more link.
                        ++countLinks;
                    }
            }
            while (countLinks < 1);
            #endregion

            // 4. Additional jobs.
            #region
            // Mark all the map as inside.
            for (int x = 0; x < sewers.Width; x++)
                for (int y = 0; y < sewers.Height; y++)
                    sewers.GetTileAt(x, y).IsInside = true;
            #endregion

            // 5. Sewers Maintenance Room & Building(surface).
            #region
            // search a suitable surface blocks.
            List<Block> goodBlocks = null;
            foreach (Block b in m_SurfaceBlocks)
            {
                // surface building must be of minimal size.
                if (b.BuildingRect.Width > m_Params.MinBlockSize + 2 || b.BuildingRect.Height > m_Params.MinBlockSize + 2)
                    continue;

                // must not be a special building or have an exit (eg: houses with basements)
                if (IsThereASpecialBuilding(surface, b.InsideRect))
                    continue;

                // we must carve a room in the sewers.
                bool hasRoom = true;
                for (int x = b.Rectangle.Left; x < b.Rectangle.Right && hasRoom; x++)
                    for (int y = b.Rectangle.Top; y < b.Rectangle.Bottom && hasRoom; y++)
                    {
                        if (sewers.GetTileAt(x, y).Model.IsWalkable)
                            hasRoom = false;
                    }
                if (!hasRoom)
                    continue;

                // found one.
                if (goodBlocks == null)
                    goodBlocks = new List<Block>(m_SurfaceBlocks.Count);
                goodBlocks.Add(b);
                break;
            }

            // if found, make maintenance room in sewers and building on surface.
            if (goodBlocks != null)
            {
                // pick one at random.
                Block surfaceBlock = goodBlocks[m_DiceRoller.Roll(0, goodBlocks.Count)];

                // clear surface building.
                ClearRectangle(surface, surfaceBlock.BuildingRect);
                TileFill(surface, m_Game.GameTiles.FLOOR_CONCRETE, surfaceBlock.BuildingRect);
                m_SurfaceBlocks.Remove(surfaceBlock);

                // make maintenance building on the surface & room in the sewers.
                Block newSurfaceBlock = new Block(surfaceBlock.Rectangle);
                Point ladderHolePos = new Point(newSurfaceBlock.BuildingRect.Left + newSurfaceBlock.BuildingRect.Width / 2, newSurfaceBlock.BuildingRect.Top + newSurfaceBlock.BuildingRect.Height / 2);
                MakeSewersMaintenanceBuilding(surface, true, newSurfaceBlock, sewers, ladderHolePos);
                Block sewersRoom = new Block(surfaceBlock.Rectangle);
                MakeSewersMaintenanceBuilding(sewers, false, sewersRoom, surface, ladderHolePos);
            }
            #endregion

            // 6. Some rooms.
            #region
            foreach (Block b in blocks)
            {
                // chance?
                if (!m_DiceRoller.RollChance(SEWERS_ROOM_CHANCE))
                    continue;

                // must be all walls = not already assigned as a room.
                if (!CheckForEachTile(sewers, b.BuildingRect, (pt) => !sewers.GetTileAt(pt).Model.IsWalkable))
                    continue;

                // carve a room.
                TileFill(sewers, m_Game.GameTiles.FLOOR_CONCRETE, b.InsideRect);

                // 4 entries.
                sewers.SetTileModelAt(b.BuildingRect.Left + b.BuildingRect.Width / 2, b.BuildingRect.Top, m_Game.GameTiles.FLOOR_CONCRETE);
                sewers.SetTileModelAt(b.BuildingRect.Left + b.BuildingRect.Width / 2, b.BuildingRect.Bottom - 1, m_Game.GameTiles.FLOOR_CONCRETE);
                sewers.SetTileModelAt(b.BuildingRect.Left, b.BuildingRect.Top + b.BuildingRect.Height / 2, m_Game.GameTiles.FLOOR_CONCRETE);
                sewers.SetTileModelAt(b.BuildingRect.Right - 1, b.BuildingRect.Top + b.BuildingRect.Height / 2, m_Game.GameTiles.FLOOR_CONCRETE);

                // zone.
                sewers.AddZone(MakeUniqueZone("room", b.InsideRect));
            }
            #endregion

            // 7. Objects.
            #region
            // junk.
            MapObjectFill(sewers, new Rectangle(0, 0, sewers.Width, sewers.Height),
                (pt) =>
                {
                    if (!m_DiceRoller.RollChance(SEWERS_JUNK_CHANCE))
                        return null;
                    if (!sewers.IsWalkable(pt.X, pt.Y))
                        return null;

                    return MakeObjJunk(GameImages.OBJ_JUNK);
                });
            #endregion

            // 8. Items.
            #region
            for (int x = 0; x < sewers.Width; x++)
                for (int y = 0; y < sewers.Height; y++)
                {
                    if (!m_DiceRoller.RollChance(SEWERS_ITEM_CHANCE))
                        continue;
                    if (!sewers.IsWalkable(x, y))
                        continue;

                    // drop item.
                    Item it;
                    int roll = m_DiceRoller.Roll(0, 3);
                    switch (roll)
                    {
                        case 0: it = MakeItemBigFlashlight(); break;
                        case 1: it = MakeItemCrowbar(); break;
                        case 2: it = MakeItemSprayPaint(); break;
                        default:
                            throw new ArgumentOutOfRangeException("unhandled roll");
                    }
                    sewers.DropItemAt(it, x, y);
                }
            #endregion

            // 9. Tags.
            #region
            for (int x = 0; x < sewers.Width; x++)
                for (int y = 0; y < sewers.Height; y++)
                {
                    if (m_DiceRoller.RollChance(SEWERS_TAG_CHANCE))
                    {
                        // must be a wall with walkables around.
                        Tile t = sewers.GetTileAt(x, y);
                        if (t.Model.IsWalkable)
                            continue;
                        if (CountAdjWalkables(sewers, x, y) < 2)
                            continue;

                        // tag.
                        t.AddDecoration(TAGS[m_DiceRoller.Roll(0, TAGS.Length)]);
                    }
                }
            #endregion

            // alpha10
            // 10. Music.
            sewers.BgMusic = GameMusics.SEWERS;

            // Done.
            return sewers;
        }
        #endregion
        #region Subway Map
        public virtual Map GenerateSubwayMap(int seed, District district)
        {
            // Create.
            m_DiceRoller = new DiceRoller(seed);
            Map subway = new Map(seed, "subway", district.EntryMap.Width, district.EntryMap.Height)
            {
                Lighting = Lighting.DARKNESS
            };
            TileFill(subway, m_Game.GameTiles.WALL_BRICK);

            /////////////////////////////////////
            // 1. Trace rail line.
            // 2. Make station linked to surface?
            // 3. Small tools room.
            // 4. Tags & Posters almost everywhere.
            // 5. Additional jobs.
            // alpha10
            // 6. Music
            /////////////////////////////////////
            Map surface = district.EntryMap;

            // 1. Trace rail line.
            #region
            int railStartX = 0;
            int railEndX = subway.Width - 1;
            int railY = subway.Width / 2 - 1;
            int railSize = 4;

            for (int x = railStartX; x <= railEndX; x++)
            {
                for (int y = railY; y < railY + railSize; y++)
                    subway.SetTileModelAt(x, y, m_Game.GameTiles.RAIL_EW);
            }
            subway.AddZone(MakeUniqueZone(RogueGame.NAME_SUBWAY_RAILS, new Rectangle(railStartX, railY, railEndX - railStartX + 1, railSize)));
            #endregion

            // 2. Make station linked to surface.
            #region
            // search a suitable surface blocks.
            List<Block> goodBlocks = null;
            foreach (Block b in m_SurfaceBlocks)
            {
                // surface building must be of minimal size.
                if (b.BuildingRect.Width > m_Params.MinBlockSize + 2 || b.BuildingRect.Height > m_Params.MinBlockSize + 2)
                    continue;

                // must not be a special building or have an exit (eg: houses with basements)
                if (IsThereASpecialBuilding(surface, b.InsideRect))
                    continue;

                // we must carve a room in the subway and must not be to close to rails.
                bool hasRoom = true;
                int minDistToRails = 8;
                for (int x = b.Rectangle.Left - minDistToRails; x < b.Rectangle.Right + minDistToRails && hasRoom; x++)
                    for (int y = b.Rectangle.Top - minDistToRails; y < b.Rectangle.Bottom + minDistToRails && hasRoom; y++)
                    {
                        if (!subway.IsInBounds(x, y))
                            continue;
                        if (subway.GetTileAt(x, y).Model.IsWalkable)
                            hasRoom = false;
                    }
                if (!hasRoom)
                    continue;

                // found one.
                if (goodBlocks == null)
                    goodBlocks = new List<Block>(m_SurfaceBlocks.Count);
                goodBlocks.Add(b);
                break;
            }

            // if found, make station room and building.
            if (goodBlocks != null)
            {
                // pick one at random.
                Block surfaceBlock = goodBlocks[m_DiceRoller.Roll(0, goodBlocks.Count)];

                // clear surface building.
                ClearRectangle(surface, surfaceBlock.BuildingRect);
                TileFill(surface, m_Game.GameTiles.FLOOR_CONCRETE, surfaceBlock.BuildingRect);
                m_SurfaceBlocks.Remove(surfaceBlock);

                // make station building on the surface & room in the subway.
                Block newSurfaceBlock = new Block(surfaceBlock.Rectangle);
                Point stairsPos = new Point(newSurfaceBlock.BuildingRect.Left + newSurfaceBlock.BuildingRect.Width / 2, newSurfaceBlock.InsideRect.Top);
                MakeSubwayStationBuilding(surface, true, newSurfaceBlock, subway, stairsPos);
                Block subwayRoom = new Block(surfaceBlock.Rectangle);
                MakeSubwayStationBuilding(subway, false, subwayRoom, surface, stairsPos);
            }
            #endregion

            // 3.  Small tools room.
            #region
            const int toolsRoomWidth = 5;
            const int toolsRoomHeight = 5;
            Direction toolsRoomDir = m_DiceRoller.RollChance(50) ? Direction.N : Direction.S;
            Rectangle toolsRoom = Rectangle.Empty;
            bool foundToolsRoom = false;
            int toolsRoomAttempt = 0;
            do
            {
                int x = m_DiceRoller.Roll(10, subway.Width - 10);
                int y = (toolsRoomDir == Direction.N ? railY - 1 : railY + railSize);

                if (!subway.GetTileAt(x, y).Model.IsWalkable)
                {
                    // make room rectangle.
                    if (toolsRoomDir == Direction.N)
                        toolsRoom = new Rectangle(x, y - toolsRoomHeight + 1, toolsRoomWidth, toolsRoomHeight);
                    else
                        toolsRoom = new Rectangle(x, y, toolsRoomWidth, toolsRoomHeight);
                    // check room rect is all walls (do not overlap with platform or other rooms)
                    foundToolsRoom = CheckForEachTile(subway, toolsRoom, (pt) => !subway.GetTileAt(pt).Model.IsWalkable);
                }
                ++toolsRoomAttempt;
            }
            while (toolsRoomAttempt < subway.Width * subway.Height && !foundToolsRoom);

            if (foundToolsRoom)
            {
                // room.
                TileFill(subway, m_Game.GameTiles.FLOOR_CONCRETE, toolsRoom);
                TileRectangle(subway, m_Game.GameTiles.WALL_BRICK, toolsRoom);
                PlaceDoor(subway, toolsRoom.Left + toolsRoomWidth / 2, (toolsRoomDir == Direction.N ? toolsRoom.Bottom - 1 : toolsRoom.Top), m_Game.GameTiles.FLOOR_CONCRETE, MakeObjIronDoor());
                subway.AddZone(MakeUniqueZone("tools room", toolsRoom));

                // shelves on walls with construction items.
                DoForEachTile(subway, toolsRoom,
                    (pt) =>
                    {
                        if (!subway.IsWalkable(pt.X, pt.Y))
                            return;
                        if (CountAdjWalls(subway, pt.X, pt.Y) == 0 || CountAdjDoors(subway, pt.X, pt.Y) > 0)
                            return;

                        subway.PlaceMapObjectAt(MakeObjShelf(GameImages.OBJ_SHOP_SHELF), pt);
                        subway.DropItemAt(MakeShopConstructionItem(), pt);
                    });
            }
            #endregion

            // 4. Tags & Posters almost everywhere.
            #region
            for (int x = 0; x < subway.Width; x++)
                for (int y = 0; y < subway.Height; y++)
                {
                    if (m_DiceRoller.RollChance(SUBWAY_TAGS_POSTERS_CHANCE))
                    {
                        // must be a wall with walkables around.
                        Tile t = subway.GetTileAt(x, y);
                        if (t.Model.IsWalkable)
                            continue;
                        if (CountAdjWalkables(subway, x, y) < 2)
                            continue;

                        // poster?
                        if (m_DiceRoller.RollChance(50))
                            t.AddDecoration(POSTERS[m_DiceRoller.Roll(0, POSTERS.Length)]);

                        // tag?
                        if (m_DiceRoller.RollChance(50))
                            t.AddDecoration(TAGS[m_DiceRoller.Roll(0, TAGS.Length)]);
                    }
                }
            #endregion


            // 5. Additional jobs.
            // Mark all the map as inside.
            for (int x = 0; x < subway.Width; x++)
                for (int y = 0; y < subway.Height; y++)
                    subway.GetTileAt(x, y).IsInside = true;

            // alpha10
            // 6. Music.
            subway.BgMusic = GameMusics.SUBWAY;

            // Done.
            return subway;
        }
        #endregion
        #region Blocks generation

        void QuadSplit(Rectangle rect, int minWidth, int minHeight, out int splitX, out int splitY, out Rectangle topLeft, out Rectangle topRight, out Rectangle bottomLeft, out Rectangle bottomRight)
        {
            // Choose a random split point.
            int leftWidthSplit = m_DiceRoller.Roll(rect.Width / 3, (2 * rect.Width) / 3);
            int topHeightSplit = m_DiceRoller.Roll(rect.Height / 3, (2 * rect.Height) / 3);

            // Ensure splitting does not produce rects below minima.
            if (leftWidthSplit < minWidth)
                leftWidthSplit = minWidth;
            if (topHeightSplit < minHeight)
                topHeightSplit = minHeight;

            int rightWidthSplit = rect.Width - leftWidthSplit;
            int bottomHeightSplit = rect.Height - topHeightSplit;

            bool doSplitX, doSplitY;
            doSplitX = doSplitY = true;

            if (rightWidthSplit < minWidth)
            {
                leftWidthSplit = rect.Width;
                rightWidthSplit = 0;
                doSplitX = false;
            }
            if (bottomHeightSplit < minHeight)
            {
                topHeightSplit = rect.Height;
                bottomHeightSplit = 0;
                doSplitY = false;
            }

            // Split point.
            splitX = rect.Left + leftWidthSplit;
            splitY = rect.Top + topHeightSplit;

            // Make the quads.
            topLeft = new Rectangle(rect.Left, rect.Top, leftWidthSplit, topHeightSplit);

            if (doSplitX)
                topRight = new Rectangle(splitX, rect.Top, rightWidthSplit, topHeightSplit);
            else
                topRight = Rectangle.Empty;

            if (doSplitY)
                bottomLeft = new Rectangle(rect.Left, splitY, leftWidthSplit, bottomHeightSplit);
            else
                bottomLeft = Rectangle.Empty;

            if (doSplitX && doSplitY)
                bottomRight = new Rectangle(splitX, splitY, rightWidthSplit, bottomHeightSplit);
            else
                bottomRight = Rectangle.Empty;
        }

        void MakeBlocks(Map map, bool makeRoads, ref List<Block> list, Rectangle rect)
        {
            const int ring = 1; // dont change, keep to 1 (0=no roads, >1 = out of map)

            ////////////
            // 1. Split
            ////////////
            int splitX, splitY;
            Rectangle topLeft, topRight, bottomLeft, bottomRight;
            // +N to account for the road ring.
            QuadSplit(rect, m_Params.MinBlockSize + ring, m_Params.MinBlockSize + ring, out splitX, out splitY, out topLeft, out topRight, out bottomLeft, out bottomRight);

            ///////////////////
            // 2. Termination?
            ///////////////////
            if (topRight.IsEmpty && bottomLeft.IsEmpty && bottomRight.IsEmpty)
            {
                // Make road ring?
                if (makeRoads)
                {
                    MakeRoad(map, m_Game.GameTiles[GameTiles.IDs.ROAD_ASPHALT_EW], new Rectangle(rect.Left, rect.Top, rect.Width, ring));        // north side
                    MakeRoad(map, m_Game.GameTiles[GameTiles.IDs.ROAD_ASPHALT_EW], new Rectangle(rect.Left, rect.Bottom - 1, rect.Width, ring)); // south side
                    MakeRoad(map, m_Game.GameTiles[GameTiles.IDs.ROAD_ASPHALT_NS], new Rectangle(rect.Left, rect.Top, ring, rect.Height));       // west side
                    MakeRoad(map, m_Game.GameTiles[GameTiles.IDs.ROAD_ASPHALT_NS], new Rectangle(rect.Right - 1, rect.Top, ring, rect.Height));       // east side

                    // Adjust rect.
                    topLeft.Width -= 2 * ring;
                    topLeft.Height -= 2 * ring;
                    topLeft.Offset(ring, ring);
                }

                // Add block.
                list.Add(new Block(topLeft));
                return;
            }

            //////////////
            // 3. Recurse
            //////////////
            // always top left.
            MakeBlocks(map, makeRoads, ref list, topLeft);
            // then recurse in non empty quads.
            if (!topRight.IsEmpty)
            {
                MakeBlocks(map, makeRoads, ref list, topRight);
            }
            if (!bottomLeft.IsEmpty)
            {
                MakeBlocks(map, makeRoads, ref list, bottomLeft);
            }
            if (!bottomRight.IsEmpty)
            {
                MakeBlocks(map, makeRoads, ref list, bottomRight);
            }
        }

        protected virtual void MakeRoad(Map map, TileModel roadModel, Rectangle rect)
        {
            base.TileFill(map, roadModel, rect,
                (tile, prevmodel, x, y) =>
                {
                    // don't overwrite roads!
                    if (m_Game.GameTiles.IsRoadModel(prevmodel))
                        map.SetTileModelAt(x, y, prevmodel);
                });
            map.AddZone(base.MakeUniqueZone("road", rect));
        }
        #endregion
        #region Door/Window placement
        protected virtual void PlaceDoor(Map map, int x, int y, TileModel floor, DoorWindow door)
        {
            map.SetTileModelAt(x, y, floor);
            base.MapObjectPlace(map, x, y, door);
        }

        protected virtual void PlaceDoorIfNoObject(Map map, int x, int y, TileModel floor, DoorWindow door)
        {
            if (map.GetMapObjectAt(x, y) != null)
                return;
            PlaceDoor(map, x, y, floor, door);
        }

        protected virtual bool PlaceDoorIfAccessible(Map map, int x, int y, TileModel floor, int minAccessibility, DoorWindow door)
        {
            int countWalkable = 0;

            Point p = new Point(x, y);
            foreach (Direction d in Direction.COMPASS)
            {
                Point next = p + d;
                if (map.IsWalkable(next.X, next.Y))
                    ++countWalkable;
            }

            if (countWalkable >= minAccessibility)
            {
                PlaceDoorIfNoObject(map, x, y, floor, door);
                return true;
            }
            else
                return false;
        }

        protected virtual bool PlaceDoorIfAccessibleAndNotAdjacent(Map map, int x, int y, TileModel floor, int minAccessibility, DoorWindow door)
        {
            int countWalkable = 0;

            Point p = new Point(x, y);
            foreach (Direction d in Direction.COMPASS)
            {
                Point next = p + d;
                if (map.IsWalkable(next.X, next.Y))
                    ++countWalkable;
                if (map.GetMapObjectAt(next.X, next.Y) is DoorWindow)
                    return false;
            }

            if (countWalkable >= minAccessibility)
            {
                PlaceDoorIfNoObject(map, x, y, floor, door);
                return true;
            }
            else
                return false;
        }
        #endregion
        #region Cars
        protected virtual void AddWreckedCarsOutside(Map map, Rectangle rect)
        {
            //////////////////////////////////////
            // Add random cars (+ on fire effect)
            //////////////////////////////////////
            base.MapObjectFill(map, rect,
                (pt) =>
                {
                    if (m_DiceRoller.RollChance(m_Params.WreckedCarChance))
                    {
                        Tile tile = map.GetTileAt(pt.X, pt.Y);
                        if (!tile.IsInside && tile.Model.IsWalkable && tile.Model != m_Game.GameTiles.FLOOR_GRASS)
                        {
                            MapObject car = base.MakeObjWreckedCar(m_DiceRoller);
                            if (m_DiceRoller.RollChance(50))
                            {
                                m_Game.ApplyOnFire(car);
                            }
                            return car;
                        }
                    }
                    return null;
                });
        }
        #endregion
    }
}
