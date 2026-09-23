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
        #region Special player events
        void ShowSpecialDialogue(Actor speaker, string[] text)
        {
            // music.
            m_MusicManager.Stop();
            m_MusicManager.Play(GameMusics.INTERLUDE, MusicPriority.PRIORITY_EVENT);

            // overlays.
            AddOverlay(new OverlayPopup(text, Color.Gold, Color.Gold, Color.DimGray, new Point(0, 0)));
            AddOverlay(new OverlayRect(Color.Yellow, new Rectangle(MapToScreen(speaker.Location.Position), new Size(TILE_SIZE, TILE_SIZE))));

            // message & wait enter.
            ClearMessages();
            if (!m_Player.IsBotPlayer)
                AddMessagePressEnter();
            ClearOverlays();  // alpha10 fix
            m_MusicManager.Stop();
        }

        void CheckSpecialPlayerEventsAfterAction(Actor player)
        {
            //////////////////////////////////////////////////////////
            //
            // Special events - limited to some factions/actors
            // 1. Breaking into CHAR office for the 1st time: !undead !char
            // 2. Visiting CHAR Underground facility for the 1st time.
            // 3. Sighting The Sewers Thing : !thesewersthing
            // 4. Police Station script.
            // 5. Sighting Jason Myers : !jasonmyers
            // 6. Sighting Duckman // alpha10 disabled
            //
            // Generic 1st Time flags :
            // 1. Visiting a new map.
            // 2. Sighting an actor : actor model, unique NPCs.
            //
            // Item interactions :
            // 1. Subway Worker Badge in Subway maps.
            //////////////////////////////////////////////////////////


            #region Special events
            // 1. Breaking into CHAR office for the 1st time: !undead !char
            #region
            if (!player.Model.Abilities.IsUndead && player.Faction != GameFactions.TheCHARCorporation)
            {
                if (!m_Session.Scoring.HasCompletedAchievement(Achievement.IDs.CHAR_BROKE_INTO_OFFICE))
                {
                    if (IsInCHAROffice(player.Location))
                    {
                        // completed.
                        m_Session.Scoring.SetCompletedAchievement(Achievement.IDs.CHAR_BROKE_INTO_OFFICE);

                        // achievement!
                        ShowNewAchievement(Achievement.IDs.CHAR_BROKE_INTO_OFFICE);
                    }
                }
            }
            #endregion

            // 2. Visiting CHAR Underground facility for the 1st time.
            #region

            if (!m_Session.Scoring.HasCompletedAchievement(Achievement.IDs.CHAR_FOUND_UNDERGROUND_FACILITY))
            {
                if (player.Location.Map == m_Session.UniqueMaps.CHARUndergroundFacility.TheMap)
                {
                    lock (m_Session) // thread safe
                    {
                        // completed.
                        m_Session.Scoring.SetCompletedAchievement(Achievement.IDs.CHAR_FOUND_UNDERGROUND_FACILITY);

                        // achievement!
                        ShowNewAchievement(Achievement.IDs.CHAR_FOUND_UNDERGROUND_FACILITY);

                        // make sure the player knows about it now and it is activated.
                        m_Session.PlayerKnows_CHARUndergroundFacilityLocation = true;
                        m_Session.CHARUndergroundFacility_Activated = true;
                        m_Session.UniqueMaps.CHARUndergroundFacility.TheMap.IsSecret = false;

                        // open the exit to and from surface for AIs.
                        Map surfaceMap = m_Session.UniqueMaps.CHARUndergroundFacility.TheMap.District.EntryMap;
                        Point? surfaceEntry = surfaceMap.FindFirstInMap(
                            (pt) =>
                            {
                                Exit e = surfaceMap.GetExitAt(pt);
                                if (e == null)
                                    return false;
                                return e.ToMap == m_Session.UniqueMaps.CHARUndergroundFacility.TheMap;
                            });
                        if (surfaceEntry == null)
                            throw new InvalidOperationException("could not find exit to CUF in surface map");
                        Exit toCUF = surfaceMap.GetExitAt(surfaceEntry.Value);
                        toCUF.IsAnAIExit = true;

                        Point? cufExit = m_Session.UniqueMaps.CHARUndergroundFacility.TheMap.FindFirstInMap(
                            (pt) =>
                            {
                                Exit e = m_Session.UniqueMaps.CHARUndergroundFacility.TheMap.GetExitAt(pt);
                                if (e == null)
                                    return false;
                                return e.ToMap == surfaceMap;
                            });
                        if (cufExit == null)
                            throw new InvalidOperationException("could not find exit to surface in CUF map");
                        Exit fromCUF = m_Session.UniqueMaps.CHARUndergroundFacility.TheMap.GetExitAt(cufExit.Value);
                        fromCUF.IsAnAIExit = true;
                    }
                }
            }
            #endregion

            // 3. Sighting The Sewers Thing : !thesewersthing
            #region
            if (player != m_Session.UniqueActors.TheSewersThing.TheActor)
            {
                if (!m_Session.PlayerKnows_TheSewersThingLocation &&
                    player.Location.Map == m_Session.UniqueActors.TheSewersThing.TheActor.Location.Map &&
                    !m_Session.UniqueActors.TheSewersThing.TheActor.IsDead)
                {
                    if (IsVisibleToPlayer(m_Session.UniqueActors.TheSewersThing.TheActor))
                    {
                        lock (m_Session) // thread safe
                        {
                            m_Session.PlayerKnows_TheSewersThingLocation = true;

                            // message + music, so the player notices it.
                            m_MusicManager.Stop();
                            m_MusicManager.Play(GameMusics.FIGHT, MusicPriority.PRIORITY_EVENT);
                            ClearMessages();
                            AddMessage(new Message("Hey! What's that THING!?", m_Session.WorldTime.TurnCounter, Color.Yellow));
                            if (!m_Player.IsBotPlayer)
                                AddMessagePressEnter();
                        }
                    }
                }
            }
            #endregion

            // 4. Police Station script.
            #region
            if (player.Location.Map == m_Session.UniqueMaps.PoliceStation_JailsLevel.TheMap &&
                !m_Session.UniqueActors.PoliceStationPrisoner.TheActor.IsDead)
            {
                Actor prisoner = m_Session.UniqueActors.PoliceStationPrisoner.TheActor;
                Map map = player.Location.Map;
                switch (m_Session.ScriptStage_PoliceStationPrisoner)
                {
                    case ScriptStage.STAGE_0:   // nothing happened yet, waiting to offer deal.
                        // alpha10.1 check if near prisoner, not generator because prisoner can now spawn in any of the cells
                        //           also slightly modified what he/she says.
                        /////////////////////////////////////////////
                        // Player is near the prisoner : offer deal.
                        /////////////////////////////////////////////
                        if (m_Rules.GridDistance(player.Location.Position, prisoner.Location.Position) <= 2 &&
                            //map.HasAnyAdjacentInMap(player.Location.Position, (pt) => map.GetMapObjectAt(pt) is PowerGenerator) &&
                            !prisoner.IsSleeping &&
                            IsVisibleToPlayer(prisoner))  // alpha10 fix: and visible!
                        {
                            lock (m_Session) // thread safe
                            {
                                // Offer deal.
                                string[] text = new string[]
                                {
                                    "\" Psssst! Hey! You over there! \"",
                                    String.Format("{0} is discretly calling you from {1} cell. You listen closely...", prisoner.Name, HisOrHer(prisoner)),
                                    "\" Listen! I shouldn't be here! Just drove a bit too fast!",
                                    "  Look, I know what's happening! I worked down there! At the CHAR facility!",
                                    "  They didn't want me to leave but I did! Like I'm stupid enough to stay down there uh?",
                                    "  Now listen! Let's make a deal...",
                                    "  Stupid cops won't listen to me. You look clever...",
                                    "  You just have to push the button at the end of the corridor to open my cell.",
                                    "  The cops are too busy to care about small fish like me!",
                                    "  Then I'll tell you where is the underground facility and just get the hell out of here.",
                                    "  I don't give a fuck about CHAR anymore, you can do what you want with that!",
                                    "  There are plenty of cool stuff to loot down there!",
                                    "  Do it PLEASE! I REALLY shoudn't be there! \"",
                                    String.Format("Looks like {0} wants you to turn the generator on to open the cells...", HeOrShe(prisoner))
                                };
                                ShowSpecialDialogue(prisoner, text);

                                // Scoring event.
                                m_Session.Scoring.AddEvent(m_Session.WorldTime.TurnCounter, String.Format("{0} offered a deal.", prisoner.Name));

                                // Next stage.
                                m_Session.ScriptStage_PoliceStationPrisoner = ScriptStage.STAGE_1;
                            }
                        }
                        break;

                    case ScriptStage.STAGE_1:  // offered deal, waiting for opened cell.
                        ///////////////////////////////////////////////
                        // Wait to get out of cell and next to player.
                        ///////////////////////////////////////////////
                        if (!map.HasZonePartiallyNamedAt(prisoner.Location.Position, NAME_POLICE_STATION_JAILS_CELL) &&
                            m_Rules.IsAdjacent(player.Location.Position, prisoner.Location.Position) &&
                            !prisoner.IsSleeping)
                        {
                            lock (m_Session) // thread safe
                            {
                                // Thank you and give info.
                                string[] text = new string[]
                                {
                                    "\" Thank you! Thank you so much!",
                                    "  As promised, I'll tell you the big secret!",
                                    String.Format("  The CHAR Underground Facility is in district {0}.", World.CoordToString(m_Session.UniqueMaps.CHARUndergroundFacility.TheMap.District.WorldPosition.X,  m_Session.UniqueMaps.CHARUndergroundFacility.TheMap.District.WorldPosition.Y)),
                                    "  Look for a CHAR Office, a room with an iron door.",
                                    "  Now I must hurry! Thanks a lot for saving me!",
                                    "  I don't want them to... UGGH...",
                                    "  What's happening? NO!",
                                    "  NO NOT ME! aAAAAAaaaa! NOT NOW! AAAGGGGGGGRRR \""
                                };
                                ShowSpecialDialogue(prisoner, text);

                                // Scoring event.
                                m_Session.Scoring.AddEvent(m_Session.WorldTime.TurnCounter, String.Format("Freed {0}.", prisoner.Name));

                                // reveal location.
                                m_Session.PlayerKnows_CHARUndergroundFacilityLocation = true;

                                // Scoring event.
                                m_Session.Scoring.AddEvent(m_Session.WorldTime.TurnCounter, "Learned the location of the CHAR Underground Facility.");

                                // transformation.
                                // - zombify.
                                KillActor(null, prisoner, "transformation", false);  // alpha10 don't drop corpse!
                                Actor monster = Zombify(null, prisoner, false);
                                // - turn into a ZP.
                                monster.Model = m_GameActors.ZombiePrince;
                                // - zero AP so player don't get hit asap.
                                monster.ActionPoints = 0;

                                // Scoring event.
                                m_Session.Scoring.AddEvent(m_Session.WorldTime.TurnCounter, String.Format("{0} turned into a {1}!", prisoner.Name, monster.Model.Name));

                                // fight music!
                                m_MusicManager.Play(GameMusics.FIGHT, MusicPriority.PRIORITY_EVENT);

                                // Next stage.
                                m_Session.ScriptStage_PoliceStationPrisoner = ScriptStage.STAGE_2;
                            }
                        }
                        break;

                    case ScriptStage.STAGE_2: // monsterized!
                        // nothing to do...
                        break;

                    default:
                        throw new ArgumentOutOfRangeException("unhandled script stage " + m_Session.ScriptStage_PoliceStationPrisoner);
                }
            }
            #endregion

            // 5. Sighting Jason Myer : !jasonmyers
            #region
            if (player != m_Session.UniqueActors.JasonMyers.TheActor)
            {
                if (!m_Session.UniqueActors.JasonMyers.TheActor.IsDead)
                {
                    if (IsVisibleToPlayer(m_Session.UniqueActors.JasonMyers.TheActor))
                    {
                        lock (m_Session) // thread safe
                        {
                            // music.
                            if (m_MusicManager.Music != GameMusics.INSANE)
                            {
                                m_MusicManager.Stop();
                                m_MusicManager.Play(GameMusics.INSANE, MusicPriority.PRIORITY_EVENT);
                            }

                            // message if 1st time.
                            if (!m_Session.Scoring.HasSighted(m_Session.UniqueActors.JasonMyers.TheActor.Model.ID))
                            {
                                ClearMessages();
                                AddMessage(new Message("Nice axe you have there!", m_Session.WorldTime.TurnCounter, Color.Yellow));
                                if (!m_Player.IsBotPlayer)
                                    AddMessagePressEnter();
                            }
                        }
                    }
                }
            }
            #endregion

            // 6. Sighting Duckman
            #region
            // alpha10 disabled
            //if (player != m_Session.UniqueActors.Duckman.TheActor)
            //{
            //    if (!m_Session.UniqueActors.Duckman.TheActor.IsDead && IsVisibleToPlayer(m_Session.UniqueActors.Duckman.TheActor))
            //    {
            //        // keep playing music.
            //        if (!m_MusicManager.IsPlaying(GameMusics.DUCKMAN_THEME_SONG))
            //        {
            //            m_MusicManager.Play(GameMusics.DUCKMAN_THEME_SONG);
            //        }
            //    }
            //    else if (m_MusicManager.IsPlaying(GameMusics.DUCKMAN_THEME_SONG))
            //    {
            //        m_MusicManager.Stop(GameMusics.DUCKMAN_THEME_SONG);
            //    }
            //}
            #endregion

            #endregion

            #region Item interactions
            // 1. Subway Worker Badge in Subway maps.
            //    conditions: In Subway, Must be Equipped, Next to closed gates.
            //    effects: Turn all generators on.
            if (m_Session.UniqueItems.TheSubwayWorkerBadge.TheItem.IsEquipped &&
                player.Location.Map == player.Location.Map.District.SubwayMap &&
                player.Inventory.Contains(m_Session.UniqueItems.TheSubwayWorkerBadge.TheItem))
            {
                // must be adjacent to closed gates.
                Map map = player.Location.Map;
                if (map.HasAnyAdjacentInMap(player.Location.Position, (pt) =>
                {
                    MapObject obj = map.GetMapObjectAt(pt);
                    if (obj == null)
                        return false;
                    return obj.ImageID == GameImages.OBJ_GATE_CLOSED;
                }))
                {
                    // turn all power on!
                    DoTurnAllGeneratorsOn(map);

                    // message.
                    AddMessage(new Message("The gate system scanned your badge and turned the power on!", m_Session.WorldTime.TurnCounter, Color.Green));
                }
            }
            #endregion

            #region Generic 1st time flags
            // 1. Visiting a new map.
            if (!m_Session.Scoring.HasVisited(player.Location.Map))
            {
                // visit.
                m_Session.Scoring.AddVisit(m_Session.WorldTime.TurnCounter, player.Location.Map);
                m_Session.Scoring.AddEvent(m_Session.WorldTime.TurnCounter, String.Format("Visited {0}.", player.Location.Map.Name));
            }

            // 2. Sighting an actor : actor model, unique NPCs.
            foreach (Point p in m_PlayerFOV)
            {
                Actor other = player.Location.Map.GetActorAt(p);
                if (other == null || other == player)
                    continue;
                m_Session.Scoring.AddSighting(other.Model.ID, m_Session.WorldTime.TurnCounter);
                // alpha10 unique npcs lose their invincibility when sighted and highlight them.
                if (other.IsUnique)
                {
                    if (other.IsInvincible)  // 1st sighting
                    {
                        PlayUniqueActorMusicAndMessage(m_Session.ActorToUniqueActor(other), false);
                        other.IsInvincible = false;
                    }
                }
            }
            #endregion
        }
        #endregion
        #region Reincarnation
        void HandleReincarnation()
        {
            // Reincarnate?
            // don't bother if option set to zero.
            if (s_Options.MaxReincarnations <= 0 || !AskForReincarnation())
            {
                m_MusicManager.Stop();
                return;
            }

            // play music.
            m_MusicManager.Stop();
            m_MusicManager.PlayLooping(GameMusics.LIMBO, MusicPriority.PRIORITY_EVENT);

            // Waiting screen...
            m_UI.UI_Clear(Color.Black);
            m_UI.UI_DrawStringBold(Color.Yellow, "Reincarnation - Purgatory", 0, 0);
            m_UI.UI_DrawStringBold(Color.White, "(preparing reincarnations, please wait...)", 0, 2 * BOLD_LINE_SPACING);
            m_UI.UI_Repaint();

            // Decide available reincarnation targets.
            int countDummy;
            Actor randomR = FindReincarnationAvatar(GameOptions.ReincMode.RANDOM_ACTOR, out countDummy);
            int countLivings;
            Actor livingR = FindReincarnationAvatar(GameOptions.ReincMode.RANDOM_LIVING, out countLivings);
            int countUndead;
            Actor undeadR = FindReincarnationAvatar(GameOptions.ReincMode.RANDOM_UNDEAD, out countUndead);
            int countFollower;
            Actor followerR = FindReincarnationAvatar(GameOptions.ReincMode.RANDOM_FOLLOWER, out countFollower);
            Actor killerR = FindReincarnationAvatar(GameOptions.ReincMode.KILLER, out countDummy);
            Actor zombifiedR = FindReincarnationAvatar(GameOptions.ReincMode.ZOMBIFIED, out countDummy);

            // Get fun facts.
            string[] funFacts = CompileDistrictFunFacts(m_Player.Location.Map.District);

            // Reincarnate.
            // Choose avatar from a set of reincarnation modes.
            bool choiceMade = false;
            string[] entries =
            {
                GameOptions.Name(GameOptions.ReincMode.RANDOM_ACTOR),
                GameOptions.Name(GameOptions.ReincMode.RANDOM_LIVING),
                GameOptions.Name(GameOptions.ReincMode.RANDOM_UNDEAD),
                GameOptions.Name(GameOptions.ReincMode.RANDOM_FOLLOWER),
                GameOptions.Name(GameOptions.ReincMode.KILLER),
                GameOptions.Name(GameOptions.ReincMode.ZOMBIFIED)
            };
            string[] values =
            {
                DescribeAvatar(randomR),
                String.Format("{0}   (out of {1} possibilities)", DescribeAvatar(livingR), countLivings),
                String.Format("{0}   (out of {1} possibilities)", DescribeAvatar(undeadR), countUndead),
                String.Format("{0}   (out of {1} possibilities)", DescribeAvatar(followerR), countFollower),
                DescribeAvatar(killerR),
                DescribeAvatar(zombifiedR)
            };
            int selected = 0;
            Actor avatar = null;
            do
            {
                // show screen.
                int gx, gy;
                gx = gy = 0;
                m_UI.UI_Clear(Color.Black);
                m_UI.UI_DrawStringBold(Color.Yellow, "Reincarnation - Choose Avatar", gx, gy);
                gy += 2 * BOLD_LINE_SPACING;

                DrawMenuOrOptions(selected, Color.White, entries, Color.LightGreen, values, gx, ref gy);
                gy += 2 * BOLD_LINE_SPACING;

                m_UI.UI_DrawStringBold(Color.Pink, ".-* District Fun Facts! *-.", gx, gy);
                gy += BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(Color.Pink, String.Format("at current date : {0}.", new WorldTime(m_Session.WorldTime.TurnCounter).ToString()), gx, gy);
                gy += 2 * BOLD_LINE_SPACING;
                for (int i = 0; i < funFacts.Length; i++)
                {
                    m_UI.UI_DrawStringBold(Color.Pink, funFacts[i], gx, gy);
                    gy += BOLD_LINE_SPACING;
                }

                DrawFootnote(Color.White, "cursor to move, ENTER to select, ESC to cancel and end game");

                m_UI.UI_Repaint();

                // get menu action.
                KeyEventArgs key = m_UI.UI_WaitKey();
                switch (key.KeyCode)
                {
                    case Keys.Up:       // move up
                        if (selected > 0) --selected;
                        else selected = entries.Length - 1;
                        break;
                    case Keys.Down:     // move down
                        selected = (selected + 1) % entries.Length;
                        break;
                    case Keys.Escape:   // cancel & end game
                        choiceMade = true;
                        avatar = null;
                        break;

                    case Keys.Enter:    // validate
                        {
                            switch (selected)
                            {
                                case 0: // random actor
                                    avatar = randomR;
                                    break;
                                case 1: // random survivor
                                    avatar = livingR;
                                    break;
                                case 2: // random undead
                                    avatar = undeadR;
                                    break;
                                case 3: // random follower
                                    avatar = followerR;
                                    break;
                                case 4: // killer
                                    avatar = killerR;
                                    break;
                                case 5: // zombified
                                    avatar = zombifiedR;
                                    break;
                            }
                            choiceMade = avatar != null;
                            break;
                        }
                }
            }
            while (!choiceMade);

            // If canceled, stop.
            if (avatar == null)
            {
                m_MusicManager.Stop();
                return;
            }

            // Perform reincarnation.
            // 1. Make actor the player.
            // 2. Update all player-centric data.
            #region
            // 1. Make actor the player.
            avatar.Controller = new PlayerController();
            if (avatar.Activity != Activity.SLEEPING)
                avatar.Activity = Activity.IDLE;
            PrepareActorForPlayerControl(avatar);

            // 2. Update all player-centric data.
            m_Player = avatar;
            m_Session.CurrentMap = avatar.Location.Map;
            m_Session.Scoring.StartNewLife(m_Session.WorldTime.TurnCounter);
            m_Session.Scoring.AddEvent(m_Session.WorldTime.TurnCounter, String.Format("(reincarnation {0})", m_Session.Scoring.ReincarnationNumber));
            m_Session.Scoring.Side = m_Player.Model.Abilities.IsUndead ? DifficultySide.FOR_UNDEAD : DifficultySide.FOR_SURVIVOR;
            m_Session.Scoring.DifficultyRating = Scoring.ComputeDifficultyRating(s_Options, m_Session.Scoring.Side, m_Session.Scoring.ReincarnationNumber);
            /// forget all maps memory.
            for (int dx = 0; dx < m_Session.World.Size; dx++)
                for (int dy = 0; dy < m_Session.World.Size; dy++)
                {
                    District d = m_Session.World[dx, dy];
                    foreach (Map m in d.Maps)
                        m.SetAllAsUnvisited();
                }
            #endregion

            // Cleanup and refresh.
            m_MusicManager.Stop();
            UpdatePlayerFOV(m_Player);
            ComputeViewRect(m_Player.Location.Position);
            ClearMessages();
            AddMessage(new Message(String.Format("{0} feels disoriented for a second...", m_Player.Name), m_Session.WorldTime.TurnCounter, Color.Yellow));
            RedrawPlayScreen();

            // Play reinc sfx or special music for actor.
            string music = GameMusics.REINCARNATE;
            if (m_Player == m_Session.UniqueActors.JasonMyers.TheActor)
                music = GameMusics.INSANE;
            // apha10 replace with sfx
            m_MusicManager.Stop();
            m_MusicManager.Play(music, MusicPriority.PRIORITY_EVENT);

            // restart sim thread.
            StopSimThread(false);  // alpha10 stop-start
            StartSimThread();
        }

        string DescribeAvatar(Actor a)
        {
            if (a == null)
                return "(N/A)";
            bool isLeader = a.CountFollowers > 0;
            bool isFollower = a.HasLeader;
            return String.Format("{0}, a {1}{2}", a.Name, a.Model.Name, isLeader ? ", leader" : isFollower ? ", follower" : "");
        }

        bool AskForReincarnation()
        {
            // show screen.
            int gx, gy;
            gx = gy = 0;
            m_UI.UI_Clear(Color.Black);
            m_UI.UI_DrawStringBold(Color.Yellow, "Limbo", gx, gy);
            gy += 2 * BOLD_LINE_SPACING;
            m_UI.UI_DrawStringBold(Color.White, String.Format("Leave body {0}/{1}.", (1 + m_Session.Scoring.ReincarnationNumber), (1 + s_Options.MaxReincarnations)), gx, gy);
            gy += BOLD_LINE_SPACING;
            m_UI.UI_DrawStringBold(Color.White, "Remember lives.", gx, gy);
            gy += BOLD_LINE_SPACING;
            m_UI.UI_DrawStringBold(Color.White, "Remember purpose.", gx, gy);
            gy += BOLD_LINE_SPACING;
            m_UI.UI_DrawStringBold(Color.White, "Clear again.", gx, gy);
            gy += BOLD_LINE_SPACING;

            // ask question or no more lives left.
            if (m_Session.Scoring.ReincarnationNumber >= s_Options.MaxReincarnations)
            {
                // no more lives left.
                m_UI.UI_DrawStringBold(Color.LightGreen, "Humans interesting.", gx, gy);
                gy += BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(Color.LightGreen, "Time to leave.", gx, gy);
                gy += BOLD_LINE_SPACING;
                gy += 2 * BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(Color.Yellow, "No more reincarnations left.", gx, gy);
                DrawFootnote(Color.White, "press ENTER");
                m_UI.UI_Repaint();
                WaitEnter();
                return false;
            }
            else
            {
                // one more life available.
                m_UI.UI_DrawStringBold(Color.White, "Leave?", gx, gy);
                gy += BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(Color.White, "Live?", gx, gy);

                gy += 2 * BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(Color.Yellow, "Reincarnate? Y to confirm, N to cancel.", gx, gy);
                m_UI.UI_Repaint();

                // ask question.
                return WaitYesOrNo();
            }
        }

        bool IsSuitableReincarnation(Actor a, bool asLiving)
        {
            if (a == null)
                return false;
            if (a.IsDead || a.IsPlayer)
                return false;

            // same district only.
            if (a.Location.Map.District != m_Session.CurrentMap.District)
                return false;

            // forbid some special maps.
            if (a.Location.Map == m_Session.UniqueMaps.CHARUndergroundFacility.TheMap)
                return false;

            // forbid some special actors.
            if (a == m_Session.UniqueActors.PoliceStationPrisoner.TheActor)
                return false;

            // (option) not in sewers.
            if (a.Location.Map == a.Location.Map.District.SewersMap)
                return false;

            // living vs undead checks.
            if (asLiving)
            {
                if (a.Model.Abilities.IsUndead)
                    return false;
                // (option) civilians only.
                if (s_Options.IsLivingReincRestricted && a.Faction != GameFactions.TheCivilians)
                    return false;

                return true;
            }
            else
            {
                if (a.Model.Abilities.IsUndead)
                {
                    // (option) not rats.
                    if (!s_Options.CanReincarnateAsRat && a.Model == GameActors.RatZombie)
                        return false;

                    return true;
                }
                else
                    return false;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="reincMode"></param>
        /// <param name="matchingActors">how many actors where matching the reincarnation mode</param>
        /// <returns>null if not found</returns>
        Actor FindReincarnationAvatar(GameOptions.ReincMode reincMode, out int matchingActors)
        {
            switch (reincMode)
            {
                case GameOptions.ReincMode.RANDOM_FOLLOWER:
                    #region
                    {
                        if (m_Session.Scoring.FollowersWhendDied == null)
                        {
                            matchingActors = 0;
                            return null;
                        }

                        // list all suitable followers.
                        List<Actor> suitableFollowers = new List<Actor>(m_Session.Scoring.FollowersWhendDied.Count);
                        foreach (Actor fo in m_Session.Scoring.FollowersWhendDied)
                            if (IsSuitableReincarnation(fo, true))
                                suitableFollowers.Add(fo);

                        // make sure we have at least one suitable!
                        matchingActors = suitableFollowers.Count;
                        if (suitableFollowers.Count == 0)
                            return null;

                        // random one.
                        return suitableFollowers[m_Rules.Roll(0, suitableFollowers.Count)];
                    }
                #endregion

                case GameOptions.ReincMode.KILLER:
                    #region
                    {
                        Actor killer = m_Session.Scoring.Killer;
                        if (IsSuitableReincarnation(killer, true) || IsSuitableReincarnation(killer, false))
                        {
                            matchingActors = 1;
                            return killer;
                        }
                        else
                        {
                            matchingActors = 0;
                            return null;
                        }
                    }
                #endregion

                case GameOptions.ReincMode.RANDOM_ACTOR:
                case GameOptions.ReincMode.RANDOM_LIVING:
                case GameOptions.ReincMode.RANDOM_UNDEAD:
                    #region
                    {
                        // get a list of all suitable actors in the world.
                        bool asLiving = (reincMode == GameOptions.ReincMode.RANDOM_LIVING || (reincMode == GameOptions.ReincMode.RANDOM_ACTOR && m_Rules.RollChance(50)));
                        List<Actor> allSuitables = new List<Actor>();
                        for (int dx = 0; dx < m_Session.World.Size; dx++)
                            for (int dy = 0; dy < m_Session.World.Size; dy++)
                            {
                                District district = m_Session.World[dx, dy];
                                foreach (Map map in district.Maps)
                                    foreach (Actor a in map.Actors)
                                        if (IsSuitableReincarnation(a, asLiving))
                                            allSuitables.Add(a);
                            }

                        // pick one at random.
                        matchingActors = allSuitables.Count;
                        if (allSuitables.Count == 0)
                            return null;
                        else
                            return allSuitables[m_Rules.Roll(0, allSuitables.Count)];
                    }
                #endregion

                case GameOptions.ReincMode.ZOMBIFIED:
                    #region
                    {
                        Actor zombie = m_Session.Scoring.ZombifiedPlayer;
                        if (IsSuitableReincarnation(zombie, false))
                        {
                            matchingActors = 1;
                            return zombie;
                        }
                        else
                        {
                            matchingActors = 0;
                            return null;
                        }
                    }
                #endregion

                default:
                    throw new ArgumentOutOfRangeException("unhandled reincarnation mode " + reincMode.ToString());
            }
        }
        #endregion
        #region Insanity
        ActorAction GenerateInsaneAction(Actor actor)
        {
            // Let's the insanity flow...
            int roll = m_Rules.Roll(0, 5);
            switch (roll)
            {
                // shout
                case 0: return new ActionShout(actor, this, "AAAAAAAAAAA!!!");

                // random bump
                case 1: return new ActionBump(actor, this, m_Rules.RollDirection());

                // random bash.
                case 2:
                    Direction d = m_Rules.RollDirection();
                    MapObject mobj = actor.Location.Map.GetMapObjectAt(actor.Location.Position + d);
                    if (mobj == null) return null;
                    return new ActionBreak(actor, this, mobj);

                // random use/unequip-drop
                case 3:
                    Inventory inv = actor.Inventory;
                    if (inv == null || inv.CountItems == 0) return null;
                    Item it = inv[m_Rules.Roll(0, inv.CountItems)];
                    ActionUseItem useIt = new ActionUseItem(actor, this, it);
                    if (useIt.IsLegal())
                        return useIt;
                    if (it.IsEquipped)
                        return new ActionUnequipItem(actor, this, it);
                    return new ActionDropItem(actor, this, it);

                // random agression.
                case 4:
                    int fov = m_Rules.ActorFOV(actor, actor.Location.Map.LocalTime, m_Session.World.Weather);
                    foreach (Actor a in actor.Location.Map.Actors)
                    {
                        if (a == actor) continue;
                        if (m_Rules.AreEnemies(actor, a)) continue;
                        if (!LOS.CanTraceViewLine(actor.Location, a.Location.Position, fov)) continue;
                        if (m_Rules.RollChance(50))
                        {
                            // force leaving of leader.
                            if (actor.HasLeader)
                            {
                                actor.Leader.RemoveFollower(actor);
                                actor.TrustInLeader = Rules.TRUST_NEUTRAL;
                            }
                            // agress.
                            DoMakeAggression(actor, a);
                            return new ActionSay(actor, this, a, "YOU ARE ONE OF THEM!!", Sayflags.IS_IMPORTANT | Sayflags.IS_DANGER);
                        }
                    }
                    return null;

                default:
                    return null;
            }
        }

        void SeeingCauseInsanity(Actor whoDoesTheAction, Location loc, int sanCost, string what)
        {
            foreach (Actor a in loc.Map.Actors)
            {
                if (!a.Model.Abilities.HasSanity) continue;

                // can't see if sleeping or out of fov.
                if (a.IsSleeping) continue;
                int fov = m_Rules.ActorFOV(a, loc.Map.LocalTime, m_Session.World.Weather);
                if (!LOS.CanTraceViewLine(loc, a.Location.Position, fov)) continue;

                // san hit.
                SpendActorSanity(a, sanCost);

                // msg.
                if (whoDoesTheAction == a)
                {
                    if (a.IsPlayer)
                        AddMessage(new Message("That was a very disturbing thing to do...", loc.Map.LocalTime.TurnCounter, Color.Orange));
                    else if (IsVisibleToPlayer(a))
                        AddMessage(MakeMessage(a, String.Format("{0} done something very disturbing...", Conjugate(a, VERB_HAVE))));
                }
                else
                {
                    if (a.IsPlayer)
                        AddMessage(new Message(String.Format("Seeing {0} is very disturbing...", what), loc.Map.LocalTime.TurnCounter, Color.Orange));
                    else if (IsVisibleToPlayer(a))
                        AddMessage(MakeMessage(a, String.Format("{0} something very disturbing...", Conjugate(a, VERB_SEE))));
                }
            }
        }
        #endregion
        #region Special map events
        void OnMapPowerGeneratorSwitch(Location location, PowerGenerator powGen)
        {
            Map map = location.Map;
            //////////////////////////////////////////////////////
            // Maps:
            // 1. CHAR Underground Facility.
            // 2. Subway
            // 3. Police Station Jails.
            // 4. Hospital Power.
            /////////////////////////////////////////////////////

            // 1. CHAR Underground Facility
            // Darkness->Lit, TODO: Elevator off->on.
            #region
            if (map == m_Session.UniqueMaps.CHARUndergroundFacility.TheMap)
            {
                lock (m_Session) // thread safe
                {
                    // check all power generators are on.
                    bool allAreOn = m_Rules.ComputeMapPowerRatio(map) >= 1.0f;

                    // change map lighting.
                    if (allAreOn)
                    {
                        if (map.Lighting != Lighting.LIT)
                        {
                            map.Lighting = Lighting.LIT;

                            // message.
                            if (m_Player.Location.Map == map)
                            {
                                ClearMessages();
                                AddMessage(new Message("The Facility lights turn on!", map.LocalTime.TurnCounter, Color.Green));
                                RedrawPlayScreen();
                            }

                            // achievement?
                            if (!m_Session.Scoring.HasCompletedAchievement(Achievement.IDs.CHAR_POWER_UNDERGROUND_FACILITY))
                            {
                                // completed!
                                m_Session.Scoring.SetCompletedAchievement(Achievement.IDs.CHAR_POWER_UNDERGROUND_FACILITY);

                                // achievement!
                                ShowNewAchievement(Achievement.IDs.CHAR_POWER_UNDERGROUND_FACILITY);
                            }
                        }
                    }
                    else // part off
                    {
                        if (map.Lighting != Lighting.DARKNESS)
                        {
                            map.Lighting = Lighting.DARKNESS;

                            // message.
                            if (m_Player.Location.Map == map)
                            {
                                ClearMessages();
                                AddMessage(new Message("The Facility lights turn off!", map.LocalTime.TurnCounter, Color.Red));
                                RedrawPlayScreen();
                            }
                        }
                    }
                }
            }
            #endregion

            // 2. Subway
            // Darkness->Lit, Gates->open.
            #region
            if (map == map.District.SubwayMap)
            {
                lock (m_Session) // thread safe
                {
                    // check all power generators are on.
                    bool allAreOn = m_Rules.ComputeMapPowerRatio(map) >= 1.0f;

                    // change map lighting, open/close fences.
                    if (allAreOn)
                    {
                        if (map.Lighting != Lighting.LIT)
                        {
                            // lit.
                            map.Lighting = Lighting.LIT;

                            // message.
                            if (m_Player.Location.Map == map)
                            {
                                ClearMessages();
                                AddMessage(new Message("The station power turns on!", map.LocalTime.TurnCounter, Color.Green));
                                AddMessage(new Message("You hear the gates opening.", map.LocalTime.TurnCounter, Color.Green));
                                RedrawPlayScreen();
                            }

                            // open iron gates.
                            DoOpenSubwayGates(map);
                        }
                    }
                    else // part off
                    {
                        if (map.Lighting != Lighting.DARKNESS)
                        {
                            // message.
                            if (m_Player.Location.Map == map)
                            {
                                ClearMessages();
                                AddMessage(new Message("The station power turns off!", map.LocalTime.TurnCounter, Color.Red));
                                AddMessage(new Message("You hear the gates closing.", map.LocalTime.TurnCounter, Color.Red));
                                RedrawPlayScreen();
                            }

                            // darkness.
                            map.Lighting = Lighting.DARKNESS;

                            // close iron gates.
                            DoCloseSubwayGates(map);

                        }
                    }
                }
            }
            #endregion

            // 3. Police Station Jails.
            #region
            if (map == m_Session.UniqueMaps.PoliceStation_JailsLevel.TheMap)
            {
                lock (m_Session) // thread safe
                {
                    // check all power generators are on.
                    bool allAreOn = m_Rules.ComputeMapPowerRatio(map) >= 1.0f;

                    // open/close cells.
                    if (allAreOn)
                    {
                        // message.
                        if (m_Player.Location.Map == map)
                        {
                            ClearMessages();
                            AddMessage(new Message("The cells are opening.", map.LocalTime.TurnCounter, Color.Green));
                            RedrawPlayScreen();
                        }

                        // open cells.
                        DoOpenPoliceJailCells(map);
                    }
                    else
                    {
                        // message.
                        if (m_Player.Location.Map == map)
                        {
                            ClearMessages();
                            AddMessage(new Message("The cells are closing.", map.LocalTime.TurnCounter, Color.Green));
                            RedrawPlayScreen();
                        }

                        // open cells.
                        DoClosePoliceJailCells(map);
                    }
                }
            }
            #endregion

            // 4. Hospital Power.
            #region
            if (map == m_Session.UniqueMaps.Hospital_Power.TheMap)
            {
                lock (m_Session) // thread safe
                {
                    // check all power generators are on.
                    bool allAreOn = m_Rules.ComputeMapPowerRatio(map) >= 1.0f;

                    // open/close cells.
                    if (allAreOn)
                    {
                        // message.
                        if (m_Player.Location.Map == map)
                        {
                            ClearMessages();
                            AddMessage(new Message("The lights turn on and you hear something opening upstairs.", map.LocalTime.TurnCounter, Color.Green));
                            RedrawPlayScreen();
                        }

                        // turn power on.
                        DoHospitalPowerOn();
                    }
                    else
                    {
                        if (map.Lighting != Lighting.DARKNESS)
                        {
                            // message.
                            if (m_Player.Location.Map == map)
                            {
                                ClearMessages();
                                AddMessage(new Message("The lights turn off and you hear something closing upstairs.", map.LocalTime.TurnCounter, Color.Green));
                                RedrawPlayScreen();
                            }

                            // turn power off.
                            DoHospitalPowerOff();
                        }
                    }
                }
            }
            #endregion
        }

        // alpha10.1 common code for checking crushing closing gates.
        // they do not insta-kill the actor anymore but inflict (large) damage and can't close if the actor is still there.
        /// <summary>
        ///
        /// </summary>
        /// <param name="gate"></param>
        /// <param name="crushingDamage">damage to inflict, bypass protection</param>
        /// <returns>true if the gate can close, false if it must stay open</returns>
        bool CheckForGateClosingCrush(MapObject gate, int crushingDamage)
        {
            Actor crushedActor = gate.Location.Map.GetActorAt(gate.Location.Position);
            if (crushedActor == null)
                return true;
            if (crushedActor.IsInvincible)
                return false;

            InflictDamage(crushedActor, crushingDamage);
            if (IsVisibleToPlayer(crushedActor))
            {
                AddMessage(MakeMessage(crushedActor, String.Format("is crushed for {0} damage!", crushingDamage)));
                AddOverlay(new OverlayImage(MapToScreen(crushedActor.Location.Position), GameImages.ICON_MELEE_DAMAGE));
                AddOverlay(new OverlayText(MapToScreen(crushedActor.Location.Position).Add(DAMAGE_DX, DAMAGE_DY), Color.White, crushingDamage.ToString(), Color.Black));
                RedrawPlayScreen();
                AnimDelay(crushedActor.IsPlayer ? DELAY_NORMAL : DELAY_SHORT);
                ClearOverlays();
                RedrawPlayScreen();
            }

            if (crushedActor.HitPoints <= 0)
            {
                KillActor(null, crushedActor, "crushed");
                return true;
            }
            else
                return false;
        }


        void DoOpenSubwayGates(Map map)
        {
            foreach (MapObject obj in map.MapObjects)
            {
                if (obj.ImageID == GameImages.OBJ_GATE_CLOSED)
                {
                    obj.IsWalkable = true;
                    obj.ImageID = GameImages.OBJ_GATE_OPEN;
                }
            }
        }

        void DoCloseSubwayGates(Map map)
        {
            foreach (MapObject obj in map.MapObjects)
            {
                if (obj.ImageID == GameImages.OBJ_GATE_OPEN)
                {
                    // alpha10.1
                    if (CheckForGateClosingCrush(obj, Rules.CRUSHING_GATES_DAMAGE))
                    {
                        obj.IsWalkable = false;
                        obj.ImageID = GameImages.OBJ_GATE_CLOSED;
                    }
                    /* obsolete
                    obj.IsWalkable = false;
                    obj.ImageID = GameImages.OBJ_GATE_CLOSED;
                    Actor crushedActor = map.GetActorAt(obj.Location.Position);
                    if (crushedActor != null && !crushedActor.IsInvincible) // alpha10
                    {
                        KillActor(null, crushedActor, "crushed");
                        if (m_Player.Location.Map == map)
                        {
                            AddMessage(new Message("Someone got crushed between the closing gates!", map.LocalTime.TurnCounter, Color.Red));
                            RedrawPlayScreen();
                        }
                    }
                    */
                }
            }
        }

        void DoOpenPoliceJailCells(Map map)
        {
            foreach (MapObject obj in map.MapObjects)
            {
                if (obj.ImageID == GameImages.OBJ_GATE_CLOSED)
                {
                    obj.IsWalkable = true;
                    obj.ImageID = GameImages.OBJ_GATE_OPEN;
                }
            }
        }

        void DoClosePoliceJailCells(Map map)
        {
            foreach (MapObject obj in map.MapObjects)
            {
                if (obj.ImageID == GameImages.OBJ_GATE_OPEN)
                {
                    // alpha10.1
                    if (CheckForGateClosingCrush(obj, Rules.CRUSHING_GATES_DAMAGE))
                    {
                        obj.IsWalkable = false;
                        obj.ImageID = GameImages.OBJ_GATE_CLOSED;
                    }
                    /* obsolete
                    obj.IsWalkable = false;
                    obj.ImageID = GameImages.OBJ_GATE_CLOSED;
                    Actor crushedActor = map.GetActorAt(obj.Location.Position);
                    if (crushedActor != null && !crushedActor.IsInvincible) // alpha10
                    {
                        KillActor(null, crushedActor, "crushed");
                        if (m_Player.Location.Map == map)
                        {
                            AddMessage(new Message("Someone got crushed between the closing cells!", map.LocalTime.TurnCounter, Color.Red));
                            RedrawPlayScreen();
                        }
                    }
                    */
                }
            }
        }

        void DoHospitalPowerOn()
        {
            // turn all hospital lights on.
            m_Session.UniqueMaps.Hospital_Admissions.TheMap.Lighting = Lighting.LIT;
            m_Session.UniqueMaps.Hospital_Offices.TheMap.Lighting = Lighting.LIT;
            m_Session.UniqueMaps.Hospital_Patients.TheMap.Lighting = Lighting.LIT;
            m_Session.UniqueMaps.Hospital_Power.TheMap.Lighting = Lighting.LIT;
            m_Session.UniqueMaps.Hospital_Storage.TheMap.Lighting = Lighting.LIT;

            // open storage gates.
            foreach (MapObject obj in m_Session.UniqueMaps.Hospital_Storage.TheMap.MapObjects)
            {
                if (obj.ImageID == GameImages.OBJ_GATE_CLOSED)
                {
                    obj.IsWalkable = true;
                    obj.ImageID = GameImages.OBJ_GATE_OPEN;
                }
            }
        }

        void DoHospitalPowerOff()
        {
            // turn all hospital lights off.
            m_Session.UniqueMaps.Hospital_Admissions.TheMap.Lighting = Lighting.DARKNESS;
            m_Session.UniqueMaps.Hospital_Offices.TheMap.Lighting = Lighting.DARKNESS;
            m_Session.UniqueMaps.Hospital_Patients.TheMap.Lighting = Lighting.DARKNESS;
            m_Session.UniqueMaps.Hospital_Power.TheMap.Lighting = Lighting.DARKNESS;
            m_Session.UniqueMaps.Hospital_Storage.TheMap.Lighting = Lighting.DARKNESS;

            // close storage gate.
            Map map = m_Session.UniqueMaps.Hospital_Storage.TheMap;
            foreach (MapObject obj in map.MapObjects)
            {
                if (obj.ImageID == GameImages.OBJ_GATE_OPEN)
                {
                    // alpha10.1
                    if (CheckForGateClosingCrush(obj, Rules.CRUSHING_GATES_DAMAGE))
                    {
                        obj.IsWalkable = false;
                        obj.ImageID = GameImages.OBJ_GATE_CLOSED;
                    }
                    /* obsolete
                    obj.IsWalkable = false;
                    obj.ImageID = GameImages.OBJ_GATE_CLOSED;
                    Actor crushedActor = map.GetActorAt(obj.Location.Position);
                    if (crushedActor != null)
                    {
                        KillActor(null, crushedActor, "crushed");
                        if (m_Player.Location.Map == map)
                        {
                            AddMessage(new Message("Someone got crushed between the closing gate!", map.LocalTime.TurnCounter, Color.Red));
                            RedrawPlayScreen();
                        }
                    }
                    */
                }
            }
        }

        void DoTurnAllGeneratorsOn(Map map)
        {
            foreach (MapObject obj in map.MapObjects)
            {
                PowerGenerator powGen = obj as PowerGenerator;
                if (powGen == null)
                    continue;
                if (!powGen.IsOn)
                {
                    powGen.TogglePower();
                    OnMapPowerGeneratorSwitch(powGen.Location, powGen);
                }
            }
        }
        #endregion
        #region Fun Facts!
        [Flags]
        enum MapListFlags
        {
            NONE = 0,

            /// <summary>
            /// Exclude map with the IsSecret property.
            /// </summary>
            EXCLUDE_SECRET_MAPS = (1 << 0)
        }

        List<Actor> ListWorldActors(Predicate<Actor> pred, MapListFlags flags)
        {
            List<Actor> list = new List<Actor>();

            for (int dx = 0; dx < m_Session.World.Size; dx++)
                for (int dy = 0; dy < m_Session.World.Size; dy++)
                    list.AddRange(ListDistrictActors(m_Session.World[dx, dy], flags, pred));

            return list;
        }

        List<Actor> ListDistrictActors(District d, MapListFlags flags, Predicate<Actor> pred)
        {
            List<Actor> list = new List<Actor>();

            foreach (Map m in d.Maps)
            {
                if ((flags & MapListFlags.EXCLUDE_SECRET_MAPS) != 0 && m.IsSecret)
                    continue;
                foreach (Actor a in m.Actors)
                    if (pred == null || pred(a))
                        list.Add(a);
            }

            return list;
        }

        string FunFactActorResume(Actor a, string info)
        {
            if (a == null)
                return "(N/A)";
            return String.Format("{0} - {1}, a {2} - {3}",
                info, a.TheName, a.Model.Name, a.Location.Map.Name);
        }

        string[] CompileDistrictFunFacts(District d)
        {
            List<string> list = new List<string>();

            ///////////////////////////////////////////
            // 1. Oldest actors alive living & undead.
            // 2. Most kills living & undead.
            // 3. Most murders.
            ///////////////////////////////////////////

            // list actors.
            List<Actor> allLivings = ListDistrictActors(d, MapListFlags.EXCLUDE_SECRET_MAPS, (a) => !a.IsDead && !a.Model.Abilities.IsUndead);
            List<Actor> allUndeads = ListDistrictActors(d, MapListFlags.EXCLUDE_SECRET_MAPS, (a) => !a.IsDead && a.Model.Abilities.IsUndead);
            List<Actor> allActors = ListDistrictActors(d, MapListFlags.EXCLUDE_SECRET_MAPS, null);

            // add player (cause he's dead now)
            if (m_Player.Model.Abilities.IsUndead)
                allUndeads.Add(m_Player);
            else
                allLivings.Add(m_Player);
            allActors.Add(m_Player);

            // 1. Oldest actors alive living & undead.
            if (allLivings.Count > 0)
            {
                allLivings.Sort((a, b) => a.SpawnTime < b.SpawnTime ? -1 : a.SpawnTime == b.SpawnTime ? 0 : 1);
                list.Add("- Oldest Livings Surviving");
                list.Add(String.Format("    1st {0}.", FunFactActorResume(allLivings[0], new WorldTime(allLivings[0].SpawnTime).ToString())));
                if (allLivings.Count > 1)
                    list.Add(String.Format("    2nd {0}.", FunFactActorResume(allLivings[1], new WorldTime(allLivings[1].SpawnTime).ToString())));
            }
            else
                list.Add("    No living actors alive!");

            if (allUndeads.Count > 0)
            {
                allUndeads.Sort((a, b) => a.SpawnTime < b.SpawnTime ? -1 : a.SpawnTime == b.SpawnTime ? 0 : 1);
                list.Add("- Oldest Undeads Rotting Around");
                list.Add(String.Format("    1st {0}.", FunFactActorResume(allUndeads[0], new WorldTime(allUndeads[0].SpawnTime).ToString())));
                if (allUndeads.Count > 1)
                    list.Add(String.Format("    2nd {0}.", FunFactActorResume(allUndeads[1], new WorldTime(allUndeads[1].SpawnTime).ToString())));
            }
            else
                list.Add("    No undeads shambling around!");

            // 2. Most kills living & undead.
            if (allLivings.Count > 0)
            {
                allLivings.Sort((a, b) => a.KillsCount > b.KillsCount ? -1 : a.KillsCount == b.KillsCount ? 0 : 1);
                list.Add("- Deadliest Livings Kicking ass");
                if (allLivings[0].KillsCount > 0)
                {
                    list.Add(String.Format("    1st {0}.", FunFactActorResume(allLivings[0], allLivings[0].KillsCount.ToString())));
                    if (allLivings.Count > 1 && allLivings[1].KillsCount > 0)
                        list.Add(String.Format("    2nd {0}.", FunFactActorResume(allLivings[1], allLivings[1].KillsCount.ToString())));
                }
                else
                    list.Add("    Livings can't fight for their lives apparently.");
            }
            if (allUndeads.Count > 0)
            {
                allUndeads.Sort((a, b) => a.KillsCount > b.KillsCount ? -1 : a.KillsCount == b.KillsCount ? 0 : 1);
                list.Add("- Deadliest Undeads Chewing Brains");
                if (allUndeads[0].KillsCount > 0)
                {
                    list.Add(String.Format("    1st {0}.", FunFactActorResume(allUndeads[0], allUndeads[0].KillsCount.ToString())));
                    if (allUndeads.Count > 1 && allUndeads[1].KillsCount > 0)
                        list.Add(String.Format("    2nd {0}.", FunFactActorResume(allUndeads[1], allUndeads[1].KillsCount.ToString())));
                }
                else
                    list.Add("    Undeads don't care for brains apparently.");
            }

            // 3. Most murders.
            if (allLivings.Count > 0)
            {
                allLivings.Sort((a, b) => a.MurdersCounter > b.MurdersCounter ? -1 : a.MurdersCounter == b.MurdersCounter ? 0 : 1);
                list.Add("- Most Murderous Murderer Murdering");
                if (allLivings[0].MurdersCounter > 0)
                {
                    list.Add(String.Format("    1st {0}.", FunFactActorResume(allLivings[0], allLivings[0].MurdersCounter.ToString())));
                    if (allLivings.Count > 1 && allLivings[1].MurdersCounter > 0)
                        list.Add(String.Format("    2nd {0}.", FunFactActorResume(allLivings[1], allLivings[1].MurdersCounter.ToString())));
                }
                else
                    list.Add("    No murders committed!");
            }

            // done.
            return list.ToArray();
        }
        #endregion
    }
}
