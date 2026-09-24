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
        #region View & Drawing

        #region View
        public void ComputeViewRect(Point mapCenter)
        {
            int left = mapCenter.X - HALF_VIEW_WIDTH;
            int right = mapCenter.X + HALF_VIEW_WIDTH;

            int top = mapCenter.Y - HALF_VIEW_HEIGHT;
            int bottom = mapCenter.Y + HALF_VIEW_HEIGHT;

            m_MapViewRect = new Rectangle(left, top, 1 + right - left, 1 + bottom - top);
        }

        public bool IsInViewRect(Point mapPosition)
        {
            return m_MapViewRect.Contains(mapPosition);
        }
        #endregion

        public void RedrawPlayScreen()
        {
            // alpha10 dont display some infos
            bool canSeeSky = m_Rules.CanActorSeeSky(m_Player);
            bool canKnowTime = m_Rules.CanActorKnowTime(m_Player);

            // get mutex.
            Monitor.Enter(m_UI);

            m_UI.UI_Clear(Color.Black);
            {
                // map & minimap
                Color mapTint = Color.White; // disabled changing brightness bad for the eyes TintForDayPhase(m_Session.WorldTime.Phase);
                m_UI.UI_DrawLine(Color.DarkGray, RIGHTPANEL_X, 0, RIGHTPANEL_X, MESSAGES_Y);
                DrawMap(m_Session.CurrentMap, mapTint);

                m_UI.UI_DrawLine(Color.DarkGray, RIGHTPANEL_X, MINIMAP_Y - 4, CANVAS_WIDTH, MINIMAP_Y - 4);
                DrawMiniMap(m_Session.CurrentMap);

                // messages
                m_UI.UI_DrawLine(Color.DarkGray, MESSAGES_X, MESSAGES_Y - 1, CANVAS_WIDTH, MESSAGES_Y - 1);
                DrawMessages();

                // location info.
                #region
                //    x0            x1
                // y0 <map name>
                // y1 <zone name>
                // y2 <day>        <dayphase>
                // y3 <hour>       <weather>/<lighting>
                // y4 <turn>       <scoring>@<difficulty> <mode>
                // y5 <life>/<lives>
                // y6 <murders>
                const int X0 = LOCATIONPANEL_TEXT_X;
                const int X1 = LOCATIONPANEL_TEXT_X + 128;
                const int Y0 = LOCATIONPANEL_TEXT_Y;
                const int Y1 = Y0 + LINE_SPACING;
                const int Y2 = Y1 + LINE_SPACING;
                const int Y3 = Y2 + LINE_SPACING;
                const int Y4 = Y3 + LINE_SPACING;
                const int Y5 = Y4 + LINE_SPACING;
                const int Y6 = Y5 + LINE_SPACING;

                m_UI.UI_DrawLine(Color.DarkGray, LOCATIONPANEL_X, LOCATIONPANEL_Y, LOCATIONPANEL_X, CANVAS_HEIGHT);
                m_UI.UI_DrawString(Color.White, m_Session.CurrentMap.Name, X0, Y0);
                m_UI.UI_DrawString(Color.White, LocationText(m_Session.CurrentMap, m_Player), X0, Y1);
                m_UI.UI_DrawString(Color.White, String.Format("Day  {0}", m_Session.WorldTime.Day), X0, Y2);
                if (canKnowTime)
                    m_UI.UI_DrawString(Color.White, String.Format("Hour {0}", m_Session.WorldTime.Hour), X0, Y3);
                else
                    m_UI.UI_DrawString(Color.White, "Hour ??", X0, Y3);

                // alpha10 desc day fov effect, not if cant know time
                string dayPhaseString;
                if (canKnowTime)
                {
                    dayPhaseString = DescribeDayPhase(m_Session.WorldTime.Phase);
                    int timeFovPenalty = m_Rules.NightFovPenalty(m_Player, m_Session.WorldTime);
                    if (timeFovPenalty != 0)
                        dayPhaseString += "  fov -" + timeFovPenalty;
                }
                else
                {
                    dayPhaseString = "???";
                }

                m_UI.UI_DrawString(m_Session.WorldTime.IsNight ? NIGHT_COLOR : DAY_COLOR, dayPhaseString, X1, Y2);

                Color weatherOrLightingColor;
                string weatherOrLightingString;
                switch (m_Session.CurrentMap.Lighting)
                {
                    case Lighting.OUTSIDE:
                        weatherOrLightingColor = WeatherColor(m_Session.World.Weather);
                        // alpha10 only show weather if can see it
                        if (m_Rules.CanActorSeeSky(m_Player))
                        {
                            weatherOrLightingString = DescribeWeather(m_Session.World.Weather);
                            // alpha10 desc weather fov effect
                            int fovPenalty = m_Rules.WeatherFovPenalty(m_Player, m_Session.World.Weather);
                            if (fovPenalty != 0)
                                weatherOrLightingString += "  fov -" + fovPenalty;
                        }
                        else
                            weatherOrLightingString = "???";
                        break;
                    case Lighting.DARKNESS:
                        weatherOrLightingColor = Color.Blue;
                        weatherOrLightingString = "Darkness";
                        // alpha10 desc darkness fov effect
                        int darknessFov = m_Rules.DarknessFov(m_Player);
                        if (darknessFov != m_Player.Sheet.BaseViewRange)
                            weatherOrLightingString += "  fov " + darknessFov;
                        break;
                    case Lighting.LIT:
                        weatherOrLightingColor = Color.Yellow;
                        weatherOrLightingString = "Lit";
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("unhandled lighting");
                }
                m_UI.UI_DrawString(weatherOrLightingColor, weatherOrLightingString, X1, Y3);
                m_UI.UI_DrawString(Color.White, String.Format("Turn {0}", m_Session.WorldTime.TurnCounter), X0, Y4);
                m_UI.UI_DrawString(Color.White, String.Format("Score   {0}@{1}% {2}", m_Session.Scoring.TotalPoints, (int)(100 * Scoring.ComputeDifficultyRating(s_Options, m_Session.Scoring.Side, m_Session.Scoring.ReincarnationNumber)), Session.DescShortGameMode(m_Session.GameMode)), X1, Y4);
                m_UI.UI_DrawString(Color.White, String.Format("Avatar  {0}/{1}", (1 + m_Session.Scoring.ReincarnationNumber), (1 + s_Options.MaxReincarnations)), X1, Y5);
                if (m_Player.MurdersCounter > 0)
                    m_UI.UI_DrawString(Color.White, String.Format("Murders {0}", m_Player.MurdersCounter), X1, Y6);
                #endregion

                // character status.
                if (m_Player != null)
                    DrawActorStatus(m_Player, RIGHTPANEL_TEXT_X, RIGHTPANEL_TEXT_Y);

                // inventories.
                if (m_Player != null)
                {
                    if (m_Player.Inventory != null && m_Player.Model.Abilities.HasInventory)
                        DrawInventory(m_Player.Inventory, "Inventory", true, INVENTORY_SLOTS_PER_LINE, m_Player.Inventory.MaxCapacity, INVENTORYPANEL_X, INVENTORYPANEL_Y);
                    DrawInventory(m_Player.Location.Map.GetItemsAt(m_Player.Location.Position), "Items on ground", true, INVENTORY_SLOTS_PER_LINE, Map.GROUND_INVENTORY_SLOTS, INVENTORYPANEL_X, GROUNDINVENTORYPANEL_Y);
                    DrawCorpsesList(m_Player.Location.Map.GetCorpsesAt(m_Player.Location.Position), "Corpses on ground", INVENTORY_SLOTS_PER_LINE, INVENTORYPANEL_X, CORPSESPANEL_Y);
                }

                // character skills.
                if (m_Player != null && m_Player.Sheet.SkillTable != null && m_Player.Sheet.SkillTable.CountSkills > 0)
                    DrawActorSkillTable(m_Player, RIGHTPANEL_TEXT_X, SKILLTABLE_Y);

                // overlays
                m_Overlays.Draw(m_UI);
                DrawMouseMovePreview();

                // DEV STATS
#if DEBUG
                if (s_Options.DEV_ShowActorsStats)
                {
                    int countLiving, countUndead;
                    countLiving = CountLivings(m_Session.CurrentMap);
                    countUndead = CountUndeads(m_Session.CurrentMap);
                    m_UI.UI_DrawString(Color.White, String.Format("Living {0} vs {1} Undead", countLiving, countUndead), RIGHTPANEL_TEXT_X, SKILLTABLE_Y - 32);
                }
#endif
            }

            m_UI.UI_Repaint();

            // release mutex.
            Monitor.Exit(m_UI);
        }

        string LocationText(Map map, Actor actor)
        {
            if (map == null || actor == null)
                return "";

            StringBuilder sb = new StringBuilder(String.Format("({0},{1}) ", actor.Location.Position.X, actor.Location.Position.Y));

            List<Zone> zones = map.GetZonesAt(actor.Location.Position.X, actor.Location.Position.Y);
            if (zones == null || zones.Count == 0)
                return sb.ToString();

            foreach (Zone z in zones)
                sb.Append(String.Format("{0} ", z.Name));

            return sb.ToString();
        }

        /// <summary>
        /// OBSOLETE
        /// </summary>
        /// <param name="phase"></param>
        /// <returns></returns>
        Color TintForDayPhase(DayPhase phase)
        {
            switch (phase)
            {
                case DayPhase.MORNING:
                case DayPhase.MIDDAY:
                case DayPhase.AFTERNOON:
                    return TINT_DAY;

                case DayPhase.SUNRISE:
                    return TINT_SUNRISE;

                case DayPhase.SUNSET:
                    return TINT_SUNSET;

                case DayPhase.MIDNIGHT:
                    return TINT_MIDNIGHT;

                case DayPhase.DEEP_NIGHT:
                    return TINT_NIGHT;

                case DayPhase.EVENING:
                    return TINT_EVENING;
                default:
                    throw new ArgumentOutOfRangeException("unhandled dayphase");
            }
        }


        #region Overlays
        void AddOverlay(Overlay o)
        {
            m_Overlays.Add(o);
        }

        void ClearOverlays()
        {
            m_Overlays.Clear();
        }

        void RemoveOverlay(Overlay o)
        {
            m_Overlays.Remove(o);
        }

        // alpha10
        bool HasOverlay(Overlay o)
        {
            return m_Overlays.Contains(o);
        }
        #endregion

        #region Coordinates conversion
        Point MapToScreen(Point mapPosition)
        {
            return MapToScreen(mapPosition.X, mapPosition.Y);
        }

        Point MapToScreen(int x, int y)
        {
            return new Point((x - m_MapViewRect.Left) * RogueGame.TILE_SIZE, (y - m_MapViewRect.Top) * RogueGame.TILE_SIZE);
        }

        Point ScreenToMap(Point screenPosition)
        {
            return ScreenToMap(screenPosition.X, screenPosition.Y);
        }

        Point ScreenToMap(int gx, int gy)
        {
            return new Point(m_MapViewRect.Left + gx / RogueGame.TILE_SIZE, m_MapViewRect.Top + gy / RogueGame.TILE_SIZE);
        }

        Point MouseToMap(Point mousePosition)
        {
            return MouseToMap(mousePosition.X, mousePosition.Y);
        }

        Point MouseToMap(int mouseX, int mouseY)
        {
            mouseX = (int)(mouseX / m_UI.UI_GetCanvasScaleX());
            mouseY = (int)(mouseY / m_UI.UI_GetCanvasScaleY());
            return ScreenToMap(mouseX, mouseY);
        }

        Point MouseToInventorySlot(int invX, int invY, int mouseX, int mouseY)
        {
            mouseX = (int)(mouseX / m_UI.UI_GetCanvasScaleX());
            mouseY = (int)(mouseY / m_UI.UI_GetCanvasScaleY());

            return new Point((mouseX - invX) / 32, (mouseY - invY) / 32);
        }

        Point InventorySlotToScreen(int invX, int invY, int slotX, int slotY)
        {
            return new Point(invX + slotX * 32, invY + slotY * 32);
        }
        #endregion

        #region Visibility/Smell & memory helpers
        bool IsVisibleToPlayer(Location location)
        {
            return IsVisibleToPlayer(location.Map, location.Position);
        }

        bool IsVisibleToPlayer(Map map, Point position)
        {
            return m_Player != null
                && map == m_Player.Location.Map && map.IsInBounds(position.X, position.Y)
                && map.GetTileAt(position.X, position.Y).IsInView;
        }

        bool IsVisibleToPlayer(Actor actor)
        {
            return actor == m_Player || IsVisibleToPlayer(actor.Location);
        }

        bool IsVisibleToPlayer(MapObject mapObj)
        {
            return IsVisibleToPlayer(mapObj.Location);
        }

        bool IsKnownToPlayer(Map map, Point position)
        {
            return map.IsInBounds(position.X, position.Y) && map.GetTileAt(position.X, position.Y).IsVisited;
        }

        bool IsKnownToPlayer(Location location)
        {
            return IsKnownToPlayer(location.Map, location.Position);
        }

        bool IsKnownToPlayer(MapObject mapObj)
        {
            return IsKnownToPlayer(mapObj.Location);
        }

        bool IsPlayerSleeping()
        {
            return m_Player != null && m_Player.IsSleeping;
        }
        #endregion

        #region Text helpers
        int FindLongestLine(string[] lines)
        {
            if (lines == null || lines.Length == 0)
                return 0;

            int max = Int32.MinValue;

            foreach (string s in lines)
            {
                if (s == null)  // sanity check.
                    continue;
                if (s.Length > max)
                    max = s.Length;
            }

            return max;
        }
        #endregion

        #endregion
    }
}
