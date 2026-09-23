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
        #region Constants
        public const int MAP_MAX_HEIGHT = 100;
        public const int MAP_MAX_WIDTH = 100;

        public const int TILE_SIZE = 32;
        public const int ACTOR_SIZE = 32;
        public const int ACTOR_OFFSET = (TILE_SIZE - ACTOR_SIZE) / 2;
        public const int TILE_VIEW_WIDTH = 21;
        public const int TILE_VIEW_HEIGHT = 21;
        const int HALF_VIEW_WIDTH = 10;
        const int HALF_VIEW_HEIGHT = 10;

        public const int CANVAS_WIDTH = 1024;
        public const int CANVAS_HEIGHT = 768;

        const int DAMAGE_DX = 10;
        const int DAMAGE_DY = 10;

        #region UI elements
        const int RIGHTPANEL_X = TILE_SIZE * TILE_VIEW_WIDTH + 4;
        const int RIGHTPANEL_Y = 0;
        const int RIGHTPANEL_TEXT_X = RIGHTPANEL_X + 4;
        const int RIGHTPANEL_TEXT_Y = RIGHTPANEL_Y + 4;

        const int INVENTORYPANEL_X = RIGHTPANEL_TEXT_X;
        const int INVENTORYPANEL_Y = RIGHTPANEL_TEXT_Y + 170; // alpha10  156;//142
        const int GROUNDINVENTORYPANEL_Y = INVENTORYPANEL_Y + 64;
        const int CORPSESPANEL_Y = GROUNDINVENTORYPANEL_Y + 64;
        const int INVENTORY_SLOTS_PER_LINE = 10;

        const int SKILLTABLE_Y = CORPSESPANEL_Y + 64;
        const int SKILLTABLE_LINES = 8;  // alpha10 10

        const int LOCATIONPANEL_X = RIGHTPANEL_X;
        const int LOCATIONPANEL_Y = MESSAGES_Y;
        const int LOCATIONPANEL_TEXT_X = LOCATIONPANEL_X + 4;
        const int LOCATIONPANEL_TEXT_Y = LOCATIONPANEL_Y + 4;

        const int MESSAGES_X = 4;
        const int MESSAGES_Y = TILE_VIEW_HEIGHT * TILE_SIZE + 4;
        const int MESSAGES_SPACING = 12;
        const int MESSAGES_FADEOUT = 25;
        const int MAX_MESSAGES = 7;
        const int MESSAGES_HISTORY = 59;

        public const int MINITILE_SIZE = 2;
        const int MINIMAP_X = RIGHTPANEL_X + (CANVAS_WIDTH - RIGHTPANEL_X - MAP_MAX_WIDTH * MINITILE_SIZE) / 2;
        const int MINIMAP_Y = MESSAGES_Y - MINITILE_SIZE * MAP_MAX_HEIGHT - 1;
        const int MINI_TRACKER_OFFSET = 1;

        const int DELAY_SHORT = 250;
        const int DELAY_NORMAL = 500;
        const int DELAY_LONG = 1000;

        readonly Color POPUP_FILLCOLOR = Color.FromArgb(192, Color.CornflowerBlue);

        readonly string[] CLOSE_DOOR_MODE_TEXT = new string[] { "CLOSE MODE - directions to close, ESC cancels" };
        readonly string[] BARRICADE_MODE_TEXT = new string[] { "BARRICADE/REPAIR MODE - directions to barricade/repair, ESC cancels" };
        readonly string[] BREAK_MODE_TEXT = new string[] { "BREAK MODE - directions/wait to break an object, ESC cancels" };
        readonly string[] BUILD_LARGE_FORT_MODE_TEXT = new string[] { "BUILD LARGE FORTIFICATION MODE - directions to build, ESC cancels" };
        readonly string[] BUILD_SMALL_FORT_MODE_TEXT = new string[] { "BUILD SMALL FORTIFICATION MODE - directions to build, ESC cancels" };
        readonly string[] TRADE_MODE_TEXT = new string[] { "TRADE MODE - Y to accept the deal, N to refuse" };
        readonly string[] NEGOCIATE_TRADE_MODE_TEXT = new string[] { "NEGOCIATE TRADE MODE - directions to start negociating with someone, ESC cancels" }; // alpha10
        readonly string[] ASK_NEGOCIATE_TEXT = new string[] { "DO YOU WANT TO NEGOCIATE TRADE WITH {0} - Y to negociate, N to cancel" }; // alpha10.1
        readonly string[] UPGRADE_MODE_TEXT = new string[] { "UPGRADE MODE - follow instructions in the message panel" };
        readonly string[] FIRE_MODE_TEXT = new string[] { "FIRE MODE - F to fire, T next target, M toggle mode, ESC cancels" };
        readonly string[] SWITCH_PLACE_MODE_TEXT = new string[] { "SWITCH PLACE MODE - directions to switch place with a follower, ESC cancels" };
        readonly string[] TAKE_LEAD_MODE_TEXT = new string[] { "TAKE LEAD MODE - directions to recruit a follower, ESC cancels" };
        readonly string[] PULL_MODE_TEXT = new string[] { "PULL MODE - directions to select object, ESC cancels" }; // alpha10
        readonly string[] PUSH_MODE_TEXT = new string[] { "PUSH/SHOVE MODE - directions to push/shove, ESC cancels" };
        readonly string[] TAG_MODE_TEXT = new string[] { "TAG MODE - directions to tag a wall or on the floor, ESC cancels" };
        readonly string[] SPRAY_MODE_TEXT = new string[] { "SPRAY MODE - directions to spray or wait key to spray on yourself, ESC cancels" };
        readonly string PULL_OBJECT_MODE_TEXT = "PULLING {0} - directions to walk to, ESC cancels";  // alpha10
        readonly string PULL_ACTOR_MODE_TEXT = "PULLING {0} - directions to walk to, ESC cancels";  // alpha10
        readonly string PUSH_OBJECT_MODE_TEXT = "PUSHING {0} - directions to push, ESC cancels";
        readonly string SHOVE_ACTOR_MODE_TEXT = "SHOVING {0} - directions to shove, ESC cancels";
        readonly string[] ORDER_MODE_TEXT = new string[] { "ORDER MODE - follow instructions in the message panel, ESC cancels" };
        readonly string[] GIVE_MODE_TEXT = new string[] { "GIVE MODE - directions to give item to someone, ESC cancels" };
        readonly string[] THROW_GRENADE_MODE_TEXT = new string[] { "THROW GRENADE MODE - directions to select, F to fire,  ESC cancels" };
        readonly string[] MARK_ENEMIES_MODE = new string[] { "MARK ENEMIES MODE - E to make enemy, T next actor, ESC cancels" };
        readonly string[] TRADING_DIALOG_MODE_TEXT = new string[] { "TRADING MODE - TAB switch mode, 0..9 select, ESC cancels" }; // alpha10
        readonly Color MODE_TEXTCOLOR = Color.Yellow;
        readonly Color MODE_BORDERCOLOR = Color.Yellow;
        readonly Color MODE_FILLCOLOR = Color.FromArgb(192, Color.Gray);

        // alpha10
        readonly Color TRADE_COLOR_SELECTED_ITEM = Color.LightBlue;
        readonly Color TRADE_COLOR_ACCEPT = Color.LightGreen;
        readonly Color TRADE_COLOR_REFUSE = Color.DarkRed;
        readonly Color TRADE_COLOR_MAYBE_SUCCESS = Color.Green;
        readonly Color TRADE_COLOR_MAYBE_FAILED = Color.Red;

        readonly Color PLAYER_ACTION_COLOR = Color.White;
        readonly Color OTHER_ACTION_COLOR = Color.Gray;
        readonly Color SAYOREMOTE_DANGER_COLOR = Color.Brown; // alpha10
        readonly Color SAYOREMOTE_NORMAL_COLOR = Color.DarkCyan; // alpha10
        readonly Color PLAYER_AUDIO_COLOR = Color.Green;

        const int LINE_SPACING = 12;
        const int BOLD_LINE_SPACING = 14;
        const int CREDIT_CHAR_SPACING = 8;
        const int CREDIT_LINE_SPACING = LINE_SPACING;

        readonly Color NIGHT_COLOR = Color.Cyan;
        readonly Color DAY_COLOR = Color.Gold;

        const int TEXTFILE_CHARS_PER_LINE = 120;
        const int TEXTFILE_LINES_PER_PAGE = 50;
        #endregion

        #region Notable Zone names
        public const string NAME_SUBWAY_STATION = "Subway Station";
        public const string NAME_SEWERS_MAINTENANCE = "Sewers Maintenance";
        public const string NAME_SUBWAY_RAILS = "rails";
        public const string NAME_POLICE_STATION_JAILS_CELL = "jail";
        #endregion

        #region Events
        const int SPAWN_DISTANCE_TO_PLAYER = 10;

        #region Zombie invasion

        #endregion

        #region Sewers invasion
        const int SEWERS_INVASION_CHANCE = 1;
        public const float SEWERS_UNDEADS_FACTOR = 0.50f;  // 1.0 for as much as surface undead spawning.
        #endregion

        #region DISABLED Subway invasion
#if false
        const int SUBWAY_INVASION_CHANCE = 1;
        public const float SUBWAY_UNDEADS_FACTOR = 0.25f;  // 1.0 for as much as surface undead spawning.
#endif
        #endregion

        #region Refugees
        /// <summary>
        /// How many refugees in each wave, as ratio of max civilians.
        /// </summary>
        const float REFUGEES_WAVE_SIZE = 0.20f;

        /// <summary>
        /// How many random items each new refugee will carry.
        /// </summary>
        const int REFUGEES_WAVE_ITEMS = 3;


        /// <summary>
        /// Chance to spawn on the surface vs sewers/subway.
        /// </summary>
        const int REFUGEE_SURFACE_SPAWN_CHANCE = 80;
        #endregion

        #region Unique NPC refugees
        const int UNIQUE_REFUGEE_CHECK_CHANCE = 10;
        #endregion

        #region National Guard Squad
        /// <summary>
        /// Date at which natguard can intervene.
        /// </summary>
        public const int NATGUARD_DAY = 3;

        /// <summary>
        /// Date at which natguard will stop coming.
        /// </summary>
        const int NATGUARD_END_DAY = 10;

        /// <summary>
        /// Date at which the natguard leader will bring Z-Trackers.
        /// </summary>
        const int NATGUARD_ZTRACKER_DAY = NATGUARD_DAY + 3;

        /// <summary>
        /// How many soldiers in each national guard squad.
        /// </summary>
        const int NATGUARD_SQUAD_SIZE = 5;

        /// <summary>
        /// By how many times the undeads must outnumber the livings for the nat guard to intervene.
        /// Factored by option.
        /// </summary>
        const float NATGUARD_INTERVENTION_FACTOR = 5;

        /// <summary>
        /// How many chance per turn the nat guard intervene (if other conditions are met).
        /// </summary>
        const int NATGUARD_INTERVENTION_CHANCE = 1;
        #endregion

        #region Army drop supplies
        /// <summary>
        /// Date at which army can drop supplies.
        /// </summary>
        const int ARMY_SUPPLIES_DAY = 4;

        /// <summary>
        /// Ratio total map food items nutrition / livings below which the army drop supplies event can fire.
        /// Factored by option.
        /// </summary>
        const float ARMY_SUPPLIES_FACTOR = 0.20f * Rules.FOOD_BASE_POINTS;

        /// <summary>
        /// Chances per turn the army will drop supply (if other conditions are met).
        /// </summary>
        const int ARMY_SUPPLIES_CHANCE = 2;

        /// <summary>
        /// Radius in which supplies items are dropped.
        /// One item is dropped per suitable tile in radius.
        /// </summary>
        const int ARMY_SUPPLIES_SCATTER = 1;

        #endregion

        #region Bikers raid
        /// <summary>
        /// Date at which bikers will start to raid.
        /// </summary>
        public const int BIKERS_RAID_DAY = 2;

        /// <summary>
        /// Date at which bikers will stop coming.
        /// </summary>
        const int BIKERS_END_DAY = 14;

        /// <summary>
        /// Number of bikers in the raid.
        /// </summary>
        const int BIKERS_RAID_SIZE = 6;

        /// <summary>
        /// Raid chance per turn (if others conditions are met).
        /// </summary>
        const int BIKERS_RAID_CHANCE_PER_TURN = 1;

        /// <summary>
        /// Number of days between each bikers raid.
        /// </summary>
        const int BIKERS_RAID_DAYS_GAP = 2;
        #endregion

        #region Gangstas raid
        /// <summary>
        /// Date at which gangsta will start to raid.
        /// </summary>
        public const int GANGSTAS_RAID_DAY = 7;

        /// <summary>
        /// Date at which gangstas will stop coming.
        /// </summary>
        const int GANGSTAS_END_DAY = 21;

        /// <summary>
        /// Number of gangstas in the raid.
        /// </summary>
        const int GANGSTAS_RAID_SIZE = 6;

        /// <summary>
        /// Raid chance per turn (if others conditions are met).
        /// </summary>
        const int GANGSTAS_RAID_CHANCE_PER_TURN = 1;

        /// <summary>
        /// Number of days between each gangsta raid.
        /// </summary>
        const int GANGSTAS_RAID_DAYS_GAP = 3;
        #endregion

        #region BlackOps raid
        /// <summary>
        /// Date at which blackops will start to raid.
        /// </summary>
        const int BLACKOPS_RAID_DAY = 14;

        /// <summary>
        /// Number of blackops in the raid.
        /// </summary>
        const int BLACKOPS_RAID_SIZE = 3;

        /// <summary>
        /// Raid chances per turn (if others conditions are met).
        /// </summary>
        const int BLACKOPS_RAID_CHANCE_PER_TURN = 1;

        /// <summary>
        /// Delay between each raid.
        /// </summary>
        const int BLACKOPS_RAID_DAY_GAP = 5;
        #endregion

        #region Band of Survivors
        const int SURVIVORS_BAND_DAY = 21;
        const int SURVIVORS_BAND_SIZE = 5;
        const int SURVIVORS_BAND_CHANCE_PER_TURN = 1;
        const int SURVIVORS_BAND_DAY_GAP = 5;
        #endregion

        #endregion

        #region Undeads evolution
        const int ZOMBIE_LORD_EVOLUTION_MIN_DAY = 7;
        const int DISCIPLE_EVOLUTION_MIN_DAY = 7;
        #endregion

        #region Map color tints for day phases
        readonly Color TINT_DAY = Color.White;
        readonly Color TINT_SUNSET = Color.FromArgb(235, 235, 235);
        readonly Color TINT_EVENING = Color.FromArgb(215, 215, 215);
        readonly Color TINT_MIDNIGHT = Color.FromArgb(195, 195, 195);
        readonly Color TINT_NIGHT = Color.FromArgb(205, 205, 205);
        readonly Color TINT_SUNRISE = Color.FromArgb(225, 225, 225);
        #endregion

        #region Hearing chances - avoid spamming messages.
        const int PLAYER_HEAR_FIGHT_CHANCE = 25;
        const int PLAYER_HEAR_SCREAMS_CHANCE = 10;
        const int PLAYER_HEAR_PUSHPULL_CHANCE = 25;  // alpha10 also for pulls
        const int PLAYER_HEAR_BASH_CHANCE = 25;
        const int PLAYER_HEAR_BREAK_CHANCE = 50;
        const int PLAYER_HEAR_EXPLOSION_CHANCE = 100;
        #endregion

        #region Blood splatting
        const int BLOOD_WALL_SPLAT_CHANCE = 20;
        #endregion

        #region NPC player sleeping snoring message chance
        public const int MESSAGE_NPC_SLEEP_SNORE_CHANCE = 10;
        #endregion

        #region Weather
        // alpha10
        // weather stays from 1h to 3 days and then change
        public const int WEATHER_MIN_DURATION = 1 * WorldTime.TURNS_PER_HOUR;
        public const int WEATHER_MAX_DURATION = 3 * WorldTime.TURNS_PER_DAY;
        #endregion

        // alpha10
        #region Music
        // check bg music every Nth game hours
        const int BGMUSIC_UPDATE_TURNS = 4 * WorldTime.TURNS_PER_HOUR;
        #endregion

        #region World Gen
        const int DISTRICT_EXIT_CHANCE_PER_TILE = 15;
        #endregion

        #region Common verbs
        readonly Verb VERB_ACCEPT_THE_DEAL = new Verb("accept the deal", "accepts the deal");
        readonly Verb VERB_ACTIVATE = new Verb("activate");
        readonly Verb VERB_AVOID = new Verb("avoid");
        readonly Verb VERB_BARRICADE = new Verb("barricade");
        readonly Verb VERB_BASH = new Verb("bash", "bashes");
        readonly Verb VERB_BE = new Verb("are", "is");
        readonly Verb VERB_BUILD = new Verb("build");
        readonly Verb VERB_BREAK = new Verb("break");
        readonly Verb VERB_BUTCHER = new Verb("butcher");
        readonly Verb VERB_CATCH = new Verb("catch", "catches");
        readonly Verb VERB_CHAT_WITH = new Verb("chat with", "chats with");
        readonly Verb VERB_CLOSE = new Verb("close");
        readonly Verb VERB_COLLAPSE = new Verb("collapse");
        readonly Verb VERB_CRUSH = new Verb("crush", "crushes");
        readonly Verb VERB_DESACTIVATE = new Verb("desactivate");
        readonly Verb VERB_DESTROY = new Verb("destroy");
        readonly Verb VERB_DIE = new Verb("die");
        readonly Verb VERB_DIE_FROM_STARVATION = new Verb("die from starvation", "dies from starvation");
        readonly Verb VERB_DISARM = new Verb("disarm");  // alpha10
        readonly Verb VERB_DISCARD = new Verb("discard");
        readonly Verb VERB_DRAG = new Verb("drag");
        readonly Verb VERB_DROP = new Verb("drop");
        readonly Verb VERB_EAT = new Verb("eat");
        readonly Verb VERB_ENJOY = new Verb("enjoy");
        readonly Verb VERB_ENTER = new Verb("enter");
        readonly Verb VERB_ESCAPE = new Verb("escape");
        readonly Verb VERB_FAIL = new Verb("fail");
        readonly Verb VERB_FEAST_ON = new Verb("feast on", "feasts on");
        readonly Verb VERB_FEEL = new Verb("feel");
        readonly Verb VERB_GET = new Verb("get");
        readonly Verb VERB_GIVE = new Verb("give");
        readonly Verb VERB_GRAB = new Verb("grab");
        readonly Verb VERB_EQUIP = new Verb("equip");
        readonly Verb VERB_HAVE = new Verb("have", "has");
        readonly Verb VERB_HELP = new Verb("help");
        readonly Verb VERB_HEAL_WITH = new Verb("heal with", "heals with");
        readonly Verb VERB_JUMP_ON = new Verb("jump on", "jumps on");
        readonly Verb VERB_KILL = new Verb("kill");
        readonly Verb VERB_LEAVE = new Verb("leave");
        readonly Verb VERB_MISS = new Verb("miss", "misses");
        readonly Verb VERB_MURDER = new Verb("murder");
        readonly Verb VERB_OFFER = new Verb("offer");
        readonly Verb VERB_OPEN = new Verb("open");
        readonly Verb VERB_ORDER = new Verb("order");
        readonly Verb VERB_PERSUADE = new Verb("persuade");
        readonly Verb VERB_PULL = new Verb("pull", "pulls");  // alpha10
        readonly Verb VERB_PUSH = new Verb("push", "pushes");
        readonly Verb VERB_RAISE_ALARM = new Verb("raise the alarm", "raises the alarm");
        readonly Verb VERB_REFUSE_THE_DEAL = new Verb("refuse the deal", "refuses the deal");
        readonly Verb VERB_RELOAD = new Verb("reload");
        readonly Verb VERB_RECHARGE = new Verb("recharge");
        readonly Verb VERB_REPAIR = new Verb("repair");
        readonly Verb VERB_REVIVE = new Verb("revive");
        readonly Verb VERB_SEE = new Verb("see");
        readonly Verb VERB_SHOUT = new Verb("shout");
        readonly Verb VERB_SHOVE = new Verb("shove");
        readonly Verb VERB_SNORE = new Verb("snore");
        readonly Verb VERB_SPRAY = new Verb("spray");
        readonly Verb VERB_START = new Verb("start");
        readonly Verb VERB_STOP = new Verb("stop");
        readonly Verb VERB_STUMBLE = new Verb("stumble");
        readonly Verb VERB_SWITCH = new Verb("switch", "switches");
        readonly Verb VERB_SWITCH_PLACE_WITH = new Verb("switch place with", "switches place with");
        readonly Verb VERB_TAKE = new Verb("take");
        readonly Verb VERB_THROW = new Verb("throw");
        readonly Verb VERB_TRADE = new Verb("trade");  // alpha10
        readonly Verb VERB_TRANSFORM_INTO = new Verb("transform into", "transforms into");
        readonly Verb VERB_UNEQUIP = new Verb("unequip");
        readonly Verb VERB_VOMIT = new Verb("vomit");
        readonly Verb VERB_WAIT = new Verb("wait");
        readonly Verb VERB_WAKE_UP = new Verb("wake up", "wakes up");
        #endregion

        #endregion

        #region Types
        #region Overlays
        abstract class Overlay
        {
            public abstract void Draw(IRogueUI ui);
        }

        class OverlayImage : Overlay
        {
            public Point ScreenPosition { get; set; }
            public string ImageID { get; set; }

            public OverlayImage(Point screenPosition, string imageID)
            {
                this.ScreenPosition = screenPosition;
                this.ImageID = imageID;
            }

            public override void Draw(IRogueUI ui)
            {
                ui.UI_DrawImage(ImageID, ScreenPosition.X, ScreenPosition.Y);
            }
        }

        class OverlayTransparentImage : Overlay
        {
            public float Alpha { get; set; }
            public Point ScreenPosition { get; set; }
            public string ImageID { get; set; }

            public OverlayTransparentImage(float alpha, Point screenPosition, string imageID)
            {
                this.Alpha = alpha;
                this.ScreenPosition = screenPosition;
                this.ImageID = imageID;
            }

            public override void Draw(IRogueUI ui)
            {
                ui.UI_DrawTransparentImage(Alpha, ImageID, ScreenPosition.X, ScreenPosition.Y);
            }
        }

        class OverlayText : Overlay
        {
            public Point ScreenPosition { get; set; }
            public string Text { get; set; }
            public Color Color { get; set; }
            public Color? ShadowColor { get; set; }

            public OverlayText(Point screenPosition, Color color, string text)
                : this(screenPosition, color, text, null)
            {
            }

            public OverlayText(Point screenPosition, Color color, string text, Color? shadowColor)
            {
                this.ScreenPosition = screenPosition;
                this.Color = color;
                this.ShadowColor = shadowColor;
                this.Text = text;
            }

            public override void Draw(IRogueUI ui)
            {
                if (ShadowColor.HasValue)
                    ui.UI_DrawString(ShadowColor.Value, Text, ScreenPosition.X + 1, ScreenPosition.Y + 1);
                ui.UI_DrawString(Color, Text, ScreenPosition.X, ScreenPosition.Y);
            }
        }

        class OverlayLine : Overlay
        {
            public Point ScreenFrom { get; set; }
            public Point ScreenTo { get; set; }
            public Color Color { get; set; }

            public OverlayLine(Point screenFrom, Color color, Point screenTo)
            {
                ScreenFrom = screenFrom;
                ScreenTo = screenTo;
                Color = color;
            }

            public override void Draw(IRogueUI ui)
            {
                ui.UI_DrawLine(Color, ScreenFrom.X, ScreenFrom.Y, ScreenTo.X, ScreenTo.Y);
            }
        }

        class OverlayRect : Overlay
        {
            public Rectangle Rectangle { get; set; }
            public Color Color { get; set; }

            public OverlayRect(Color color, Rectangle rect)
            {
                this.Rectangle = rect;
                this.Color = color;
            }

            public override void Draw(IRogueUI ui)
            {
                ui.UI_DrawRect(this.Color, this.Rectangle);
            }
        }

        class OverlayPopup : Overlay
        {
            public Point ScreenPosition { get; set; }
            public Color TextColor { get; set; }
            public Color BoxBorderColor { get; set; }
            public Color BoxFillColor { get; set; }
            public string[] Lines { get; set; }

            /// <summary>
            ///
            /// </summary>
            /// <param name="lines">can be null if want to set text property later</param>
            /// <param name="textColor"></param>
            /// <param name="boxBorderColor"></param>
            /// <param name="boxFillColor"></param>
            /// <param name="screenPos"></param>
            public OverlayPopup(string[] lines, Color textColor, Color boxBorderColor, Color boxFillColor, Point screenPos)
            {
                this.ScreenPosition = screenPos;
                this.TextColor = textColor;
                this.BoxBorderColor = boxBorderColor;
                this.BoxFillColor = boxFillColor;
                this.Lines = lines;
            }

            public override void Draw(IRogueUI ui)
            {
                ui.UI_DrawPopup(Lines, TextColor, BoxBorderColor, BoxFillColor, ScreenPosition.X, ScreenPosition.Y);
            }
        }

        // alpha10

        class OverlayPopupTitle : Overlay
        {
            public Point ScreenPosition { get; set; }
            public string Title { get; set; }
            public Color TitleColor { get; set; }
            public string[] Lines { get; set; }
            public Color TextColor { get; set; }
            public Color BoxBorderColor { get; set; }
            public Color BoxFillColor { get; set; }

            public OverlayPopupTitle(string title, Color titleColor, string[] lines, Color textColor, Color boxBorderColor, Color boxFillColor, Point screenPos)
            {
                this.ScreenPosition = screenPos;
                this.Title = title;
                this.TitleColor = titleColor;
                this.TextColor = textColor;
                this.BoxBorderColor = boxBorderColor;
                this.BoxFillColor = boxFillColor;
                this.Lines = lines;
            }

            public override void Draw(IRogueUI ui)
            {
                ui.UI_DrawPopupTitle(Title, TitleColor, Lines, TextColor, BoxBorderColor, BoxFillColor, ScreenPosition.X, ScreenPosition.Y);
            }
        }

        class OverlayPopupTitleColors : Overlay
        {
            public Point ScreenPosition { get; set; }
            public string Title { get; set; }
            public Color TitleColor { get; set; }
            public string[] Lines { get; set; }
            public Color[] Colors { get; set; }
            public Color BoxBorderColor { get; set; }
            public Color BoxFillColor { get; set; }

            public OverlayPopupTitleColors(string title, Color titleColor, string[] lines, Color[] colors, Color boxBorderColor, Color boxFillColor, Point screenPos)
            {
                this.ScreenPosition = screenPos;
                this.Title = title;
                this.TitleColor = titleColor;
                this.Colors = colors;
                this.BoxBorderColor = boxBorderColor;
                this.BoxFillColor = boxFillColor;
                this.Lines = lines;
            }

            public override void Draw(IRogueUI ui)
            {
                ui.UI_DrawPopupTitleColors(Title, TitleColor, Lines, Colors, BoxBorderColor, BoxFillColor, ScreenPosition.X, ScreenPosition.Y);
            }
        }
        #endregion

        #region Character generation
        struct CharGen
        {
            public bool IsUndead { get; set; }
            public GameActors.IDs UndeadModel { get; set; }
            public bool IsMale { get; set; }
            public Skills.IDs StartingSkill { get; set; }
        }
        #endregion

        #region Simulation
        [Flags]
        enum SimFlags
        {
            NOT_SIMULATING = 0,
            HIDETAIL_TURN = (1 << 0),
            LODETAIL_TURN = (1 << 1)
        }
        #endregion

        #endregion

        #region Fields
        readonly IRogueUI m_UI;
        Rules m_Rules;
        Session m_Session;
        HiScoreTable m_HiScoreTable;
        MessageManager m_MessageManager;
        bool m_IsGameRunning = true;
        bool m_HasLoadedGame = false;
        List<Overlay> m_Overlays = new List<Overlay>();
        Actor m_Player;
        HashSet<Point> m_PlayerFOV = new HashSet<Point>();
        Rectangle m_MapViewRect;

        static GameOptions s_Options;
        static Keybindings s_KeyBindings;
        static GameHintsStatus s_Hints;

        OverlayPopup m_HintAvailableOverlay;  // alpha10

        BaseTownGenerator m_TownGenerator;

        bool m_PlayedIntro;
        IMusicManager m_MusicManager;

        CharGen m_CharGen;

        TextFile m_Manual;
        int m_ManualLine;

        GameFactions m_GameFactions;
        GameActors m_GameActors;
        GameItems m_GameItems;
        GameTiles m_GameTiles;

        bool m_IsPlayerLongWait;
        bool m_IsPlayerLongWaitForcedStop;
        WorldTime m_PlayerLongWaitEnd;

        // alpha10 new sim thread management
        //Object m_SimMutex = new Object();  // alpha10 obsolete
        Thread m_SimThread;
        readonly Object m_SimStateLock = new Object(); // alpha10 lock when reading sim thread state flags
        bool m_SimThreadDoRun;  // alpha10 sim thread state: set by main thread to false to ask sim thread to stop.
        bool m_SimThreadIsWorking;  // alpha10 sim thread state: set by sim thread to false when has exited loop.
        #endregion

        #region Properties
        public Session Session
        {
            get { return m_Session; }
        }

        public Rules Rules
        {
            get { return m_Rules; }
        }

        public IRogueUI UI
        {
            get { return m_UI; }
        }

        public bool IsGameRunning
        {
            get { return m_IsGameRunning; }
            set { m_IsGameRunning = value; }
        }

        public static GameOptions Options
        {
            get { return s_Options; }
        }

        public static Keybindings KeyBindings
        {
            get { return s_KeyBindings; }
        }

        public GameFactions GameFactions
        {
            get { return m_GameFactions; }
        }

        public GameActors GameActors
        {
            get { return m_GameActors; }
        }

        public GameItems GameItems
        {
            get { return m_GameItems; }
        }

        public GameTiles GameTiles
        {
            get { return m_GameTiles; }
        }

        public Actor Player
        {
            get { return m_Player; }
        }
        #endregion

        // alpha10
        #region Debug
        // Looping ai detection code:
        // detect cases where an ai is proably performing an infinite sequence of ap free actions.
        Actor m_DEBUG_prevAiActor;
        int m_DEBUG_sameAiActorCount;
        const int DEBUG_AI_ACTOR_LOOP_COUNT_WARNING = 10;
        #endregion

        #region Init
        public RogueGame(IRogueUI UI)
        {
            Logger.WriteLine(Logger.Stage.INIT_MAIN, "RogueGame()");

            m_UI = UI;

            Logger.WriteLine(Logger.Stage.INIT_MAIN, "creating MusicManager");
            switch (SetupConfig.Sound)
            {
                case SetupConfig.eSound.SOUND_MANAGED_DIRECTX:
                    m_MusicManager = new MDXSoundManager();
                    break;
                case SetupConfig.eSound.SOUND_SFML:
                    m_MusicManager = new SFMLSoundManager();
                    break;
                default:
                    m_MusicManager = new NullSoundManager();
                    break;
            }

            Logger.WriteLine(Logger.Stage.INIT_MAIN, "creating MessageManager");
            m_MessageManager = new MessageManager(MESSAGES_SPACING, MESSAGES_FADEOUT, MESSAGES_HISTORY);

            m_Session = Session.Get;
            Logger.WriteLine(Logger.Stage.INIT_MAIN, "creating Rules");
            m_Rules = new Rules(new DiceRoller(m_Session.Seed));

            BaseTownGenerator.Parameters genParams = BaseTownGenerator.DEFAULT_PARAMS;
            genParams.MapWidth = genParams.MapHeight = 100;
            Logger.WriteLine(Logger.Stage.INIT_MAIN, "creating Generator");
            m_TownGenerator = new StdTownGenerator(this, genParams);

            Logger.WriteLine(Logger.Stage.INIT_MAIN, "creating options, keys, hints.");
            s_Options = new GameOptions();
            s_Options.ResetToDefaultValues();
            s_KeyBindings = new Keybindings();
            s_KeyBindings.ResetToDefaults();
            s_Hints = new GameHintsStatus();
            s_Hints.ResetAllHints();

            Logger.WriteLine(Logger.Stage.INIT_MAIN, "creating dbs");
            m_GameFactions = new GameFactions();
            m_GameActors = new GameActors();
            m_GameItems = new GameItems();
            m_GameTiles = new GameTiles();

            Logger.WriteLine(Logger.Stage.INIT_MAIN, "RogueGame() done.");
        }
        #endregion


        #region Anim Delay
        void AnimDelay(int msecs)
        {
            if (s_Options.IsAnimDelayOn)
                m_UI.UI_Wait(msecs);
        }
        #endregion





        #region Updating Player FOV
        void UpdatePlayerFOV(Actor player)
        {
            if (player == null)
                return;
            m_PlayerFOV = LOS.ComputeFOVFor(m_Rules, player, m_Session.WorldTime, m_Session.World.Weather);
            player.Location.Map.SetViewAndMarkVisited(m_PlayerFOV);
        }
        #endregion

        // alpha10.1 Bot Mode - DEBUG build only
        #region Experimental Bot Mode
#if DEBUG
        bool m_isBotMode = false;
        BaseAI m_botControl = null;
        const int BOT_DELAY = DELAY_SHORT;
        readonly Object m_botLock = new Object(); // necessary because dev keys presses from RogueForm can happen at any time

        public void BotToggleControl()
        {
            lock (m_botLock)
            {
                if (m_isBotMode)
                    BotReleaseControl();
                else
                    BotTakeControl();
            }
        }

        void BotTakeControl()
        {
            // bot restrictions check
            if (m_Player == null || m_Player.IsDead)
            {
                AddMessage(MakeErrorMessage("Bot cannot take control of null/dead player"));
                return;
            }

            if (m_botControl != null)
                m_botControl.LeaveControl();

            try
            {
                Type aiClass = m_Player.Model.DefaultController;
                if (aiClass == null)
                    throw new Exception("actor model has null defaultcontroller");
                ActorController aiController = aiClass.GetConstructor(Type.EmptyTypes).Invoke(null) as ActorController;
                if (!(aiController is BaseAI))
                    throw new Exception("actor model defaultcontroller is not BaseAI");

                m_botControl = aiController as BaseAI;
                m_botControl.TakeControl(m_Player);
                m_Player.IsBotPlayer = true;
                m_isBotMode = true;
                AddMessage(MakeMessage(m_Player, "is now bot controlled by " + m_botControl.GetType() + ".", Color.LightGreen));
            }
            catch (Exception e)
            {
                ClearMessages();
                AddMessage(MakeErrorMessage("error while creating bot ai:"));
                AddMessage(MakeErrorMessage(e.Message));
                AddMessagePressEnter();
            }
        }

        void BotReleaseControl()
        {
            if (m_botControl == null)
                return;
            if (m_Player != null)
                m_Player.IsBotPlayer = false;
            m_botControl.LeaveControl();
            m_botControl = null;
            m_isBotMode = false;
            if (m_Player != null)
                AddMessage(MakeMessage(m_Player, "is now human controlled.", Color.LightGreen));
        }
#endif
        #endregion





        #region Translating commands
        public static Direction CommandToDirection(PlayerCommand cmd)
        {
            switch (cmd)
            {
                case PlayerCommand.MOVE_N:
                    return Direction.N;
                case PlayerCommand.MOVE_NE:
                    return Direction.NE;
                case PlayerCommand.MOVE_E:
                    return Direction.E;
                case PlayerCommand.MOVE_SE:
                    return Direction.SE;
                case PlayerCommand.MOVE_S:
                    return Direction.S;
                case PlayerCommand.MOVE_SW:
                    return Direction.SW;
                case PlayerCommand.MOVE_W:
                    return Direction.W;
                case PlayerCommand.MOVE_NW:
                    return Direction.NW;
                case PlayerCommand.WAIT_OR_SELF:
                    return Direction.NEUTRAL;

                default:
                    return null;
            }
        }
        #endregion


        #region Loud noises
        void OnLoudNoise(Map map, Point noisePosition, string noiseName)
        {
            ////////////////////////////////////////////
            // Check if nearby sleeping actors wake up.
            // Check long wait interruption.
            ////////////////////////////////////////////
            int xmin = noisePosition.X - Rules.LOUD_NOISE_RADIUS;
            int xmax = noisePosition.X + Rules.LOUD_NOISE_RADIUS;
            int ymin = noisePosition.Y - Rules.LOUD_NOISE_RADIUS;
            int ymax = noisePosition.Y + Rules.LOUD_NOISE_RADIUS;
            map.TrimToBounds(ref xmin, ref ymin);
            map.TrimToBounds(ref xmax, ref ymax);

            ///////////////////////////
            // Waking up nearby actors.
            ///////////////////////////
            for (int x = xmin; x <= xmax; x++)
            {
                for (int y = ymin; y <= ymax; y++)
                {
                    // sleeping actor?
                    Actor actor = map.GetActorAt(x, y);
                    if (actor == null || !actor.IsSleeping)
                        continue;

                    // ignore if too far.
                    int noiseDistance = m_Rules.GridDistance(noisePosition, x, y);
                    if (noiseDistance > Rules.LOUD_NOISE_RADIUS)
                        continue;

                    // roll chance of waking up.
                    int wakeupChance = m_Rules.ActorLoudNoiseWakeupChance(actor, noiseDistance);
                    if (!m_Rules.RollChance(wakeupChance))
                        continue;

                    // wake up!
                    DoWakeUp(actor);
                    if (IsVisibleToPlayer(actor))
                    {
                        AddMessage(new Message(String.Format("{0} wakes {1} up!", noiseName, actor.TheName), map.LocalTime.TurnCounter, actor == m_Player ? Color.Red : Color.White));
                        RedrawPlayScreen();
                    }
                }
            }

            ///////////////////////////
            // Interrupting long wait.
            ///////////////////////////
            if (m_IsPlayerLongWait && map == m_Player.Location.Map && IsVisibleToPlayer(map, noisePosition))
            {
                // interrupt!
                m_IsPlayerLongWaitForcedStop = true;
            }
        }
        #endregion



        #region Blood & Remains
        public void SplatterBlood(Map map, Point position)
        {
            // splatter floor there.
            Tile tile = map.GetTileAt(position.X, position.Y);
            if (map.IsWalkable(position.X, position.Y) && !tile.HasDecoration(GameImages.DECO_BLOODIED_FLOOR))
            {
                tile.AddDecoration(GameImages.DECO_BLOODIED_FLOOR);
                map.AddTimer(new TaskRemoveDecoration(WorldTime.TURNS_PER_DAY, position.X, position.Y, GameImages.DECO_BLOODIED_FLOOR));
            }

            // splatter adjacent walls.
            foreach (Direction d in Direction.COMPASS)
            {
                if (!m_Rules.RollChance(BLOOD_WALL_SPLAT_CHANCE))
                    continue;
                Point next = position + d;
                if (!map.IsInBounds(next))
                    continue;
                Tile tileNext = map.GetTileAt(next.X, next.Y);
                if (tileNext.Model.IsWalkable)
                    continue;
                if (tileNext.HasDecoration(GameImages.DECO_BLOODIED_WALL))
                    continue;
                tileNext.AddDecoration(GameImages.DECO_BLOODIED_WALL);
                map.AddTimer(new TaskRemoveDecoration(WorldTime.TURNS_PER_DAY, next.X, next.Y, GameImages.DECO_BLOODIED_WALL));
            }
        }

        public void UndeadRemains(Map map, Point position)
        {
            // add deco there.
            Tile tile = map.GetTileAt(position.X, position.Y);
            if (map.IsWalkable(position.X, position.Y) && !tile.HasDecoration(GameImages.DECO_ZOMBIE_REMAINS))
                tile.AddDecoration(GameImages.DECO_ZOMBIE_REMAINS);
        }
        #endregion

        #region Corpses
        public void DropCorpse(Actor deadGuy)
        {
            // add blood to deadguy.
            deadGuy.Doll.AddDecoration(DollPart.TORSO, GameImages.BLOODIED);

            // make and add corpse.
            int corpseHp = m_Rules.ActorMaxHPs(deadGuy);
            float rotation = m_Rules.Roll(30, 60);
            if (m_Rules.RollChance(50)) rotation = -rotation;
            float scale = 1.0f;
            Corpse corpse = new Corpse(deadGuy, corpseHp, corpseHp, deadGuy.Location.Map.LocalTime.TurnCounter, rotation, scale);
            deadGuy.Location.Map.AddCorpseAt(corpse, deadGuy.Location.Position);
        }
        #endregion



        #region Infection & Zombification
        void InfectActor(Actor actor, int addInfection)
        {
            actor.Infection = Math.Min(m_Rules.ActorInfectionHPs(actor), actor.Infection + addInfection);
        }

        /// <summary>
        /// Zombify an actor during the game or zombify the player at game start.
        /// </summary>
        /// <param name="zombifier"></param>
        /// <param name="deadVictim"></param>
        /// <param name="isStartingGame"></param>
        /// <returns></returns>
        Actor Zombify(Actor zombifier, Actor deadVictim, bool isStartingGame)
        {
            Actor newZombie = m_TownGenerator.MakeZombified(zombifier, deadVictim, isStartingGame ? 0 : deadVictim.Location.Map.LocalTime.TurnCounter);

            // add to map.
            if (!isStartingGame)
                deadVictim.Location.Map.PlaceActorAt(newZombie, deadVictim.Location.Position);

            // reset AP - dont act this turn.
            newZombie.ActionPoints = 0;

            // if zombifying player, remember it!
            if (deadVictim == m_Player || deadVictim.IsPlayer)
                m_Session.Scoring.SetZombifiedPlayer(newZombie);

            // keep half of the skills from living form at random.
            SkillTable livingSkills = deadVictim.Sheet.SkillTable;
            if (livingSkills != null && livingSkills.CountSkills > 0)
            {
                if (newZombie.Sheet.SkillTable == null)
                    newZombie.Sheet.SkillTable = new SkillTable();
                int nbLivingSkills = livingSkills.CountSkills;
                int nbSkillsToKeep = livingSkills.CountTotalSkillLevels / 2;
                for (int i = 0; i < nbSkillsToKeep; i++)
                {
                    Skills.IDs keepSkill = (Skills.IDs)livingSkills.SkillsList[m_Rules.Roll(0, nbLivingSkills)];
                    Skills.IDs? zombiefiedSkill = ZombifySkill(keepSkill);
                    if (zombiefiedSkill.HasValue)
                        SkillUpgrade(newZombie, zombiefiedSkill.Value);
                }
                m_TownGenerator.RecomputeActorStartingStats(newZombie);
            }

            // cause insanity.
            if (!isStartingGame)
                SeeingCauseInsanity(newZombie, newZombie.Location, Rules.SANITY_HIT_ZOMBIFY, String.Format("{0} turning into a zombie", deadVictim.Name));

            // done.
            return newZombie;
        }

        public Skills.IDs? ZombifySkill(Skills.IDs skill)
        {
            switch (skill)
            {
                case Skills.IDs.AGILE: return Skills.IDs.Z_AGILE;
                case Skills.IDs.LIGHT_EATER: return Skills.IDs.Z_LIGHT_EATER;
                case Skills.IDs.LIGHT_FEET: return Skills.IDs.Z_LIGHT_FEET;
                case Skills.IDs.MEDIC: return Skills.IDs.Z_INFECTOR;
                case Skills.IDs.STRONG: return Skills.IDs.Z_STRONG;
                case Skills.IDs.TOUGH: return Skills.IDs.Z_TOUGH;
                default: return null;
            }
        }
        #endregion

        #region Applying/Unapplying effects: ApplyXXX/UnapplyXXX

        /// <summary>
        /// Put the object on fire : firestate = onfire, jump -1.
        /// </summary>
        /// <param name="mapObj"></param>
        public void ApplyOnFire(MapObject mapObj)
        {
            // put object on fire.
            mapObj.FireState = MapObject.Fire.ONFIRE;
            // can't jump on it.
            --mapObj.JumpLevel;
        }

        /// <summary>
        /// Unapply fire effects. FIXME: need to distinguish Unapply (burnable again) vs PutOutFire (ashes)?
        /// </summary>
        /// <param name="mapObj"></param>
        public void UnapplyOnFire(MapObject mapObj)
        {
            // restore jumpability.
            ++mapObj.JumpLevel;
            // extinguish fire, burnable again.
            mapObj.FireState = MapObject.Fire.BURNABLE;
        }
        #endregion






        #region Menu helpers
        /// <summary>
        ///
        /// </summary>
        /// <param name="entries">choices text</param>
        /// <param name="values">(options values) can be null, array must be same length as choices</param>
        /// <param name="gx"></param>
        /// <param name="gy"></param>
        /// <param name="valuesOnNewLine">false: draw values on same line as entries; true: draw values on new lines</param>
        /// <param param name="rightPadding">x padding to add when displaying values</param>
        /// <returns></returns>
        void DrawMenuOrOptions(int currentChoice, Color entriesColor, string[] entries, Color valuesColor, string[] values, int gx, ref int gy, bool valuesOnNewLine = false, int rightPadding = 256)
        {
            int right = gx + rightPadding;

            if (values != null && entries.Length != values.Length)
                throw new ArgumentException("values length!= choices length");

            // display.
            Color entriesShadowColor = Color.FromArgb(entriesColor.A, entriesColor.R / 2, entriesColor.G / 2, entriesColor.B / 2);
            for (int i = 0; i < entries.Length; i++)
            {
                string choiceStr;
                if (i == currentChoice)
                    choiceStr = String.Format("---> {0}", entries[i]);
                else
                    choiceStr = String.Format("     {0}", entries[i]);
                m_UI.UI_DrawStringBold(entriesColor, choiceStr, gx, gy, entriesShadowColor);

                if (values != null)
                {
                    string valueStr;
                    if (i == currentChoice && !valuesOnNewLine)
                        valueStr = String.Format("{0} <---", values[i]);
                    else
                        valueStr = values[i];

                    if (valuesOnNewLine)
                    {
                        gy += BOLD_LINE_SPACING;
                        m_UI.UI_DrawStringBold(valuesColor, valueStr, gx + right, gy);
                    }
                    else
                    {
                        m_UI.UI_DrawStringBold(valuesColor, valueStr, right, gy);
                    }
                }

                gy += BOLD_LINE_SPACING;
            }
        }
        #endregion

        #region Drawing headers & footnotes
        void DrawHeader()
        {
            m_UI.UI_DrawStringBold(Color.Red, "ROGUE SURVIVOR - " + SetupConfig.GAME_VERSION, 0, 0, Color.DarkRed);
        }

        void DrawFootnote(Color color, string text)
        {
            Color shadowColor = Color.FromArgb(color.A, color.R / 2, color.G / 2, color.B / 2);
            m_UI.UI_DrawStringBold(color, String.Format("<{0}>", text), 0, CANVAS_HEIGHT - BOLD_LINE_SPACING, shadowColor);
        }
        #endregion






        #region Achievement banner
        void ShowNewAchievement(Achievement.IDs id)
        {
            // one more achievement.
            ++m_Session.Scoring.CompletedAchievementsCount;

            // get data.
            Achievement ach = m_Session.Scoring.GetAchievement(id);
            string musicToPlay = ach.MusicID;
            string title = ach.Name;
            string[] text = ach.Text;

            // add event.
            m_Session.Scoring.AddEvent(m_Session.WorldTime.TurnCounter, String.Format("** Achievement : {0} for {1} points. **", title, ach.ScoreValue));

            // music.
            m_MusicManager.Stop();
            m_MusicManager.Play(musicToPlay, MusicPriority.PRIORITY_EVENT);

            // prepare banner.
            int longestLine = FindLongestLine(text);
            string starsLine = new string('*', Math.Max(longestLine, 50));
            List<string> lines = new List<string>(text.Length + 3 + 2);
            lines.Add(starsLine);
            lines.Add(String.Format("ACHIEVEMENT : {0}", title));
            lines.Add("CONGRATULATIONS!");
            for (int i = 0; i < text.Length; i++)
                lines.Add(text[i]);
            lines.Add(String.Format("Achievements : {0}/{1}.", m_Session.Scoring.CompletedAchievementsCount, Scoring.MAX_ACHIEVEMENTS));
            lines.Add(starsLine);

            // banner.
            Point pos = new Point(0, 0);
            AddOverlay(new OverlayPopup(lines.ToArray(), Color.Gold, Color.Gold, Color.DimGray, pos));
            ClearMessages();
            if (!m_Player.IsBotPlayer)
                AddMessagePressEnter();
            ClearOverlays();
        }
        #endregion





        #region Various predicates
        public static bool IsInCHAROffice(Location location)
        {
            List<Zone> zones = location.Map.GetZonesAt(location.Position.X, location.Position.Y);
            if (zones == null)
                return false;
            foreach (Zone z in zones)
            {
                if (z.HasGameAttribute(ZoneAttributes.IS_CHAR_OFFICE))
                    return true;
            }
            return false;
        }

        public bool IsInCHARProperty(Location location)
        {
            return location.Map == Session.UniqueMaps.CHARUndergroundFacility.TheMap ||
                IsInCHAROffice(location);
        }

        #endregion

        #region Phone
        bool AreLinkedByPhone(Actor speaker, Actor target)
        {
            // only leader-follower
            if (speaker.Leader != target && target.Leader != speaker)
                return false;

            // check if equipped phones.
            ItemTracker trSpeaker = speaker.GetEquippedItem(DollPart.LEFT_HAND) as ItemTracker;
            if (trSpeaker == null || !trSpeaker.CanTrackFollowersOrLeader)
                return false;
            ItemTracker trTarget = target.GetEquippedItem(DollPart.LEFT_HAND) as ItemTracker;
            if (trTarget == null || !trTarget.CanTrackFollowersOrLeader)
                return false;

            // yep!
            return true;
        }
        #endregion


        #region Dev commands
        public void DEV_ToggleShowActorsStats()
        {
            s_Options.DEV_ShowActorsStats = !s_Options.DEV_ShowActorsStats;
        }

        // alpha10
        public void DEV_TogglePlayerInvincibility()
        {
            if (m_Session == null || m_Player == null)
                return;

            m_Player.IsInvincible = !m_Player.IsInvincible;
            AddMessage(new Message("DEAR DEV, YOU ARE NOW " + (m_Player.IsInvincible ? "INVINCIBLE" : "NOT INVINCIBLE"), m_Session.WorldTime.TurnCounter, Color.LightGreen));
        }

        // alpha10.1
        public void DEV_MaxTrust()
        {
            if (m_Session == null || m_Player == null)
                return;
            if (m_Player.Followers == null)  // alpha10.1 fix
                return;

            foreach (Actor f in m_Player.Followers)
                f.TrustInLeader = Rules.TRUST_MAX;
            AddMessage(new Message("DEAR DEV, FOLLOWERS TRUST MAXED.", m_Session.WorldTime.TurnCounter, Color.LightGreen));
        }
        #endregion

        #region Data files
        void LoadData()
        {
            LoadDataSkills();
            LoadDataItems();
            LoadDataActors();
        }

        void LoadDataActors()
        {
            m_GameActors.LoadFromCSV(m_UI, @"Resources\Data\Actors.csv");
        }

        void LoadDataItems()
        {
            // load all data.
            m_GameItems.LoadMedicineFromCSV(m_UI, @"Resources\Data\Items_Medicine.csv");
            m_GameItems.LoadFoodFromCSV(m_UI, @"Resources\Data\Items_Food.csv");
            m_GameItems.LoadMeleeWeaponsFromCSV(m_UI, @"Resources\Data\Items_MeleeWeapons.csv");
            m_GameItems.LoadRangedWeaponsFromCSV(m_UI, @"Resources\Data\Items_RangedWeapons.csv");
            m_GameItems.LoadExplosivesFromCSV(m_UI, @"Resources\Data\Items_Explosives.csv");
            m_GameItems.LoadBarricadingMaterialFromCSV(m_UI, @"Resources\Data\Items_Barricading.csv");
            m_GameItems.LoadArmorsFromCSV(m_UI, @"Resources\Data\Items_Armors.csv");
            m_GameItems.LoadTrackersFromCSV(m_UI, @"Resources\Data\Items_Trackers.csv");
            m_GameItems.LoadSpraypaintsFromCSV(m_UI, @"Resources\Data\Items_Spraypaints.csv");
            m_GameItems.LoadLightsFromCSV(m_UI, @"Resources\Data\Items_Lights.csv");
            m_GameItems.LoadScentspraysFromCSV(m_UI, @"Resources\Data\Items_Scentsprays.csv");
            m_GameItems.LoadTrapsFromCSV(m_UI, @"Resources\Data\Items_Traps.csv");
            m_GameItems.LoadEntertainmentFromCSV(m_UI, @"Resources\Data\Items_Entertainment.csv");

            // create.
            m_GameItems.CreateModels();
        }

        void LoadDataSkills()
        {
            Skills.LoadSkillsFromCSV(m_UI, @"Resources\Data\Skills.csv");
        }
        #endregion

        // alpha10
        #region Background music
        void UpdateBgMusic()
        {
            if (!s_Options.PlayMusic)
                return;
            if (m_Player == null)
                return;

            // don't interrupt music that has higher priority than bg
            if (m_MusicManager.IsPlaying && m_MusicManager.Priority > MusicPriority.PRIORITY_BGM)
                return;

            // get current map music and play it if not already playing it
            string mapMusic = m_Session.CurrentMap.BgMusic;
            if (string.IsNullOrEmpty(mapMusic))
                return;
            if (m_MusicManager.Music == mapMusic && m_MusicManager.IsPlaying)
                return;

            m_MusicManager.Stop();
            m_MusicManager.Play(mapMusic, MusicPriority.PRIORITY_BGM);
        }
        #endregion

#if DEBUG
        #region Dev
        void AddDevCheatItems()
        {
            /*
            Inventory inv = m_Player.Inventory;

            Item it = new ItemBarricadeMaterial(m_GameItems.WOODENPLANK) { Quantity = 4 };
            inv.AddAll(it);
            it = new ItemBarricadeMaterial(m_GameItems.WOODENPLANK) { Quantity = 3 };
            inv.AddAll(it);
            it = new ItemBarricadeMaterial(m_GameItems.WOODENPLANK) { Quantity = 2 };
            inv.AddAll(it);

            Map map = m_Player.Location.Map;
            Point pos = m_Player.Location.Position;
            it = new ItemBarricadeMaterial(m_GameItems.WOODENPLANK) { Quantity = 3 };
            map.DropItemAt(it, pos);
            it = new ItemBarricadeMaterial(m_GameItems.WOODENPLANK) { Quantity = 3 };
            map.DropItemAt(it, pos);
            Item it2 = new ItemBarricadeMaterial(m_GameItems.WOODENPLANK) { Quantity = 3 };
            map.DropItemAt(it2, pos);
            it.Quantity--;
            */

            /* traps
            Item it = new ItemTrap(m_GameItems.BEAR_TRAP);
            inv.AddAll(it);
            it = new ItemTrap(m_GameItems.BEAR_TRAP);
            inv.AddAll(it);
            it = new ItemTrap(m_GameItems.BEAR_TRAP);
            inv.AddAll(it);
            it = new ItemTrap(m_GameItems.BEAR_TRAP);
            inv.AddAll(it);
            */
        }

        void AddDevCheatSkills()
        {
            // all the living skills.
            /*
            for (int id = (int)Skills.IDs._FIRST_LIVING; id < (int)Skills.IDs._FIRST_UNDEAD; id++)
                for (int l = 0; l < 5; l++)
                    m_Player.Sheet.SkillTable.AddOrIncreaseSkill(id);
            */
        }

        void AddDevMiscStuff()
        {
            // insane right of the bat.
            /*
            m_Player.Sanity = 0;
            */

            // join cops.
            //m_Player.Faction = GameFactions.ThePolice;
        }

        void DrawTileDev(Map m, Tile t, int x, int y, Point toScreen)
        {
        }
        #endregion
#endif
    }
}
