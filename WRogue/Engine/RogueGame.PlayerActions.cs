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
        #region Handling Player/AI actor action

        void HandlePlayerActor(Actor player)
        {
            PlayerInputReader inputReader = new PlayerInputReader(new UiPlayerInputSource(m_UI));
            // Upkeep.
            UpdatePlayerFOV(player);    // make sure LOS is up to date.
            m_Player = player;      // remember player.
            ComputeViewRect(player.Location.Position);
            m_MouseMoveHover = null;
            m_MouseMovePreview = null;

            // Update survival scoring.
            m_Session.Scoring.TurnsSurvived = m_Session.WorldTime.TurnCounter;

            // Check if long wait.
            #region
            if (m_IsPlayerLongWait)
            {
                if (CheckPlayerWaitLong(player))
                {
                    // continue waiting.
                    DoWait(player);
                    return;
                }
                else
                {
                    // stop long wait.
                    m_IsPlayerLongWait = false;
                    m_IsPlayerLongWaitForcedStop = false;

                    // wait ended or interrupted.
                    if (m_Session.WorldTime.TurnCounter >= m_PlayerLongWaitEnd.TurnCounter)
                    {
                        AddMessage(new Message("Wait ended.", m_Session.WorldTime.TurnCounter, Color.Yellow));
                    }
                    else
                    {
                        AddMessage(new Message("Wait interrupted!", m_Session.WorldTime.TurnCounter, Color.Red));
                    }
                }
            }
            #endregion

            if (ContinueMouseMove(player))
                return;

            /////////////////////////////////////////////////
            // Loop until the player has made a valid choice
            /////////////////////////////////////////////////
            bool loop = true;
            do
            {
                ///////////////////
                // 1. Redraw
                // 2. Get input.
                // 3. Handle input
                ///////////////////

                // 1. Redraw
                m_UI.UI_SetCursor(null);

                // alpha10.1 bot mode?
#if DEBUG
                lock (m_botLock)
                {
                    if (m_isBotMode)
                    {
                        try { Thread.Sleep(BOT_DELAY); } catch { }  // AnimDelay() does not work here because it just pause the ui
                        RedrawPlayScreen();
                        if (m_botControl != null) // for some reason even with the lock this can become null here. wth?? is thread.sleep the culprit??
                        {
                            ActorAction botAction = m_botControl.GetAction(this);
                            if (botAction == null || !botAction.IsLegal())
                            {
                                AddMessage(MakeErrorMessage("Bot issued " + (botAction == null ? "NULL" : "illegal " + botAction.ToString()) + " action"));
                                botAction = new ActionWait(player, this);
                            }
                            botAction.Perform();
                            // copy-paste is bad
                            UpdatePlayerFOV(player);
                            ComputeViewRect(player.Location.Position);
                            m_Session.LastTurnPlayerActed = m_Session.WorldTime.TurnCounter;
                            RedrawPlayScreen();
                        }
                        return;
                    }
                }
#endif

                // hint available?
                // alpha10 no hint if undead
                if (m_Player != null && !m_Player.IsDead && !m_Player.Model.Abilities.IsUndead)
                {
                    // alpha10 fix properly handle hint overlay
                    int availableHint = -1;
                    if (s_Options.IsAdvisorEnabled && (availableHint = GetAdvisorFirstAvailableHint()) != -1)
                    {
                        Point overlayPos = MapToScreen(m_Player.Location.Position.X - 3, m_Player.Location.Position.Y - 1);
                        if (m_HintAvailableOverlay == null)
                        {
                            m_HintAvailableOverlay = new OverlayPopup(
                                null,
                                Color.White, Color.White, Color.Black,
                                overlayPos);
                            AddOverlay(m_HintAvailableOverlay);
                        }
                        else
                        {
                            m_HintAvailableOverlay.ScreenPosition = overlayPos;
                            if (!HasOverlay(m_HintAvailableOverlay))
                                AddOverlay(m_HintAvailableOverlay);
                        }

                        string hintTitle;
                        string[] hintBody;
                        GetAdvisorHintText((AdvisorHint)availableHint, out hintTitle, out hintBody);
                        m_HintAvailableOverlay.Lines = new string[] {
                            string.Format("HINT AVAILABLE PRESS <{0}>", s_KeyBindings.Get(PlayerCommand.ADVISOR).ToString()),
                            hintTitle };
                    }
                    else if (m_HintAvailableOverlay != null && HasOverlay(m_HintAvailableOverlay))
                    {
                        RemoveOverlay(m_HintAvailableOverlay);
                    }
                }
                RedrawPlayScreen();

                // 2. Get input.
                PlayerInputEvent input = inputReader.Read(m_MouseMoveInterruptedKey);
                m_MouseMoveInterruptedKey = null;
                bool hasKey = input.Key != null;
                KeyEventArgs inKey = input.Key;
                Point mousePos = input.MousePosition;
                MouseButtons? mouseButtons = input.MouseButtons;


                // 3. Handle input
                if (hasKey)
                {
                    //////////////
                    // Handle key
                    //////////////
                    #region
                    PlayerCommand command = InputTranslator.KeyToCommand(inKey);
                    if (command == PlayerCommand.QUIT_GAME)    // quit game.
                    {
                        if (HandleQuitGame())
                        {
                            // stop sim thread.
                            StopSimThread(true);  // alpha10 abort allowed when quitting
                            // quit asap.
                            RedrawPlayScreen();
                            m_IsGameRunning = false;
                            return;
                        }
                    }
                    else
                    {
                        switch (command)
                        {
                            #region options, menu etc...
                            case PlayerCommand.ABANDON_GAME:
                                if (HandleAbandonGame())
                                {
                                    StopSimThread(true); // alpha10 abort allowed when quitting
                                    loop = false;
                                    KillActor(null, m_Player, "suicide");
                                }
                                break;

                            case PlayerCommand.HELP_MODE:
                                HandleHelpMode();
                                break;

                            case PlayerCommand.HINTS_SCREEN_MODE:
                                HandleHintsScreen();
                                break;

                            case PlayerCommand.ADVISOR:
                                HandleAdvisor(player);
                                break;

                            case PlayerCommand.OPTIONS_MODE:
                                HandleOptions(true);
                                ApplyOptions(true);
                                break;

                            case PlayerCommand.KEYBINDING_MODE:
                                HandleRedefineKeys();
                                break;

                            case PlayerCommand.MESSAGE_LOG:
                                HandleMessageLog();
                                break;

                            case PlayerCommand.MOUSE_MOVE_MODE:
                                ToggleMouseMoveMode();
                                break;

                            // alpha10.1 moved sim thread responsability out to DoLoadGame
                            case PlayerCommand.LOAD_GAME:
                                // load.
                                HandleLoadGame();
                                // refresh player local variable!!
                                player = m_Player;
                                // stop looping.
                                loop = false;
                                // stop the update loop!
                                m_HasLoadedGame = true;
                                break;
                            // alpha10.1 moved sim thread responsability out to DoSaveGame
                            case PlayerCommand.SAVE_GAME:
                                HandleSaveGame();
                                break;

                            case PlayerCommand.SCREENSHOT:
                                HandleScreenshot();
                                break;

                            case PlayerCommand.CITY_INFO:
                                HandleCityInfo();
                                break;
                            #endregion

                            #region actual game actions.
                            case PlayerCommand.WAIT_OR_SELF:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = false;
                                DoWait(player);
                                break;

                            case PlayerCommand.WAIT_LONG:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = false;
                                StartPlayerWaitLong(player);
                                break;

                            case PlayerCommand.MOVE_N:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !DoPlayerBump(player, Direction.N);
                                break;
                            case PlayerCommand.MOVE_NE:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !DoPlayerBump(player, Direction.NE);
                                break;
                            case PlayerCommand.MOVE_E:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !DoPlayerBump(player, Direction.E);
                                break;
                            case PlayerCommand.MOVE_SE:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !DoPlayerBump(player, Direction.SE);
                                break;
                            case PlayerCommand.MOVE_S:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !DoPlayerBump(player, Direction.S);
                                break;
                            case PlayerCommand.MOVE_SW:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !DoPlayerBump(player, Direction.SW);
                                break;
                            case PlayerCommand.MOVE_W:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !DoPlayerBump(player, Direction.W);
                                break;
                            case PlayerCommand.MOVE_NW:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !DoPlayerBump(player, Direction.NW);
                                break;
                            case PlayerCommand.USE_EXIT:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !DoUseExit(player, player.Location.Position);
                                break;

                            case PlayerCommand.ITEM_SLOT_0:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !DoPlayerItemSlot(player, 0, inKey);
                                break;
                            case PlayerCommand.ITEM_SLOT_1:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !DoPlayerItemSlot(player, 1, inKey);
                                break;
                            case PlayerCommand.ITEM_SLOT_2:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !DoPlayerItemSlot(player, 2, inKey);
                                break;
                            case PlayerCommand.ITEM_SLOT_3:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !DoPlayerItemSlot(player, 3, inKey);
                                break;
                            case PlayerCommand.ITEM_SLOT_4:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !DoPlayerItemSlot(player, 4, inKey);
                                break;
                            case PlayerCommand.ITEM_SLOT_5:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !DoPlayerItemSlot(player, 5, inKey);
                                break;
                            case PlayerCommand.ITEM_SLOT_6:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !DoPlayerItemSlot(player, 6, inKey);
                                break;
                            case PlayerCommand.ITEM_SLOT_7:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !DoPlayerItemSlot(player, 7, inKey);
                                break;
                            case PlayerCommand.ITEM_SLOT_8:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !DoPlayerItemSlot(player, 8, inKey);
                                break;
                            case PlayerCommand.ITEM_SLOT_9:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !DoPlayerItemSlot(player, 9, inKey);
                                break;

                            case PlayerCommand.RUN_TOGGLE:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                HandlePlayerRunToggle(player);
                                break;

                            case PlayerCommand.CLOSE_DOOR:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !HandlePlayerCloseDoor(player);
                                break;
                            case PlayerCommand.BARRICADE_MODE:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !HandlePlayerBarricade(player);
                                break;
                            case PlayerCommand.BREAK_MODE:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !HandlePlayerBreak(player);
                                break;
                            case PlayerCommand.BUILD_LARGE_FORTIFICATION:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !HandlePlayerBuildFortification(player, true);
                                break;
                            case PlayerCommand.BUILD_SMALL_FORTIFICATION:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !HandlePlayerBuildFortification(player, false);
                                break;
                            case PlayerCommand.ORDER_MODE:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !HandlePlayerOrderMode(player);
                                break;
                            case PlayerCommand.PULL_MODE: // alpha10
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !HandlePlayerPull(player);
                                break;
                            case PlayerCommand.PUSH_MODE:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !HandlePlayerPush(player);
                                break;
                            case PlayerCommand.FIRE_MODE:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !HandlePlayerFireMode(player);
                                break;

                            case PlayerCommand.SHOUT:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !HandlePlayerShout(player, null);
                                break;

                            case PlayerCommand.SLEEP:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !HandlePlayerSleep(player);
                                break;

                            case PlayerCommand.SWITCH_PLACE:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !HandlePlayerSwitchPlace(player);
                                break;

                            case PlayerCommand.USE_SPRAY:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !HandlePlayerUseSpray(player);
                                break;

                            case PlayerCommand.LEAD_MODE:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !HandlePlayerTakeLead(player);
                                break;

                            case PlayerCommand.GIVE_ITEM:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !HandlePlayerGiveItem(player, mousePos);
                                break;

                            case PlayerCommand.NEGOCIATE_TRADE:  // alpha10
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !HandlePlayerNegociateTrade(player); // alpha10
                                break;

                            case PlayerCommand.MARK_ENEMIES_MODE:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                HandlePlayerMarkEnemies(player);
                                break;

                            case PlayerCommand.EAT_CORPSE:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !HandlePlayerEatCorpse(player, mousePos);
                                break;

                            case PlayerCommand.REVIVE_CORPSE:
                                if (TryPlayerInsanity())
                                {
                                    loop = false;
                                    break;
                                }
                                loop = !HandlePlayerReviveCorpse(player, mousePos);
                                break;
                            #endregion

                            case PlayerCommand.NONE:
                                break;

                            default:
                                throw new ArgumentException("command unhandled");
                        }
                    }
                    #endregion
                } // has key
                else
                {
                    ////////////////
                    // Handle mouse
                    ////////////////
                    #region
                    if (m_IsMouseMoveMode && HandleMouseMove(player, mousePos, mouseButtons, out loop))
                        continue;
                    // Look?
                    bool isLooking = HandleMouseLook(mousePos);
                    if (isLooking)
                        continue;

                    // Inventory?
                    bool hasDoneInventoryAction;
                    bool isInventory = HandleMouseInventory(mousePos, mouseButtons, out hasDoneInventoryAction);
                    if (isInventory)
                    {
                        if (hasDoneInventoryAction)
                        {
                            loop = false;
                        }
                        else
                            continue;
                    }

                    // Corpses?
                    bool hasDoneCorpsesAction;
                    bool isCorpses = HandleMouseOverCorpses(mousePos, mouseButtons, out hasDoneCorpsesAction);
                    if (isCorpses)
                    {
                        if (hasDoneCorpsesAction)
                        {
                            loop = false;
                        }
                        else
                            continue;
                    }

                    // Neither look nor inventory nor corpses, cleanup.
                    ClearOverlays();
                    #endregion
                }
            }
            while (loop);

            // Upkeep.
            UpdatePlayerFOV(player);    // make sure LOS is up to date.
            ComputeViewRect(player.Location.Position);
            m_Session.LastTurnPlayerActed = m_Session.WorldTime.TurnCounter;
        }

        bool TryPlayerInsanity()
        {
            if (!m_Rules.IsActorInsane(m_Player))
                return false;
            if (!m_Rules.RollChance(Rules.SANITY_INSANE_ACTION_CHANCE))
                return false;

            ActorAction insaneAction = GenerateInsaneAction(m_Player);
            if (insaneAction == null)
                return false;
            if (!insaneAction.IsLegal())
                return false;

            ClearMessages();
            AddMessage(new Message("(your insanity takes over)", m_Player.Location.Map.LocalTime.TurnCounter, Color.Orange));
            if (!m_Player.IsBotPlayer)
                AddMessagePressEnter();

            insaneAction.Perform();

            return true;
        }

        bool HandleQuitGame()
        {
            AddMessage(MakeYesNoMessage("REALLY QUIT GAME"));
            RedrawPlayScreen();

            bool answer = WaitYesOrNo();

            if (!answer)
                AddMessage(new Message("Good. Keep roguing!", m_Session.WorldTime.TurnCounter, Color.Yellow));
            else
                AddMessage(new Message("Bye!", m_Session.WorldTime.TurnCounter, Color.Yellow));

            return answer;
        }

        bool HandleAbandonGame()
        {
            AddMessage(MakeYesNoMessage("REALLY KILL YOURSELF"));
            RedrawPlayScreen();

            bool answer = WaitYesOrNo();

            if (!answer)
                AddMessage(new Message("Good. No reason to make the undeads life easier by removing yours!", m_Session.WorldTime.TurnCounter, Color.Yellow));
            else
                AddMessage(new Message("You can't bear the horror anymore...", m_Session.WorldTime.TurnCounter, Color.Yellow));

            return answer;
        }

        void HandleScreenshot()
        {
            // prepare.
            AddMessage(new Message("Taking screenshot...", m_Session.WorldTime.TurnCounter, Color.Yellow));
            RedrawPlayScreen();

            // shot it!
            string shotname = DoTakeScreenshot();
            if (shotname == null)
            {
                AddMessage(new Message("Could not save screenshot.", m_Session.WorldTime.TurnCounter, Color.Red));
            }
            else
            {
                AddMessage(new Message(String.Format("screenshot {0} saved.", shotname), m_Session.WorldTime.TurnCounter, Color.Yellow));
            }

            // refresh.
            RedrawPlayScreen();
        }

        string DoTakeScreenshot()
        {
            string shotname = GetUserNewScreenshotName();
            if (m_UI.UI_SaveScreenshot(ScreenshotFilePath(shotname)) != null)
                return shotname;
            else
                return null;
        }

        void HandleHelpMode()
        {
            if (m_Manual == null)
            {
                m_UI.UI_Clear(Color.Black);
                int gy = 0;
                m_UI.UI_DrawStringBold(Color.Red, "Game manual not available ingame.", 0, gy);
                gy += BOLD_LINE_SPACING;
                DrawFootnote(Color.White, "press ENTER");
                m_UI.UI_Repaint();
                WaitEnter();
                return;
            }

            bool loop = true;
            IList<string> lines = m_Manual.Lines;
            do
            {
                // draw header.
                m_UI.UI_Clear(Color.Black);
                int gy = 0;
                DrawHeader();
                gy += BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(Color.Yellow, "Game Manual", 0, gy);
                gy += BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(Color.White, "---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+", 0, gy);
                gy += BOLD_LINE_SPACING;

                // draw manual.
                int iLine = m_Manual.Line;
                while (iLine < lines.Count && gy < CANVAS_HEIGHT - 2 * BOLD_LINE_SPACING)
                {
                    // ignore commands
                    bool ignore = (lines[iLine] == "<SECTION>");

                    if (!ignore)
                    {
                        m_UI.UI_DrawStringBold(Color.LightGray, lines[iLine], 0, gy);
                        gy += BOLD_LINE_SPACING;
                    }
                    ++iLine;
                }

                // draw foot.
                m_UI.UI_DrawStringBold(Color.White, "---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+", 0, gy);
                gy += BOLD_LINE_SPACING;
                DrawFootnote(Color.White, "cursor and PgUp/PgDn to move, numbers to jump to section, ESC to leave");

                m_UI.UI_Repaint();

                // get command.
                KeyEventArgs key = m_UI.UI_WaitKey();
                int choice = KeyToChoiceNumber(key.KeyCode);

                if (choice < 0 && key.KeyCode == Keys.Escape)
                    loop = false;
                else
                    m_Manual.Move(key.KeyCode, choice, TEXTFILE_LINES_PER_PAGE);
            }
            while (loop);
        }

        #region Hints screen
        void HandleHintsScreen()
        {
            // draw header.
            m_UI.UI_Clear(Color.Black);
            int gy = 0;
            DrawHeader();
            gy += BOLD_LINE_SPACING;
            m_UI.UI_DrawStringBold(Color.Yellow, "Advisor Hints", 0, gy);
            gy += BOLD_LINE_SPACING;

            // prepare : get all the hints text into one huuuuuge list of line :D
            m_UI.UI_DrawStringBold(Color.White, "preparing...", 0, gy);
            gy += BOLD_LINE_SPACING;
            m_UI.UI_Repaint();
            List<string> lines = new List<string>();
            for (int i = (int)AdvisorHint._FIRST; i < (int)AdvisorHint._COUNT; i++)
            {
                string title;
                string[] body;
                GetAdvisorHintText((AdvisorHint)i, out title, out body);
                if (s_Hints.IsAdvisorHintGiven((AdvisorHint)i)) title += " (hint already given)"; // alpha10

                lines.Add(String.Format("HINT {0} : {1}", i, title));
                lines.AddRange(body);
                lines.Add("~~~~");
                lines.Add("");
            }

            // display & handle loop.
            int currentLine = 0;
            bool loop = true;
            do
            {
                // header.
                m_UI.UI_Clear(Color.Black);
                gy = 0;
                DrawHeader();
                gy += BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(Color.Yellow, "Advisor Hints", 0, gy);
                gy += BOLD_LINE_SPACING;

                // display currently viewed lines.
                m_UI.UI_DrawStringBold(Color.White, "---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+", 0, gy);
                gy += BOLD_LINE_SPACING;
                int iLine = currentLine;
                do
                {
                    m_UI.UI_DrawStringBold(Color.LightGray, lines[iLine], 0, gy);
                    gy += BOLD_LINE_SPACING;
                    ++iLine;
                }
                while (iLine < lines.Count && gy < CANVAS_HEIGHT - 2 * BOLD_LINE_SPACING);

                // draw foot.
                m_UI.UI_DrawStringBold(Color.White, "---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+", 0, gy);
                gy += BOLD_LINE_SPACING;
                DrawFootnote(Color.White, "cursor and PgUp/PgDn to move, R to reset hints, ESC to leave");

                m_UI.UI_Repaint();

                // get command.
                KeyEventArgs key = m_UI.UI_WaitKey();
                switch (key.KeyCode)
                {
                    case Keys.Escape:
                        loop = false;
                        break;

                    case Keys.Up:
                        --currentLine;
                        break;
                    case Keys.Down:
                        ++currentLine;
                        break;
                    case Keys.PageUp:
                        currentLine -= TEXTFILE_LINES_PER_PAGE;
                        break;
                    case Keys.PageDown:
                        currentLine += TEXTFILE_LINES_PER_PAGE;
                        break;

                    case Keys.R:
                        // do it.
                        s_Hints.ResetAllHints();

                        // notify.
                        m_UI.UI_Clear(Color.Black);
                        gy = 0;
                        DrawHeader();
                        gy += BOLD_LINE_SPACING;
                        m_UI.UI_DrawStringBold(Color.Yellow, "Advisor Hints", 0, gy);
                        gy += BOLD_LINE_SPACING;
                        m_UI.UI_DrawStringBold(Color.White, "Hints reset done.", 0, gy);
                        m_UI.UI_Repaint();
                        m_UI.UI_Wait(DELAY_LONG);
                        break;
                }

                if (currentLine < 0) currentLine = 0;
                if (currentLine + TEXTFILE_LINES_PER_PAGE >= lines.Count) currentLine = Math.Max(0, lines.Count - TEXTFILE_LINES_PER_PAGE);
            }
            while (loop);

        }
        #endregion

        void HandleMessageLog()
        {
            // draw header.
            m_UI.UI_Clear(Color.Black);
            int gy = 0;
            DrawHeader();
            gy += BOLD_LINE_SPACING;
            m_UI.UI_DrawStringBold(Color.Yellow, "Message Log", 0, gy);
            gy += BOLD_LINE_SPACING;
            m_UI.UI_DrawStringBold(Color.White, "---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+", 0, gy);
            gy += BOLD_LINE_SPACING;

            // log.
            foreach (Message msg in m_MessageManager.History)
            {
                m_UI.UI_DrawString(msg.Color, msg.Text, 0, gy);
                gy += LINE_SPACING;
            }

            // foot.
            DrawFootnote(Color.White, "press ESC to leave");

            // wait.
            m_UI.UI_Repaint();
            WaitEscape();
        }

        void HandleCityInfo()
        {
            int gx, gy;

            gx = gy = 0;
            m_UI.UI_Clear(Color.Black);
            m_UI.UI_DrawStringBold(Color.White, "CITY INFORMATION", gy, gy);
            gy += 2 * BOLD_LINE_SPACING;

            /////////////////////
            // Undead : no info!
            // Living : normal.
            /////////////////////
            if (m_Player.Model.Abilities.IsUndead)
            {
                #region Undead : no info
                m_UI.UI_DrawStringBold(Color.Red, "You can't remember where you are...", gx, gy);
                gy += BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(Color.Red, "Must be that rotting brain of yours...", gx, gy);
                gy += 2 * BOLD_LINE_SPACING;
                #endregion
            }
            else
            {
                #region Living : show info
                ////////////
                // City map
                ////////////
                #region
                m_UI.UI_DrawStringBold(Color.White, "> DISTRICTS LAYOUT", gx, gy);
                gy += BOLD_LINE_SPACING;

                // coordinates.
                gy += BOLD_LINE_SPACING;
                for (int y = 0; y < m_Session.World.Size; y++)
                {
                    Color color = (y == m_Player.Location.Map.District.WorldPosition.Y ? Color.LightGreen : Color.White);
                    m_UI.UI_DrawStringBold(color, y.ToString(), 20, gy + y * 3 * BOLD_LINE_SPACING + BOLD_LINE_SPACING);
                    m_UI.UI_DrawStringBold(color, ".", 20, gy + y * 3 * BOLD_LINE_SPACING);
                    m_UI.UI_DrawStringBold(color, ".", 20, gy + y * 3 * BOLD_LINE_SPACING + 2 * BOLD_LINE_SPACING);
                }
                gy -= BOLD_LINE_SPACING;
                for (int x = 0; x < m_Session.World.Size; x++)
                {
                    Color color = (x == m_Player.Location.Map.District.WorldPosition.X ? Color.LightGreen : Color.White);
                    m_UI.UI_DrawStringBold(color, String.Format("..{0}..", (char)('A' + x)), 32 + x * 48, gy);
                }
                // districts.
                gy += BOLD_LINE_SPACING;
                int mx = 32;
                int my = gy;
                for (int y = 0; y < m_Session.World.Size; y++)
                    for (int x = 0; x < m_Session.World.Size; x++)
                    {
                        District d = m_Session.World[x, y];
                        char dStatus = d == m_Session.CurrentMap.District ? '*' : m_Session.Scoring.HasVisited(d.EntryMap) ? '-' : '?';
                        Color dColor;
                        string dChar;
                        switch (d.Kind)
                        {
                            case DistrictKind.BUSINESS: dColor = Color.Red; dChar = "Bus"; break;
                            case DistrictKind.GENERAL: dColor = Color.Gray; dChar = "Gen"; break;
                            case DistrictKind.GREEN: dColor = Color.Green; dChar = "Gre"; break;
                            case DistrictKind.RESIDENTIAL: dColor = Color.Orange; dChar = "Res"; break;
                            case DistrictKind.SHOPPING: dColor = Color.White; dChar = "Sho"; break;
                            default:
                                throw new ArgumentOutOfRangeException("unhandled district kind");
                        }

                        string lchar = "";
                        for (int i = 0; i < 5; i++)
                            lchar += dStatus;
                        Color lColor = (d == m_Player.Location.Map.District ? Color.LightGreen : dColor);

                        m_UI.UI_DrawStringBold(lColor, lchar, mx + x * 48, my + (y * 3) * BOLD_LINE_SPACING);
                        m_UI.UI_DrawStringBold(lColor, dStatus.ToString(), mx + x * 48, my + (y * 3 + 1) * BOLD_LINE_SPACING);
                        m_UI.UI_DrawStringBold(dColor, dChar, mx + x * 48 + 8, my + (y * 3 + 1) * BOLD_LINE_SPACING);
                        m_UI.UI_DrawStringBold(lColor, dStatus.ToString(), mx + x * 48 + 4 * 8, my + (y * 3 + 1) * BOLD_LINE_SPACING);
                        m_UI.UI_DrawStringBold(lColor, lchar, mx + x * 48, my + (y * 3 + 2) * BOLD_LINE_SPACING);
                    }
                // subway line.
                const string subwayChar = "=";
                int subwayY = m_Session.World.Size / 2;
                for (int x = 1; x < m_Session.World.Size; x++)
                {
                    m_UI.UI_DrawStringBold(Color.White, subwayChar, mx + x * 48 - 8, my + (subwayY * 3) * BOLD_LINE_SPACING + BOLD_LINE_SPACING);
                }

                gy += (m_Session.World.Size * 3 + 1) * BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(Color.White, "Legend", gx, gy);
                gy += BOLD_LINE_SPACING;
                m_UI.UI_DrawString(Color.White, "  *   - current     ?   - unvisited", gx, gy);
                gy += LINE_SPACING;
                m_UI.UI_DrawString(Color.White, "  Bus - Business    Gen - General    Gre - Green", gx, gy);
                gy += LINE_SPACING;
                m_UI.UI_DrawString(Color.White, "  Res - Residential Sho - Shopping", gx, gy);
                gy += LINE_SPACING;
                m_UI.UI_DrawString(Color.White, "  =   - Subway Line", gx, gy);
                gy += LINE_SPACING;
                #endregion

                /////////////////////
                // Notable locations
                /////////////////////
                #region
                gy += BOLD_LINE_SPACING;
                m_UI.UI_DrawStringBold(Color.White, "> NOTABLE LOCATIONS", gx, gy);
                gy += BOLD_LINE_SPACING;
                int buildingsY = gy;
                for (int y = 0; y < m_Session.World.Size; y++)
                    for (int x = 0; x < m_Session.World.Size; x++)
                    {
                        District d = m_Session.World[x, y];
                        Map map = d.EntryMap;

                        // Subway station?
                        Zone subwayZone;
                        if ((subwayZone = map.GetZoneByPartialName(NAME_SUBWAY_STATION)) != null)
                        {
                            m_UI.UI_DrawStringBold(Color.Blue, String.Format("at {0} : {1}.", World.CoordToString(x, y), subwayZone.Name), gx, gy);
                            gy += BOLD_LINE_SPACING;
                            if (gy >= CANVAS_HEIGHT - 2 * BOLD_LINE_SPACING)
                            {
                                gy = buildingsY;
                                gx += 25 * BOLD_LINE_SPACING;
                            }

                        }

                        // Sewers maintenance?
                        // alpha10 removed
                        //Zone sewersZone;
                        //if ((sewersZone = map.GetZoneByPartialName(NAME_SEWERS_MAINTENANCE)) != null)
                        //{
                        //    m_UI.UI_DrawStringBold(Color.Green, String.Format("at {0} : {1}.", World.CoordToString(x, y), sewersZone.Name), gx, gy);
                        //    gy += BOLD_LINE_SPACING;
                        //    if (gy >= CANVAS_HEIGHT - 2 * BOLD_LINE_SPACING)
                        //    {
                        //        gy = buildingsY;
                        //        gx += 25 * BOLD_LINE_SPACING;
                        //    }
                        //}

                        // Police station?
                        if (map == m_Session.UniqueMaps.PoliceStation_OfficesLevel.TheMap.District.EntryMap)
                        {
                            m_UI.UI_DrawStringBold(Color.CadetBlue, String.Format("at {0} : Police Station.", World.CoordToString(x, y)), gx, gy);
                            gy += BOLD_LINE_SPACING;
                            if (gy >= CANVAS_HEIGHT - 2 * BOLD_LINE_SPACING)
                            {
                                gy = buildingsY;
                                gx += 25 * BOLD_LINE_SPACING;
                            }
                        }

                        // Hospital?
                        if (map == m_Session.UniqueMaps.Hospital_Admissions.TheMap.District.EntryMap)
                        {
                            m_UI.UI_DrawStringBold(Color.White, String.Format("at {0} : Hospital.", World.CoordToString(x, y)), gx, gy);
                            gy += BOLD_LINE_SPACING;
                            if (gy >= CANVAS_HEIGHT - 2 * BOLD_LINE_SPACING)
                            {
                                gy = buildingsY;
                                gx += 25 * BOLD_LINE_SPACING;
                            }
                        }

                        // Secrets
                        // - CHAR Underground Facility?
                        if (m_Session.PlayerKnows_CHARUndergroundFacilityLocation && map == m_Session.UniqueMaps.CHARUndergroundFacility.TheMap.District.EntryMap)
                        {
                            m_UI.UI_DrawStringBold(Color.Red, String.Format("at {0} : {1}.", World.CoordToString(x, y), m_Session.UniqueMaps.CHARUndergroundFacility.TheMap.Name), gx, gy);
                            gy += BOLD_LINE_SPACING;
                            if (gy >= CANVAS_HEIGHT - 2 * BOLD_LINE_SPACING)
                            {
                                gy = buildingsY;
                                gx += 25 * BOLD_LINE_SPACING;
                            }
                        }
                        // - The Sewers Thing?
                        if (m_Session.PlayerKnows_TheSewersThingLocation &&
                            map == m_Session.UniqueActors.TheSewersThing.TheActor.Location.Map.District.EntryMap &&
                            !m_Session.UniqueActors.TheSewersThing.TheActor.IsDead)
                        {
                            m_UI.UI_DrawStringBold(Color.Red, String.Format("at {0} : The Sewers Thing lives down there.", World.CoordToString(x, y)), gx, gy);
                            gy += BOLD_LINE_SPACING;
                            if (gy >= CANVAS_HEIGHT - 2 * BOLD_LINE_SPACING)
                            {
                                gy = buildingsY;
                                gx += 25 * BOLD_LINE_SPACING;
                            }
                        }
                    }
                #endregion
                #endregion
            }

            DrawFootnote(Color.White, "press ESC to leave");
            m_UI.UI_Repaint();
            WaitEscape();
        }

        #region Ingame actions modes
        #endregion

        void HandleAiActor(Actor aiActor)
        {
            // Get and perform action from AI controler.
            ActorAction desiredAction = aiActor.Controller.GetAction(this);

            // Insane effect?
            if (m_Rules.IsActorInsane(aiActor) && m_Rules.RollChance(Rules.SANITY_INSANE_ACTION_CHANCE))
            {
                ActorAction insaneAction = GenerateInsaneAction(aiActor);
                if (insaneAction != null && insaneAction.IsLegal())
                    desiredAction = insaneAction;
            }

            // Do action.
            if (desiredAction != null)
            {
                if (desiredAction.IsLegal())
                    desiredAction.Perform();
                else
                {
                    // AI attempted illegal action.
                    SpendActorActionPoints(aiActor, Rules.BASE_ACTION_COST);

                    // alpha10.1
                    // in debug build, throw exception.
                    // in release build just complain and do a wait action instead.
#if DEBUG
                    throw new InvalidOperationException(String.Format("AI attempted illegal action {0}; actorAI: {1}; fail reason : {2}.",
                        desiredAction.GetType().ToString(), aiActor.Controller.GetType().ToString(), desiredAction.FailReason));
#else
                    DoWait(aiActor);
                    DoEmote(aiActor, "My AI attempted an illegal action, I'll  wait instead of crashing your game :)", true);
#endif
                }
            }
            else
                throw new InvalidOperationException("AI returned null action.");
        }
        #endregion
    }
}
