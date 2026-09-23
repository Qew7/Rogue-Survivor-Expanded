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
        #region Events
        #region Zombie Invasion
        bool CheckForEvent_ZombieInvasion(Map map)
        {
            // when midnight strikes only.
            if (!map.LocalTime.IsStrikeOfMidnight)
                return false;

            // if not enough zombies only.
            int undeads = CountUndeads(map);
            if (undeads >= s_Options.MaxUndeads)
                return false;

            // clear.
            return true;
        }

        void FireEvent_ZombieInvasion(Map map)
        {
            // announce.
            if (map == m_Player.Location.Map && !m_Player.IsSleeping && !m_Player.Model.Abilities.IsUndead)
            {
                // message.
                AddMessage(new Message("It is Midnight! Zombies are invading!", m_Session.WorldTime.TurnCounter, Color.Crimson));
                RedrawPlayScreen();
            }

            // do it.
            int undeads = CountUndeads(map);
            float invasionRatio = Math.Min(1.0f, (map.LocalTime.Day * s_Options.ZombieInvasionDailyIncrease + s_Options.DayZeroUndeadsPercent) / 100.0f);
            int targetUndeadsCount = 1 + (int)(invasionRatio * s_Options.MaxUndeads);
            int undeadsToSpawn = targetUndeadsCount - undeads;
            for (int i = 0; i < undeadsToSpawn; i++)
                SpawnNewUndead(map, map.LocalTime.Day);

        }
        #endregion

        #region Sewers Invasion
        bool CheckForEvent_SewersInvasion(Map map)
        {
            // check game mode.
            if (!Rules.HasZombiesInSewers(m_Session.GameMode))
                return false;

            // randomly.
            if (!m_Rules.RollChance(SEWERS_INVASION_CHANCE))
                return false;

            // if not enough zombies only.
            int undeads = CountUndeads(map);
            if (undeads >= s_Options.MaxUndeads * SEWERS_UNDEADS_FACTOR)
                return false;

            // clear.
            return true;
        }

        void FireEvent_SewersInvasion(Map map)
        {
            // do it silently.
            int undeads = CountUndeads(map);
            float invasionRatio = Math.Min(1.0f, (map.LocalTime.Day * s_Options.ZombieInvasionDailyIncrease + s_Options.DayZeroUndeadsPercent) / 100.0f);
            int targetUndeadsCount = 1 + (int)(invasionRatio * s_Options.MaxUndeads * SEWERS_UNDEADS_FACTOR);
            int undeadsToSpawn = targetUndeadsCount - undeads;
            for (int i = 0; i < undeadsToSpawn; i++)
                SpawnNewSewersUndead(map, map.LocalTime.Day);

        }
        #endregion

        #region DISABLED Subway Invasion
#if false
        bool CheckForEvent_SubwayInvasion(Map map)
        {
            // randomly.
            if (!m_Rules.RollChance(SUBWAY_INVASION_CHANCE))
                return false;

            // if not enough zombies only.
            int undeads = CountUndeads(map);
            if (undeads >= s_Options.MaxUndeads * SUBWAY_UNDEADS_FACTOR)
                return false;

            // clear.
            return true;
        }

        void FireEvent_SubwayInvasion(Map map)
        {
            // do it silently.
            int undeads = CountUndeads(map);
            float invasionRatio = Math.Min(1.0f, (map.LocalTime.Day * s_Options.ZombieInvasionDailyIncrease + s_Options.DayZeroUndeadsPercent) / 100.0f);
            int targetUndeadsCount = 1 + (int)(invasionRatio * s_Options.MaxUndeads * SUBWAY_UNDEADS_FACTOR);
            int undeadsToSpawn = targetUndeadsCount - undeads;
            for (int i = 0; i < undeadsToSpawn; i++)
                SpawnNewSubwayUndead(map, map.LocalTime.Day);

        }
#endif
        #endregion

        #region Refugees wave
        bool CheckForEvent_RefugeesWave(Map map)
        {
            // when midday strikes only.
            if (!map.LocalTime.IsStrikeOfMidday)
                return false;

            // if not enough civs only.
#if false // TEST: disabled
            int civs = CountActors(map, (a) => a.Faction == GameFactions.TheCivilians || a.Faction == GameFactions.ThePolice);
            if (civs >= Options.MaxCivilians)
                return false;
#endif

            // clear.
            return true;
        }

        /// <summary>
        /// Double factor on city borders, half factor in city center, normal factor in all other districts.
        /// </summary>
        /// <param name="d"></param>
        /// <returns></returns>
        float RefugeesEventDistrictFactor(District d)
        {
            int dx = d.WorldPosition.X;
            int dy = d.WorldPosition.Y;
            int border = m_Session.World.Size - 1;
            int center = border / 2;

            return (dx == 0 || dy == 0 || dx == border || dy == border ? 2f :
                dx == center && dy == center ? 0.5f :
                1f);
        }

        void FireEvent_RefugeesWave(District district)
        {
            // announce.
            if (district == m_Player.Location.Map.District && !m_Player.IsSleeping && !m_Player.Model.Abilities.IsUndead)
            {
                // message.
                AddMessage(new Message("A new wave of refugees has arrived!", m_Session.WorldTime.TurnCounter, Color.Pink));
                RedrawPlayScreen();
            }

            // Spawn most on the surface and a small number in sewers and subway.
            int civilians = CountActors(district.EntryMap, (a) => a.Faction == GameFactions.TheCivilians || a.Faction == GameFactions.ThePolice);
            int size = 1 + (int)(REFUGEES_WAVE_SIZE * RefugeesEventDistrictFactor(district) * s_Options.MaxCivilians);
            int civiliansToSpawn = Math.Min(size, s_Options.MaxCivilians - civilians);
            Map spawnMap = null;
            for (int i = 0; i < civiliansToSpawn; i++)
            {
                // map: surface or sewers/subway.
                if (m_Rules.RollChance(REFUGEE_SURFACE_SPAWN_CHANCE))
                    spawnMap = district.EntryMap;
                else
                {
                    // 50% sewers and 50% subway, but some districts have no subway.
                    if (district.HasSubway)
                        spawnMap = m_Rules.RollChance(50) ? district.SubwayMap : district.SewersMap;
                    else
                        spawnMap = district.SewersMap;
                }
                // do it.
                SpawnNewRefugee(spawnMap);
            }

            // check for uniques, always in surface.
            if (m_Rules.RollChance(UNIQUE_REFUGEE_CHECK_CHANCE))
            {
                lock (m_Session) // thread safe
                {
                    UniqueActor[] array = m_Session.UniqueActors.ToArray();
                    UniqueActor[] mayArrive = Array.FindAll(array,
                        (UniqueActor a) =>
                        {
                            return a.IsWithRefugees && !a.IsSpawned && !a.TheActor.IsDead;
                        });
                    if (mayArrive != null && mayArrive.Length > 0)
                    {
                        int iArrive = m_Rules.Roll(0, mayArrive.Length);
                        FireEvent_UniqueActorArrive(district.EntryMap, mayArrive[iArrive]);
                    }
                }
            }
        }

        void FireEvent_UniqueActorArrive(Map map, UniqueActor unique)
        {
            // try to find a spot.
            bool spawned = SpawnActorOnMapBorder(map, unique.TheActor, SPAWN_DISTANCE_TO_PLAYER, true);

            // if failed, cancel.
            if (!spawned)
                return;

            // mark as spawned.
            unique.IsSpawned = true;

            // announce.
            if (map == m_Player.Location.Map && !m_Player.IsSleeping && !m_Player.Model.Abilities.IsUndead)
            {
                // alpha factorized to PlayUniqueActorMusicAndMessage
                //// message and music.
                //if (unique.EventMessage != null)
                //{
                //    if (unique.EventThemeMusic != null)
                //    {
                //        m_MusicManager.Stop();
                //        m_MusicManager.Play(unique.EventThemeMusic, MusicPriority.PRIORITY_EVENT);
                //    }
                //    // message.
                //    ClearMessages();
                //    AddMessage(new Message(unique.EventMessage, m_Session.WorldTime.TurnCounter, Color.Pink));
                //    AddMessage(MakePlayerCentricMessage("Seems to come from", unique.TheActor.Location.Position));
                //    AddMessagePressEnter();
                //    ClearMessages();
                //}
                PlayUniqueActorMusicAndMessage(unique, true);
                // scoring event.
                m_Session.Scoring.AddEvent(m_Session.WorldTime.TurnCounter, unique.TheActor.Name + " arrived.");
            }
        }

        // alpha10
        void PlayUniqueActorMusicAndMessage(UniqueActor unique, bool hasArrived)
        {
            if (unique.EventMessage != null)
            {
                Overlay highlightOverlay = null;

                if (unique.EventThemeMusic != null)
                {
                    m_MusicManager.Stop();
                    m_MusicManager.Play(unique.EventThemeMusic, MusicPriority.PRIORITY_EVENT);
                }

                ClearMessages();
                AddMessage(new Message(unique.EventMessage, m_Session.WorldTime.TurnCounter, Color.Pink));
                if (hasArrived)
                    AddMessage(MakePlayerCentricMessage("Seems to come from", unique.TheActor.Location.Position));
                else
                {
                    highlightOverlay = new OverlayRect(Color.Pink, new Rectangle(MapToScreen(unique.TheActor.Location.Position), new Size(TILE_SIZE, TILE_SIZE)));
                    AddOverlay(highlightOverlay);
                }
                if (!m_Player.IsBotPlayer)
                {
                    AddMessagePressEnter();
                    ClearMessages();
                }
                if (highlightOverlay != null)
                    RemoveOverlay(highlightOverlay);
            }
        }
        #endregion

        #region National guard
        bool CheckForEvent_NationalGuard(Map map)
        {
            // if option zeroed, don't bother.
            if (s_Options.NatGuardFactor == 0)
                return false;

            // during day only.
            if (map.LocalTime.IsNight)
                return false;

            // date.
            if (map.LocalTime.Day < NATGUARD_DAY)
                return false;
            if (map.LocalTime.Day >= NATGUARD_END_DAY)
                return false;

            // check chance.
            if (!m_Rules.RollChance(NATGUARD_INTERVENTION_CHANCE))
                return false;

            // if zombies significantly outnumber livings only (army count as 2 livings).
            int livings = CountLivings(map) + CountFaction(map, GameFactions.TheArmy);
            int undeads = CountUndeads(map);
            float undeadsPerLiving = (float)undeads / (float)livings;
            if (undeadsPerLiving * (s_Options.NatGuardFactor / 100f) < NATGUARD_INTERVENTION_FACTOR)
                return false;

            // clear.
            return true;
        }

        void FireEvent_NationalGuard(Map map)
        {
            // do it.
            // spawn squad leader then troopers.
            Actor squadLeader = SpawnNewNatGuardLeader(map);
            if (squadLeader != null)
            {
                for (int i = 0; i < NATGUARD_SQUAD_SIZE - 1; i++)
                {
                    // spawn trooper.
                    Actor trooper = SpawnNewNatGuardTrooper(map, squadLeader.Location.Position);
                    // add to leader squad.
                    if (trooper != null)
                        squadLeader.AddFollower(trooper);
                }
            }
            if (squadLeader == null)
                return;

            // notify AI.
            NotifyOrderablesAI(map, RaidType.NATGUARD, squadLeader.Location.Position);

            // announce.
            if (map == m_Player.Location.Map && !m_Player.IsSleeping && !m_Player.Model.Abilities.IsUndead)
            {
                // music.
                m_MusicManager.Stop();
                m_MusicManager.Play(GameMusics.ARMY, MusicPriority.PRIORITY_EVENT);

                // message.
                ClearMessages();
                AddMessage(new Message("A National Guard squad has arrived!", m_Session.WorldTime.TurnCounter, Color.LightGreen));
                AddMessage(MakePlayerCentricMessage("Soldiers seem to come from", squadLeader.Location.Position));
                if (!m_Player.IsBotPlayer)
                {
                    AddMessagePressEnter();
                    ClearMessages();
                }
            }

            // scoring event.
            if (map == m_Player.Location.Map)
            {
                m_Session.Scoring.AddEvent(m_Session.WorldTime.TurnCounter, "A National Guard squad arrived.");
            }

        }
        #endregion

        #region Army drop supplies
        bool CheckForEvent_ArmySupplies(Map map)
        {
            // if option zeroed, don't bother.
            if (s_Options.SuppliesDropFactor == 0)
                return false;

            // during day only.
            if (map.LocalTime.IsNight)
                return false;

            // date.
            if (map.LocalTime.Day < ARMY_SUPPLIES_DAY)
                return false;

            // check chance.
            if (!m_Rules.RollChance(ARMY_SUPPLIES_CHANCE))
                return false;

            // count food items vs livings.
            int livingsNeedFood = 1 + CountActors(map, (a) => !a.Model.Abilities.IsUndead && a.Model.Abilities.HasToEat && a.Faction == GameFactions.TheCivilians);
            int food = 1 + CountFoodItemsNutrition(map);
            float foodPerLiving = (float)food / (float)livingsNeedFood;
            if (foodPerLiving >= (s_Options.SuppliesDropFactor / 100f) * ARMY_SUPPLIES_FACTOR)
                return false;

            // clear.
            return true;
        }

        void FireEvent_ArmySupplies(Map map)
        {
            ////////////////////////////
            // Do it.
            // 1. Pick drop point.
            // 2. Drop scattered items.
            ////////////////////////////

            // 1. Pick drop point.
            Point dropPoint;
            bool dropped = FindDropSuppliesPoint(map, out dropPoint);
            if (!dropped)
                return;

            // 2. Drop scattered items.
            // only outside and free of actor and objects.
            int xmin = dropPoint.X - ARMY_SUPPLIES_SCATTER;
            int xmax = dropPoint.X + ARMY_SUPPLIES_SCATTER;
            int ymin = dropPoint.Y - ARMY_SUPPLIES_SCATTER;
            int ymax = dropPoint.Y + ARMY_SUPPLIES_SCATTER;
            map.TrimToBounds(ref xmin, ref ymin);
            map.TrimToBounds(ref xmax, ref ymax);
            for (int sx = xmin; sx <= xmax; sx++)
                for (int sy = ymin; sy <= ymax; sy++)
                {
                    if (!IsSuitableDropSuppliesPoint(map, sx, sy))
                        continue;

                    // drop stuff.
                    Item it = m_Rules.RollChance(80) ? m_TownGenerator.MakeItemArmyRation() : m_TownGenerator.MakeItemMedikit();
                    map.DropItemAt(it, sx, sy);
                }

            // notify AI.
            NotifyOrderablesAI(map, RaidType.ARMY_SUPLLIES, dropPoint);

            // announce.
            if (map == m_Player.Location.Map && !m_Player.IsSleeping && !m_Player.Model.Abilities.IsUndead)
            {
                // music.
                m_MusicManager.Stop();
                m_MusicManager.Play(GameMusics.ARMY, MusicPriority.PRIORITY_EVENT);

                // message.
                ClearMessages();
                AddMessage(new Message("An Army chopper has dropped supplies!", m_Session.WorldTime.TurnCounter, Color.LightGreen));
                AddMessage(MakePlayerCentricMessage("The drop point seems to be", dropPoint));
                if (!m_Player.IsBotPlayer)
                {
                    AddMessagePressEnter();
                    ClearMessages();
                }
            }

            // scoring event.
            if (map == m_Player.Location.Map)
            {
                m_Session.Scoring.AddEvent(m_Session.WorldTime.TurnCounter, "An army chopper dropped supplies.");
            }
        }

        bool IsSuitableDropSuppliesPoint(Map map, int x, int y)
        {
            //////////////////////////////
            // Must be:
            // 1. In bounds.
            // 2. Outside & walkable.
            // 3. No actor nor object.
            // 4. Far enough from player.
            //////////////////////////////

            // 1. In bounds.
            if (!map.IsInBounds(x, y))
                return false;

            // 2. Outside & walkable.
            Tile tile = map.GetTileAt(x, y);
            if (tile.IsInside || !tile.Model.IsWalkable)
                return false;

            // 3. No actor nor object.
            if (map.GetActorAt(x, y) != null || map.GetMapObjectAt(x, y) != null)
                return false;

            // 4. Far enough from player.
            if (DistanceToPlayer(map, x, y) < SPAWN_DISTANCE_TO_PLAYER)
                return false;

            // all clear.
            return true;
        }

        bool FindDropSuppliesPoint(Map map, out Point dropPoint)
        {
            dropPoint = new Point();

            // try to find a suitable point.
            int maxAttempts = 4 * map.Width;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // roll.
                dropPoint.X = m_Rules.RollX(map);
                dropPoint.Y = m_Rules.RollY(map);

                // suitable?
                if (!IsSuitableDropSuppliesPoint(map, dropPoint.X, dropPoint.Y))
                    continue;

                // we're good.
                return true;
            }

            // failed
            return false;
        }
        #endregion

        #region Timed raids
        bool HasRaidHappenedSince(RaidType raid, District district, WorldTime mapTime, int sinceNTurns)
        {
            return m_Session.HasRaidHappened(raid, district) && mapTime.TurnCounter - m_Session.LastRaidTime(raid, district) < sinceNTurns;
        }

        #region Bikers raid
        bool CheckForEvent_BikersRaid(Map map)
        {
            // date.
            if (map.LocalTime.Day < BIKERS_RAID_DAY)
                return false;
            if (map.LocalTime.Day >= BIKERS_END_DAY)
                return false;

            // last time : at least N day
            if (HasRaidHappenedSince(RaidType.BIKERS, map.District, map.LocalTime, BIKERS_RAID_DAYS_GAP * WorldTime.TURNS_PER_DAY))
                return false;

            // check chance.
            if (!m_Rules.RollChance(BIKERS_RAID_CHANCE_PER_TURN))
                return false;

            // if no bikers.
#if false
            disabled to take advantage of the new rival gang feature.
            if(HasActorOfModelID(map, GameActors.IDs.BIKER_MAN))
                return false;
#endif

            // clear.
            return true;
        }

        void FireEvent_BikersRaid(Map map)
        {
            // remember time.
            m_Session.SetLastRaidTime(RaidType.BIKERS, map.District, map.LocalTime.TurnCounter);

            // roll a random gang.
            GameGangs.IDs gangId = GameGangs.BIKERS[m_Rules.Roll(0, GameGangs.BIKERS.Length)];

            // do it.
            // spawn raid leader then squadies.
            Actor raidLeader = SpawnNewBikerLeader(map, gangId);
            if (raidLeader != null)
            {
                for (int i = 0; i < BIKERS_RAID_SIZE - 1; i++)
                {
                    // spawn squadie.
                    Actor squadie = SpawnNewBiker(map, gangId, raidLeader.Location.Position);
                    // add to leader squad.
                    if (squadie != null)
                        raidLeader.AddFollower(squadie);
                }
            }
            if (raidLeader == null)
                return;

            // notify AI.
            NotifyOrderablesAI(map, RaidType.BIKERS, raidLeader.Location.Position);

            // announce.
            if (map == m_Player.Location.Map && !m_Player.IsSleeping && !m_Player.Model.Abilities.IsUndead)
            {
                // music.
                m_MusicManager.Stop();
                m_MusicManager.Play(GameMusics.BIKER, MusicPriority.PRIORITY_EVENT);

                // message.
                ClearMessages();
                AddMessage(new Message("You hear the sound of roaring engines!", m_Session.WorldTime.TurnCounter, Color.LightGreen));
                AddMessage(MakePlayerCentricMessage("Motorbikes seem to come from", raidLeader.Location.Position));
                if (!m_Player.IsBotPlayer)
                {
                    AddMessagePressEnter();
                    ClearMessages();
                }
            }

            // scoring event.
            if (map == m_Player.Location.Map)
            {
                m_Session.Scoring.AddEvent(m_Session.WorldTime.TurnCounter, "Bikers raided the district.");
            }
        }
        #endregion

        #region Gangsta raid
        bool CheckForEvent_GangstasRaid(Map map)
        {
            // date.
            if (map.LocalTime.Day < GANGSTAS_RAID_DAY)
                return false;
            if (map.LocalTime.Day >= GANGSTAS_END_DAY)
                return false;

            // last time : at least N day
            if (HasRaidHappenedSince(RaidType.GANGSTA, map.District, map.LocalTime, GANGSTAS_RAID_DAYS_GAP * WorldTime.TURNS_PER_DAY))
                return false;

            // check chance.
            if (!m_Rules.RollChance(GANGSTAS_RAID_CHANCE_PER_TURN))
                return false;

            // if no gangsta.
#if false
            disabled to take advantage of the new rival gang feature.
            if (HasActorOfModelID(map, GameActors.IDs.GANGSTA_MAN))
                return false;
#endif

            // clear.
            return true;
        }

        void FireEvent_GangstasRaid(Map map)
        {
            // remember time.
            m_Session.SetLastRaidTime(RaidType.GANGSTA, map.District, map.LocalTime.TurnCounter);

            // roll a random gang.
            GameGangs.IDs gangId = GameGangs.GANGSTAS[m_Rules.Roll(0, GameGangs.GANGSTAS.Length)];

            // do it.
            // spawn raid leader then squadies.
            Actor raidLeader = SpawnNewGangstaLeader(map, gangId);
            if (raidLeader != null)
            {
                for (int i = 0; i < GANGSTAS_RAID_SIZE - 1; i++)
                {
                    // spawn squadie.
                    Actor squadie = SpawnNewGangsta(map, gangId, raidLeader.Location.Position);
                    // add to leader squad.
                    if (squadie != null)
                        raidLeader.AddFollower(squadie);
                }
            }
            if (raidLeader == null)
                return;

            // notify AI.
            NotifyOrderablesAI(map, RaidType.GANGSTA, raidLeader.Location.Position);

            // announce.
            if (map == m_Player.Location.Map && !m_Player.IsSleeping && !m_Player.Model.Abilities.IsUndead)
            {
                // music.
                m_MusicManager.Stop();
                m_MusicManager.Play(GameMusics.GANGSTA, MusicPriority.PRIORITY_EVENT);

                // message.
                ClearMessages();
                AddMessage(new Message("You hear obnoxious loud music!", m_Session.WorldTime.TurnCounter, Color.LightGreen));
                AddMessage(MakePlayerCentricMessage("Cars seem to come from", raidLeader.Location.Position));
                if (!m_Player.IsBotPlayer)
                {
                    AddMessagePressEnter();
                    ClearMessages();
                }
            }

            // scoring event.
            if (map == m_Player.Location.Map)
            {
                m_Session.Scoring.AddEvent(m_Session.WorldTime.TurnCounter, "Gangstas raided the district.");
            }
        }
        #endregion

        #region BlackOps raid
        bool CheckForEvent_BlackOpsRaid(Map map)
        {
            // date.
            if (map.LocalTime.Day < BLACKOPS_RAID_DAY)
                return false;

            // last time : at least N day
            if (HasRaidHappenedSince(RaidType.BLACKOPS, map.District, map.LocalTime, BLACKOPS_RAID_DAY_GAP * WorldTime.TURNS_PER_DAY))
                return false;

            // check chance.
            if (!m_Rules.RollChance(BLACKOPS_RAID_CHANCE_PER_TURN))
                return false;

            // clear.
            return true;
        }

        void FireEvent_BlackOpsRaid(Map map)
        {
            // remember time.
            m_Session.SetLastRaidTime(RaidType.BLACKOPS, map.District, map.LocalTime.TurnCounter);

            // do it.
            // spawn raid leader then squadies.
            Actor raidLeader = SpawnNewBlackOpsLeader(map);
            if (raidLeader != null)
            {
                for (int i = 0; i < BLACKOPS_RAID_SIZE - 1; i++)
                {
                    // spawn squadie.
                    Actor squadie = SpawnNewBlackOpsTrooper(map, raidLeader.Location.Position);
                    // add to leader squad.
                    if (squadie != null)
                        raidLeader.AddFollower(squadie);
                }
            }
            if (raidLeader == null)
                return;

            // notify AI.
            NotifyOrderablesAI(map, RaidType.BLACKOPS, raidLeader.Location.Position);

            // announce.
            if (map == m_Player.Location.Map && !m_Player.IsSleeping && !m_Player.Model.Abilities.IsUndead)
            {
                // music.
                m_MusicManager.Stop();
                m_MusicManager.Play(GameMusics.ARMY, MusicPriority.PRIORITY_EVENT);

                // message.
                ClearMessages();
                AddMessage(new Message("You hear a chopper flying over the city!", m_Session.WorldTime.TurnCounter, Color.LightGreen));
                AddMessage(MakePlayerCentricMessage("The chopper has dropped something", raidLeader.Location.Position));
                if (!m_Player.IsBotPlayer)
                {
                    AddMessagePressEnter();
                    ClearMessages();
                }
            }

            // scoring event.
            if (map == m_Player.Location.Map)
            {
                m_Session.Scoring.AddEvent(m_Session.WorldTime.TurnCounter, "BlackOps raided the district.");
            }
        }
        #endregion

        #region Band of Survivors
        bool CheckForEvent_BandOfSurvivors(Map map)
        {
            // date.
            if (map.LocalTime.Day < SURVIVORS_BAND_DAY)
                return false;

            // last time : at least N day
            if (HasRaidHappenedSince(RaidType.SURVIVORS, map.District, map.LocalTime, SURVIVORS_BAND_DAY_GAP * WorldTime.TURNS_PER_DAY))
                return false;

            // check chance.
            if (!m_Rules.RollChance(SURVIVORS_BAND_CHANCE_PER_TURN))
                return false;

            // clear.
            return true;
        }

        void FireEvent_BandOfSurvivors(Map map)
        {
            // remember time.
            m_Session.SetLastRaidTime(RaidType.SURVIVORS, map.District, map.LocalTime.TurnCounter);

            // do it.
            // spawn dudes.
            Actor bandScout = SpawnNewSurvivor(map);
            if (bandScout != null)
            {
                for (int i = 0; i < SURVIVORS_BAND_SIZE - 1; i++)
                    SpawnNewSurvivor(map, bandScout.Location.Position);
            }
            if (bandScout == null)
                return;

            // notify AI.
            NotifyOrderablesAI(map, RaidType.SURVIVORS, bandScout.Location.Position);

            // announce.
            if (map == m_Player.Location.Map && !m_Player.IsSleeping && !m_Player.Model.Abilities.IsUndead)
            {
                // music.
                m_MusicManager.Stop();
                m_MusicManager.Play(GameMusics.SURVIVORS, MusicPriority.PRIORITY_EVENT);

                // message.
                ClearMessages();
                AddMessage(new Message("You hear shooting and honking in the distance.", m_Session.WorldTime.TurnCounter, Color.LightGreen));
                AddMessage(MakePlayerCentricMessage("A van has stopped", bandScout.Location.Position));
                if (!m_Player.IsBotPlayer)
                {
                    AddMessagePressEnter();
                    ClearMessages();
                }
            }

            // scoring event.
            if (map == m_Player.Location.Map)
            {
                m_Session.Scoring.AddEvent(m_Session.WorldTime.TurnCounter, "A Band of Survivors entered the district.");
            }
        }
        #endregion

        #endregion

        #endregion
        #region Spawning new actors
        int DistanceToPlayer(Map map, int x, int y)
        {
            if (m_Player == null || m_Player.Location.Map != map)
                return int.MaxValue;
            return m_Rules.GridDistance(m_Player.Location.Position, x, y);
        }

        int DistanceToPlayer(Map map, Point pos)
        {
            return DistanceToPlayer(map, pos.X, pos.Y);
        }

        bool IsAdjacentToEnemy(Map map, Point pos, Actor actor)
        {
            int xmin = pos.X - 1;
            int xmax = pos.X + 1;
            int ymin = pos.Y - 1;
            int ymax = pos.Y + 1;
            map.TrimToBounds(ref xmin, ref ymin);
            map.TrimToBounds(ref xmax, ref ymax);

            for (int x = xmin; x <= xmax; x++)
                for (int y = ymin; y <= ymax; y++)
                {
                    if (x == pos.X && y == pos.Y)
                        continue;
                    Actor other = map.GetActorAt(x, y);
                    if (other == null)
                        continue;
                    if (m_Rules.AreEnemies(actor, other))
                        return true;
                }

            return false;
        }

        bool SpawnActorOnMapBorder(Map map, Actor actorToSpawn, int minDistToPlayer, bool mustBeOutside)
        {
            // find a good spot.
            int maxTries = 4 * (map.Width + map.Height);

            int i = 0;
            Point pos = new Point();
            do
            {
                ++i;

                // roll a position on the border.
                int x = (m_Rules.RollChance(50) ? 0 : map.Width - 1);
                int y = (m_Rules.RollChance(50) ? 0 : map.Height - 1);
                if (m_Rules.RollChance(50))
                    x = m_Rules.RollX(map);
                else
                    y = m_Rules.RollY(map);

                // must be free, (outside), far enough to player and not adjacent to an enemy.
                pos.X = x;
                pos.Y = y;
                if (mustBeOutside && map.GetTileAt(pos.X, pos.Y).IsInside)
                    continue;
                if (!m_Rules.IsWalkableFor(actorToSpawn, map, pos.X, pos.Y))
                    continue;
                if (DistanceToPlayer(map, pos) < minDistToPlayer)
                    continue;
                if (IsAdjacentToEnemy(map, pos, actorToSpawn))
                    continue;

                // success!
                map.PlaceActorAt(actorToSpawn, pos);
                // trigger stuff
                OnActorEnterTile(actorToSpawn);
                return true;
            }
            while (i <= maxTries);

            // failed.
            return false;
        }

        bool SpawnActorNear(Map map, Actor actorToSpawn, int minDistToPlayer, Point nearPoint, int maxDistToPoint)
        {
            // find a good spot.
            int maxTries = 4 * (map.Width + map.Height);

            int i = 0;
            Point pos = new Point();
            do
            {
                ++i;

                // roll a position.
                int x = nearPoint.X + m_Rules.Roll(1, maxDistToPoint + 1) - m_Rules.Roll(1, maxDistToPoint + 1);
                int y = nearPoint.Y + m_Rules.Roll(1, maxDistToPoint + 1) - m_Rules.Roll(1, maxDistToPoint + 1);

                // trim to map.
                pos.X = x;
                pos.Y = y;
                map.TrimToBounds(ref pos);

                // must free, outside, far enough to player and not adjacent to an enemy.
                if (map.GetTileAt(pos.X, pos.Y).IsInside)
                    continue;
                if (!m_Rules.IsWalkableFor(actorToSpawn, map, pos.X, pos.Y))
                    continue;
                if (DistanceToPlayer(map, pos) < minDistToPlayer)
                    continue;
                if (IsAdjacentToEnemy(map, pos, actorToSpawn))
                    continue;

                // success!
                map.PlaceActorAt(actorToSpawn, pos);
                return true;
            }
            while (i <= maxTries);

            // failed.
            return false;
        }

        void SpawnNewUndead(Map map, int day)
        {
            ////////////////
            // Create actor.
            ////////////////
            Actor newUndead = m_TownGenerator.CreateNewUndead(map.LocalTime.TurnCounter);

            ///////////////////
            // Spawn hi level?
            ///////////////////
            if (s_Options.AllowUndeadsEvolution && Rules.HasEvolution(m_Session.GameMode))
            {
                // chances.
                int levelupChance = Math.Min(75, day * 2); // +2% per day, max 75%.
                bool doLevelUp = false;
                GameActors.IDs levelupID = (GameActors.IDs)newUndead.Model.ID;
                if (m_Rules.RollChance(levelupChance))
                {
                    doLevelUp = true;
                    levelupID = NextUndeadEvolution((GameActors.IDs)newUndead.Model.ID);
                    if (m_Rules.RollChance(levelupChance))
                        levelupID = NextUndeadEvolution(levelupID);
                }

                // restrict some models.
                if (levelupID == GameActors.IDs.UNDEAD_ZOMBIE_LORD && day < ZOMBIE_LORD_EVOLUTION_MIN_DAY)
                    doLevelUp = false;

                // levelup?
                if (doLevelUp)
                {
                    newUndead.Model = GameActors[levelupID];
                }
            }

            ///////////////////
            // Try to spawn it.
            ///////////////////
            SpawnActorOnMapBorder(map, newUndead, SPAWN_DISTANCE_TO_PLAYER, true);
        }

        void SpawnNewSewersUndead(Map map, int day)
        {
            ////////////////
            // Create actor.
            ////////////////
            Actor newUndead = m_TownGenerator.CreateNewSewersUndead(map.LocalTime.TurnCounter);

            ///////////////////
            // Try to spawn it.
            ///////////////////
            SpawnActorOnMapBorder(map, newUndead, SPAWN_DISTANCE_TO_PLAYER, false);
        }

        void SpawnNewSubwayUndead(Map map, int day)
        {
            ////////////////
            // Create actor.
            ////////////////
            Actor newUndead = m_TownGenerator.CreateNewSubwayUndead(map.LocalTime.TurnCounter);

            ///////////////////
            // Try to spawn it.
            ///////////////////
            SpawnActorOnMapBorder(map, newUndead, SPAWN_DISTANCE_TO_PLAYER, false);
        }


        void SpawnNewRefugee(Map map)
        {
            ////////////////
            // Create actor.
            ////////////////
            Actor newCivilian = m_TownGenerator.CreateNewRefugee(map.LocalTime.TurnCounter, REFUGEES_WAVE_ITEMS);

            ///////////////////
            // Try to spawn it.
            ///////////////////
            SpawnActorOnMapBorder(map, newCivilian, SPAWN_DISTANCE_TO_PLAYER, true);
        }

        Actor SpawnNewSurvivor(Map map)
        {
            ////////////////
            // Create actor.
            ////////////////
            Actor newSurvivor = m_TownGenerator.CreateNewSurvivor(map.LocalTime.TurnCounter);

            ///////////////////
            // Try to spawn it.
            ///////////////////
            if (SpawnActorOnMapBorder(map, newSurvivor, SPAWN_DISTANCE_TO_PLAYER, true))
                return newSurvivor;
            else
                return null;
        }

        Actor SpawnNewSurvivor(Map map, Point bandPos)
        {
            ////////////////
            // Create actor.
            ////////////////
            Actor newSurvivor = m_TownGenerator.CreateNewSurvivor(map.LocalTime.TurnCounter);

            ///////////////////
            // Try to spawn it.
            ///////////////////
            if (SpawnActorNear(map, newSurvivor, SPAWN_DISTANCE_TO_PLAYER, bandPos, 3))
                return newSurvivor;
            else
                return null;
        }

        Actor SpawnNewNatGuardLeader(Map map)
        {
            ////////////////
            // Create actor.
            ////////////////
            Actor newNatLeader = m_TownGenerator.CreateNewArmyNationalGuard(map.LocalTime.TurnCounter, "Sgt");

            // skills.
            m_TownGenerator.GiveStartingSkillToActor(newNatLeader, Skills.IDs.LEADERSHIP);

            // additional items : z-tracker.
            if (map.LocalTime.Day > NATGUARD_ZTRACKER_DAY)
            {
                newNatLeader.Inventory.AddAll(m_TownGenerator.MakeItemZTracker());
            }

            ///////////////////
            // Try to spawn it.
            ///////////////////
            bool spawned = SpawnActorOnMapBorder(map, newNatLeader, SPAWN_DISTANCE_TO_PLAYER, true);

            // done.
            return spawned ? newNatLeader : null;
        }

        Actor SpawnNewNatGuardTrooper(Map map, Point leaderPos)
        {
            ////////////////
            // Create actor.
            ////////////////
            Actor newNatGuard = m_TownGenerator.CreateNewArmyNationalGuard(map.LocalTime.TurnCounter, "Pvt");

            // additional items : combat knife or grenades.
            if (m_Rules.RollChance(50))
                newNatGuard.Inventory.AddAll(m_TownGenerator.MakeItemCombatKnife());
            else
                newNatGuard.Inventory.AddAll(m_TownGenerator.MakeItemGrenade());

            ///////////////////
            // Try to spawn it.
            ///////////////////
            bool spawned = SpawnActorNear(map, newNatGuard, SPAWN_DISTANCE_TO_PLAYER, leaderPos, 3);

            // done.
            return spawned ? newNatGuard : null;
        }

        Actor SpawnNewBikerLeader(Map map, GameGangs.IDs gangId)
        {
            ////////////////
            // Create actor.
            ////////////////
            Actor newBikerLeader = m_TownGenerator.CreateNewBikerMan(map.LocalTime.TurnCounter, gangId);

            // skills.
            m_TownGenerator.GiveStartingSkillToActor(newBikerLeader, Skills.IDs.LEADERSHIP);
            m_TownGenerator.GiveStartingSkillToActor(newBikerLeader, Skills.IDs.TOUGH);
            m_TownGenerator.GiveStartingSkillToActor(newBikerLeader, Skills.IDs.TOUGH);
            m_TownGenerator.GiveStartingSkillToActor(newBikerLeader, Skills.IDs.TOUGH);
            m_TownGenerator.GiveStartingSkillToActor(newBikerLeader, Skills.IDs.STRONG);
            m_TownGenerator.GiveStartingSkillToActor(newBikerLeader, Skills.IDs.STRONG);
            m_TownGenerator.GiveStartingSkillToActor(newBikerLeader, Skills.IDs.STRONG);

            ///////////////////
            // Try to spawn it.
            ///////////////////
            bool spawned = SpawnActorOnMapBorder(map, newBikerLeader, SPAWN_DISTANCE_TO_PLAYER, true);

            // done.
            return spawned ? newBikerLeader : null;
        }

        Actor SpawnNewBiker(Map map, GameGangs.IDs gangId, Point leaderPos)
        {
            ////////////////
            // Create actor.
            ////////////////
            Actor newBiker = m_TownGenerator.CreateNewBikerMan(map.LocalTime.TurnCounter, gangId);

            // skils.
            m_TownGenerator.GiveStartingSkillToActor(newBiker, Skills.IDs.TOUGH);
            m_TownGenerator.GiveStartingSkillToActor(newBiker, Skills.IDs.STRONG);

            ///////////////////
            // Try to spawn it.
            ///////////////////
            bool spawned = SpawnActorNear(map, newBiker, SPAWN_DISTANCE_TO_PLAYER, leaderPos, 3);

            // done.
            return spawned ? newBiker : null;
        }

        Actor SpawnNewGangstaLeader(Map map, GameGangs.IDs gangId)
        {
            ////////////////
            // Create actor.
            ////////////////
            Actor newGangstaLeader = m_TownGenerator.CreateNewGangstaMan(map.LocalTime.TurnCounter, gangId);

            // skills.
            m_TownGenerator.GiveStartingSkillToActor(newGangstaLeader, Skills.IDs.LEADERSHIP);
            m_TownGenerator.GiveStartingSkillToActor(newGangstaLeader, Skills.IDs.AGILE);
            m_TownGenerator.GiveStartingSkillToActor(newGangstaLeader, Skills.IDs.AGILE);
            m_TownGenerator.GiveStartingSkillToActor(newGangstaLeader, Skills.IDs.AGILE);
            m_TownGenerator.GiveStartingSkillToActor(newGangstaLeader, Skills.IDs.FIREARMS);

            ///////////////////
            // Try to spawn it.
            ///////////////////
            bool spawned = SpawnActorOnMapBorder(map, newGangstaLeader, SPAWN_DISTANCE_TO_PLAYER, true);

            // done.
            return spawned ? newGangstaLeader : null;
        }

        Actor SpawnNewGangsta(Map map, GameGangs.IDs gangId, Point leaderPos)
        {
            ////////////////
            // Create actor.
            ////////////////
            Actor newGangsta = m_TownGenerator.CreateNewGangstaMan(map.LocalTime.TurnCounter, gangId);

            // skils.
            m_TownGenerator.GiveStartingSkillToActor(newGangsta, Skills.IDs.AGILE);

            ///////////////////
            // Try to spawn it.
            ///////////////////
            bool spawned = SpawnActorNear(map, newGangsta, SPAWN_DISTANCE_TO_PLAYER, leaderPos, 3);

            // done.
            return spawned ? newGangsta : null;
        }

        Actor SpawnNewBlackOpsLeader(Map map)
        {
            ////////////////
            // Create actor.
            ////////////////
            Actor newBOLeader = m_TownGenerator.CreateNewBlackOps(map.LocalTime.TurnCounter, "Officer");

            // skills.
            m_TownGenerator.GiveStartingSkillToActor(newBOLeader, Skills.IDs.LEADERSHIP);
            m_TownGenerator.GiveStartingSkillToActor(newBOLeader, Skills.IDs.AGILE);
            m_TownGenerator.GiveStartingSkillToActor(newBOLeader, Skills.IDs.AGILE);
            m_TownGenerator.GiveStartingSkillToActor(newBOLeader, Skills.IDs.AGILE);
            m_TownGenerator.GiveStartingSkillToActor(newBOLeader, Skills.IDs.FIREARMS);
            m_TownGenerator.GiveStartingSkillToActor(newBOLeader, Skills.IDs.FIREARMS);
            m_TownGenerator.GiveStartingSkillToActor(newBOLeader, Skills.IDs.FIREARMS);
            m_TownGenerator.GiveStartingSkillToActor(newBOLeader, Skills.IDs.TOUGH);
            m_TownGenerator.GiveStartingSkillToActor(newBOLeader, Skills.IDs.TOUGH);
            m_TownGenerator.GiveStartingSkillToActor(newBOLeader, Skills.IDs.TOUGH);

            ///////////////////
            // Try to spawn it.
            ///////////////////
            bool spawned = SpawnActorOnMapBorder(map, newBOLeader, SPAWN_DISTANCE_TO_PLAYER, true);

            // done.
            return spawned ? newBOLeader : null;
        }

        Actor SpawnNewBlackOpsTrooper(Map map, Point leaderPos)
        {
            ////////////////
            // Create actor.
            ////////////////
            Actor newBO = m_TownGenerator.CreateNewBlackOps(map.LocalTime.TurnCounter, "Agent");

            // skills.
            m_TownGenerator.GiveStartingSkillToActor(newBO, Skills.IDs.AGILE);
            m_TownGenerator.GiveStartingSkillToActor(newBO, Skills.IDs.FIREARMS);
            m_TownGenerator.GiveStartingSkillToActor(newBO, Skills.IDs.TOUGH);

            ///////////////////
            // Try to spawn it.
            ///////////////////
            bool spawned = SpawnActorNear(map, newBO, SPAWN_DISTANCE_TO_PLAYER, leaderPos, 3);

            // done.
            return spawned ? newBO : null;
        }
        #endregion
    }
}
