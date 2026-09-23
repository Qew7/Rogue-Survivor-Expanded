using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.IO;

using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.Generators;

using Message = djack.RogueSurvivor.Data.Message;
using djack.RogueSurvivor.Engine.Tasks;
using ItemRating = djack.RogueSurvivor.Gameplay.AI.BaseAI.ItemRating;
using TradeRating = djack.RogueSurvivor.Gameplay.AI.BaseAI.TradeRating;

namespace djack.RogueSurvivor.Engine
{
    partial class RogueGame
    {
        #region Generating world
        void GenerateWorld(bool isVerbose, int size)
        {
            // say so.
            if (isVerbose)
            {
                m_UI.UI_Clear(Color.Black);
                m_UI.UI_DrawStringBold(Color.White, "Generating game world...", 0, 0);
                m_UI.UI_Repaint();
            }

            //////////////////////
            // Create blank world
            //////////////////////
            if (isVerbose)
            {
                m_UI.UI_Clear(Color.Black);
                m_UI.UI_DrawStringBold(Color.White, "Creating empty world...", 0, 0);
                m_UI.UI_Repaint();
            }
            m_Session.World = new World(size);
            World world = m_Session.World;

            ////////////////////////
            // Roll initial weather
            ////////////////////////
            world.Weather = (Weather)m_Rules.Roll((int)Weather._FIRST, (int)Weather._COUNT);
            world.NextWeatherCheckTurn = m_Rules.Roll(WEATHER_MIN_DURATION, WEATHER_MAX_DURATION);  // alpha10

            //////////////////////////////////////////////
            // Roll locations of special buildings.
            // Only ONE special building max per district.
            //////////////////////////////////////////////
            List<Point> noSpecialDistricts = new List<Point>();
            for (int x = 0; x < world.Size; x++)
                for (int y = 0; y < world.Size; y++)
                    noSpecialDistricts.Add(new Point(x, y));

            Point policeStationDistrictPos = noSpecialDistricts[m_Rules.Roll(0, noSpecialDistricts.Count)];
            noSpecialDistricts.Remove(policeStationDistrictPos);

            Point hospitalDistrictPos = noSpecialDistricts[m_Rules.Roll(0, noSpecialDistricts.Count)];
            noSpecialDistricts.Remove(hospitalDistrictPos);

            /////////////////////////
            // Create districts maps
            /////////////////////////
            // Surface, Sewers and Subways.
            #region
            for (int x = 0; x < world.Size; x++)
            {
                for (int y = 0; y < world.Size; y++)
                {
                    if (isVerbose)
                    {
                        m_UI.UI_Clear(Color.Black);
                        m_UI.UI_DrawStringBold(Color.White, String.Format("Creating District@{0}...", World.CoordToString(x, y)), 0, 0);
                        m_UI.UI_Repaint();
                    }

                    // create the district.
                    District district = new District(new Point(x, y), GenerateDistrictKind(world, x, y));
                    world[x, y] = district;

                    // create the entry map.
                    district.EntryMap = GenerateDistrictEntryMap(world, district, policeStationDistrictPos, hospitalDistrictPos);
                    district.Name = district.EntryMap.Name;

                    // create other maps.
                    // - sewers
                    Map sewers = GenerateDistrictSewersMap(district);
                    district.SewersMap = sewers;
                    // - subway (only in the middle district line)
                    if (y == world.Size / 2)
                    {
                        Map subwayMap = GenerateDistrictSubwayMap(district);
                        district.SubwayMap = subwayMap;
                    }
                }
            }
            #endregion

            ///////////////
            // Unique Maps
            ///////////////
            if (isVerbose)
            {
                m_UI.UI_Clear(Color.Black);
                m_UI.UI_DrawStringBold(Color.White, "Generating unique maps...", 0, 0);
                m_UI.UI_Repaint();
            }
            m_Session.UniqueMaps.CHARUndergroundFacility = CreateUniqueMap_CHARUndegroundFacility(world);

            /////////////////
            // Unique Actors
            /////////////////
            if (isVerbose)
            {
                m_UI.UI_Clear(Color.Black);
                m_UI.UI_DrawStringBold(Color.White, "Generating unique actors...", 0, 0);
                m_UI.UI_Repaint();
            }
            // "Sewers Thing" - in one of the sewers
            m_Session.UniqueActors.TheSewersThing = SpawnUniqueSewersThing(world);
            // Unique survivors NPCs.
            m_Session.UniqueActors.BigBear = CreateUniqueBigBear(world);
            m_Session.UniqueActors.FamuFataru = CreateUniqueFamuFataru(world);
            m_Session.UniqueActors.Santaman = CreateUniqueSantaman(world);
            m_Session.UniqueActors.Roguedjack = CreateUniqueRoguedjack(world);
            m_Session.UniqueActors.Duckman = CreateUniqueDuckman(world);
            m_Session.UniqueActors.HansVonHanz = CreateUniqueHansVonHanz(world);

            // alpha10 Make all uniques npcs invincible until spotted
            foreach (UniqueActor uniqueActor in m_Session.UniqueActors.ToArray())
                uniqueActor.TheActor.IsInvincible = true;

            /////////////////
            // Unique Items
            /////////////////
            // "Subway Worker Badge" - somewhere on the subway tracks...
            m_Session.UniqueItems.TheSubwayWorkerBadge = SpawnUniqueSubwayWorkerBadge(world);

            //////////////////
            // Link districts
            //////////////////
            #region
            for (int x = 0; x < world.Size; x++)
            {
                for (int y = 0; y < world.Size; y++)
                {
                    if (isVerbose)
                    {
                        m_UI.UI_Clear(Color.Black);
                        m_UI.UI_DrawStringBold(Color.White, String.Format("Linking District@{0}...", World.CoordToString(x, y)), 0, 0);
                        m_UI.UI_Repaint();
                    }

                    #region Entry maps (surface)
                    // add exits (from and to).
                    Map map = world[x, y].EntryMap;

                    if (y > 0)
                    {
                        // north.
                        Map toMap = world[x, y - 1].EntryMap;
                        for (int fromX = 0; fromX < map.Width; fromX++)
                        {
                            int toX = fromX;
                            if (toX >= toMap.Width)
                                continue;
                            // link?
                            if (m_Rules.RollChance(DISTRICT_EXIT_CHANCE_PER_TILE))
                            {
                                Point ptMapFrom = new Point(fromX, -1);
                                Point ptMapTo = new Point(fromX, toMap.Height - 1);
                                Point ptFromMapFrom = new Point(fromX, toMap.Height);
                                Point ptFromMapTo = new Point(fromX, 0);
                                if (CheckIfExitIsGood(map, ptMapFrom, toMap, ptMapTo) &&
                                    CheckIfExitIsGood(toMap, ptFromMapFrom, map, ptFromMapTo))
                                {
                                    GenerateExit(map, ptMapFrom, toMap, ptMapTo);
                                    GenerateExit(toMap, ptFromMapFrom, map, ptFromMapTo);
                                }
                            }
                        }
                    }
                    if (x > 0)
                    {
                        // west.
                        Map toMap = world[x - 1, y].EntryMap;
                        for (int fromY = 0; fromY < map.Height; fromY++)
                        {
                            int toY = fromY;
                            if (toY >= toMap.Height)
                                continue;
                            // link?
                            if (m_Rules.RollChance(DISTRICT_EXIT_CHANCE_PER_TILE))
                            {
                                Point ptMapFrom = new Point(-1, fromY);
                                Point ptMapTo = new Point(toMap.Width - 1, fromY);
                                Point ptFromMapFrom = new Point(toMap.Width, fromY);
                                Point ptFromMapTo = new Point(0, fromY);
                                if (CheckIfExitIsGood(map, ptMapFrom, toMap, ptMapTo) &&
                                    CheckIfExitIsGood(toMap, ptFromMapFrom, map, ptFromMapTo))
                                {
                                    GenerateExit(map, ptMapFrom, toMap, ptMapTo);
                                    GenerateExit(toMap, ptFromMapFrom, map, ptFromMapTo);
                                }
                            }
                        }
                    }
                    #endregion

                    #region Sewers
                    map = world[x, y].SewersMap;
                    if (y > 0)
                    {
                        // north.
                        Map toMap = world[x, y - 1].SewersMap;
                        for (int fromX = 0; fromX < map.Width; fromX++)
                        {
                            int toX = fromX;
                            if (toX >= toMap.Width)
                                continue;
                            Point ptMapFrom = new Point(fromX, -1);
                            Point ptMapTo = new Point(fromX, toMap.Height - 1);
                            Point ptFromMapFrom = new Point(fromX, toMap.Height);
                            Point ptFromMapTo = new Point(fromX, 0);
                            GenerateExit(map, ptMapFrom, toMap, ptMapTo);
                            GenerateExit(toMap, ptFromMapFrom, map, ptFromMapTo);
                        }
                    }
                    if (x > 0)
                    {
                        // west.
                        Map toMap = world[x - 1, y].SewersMap;
                        for (int fromY = 0; fromY < map.Height; fromY++)
                        {
                            int toY = fromY;
                            if (toY >= toMap.Height)
                                continue;

                            Point ptMapFrom = new Point(-1, fromY);
                            Point ptMapTo = new Point(toMap.Width - 1, fromY);
                            Point ptFromMapFrom = new Point(toMap.Width, fromY);
                            Point ptFromMapTo = new Point(0, fromY);

                            GenerateExit(map, ptMapFrom, toMap, ptMapTo);
                            GenerateExit(toMap, ptFromMapFrom, map, ptFromMapTo);

                        }
                    }
                    #endregion

                    #region Subways
                    map = world[x, y].SubwayMap;
                    if (map != null)
                    {
                        if (x > 0)
                        {
                            // west.
                            Map toMap = world[x - 1, y].SubwayMap;
                            for (int fromY = 0; fromY < map.Height; fromY++)
                            {
                                int toY = fromY;
                                if (toY >= toMap.Height)
                                    continue;

                                Point ptMapFrom = new Point(-1, fromY);
                                Point ptMapTo = new Point(toMap.Width - 1, fromY);
                                Point ptFromMapFrom = new Point(toMap.Width, fromY);
                                Point ptFromMapTo = new Point(0, fromY);

                                if (!map.IsWalkable(map.Width - 1, fromY))
                                    continue;
                                if (!toMap.IsWalkable(0, fromY))
                                    continue;

                                GenerateExit(map, ptMapFrom, toMap, ptMapTo);
                                GenerateExit(toMap, ptFromMapFrom, map, ptFromMapTo);

                            }
                        }
                    }
                    #endregion
                }
            }
            #endregion

            //////////////////////////////////////////
            // Easter egg: "roguedjack was here" tag.
            //////////////////////////////////////////
            #region
            Map easterEggTagMap = world[0, 0].SewersMap;
            easterEggTagMap.RemoveMapObjectAt(1, 1);
            easterEggTagMap.GetTileAt(1, 1).RemoveAllDecorations();
            easterEggTagMap.GetTileAt(1, 1).AddDecoration(GameImages.DECO_ROGUEDJACK_TAG);
            #endregion

            //////////////////////////////
            // Spawn player on center map
            //////////////////////////////
            if (isVerbose)
            {
                m_UI.UI_Clear(Color.Black);
                m_UI.UI_DrawStringBold(Color.White, "Spawning player...", 0, 0);
                m_UI.UI_Repaint();
            }
            int gridCenter = world.Size / 2;

            Map startMap = world[gridCenter, gridCenter].EntryMap;
            GeneratePlayerOnMap(startMap, m_TownGenerator);
            SetCurrentMap(startMap);
            RefreshPlayer();
            UpdatePlayerFOV(m_Player);  // to make sure we get notified of actors acting before us in turn 0.
#if DEBUG
            AddDevCheatItems();
            AddDevCheatSkills();
            AddDevMiscStuff();
#endif

            ////////////////////////
            // Reveal starting map?
            ////////////////////////
            #region
            if (s_Options.RevealStartingDistrict)
            {
                List<Zone> startZones = startMap.GetZonesAt(m_Player.Location.Position.X, m_Player.Location.Position.Y);
                if (startZones != null)
                {
                    Zone startZone = startZones[0];
                    for (int x = 0; x < startMap.Width; x++)
                        for (int y = 0; y < startMap.Height; y++)
                        {
                            bool revealThisTile = false;

                            // reveal if:
                            // - starting zone (house).
                            // - outside.

                            // - starting zone (house).
                            List<Zone> zones = startMap.GetZonesAt(x, y);
                            if (zones != null && zones[0] == startZone)
                                revealThisTile = true;
                            else if (!startMap.GetTileAt(x, y).IsInside)
                                revealThisTile = true;

                            // reveal?
                            if (revealThisTile)
                                startMap.GetTileAt(x, y).IsVisited = true;
                        }
                }
            }
            #endregion

            /////////
            // Done.
            /////////
            if (isVerbose)
            {
                m_UI.UI_Clear(Color.Black);
                m_UI.UI_DrawStringBold(Color.White, "Generating game world... done!", 0, 0);
                m_UI.UI_Repaint();
            }
        }

        bool CheckIfExitIsGood(Map fromMap, Point from, Map toMap, Point to)
        {
            // Don't if tile not walkable or map object there.
            if (!toMap.GetTileAt(to.X, to.Y).Model.IsWalkable)
                return false;
            if (toMap.GetMapObjectAt(to.X, to.Y) != null)
                return false;

            // good spot.
            return true;
        }

        void GenerateExit(Map fromMap, Point from, Map toMap, Point to)
        {
            // add exit.
            fromMap.SetExitAt(from, new Exit(toMap, to));
        }

        #region Uniques
        UniqueActor SpawnUniqueSewersThing(World world)
        {
            ///////////////////////////////////////////////////////
            // 1. Pick a random sewers map.
            // 2. Create Sewers Thing.
            // 3. Spawn in sewers map.
            // 4. Add warning board in maintenance rooms (if any).
            ///////////////////////////////////////////////////////

            // 1. Pick a random sewers map.
            Map map = world[m_Rules.Roll(0, world.Size), m_Rules.Roll(0, world.Size)].SewersMap;

            // 2. Create Sewers Thing.
            ActorModel model = GameActors.SewersThing;
            Actor actor = model.CreateNamed(GameFactions.TheUndeads, "The Sewers Thing", false, 0);

            // 3. Spawn in sewers map.
            DiceRoller roller = new DiceRoller(map.Seed);
            bool spawned = m_TownGenerator.ActorPlace(roller, 10000, map, actor);
            if (!spawned)
                throw new InvalidOperationException("could not spawn unique The Sewers Thing");

            // 4. Add warning board in maintenance rooms (if any).
            Zone maintenanceZone = map.GetZoneByPartialName(NAME_SEWERS_MAINTENANCE);
            if (maintenanceZone != null)
            {
                m_TownGenerator.MapObjectPlaceInGoodPosition(map, maintenanceZone.Bounds,
                    (pt) => map.IsWalkable(pt.X, pt.Y) && map.GetActorAt(pt) == null && map.GetItemsAt(pt) == null,
                    roller,
                    (pt) => m_TownGenerator.MakeObjBoard(GameImages.OBJ_BOARD,
                        new string[] { "TO SEWER WORKERS :",
                                       "- It lives here.",
                                       "- Do not disturb.",
                                       "- Approach with caution.",
                                       "- Watch your back.",
                                       "- In case of emergency, take refuge here.",
                                       "- Do not let other people interact with it!"}));
            }

            // done.
            return new UniqueActor() { TheActor = actor, IsSpawned = true };
        }

        UniqueActor CreateUniqueBigBear(World world)
        {
            #region
            ActorModel model = GameActors.MaleCivilian;
            Actor actor = model.CreateNamed(GameFactions.TheCivilians, "Big Bear", false, 0);
            actor.IsUnique = true;
            // actor.Controller = new CivilianAI();// alpha10.1 defined by model like other actors

            actor.Doll.AddDecoration(DollPart.SKIN, GameImages.ACTOR_BIG_BEAR);

            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HAULER);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HAULER);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HAULER);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HARDY);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HARDY);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HARDY);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HARDY);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HARDY);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.STRONG);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.STRONG);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.STRONG);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.STRONG);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.STRONG);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.TOUGH);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.TOUGH);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.TOUGH);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.TOUGH);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.TOUGH);

            Item bat = new ItemMeleeWeapon(GameItems.UNIQUE_BIGBEAR_BAT) { IsUnique = true };
            actor.Inventory.AddAll(bat);
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            #endregion

            // done.
            return new UniqueActor()
            {
                TheActor = actor,
                IsSpawned = false,
                IsWithRefugees = true,
                EventMessage = "You hear an angry man shouting 'FOOLS!'",
                EventThemeMusic = GameMusics.BIGBEAR_THEME_SONG
            };
        }

        UniqueActor CreateUniqueFamuFataru(World world)
        {

            #region
            ActorModel model = GameActors.FemaleCivilian;
            Actor actor = model.CreateNamed(GameFactions.TheCivilians, "Famu Fataru", false, 0);
            actor.IsUnique = true;
            // actor.Controller = new CivilianAI(); // alpha10.1 defined by model like other actors

            actor.Doll.AddDecoration(DollPart.SKIN, GameImages.ACTOR_FAMU_FATARU);

            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HAULER);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HAULER);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HAULER);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HARDY);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HARDY);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HARDY);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HARDY);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HARDY);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.AGILE);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.AGILE);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.AGILE);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.AGILE);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.AGILE);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HIGH_STAMINA);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HIGH_STAMINA);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HIGH_STAMINA);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HIGH_STAMINA);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HIGH_STAMINA);

            Item katana = new ItemMeleeWeapon(GameItems.UNIQUE_FAMU_FATARU_KATANA) { IsUnique = true };
            actor.Inventory.AddAll(katana);
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            #endregion

            // done.
            return new UniqueActor()
            {
                TheActor = actor,
                IsSpawned = false,
                IsWithRefugees = true,
                EventMessage = "You hear a woman laughing.",
                EventThemeMusic = GameMusics.FAMU_FATARU_THEME_SONG
            };
        }

        UniqueActor CreateUniqueSantaman(World world)
        {
            #region
            ActorModel model = GameActors.MaleCivilian;
            Actor actor = model.CreateNamed(GameFactions.TheCivilians, "Santaman", false, 0);
            actor.IsUnique = true;
            // actor.Controller = new CivilianAI(); // alpha10.1 defined by model like other actors

            actor.Doll.AddDecoration(DollPart.SKIN, GameImages.ACTOR_SANTAMAN);

            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HAULER);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HAULER);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HAULER);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HARDY);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HARDY);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HARDY);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HARDY);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HARDY);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.AWAKE);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.AWAKE);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.AWAKE);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.AWAKE);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.AWAKE);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.FIREARMS);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.FIREARMS);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.FIREARMS);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.FIREARMS);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.FIREARMS);

            Item shotty = new ItemRangedWeapon(GameItems.UNIQUE_SANTAMAN_SHOTGUN) { IsUnique = true };
            actor.Inventory.AddAll(shotty);
            actor.Inventory.AddAll(m_TownGenerator.MakeItemShotgunAmmo());
            actor.Inventory.AddAll(m_TownGenerator.MakeItemShotgunAmmo());
            actor.Inventory.AddAll(m_TownGenerator.MakeItemShotgunAmmo());
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            #endregion

            // done.
            return new UniqueActor()
            {
                TheActor = actor,
                IsSpawned = false,
                IsWithRefugees = true,
                EventMessage = "You hear christmas music and drunken vomitting.",
                EventThemeMusic = GameMusics.SANTAMAN_THEME_SONG
            };
        }

        UniqueActor CreateUniqueRoguedjack(World world)
        {
            #region
            ActorModel model = GameActors.MaleCivilian;
            Actor actor = model.CreateNamed(GameFactions.TheCivilians, "Roguedjack", false, 0);
            actor.IsUnique = true;
            // actor.Controller = new CivilianAI(); // alpha10.1 defined by model like other actors

            actor.Doll.AddDecoration(DollPart.SKIN, GameImages.ACTOR_ROGUEDJACK);

            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HAULER);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HAULER);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HAULER);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HARDY);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HARDY);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HARDY);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HARDY);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HARDY);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.LEADERSHIP);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.LEADERSHIP);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.LEADERSHIP);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.LEADERSHIP);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.LEADERSHIP);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.CHARISMATIC);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.CHARISMATIC);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.CHARISMATIC);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.CHARISMATIC);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.CHARISMATIC);

            Item basher = new ItemMeleeWeapon(GameItems.UNIQUE_ROGUEDJACK_KEYBOARD) { IsUnique = true };
            actor.Inventory.AddAll(basher);
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            #endregion

            // done.
            return new UniqueActor()
            {
                TheActor = actor,
                IsSpawned = false,
                IsWithRefugees = true,
                EventMessage = "You hear a man shouting in French.",
                EventThemeMusic = GameMusics.ROGUEDJACK_THEME_SONG
            };
        }

        UniqueActor CreateUniqueDuckman(World world)
        {
            #region
            ActorModel model = GameActors.MaleCivilian;
            Actor actor = model.CreateNamed(GameFactions.TheCivilians, "Duckman", false, 0);
            actor.IsUnique = true;
            // actor.Controller = new CivilianAI(); // alpha10.1 defined by model like other actors

            actor.Doll.AddDecoration(DollPart.SKIN, GameImages.ACTOR_DUCKMAN);

            // awesome superhero!
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.CHARISMATIC);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.CHARISMATIC);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.CHARISMATIC);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.CHARISMATIC);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.CHARISMATIC);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.LEADERSHIP);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.STRONG);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.STRONG);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.STRONG);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.STRONG);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.STRONG);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HIGH_STAMINA);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HIGH_STAMINA);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HIGH_STAMINA);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HIGH_STAMINA);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HIGH_STAMINA);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.MARTIAL_ARTS);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.MARTIAL_ARTS);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.MARTIAL_ARTS);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.MARTIAL_ARTS);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.MARTIAL_ARTS);

            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            #endregion

            return new UniqueActor()
            {
                TheActor = actor,
                IsSpawned = false,
                IsWithRefugees = true,
                EventMessage = "You hear loud demented QUACKS.",
                EventThemeMusic = GameMusics.DUCKMAN_THEME_SONG
            };
        }

        UniqueActor CreateUniqueHansVonHanz(World world)
        {
            #region
            ActorModel model = GameActors.MaleCivilian;
            Actor actor = model.CreateNamed(GameFactions.TheCivilians, "Hans von Hanz", false, 0);
            actor.IsUnique = true;
            //actor.Controller = new CivilianAI(); // alpha10.1 defined by model like other actors

            actor.Doll.AddDecoration(DollPart.SKIN, GameImages.ACTOR_HANS_VON_HANZ);

            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HAULER);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HAULER);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.HAULER);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.FIREARMS);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.FIREARMS);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.FIREARMS);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.FIREARMS);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.FIREARMS);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.LEADERSHIP);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.LEADERSHIP);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.LEADERSHIP);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.LEADERSHIP);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.LEADERSHIP);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.NECROLOGY);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.NECROLOGY);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.NECROLOGY);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.NECROLOGY);
            m_TownGenerator.GiveStartingSkillToActor(actor, Skills.IDs.NECROLOGY);

            Item pistol = new ItemRangedWeapon(GameItems.UNIQUE_HANS_VON_HANZ_PISTOL) { IsUnique = true };
            actor.Inventory.AddAll(pistol);
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            actor.Inventory.AddAll(m_TownGenerator.MakeItemCannedFood());
            #endregion

            // done.
            return new UniqueActor()
            {
                TheActor = actor,
                IsSpawned = false,
                IsWithRefugees = true,
                EventMessage = "You hear a man barking orders in German.",
                EventThemeMusic = GameMusics.HANS_VON_HANZ_THEME_SONG
            };
        }

        UniqueItem SpawnUniqueSubwayWorkerBadge(World world)
        {
            ///////////////////////////////////
            // 1. Pick a random Subway map.
            //    Fails if not found.
            // 2. Pick a position in the rails.
            // 3. Drop it.
            ///////////////////////////////////

            Item it = new Item(GameItems.UNIQUE_SUBWAY_BADGE) { IsUnique = true, IsForbiddenToAI = true };

            // 1. Pick a random Subway map.
            List<Map> allSubways = new List<Map>();
            for (int x = 0; x < world.Size; x++)
                for (int y = 0; y < world.Size; y++)
                    if (world[x, y].HasSubway)
                        allSubways.Add(world[x, y].SubwayMap);
            if (allSubways.Count == 0)
                return new UniqueItem() { TheItem = it, IsSpawned = false };
            Map subway = allSubways[m_Rules.Roll(0, allSubways.Count)];

            // 2. Pick a position in the rails.
            Rectangle railsRect = subway.GetZoneByPartialName(NAME_SUBWAY_RAILS).Bounds;
            Point dropPt = new Point(m_Rules.Roll(railsRect.Left, railsRect.Right), m_Rules.Roll(railsRect.Top, railsRect.Bottom));

            // 3. Drop it.
            subway.DropItemAt(it, dropPt);
            // blood! deceased worker.
            subway.GetTileAt(dropPt).AddDecoration(GameImages.DECO_BLOODIED_FLOOR);

            // done.
            return new UniqueItem() { TheItem = it, IsSpawned = true };
        }

        UniqueMap CreateUniqueMap_CHARUndegroundFacility(World world)
        {
            ////////////////////////////////////////////////
            // 1. Find all business districts with offices.
            // 2. Pick one business district at random.
            // 3. Generate underground map there.
            ////////////////////////////////////////////////

            // 1. Find all business districts with offices.
            List<District> goodDistricts = null;
            for (int x = 0; x < world.Size; x++)
                for (int y = 0; y < world.Size; y++)
                {
                    if (world[x, y].Kind == DistrictKind.BUSINESS)
                    {
                        bool hasOffice = false;
                        foreach (Zone z in world[x, y].EntryMap.Zones)
                        {
                            if (z.HasGameAttribute(ZoneAttributes.IS_CHAR_OFFICE))
                            {
                                hasOffice = true;
                                break;
                            }
                        }
                        if (hasOffice)
                        {
                            if (goodDistricts == null)
                                goodDistricts = new List<District>();
                            goodDistricts.Add(world[x, y]);
                        }
                    }
                }

            // 2. Pick one business district at random.
            if (goodDistricts == null)
            {
                throw new InvalidOperationException("world has no business districts with offices");
            }
            District chosenDistrict = goodDistricts[m_Rules.Roll(0, goodDistricts.Count)];

            // 3. Generate underground map there.
            List<Zone> offices = new List<Zone>();
            foreach (Zone z in chosenDistrict.EntryMap.Zones)
            {
                if (z.HasGameAttribute(ZoneAttributes.IS_CHAR_OFFICE))
                {
                    offices.Add(z);
                }
            }
            Zone chosenOffice = offices[m_Rules.Roll(0, offices.Count)];
            Point baseEntryPos;  // alpha10
            Map map = m_TownGenerator.GenerateUniqueMap_CHARUnderground(chosenDistrict.EntryMap, chosenOffice, out baseEntryPos);
            map.District = chosenDistrict;
            map.Name = String.Format("CHAR Underground Facility @{0}-{1}", baseEntryPos.X, baseEntryPos.Y); // alpha10
            chosenDistrict.AddUniqueMap(map);
            return new UniqueMap() { TheMap = map };

        }
        #endregion

        #region District Maps
        DistrictKind GenerateDistrictKind(World world, int gridX, int gridY)
        {
            // Decide district kind - some districts are harcoded:
            //   - (0,0) : always Business.
            if (gridX == 0 && gridY == 0)
                return DistrictKind.BUSINESS;
            else
                return (DistrictKind)m_Rules.Roll((int)DistrictKind._FIRST, (int)DistrictKind._COUNT);
        }

        Map GenerateDistrictEntryMap(World world, District district, Point policeStationDistrictPos, Point hospitalDistrictPos)
        {
            int gridX = district.WorldPosition.X;
            int gridY = district.WorldPosition.Y;

            ///////////////////////////
            // 1. Compute unique seed.
            // 2. Set params for kind.
            // 3. Generate map.
            ///////////////////////////

            // 1. Compute unique seed.
            int gridSeed = m_Session.Seed + gridY * world.Size + gridX;

            // 3. Set gen params.
            #region
            BaseTownGenerator.Parameters genParams = BaseTownGenerator.DEFAULT_PARAMS;
            genParams.MapWidth = genParams.MapHeight = s_Options.DistrictSize;
            genParams.District = district;
            int factor = 8;
            string kindName = "District";
            switch (district.Kind)
            {
                case DistrictKind.SHOPPING:
                    // more shops, less other types.
                    kindName = "Shopping District";
                    genParams.CHARBuildingChance /= factor;
                    genParams.ShopBuildingChance *= factor;
                    genParams.ParkBuildingChance /= factor;
                    break;
                case DistrictKind.GREEN:
                    // more parks, less other types.
                    kindName = "Green District";
                    genParams.CHARBuildingChance /= factor;
                    genParams.ParkBuildingChance *= factor;
                    genParams.ShopBuildingChance /= factor;
                    break;
                case DistrictKind.BUSINESS:
                    // more offices, less other types.
                    kindName = "Business District";
                    genParams.CHARBuildingChance *= factor;
                    genParams.ParkBuildingChance /= factor;
                    genParams.ShopBuildingChance /= factor;
                    break;
                case DistrictKind.RESIDENTIAL:
                    // more housings, less other types.
                    kindName = "Residential District";
                    genParams.CHARBuildingChance /= factor;
                    genParams.ParkBuildingChance /= factor;
                    genParams.ShopBuildingChance /= factor;
                    break;
                case DistrictKind.GENERAL:
                    // use default params.
                    kindName = "District";
                    break;
                default:
                    throw new ArgumentOutOfRangeException("unhandled district kind");
            }

            // Special params.
            genParams.GeneratePoliceStation = (district.WorldPosition == policeStationDistrictPos);
            genParams.GenerateHospital = (district.WorldPosition == hospitalDistrictPos);
            #endregion

            // 4. Generate map.
            BaseTownGenerator.Parameters prevParams = m_TownGenerator.Params;
            m_TownGenerator.Params = genParams;
            Map map = m_TownGenerator.Generate(gridSeed);
            map.Name = String.Format("{0}@{1}", kindName, World.CoordToString(gridX, gridY));
            m_TownGenerator.Params = prevParams;

            // done.
            return map;
        }

        Map GenerateDistrictSewersMap(District district)
        {
            // Compute uniqueseed.
            int sewersSeed = (district.EntryMap.Seed << 1) ^ district.EntryMap.Seed;

            // Generate map.
            Map sewers = m_TownGenerator.GenerateSewersMap(sewersSeed, district);
            sewers.Name = String.Format("Sewers@{0}-{1}", district.WorldPosition.X, district.WorldPosition.Y);

            // done.
            return sewers;
        }

        Map GenerateDistrictSubwayMap(District district)
        {
            // Compute uniqueseed.
            int subwaySeed = (district.EntryMap.Seed << 2) ^ district.EntryMap.Seed;

            // Generate map.
            Map subway = m_TownGenerator.GenerateSubwayMap(subwaySeed, district);
            subway.Name = String.Format("Subway@{0}-{1}", district.WorldPosition.X, district.WorldPosition.Y);

            // done.
            return subway;
        }
        #endregion

        void GeneratePlayerOnMap(Map map, BaseTownGenerator townGen)
        {
            DiceRoller roller = new DiceRoller(map.Seed);

            /////////////////////////////////////////////////////
            // Create player actor : living/undead x male/female
            /////////////////////////////////////////////////////
            #region
            ActorModel playerModel;
            Actor player;
            if (m_CharGen.IsUndead)
            {
                // Handle specific undead type.
                // Zombified : need living, then zombify.
                switch (m_CharGen.UndeadModel)
                {
                    case GameActors.IDs.UNDEAD_SKELETON:
                        {
                            // Create the Skeleton.
                            player = m_GameActors.Skeleton.CreateNumberedName(m_GameFactions.TheUndeads, 0);
                            break;
                        }

                    case GameActors.IDs.UNDEAD_ZOMBIE:
                        {
                            // Create the Zombie.
                            player = m_GameActors.Zombie.CreateNumberedName(m_GameFactions.TheUndeads, 0);
                            break;
                        }

                    case GameActors.IDs.UNDEAD_MALE_ZOMBIFIED:
                    case GameActors.IDs.UNDEAD_FEMALE_ZOMBIFIED:
                        {
                            // First create as living.
                            playerModel = m_CharGen.IsMale ? m_GameActors.MaleCivilian : m_GameActors.FemaleCivilian;
                            player = playerModel.CreateAnonymous(m_GameFactions.TheCivilians, 0);
                            townGen.DressCivilian(roller, player);
                            townGen.GiveNameToActor(roller, player);
                            // Then zombify.
                            player = Zombify(null, player, true);
                            break;
                        }

                    case GameActors.IDs.UNDEAD_ZOMBIE_MASTER:
                        {
                            // Create the ZM.
                            player = m_GameActors.ZombieMaster.CreateNumberedName(m_GameFactions.TheUndeads, 0);
                            break;
                        }
                    default:
                        throw new ArgumentOutOfRangeException("unhandled undeadModel");
                }

                // Then make sure player related stuff are setup properly.
                PrepareActorForPlayerControl(player);
            }
            else
            {
                // Create living.
                playerModel = m_CharGen.IsMale ? m_GameActors.MaleCivilian : m_GameActors.FemaleCivilian;
                player = playerModel.CreateAnonymous(m_GameFactions.TheCivilians, 0);
                townGen.DressCivilian(roller, player);
                townGen.GiveNameToActor(roller, player);
                player.Sheet.SkillTable.AddOrIncreaseSkill((int)m_CharGen.StartingSkill);

                townGen.RecomputeActorStartingStats(player);
                OnSkillUpgrade(player, m_CharGen.StartingSkill);
                // slightly randomize Food and Sleep - 0..25%.
                int foodDeviation = (int)(0.25f * player.FoodPoints);
                player.FoodPoints = player.FoodPoints - m_Rules.Roll(0, foodDeviation);
                int sleepDeviation = (int)(0.25f * player.SleepPoints);
                player.SleepPoints = player.SleepPoints - m_Rules.Roll(0, sleepDeviation);
            }

            player.Controller = new PlayerController();
            #endregion

            /////////////
            // Spawn him.
            /////////////
            #region
            // living: try to spawn inside on a couch, then if failed spawn anywhere inside.
            // undead: spawn outside.
            // NEVER spawn in CHAR Office!!
            bool preferedSpawnOk = townGen.ActorPlace(roller, 10 * map.Width * map.Height, map, player,
                (pt) =>
                {
                    bool isInside = map.GetTileAt(pt.X, pt.Y).IsInside;
                    if ((m_CharGen.IsUndead && isInside) || (!m_CharGen.IsUndead && !isInside))
                        return false;

                    if (IsInCHAROffice(new Location(map, pt)))
                        return false;

                    MapObject mapObj = map.GetMapObjectAt(pt);
                    if (m_CharGen.IsUndead)
                        return mapObj == null;
                    else
                        return mapObj != null && mapObj.IsCouch;
                });

            if (!preferedSpawnOk)
            {
                // no couch, try inside but never in char office.
                bool spawnedInside = townGen.ActorPlace(roller, map.Width * map.Height, map, player,
                    (pt) => map.GetTileAt(pt.X, pt.Y).IsInside && !IsInCHAROffice(new Location(map, pt)));

                if (!spawnedInside)
                {
                    // could not spawn inside, do it outside...
                    while (!townGen.ActorPlace(roller, int.MaxValue, map, player, (pt) => !IsInCHAROffice(new Location(map, pt))))
                        ;
                }
            }
            #endregion
        }

        void RefreshPlayer()
        {
            // get player.
            foreach (Actor a in m_Session.CurrentMap.Actors)
            {
                if (a.IsPlayer)
                {
                    m_Player = a;
                    break;
                }
            }

            // compute view.
            if (m_Player != null)
                ComputeViewRect(m_Player.Location.Position);

        }

        void PrepareActorForPlayerControl(Actor newPlayerAvatar)
        {
            // inventory && skills.
            if (newPlayerAvatar.Inventory == null)
                newPlayerAvatar.Inventory = new Inventory(1);
            if (newPlayerAvatar.Sheet.SkillTable == null)
                newPlayerAvatar.Sheet.SkillTable = new SkillTable();

            // if follower, leave leader.
            if (newPlayerAvatar.Leader != null)
                newPlayerAvatar.Leader.RemoveFollower(newPlayerAvatar);
        }
        #endregion
    }
}
