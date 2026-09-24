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
    partial class BaseTownGenerator : BaseMapGenerator
    {
        #region Types
        public struct Parameters
        {
            #region Fields
            int m_MapWidth;
            int m_MapHeight;
            int m_MinBlockSize;
            int m_WreckedCarChance;
            int m_CHARBuildingChance;
            int m_ShopBuildingChance;
            int m_ParkBuildingChance;
            int m_PostersChance;
            int m_TagsChance;
            int m_ItemInShopShelfChance;
            int m_PolicemanChance;
            #endregion

            #region Properties
            /// <summary>
            /// District the map is currently generated in.
            /// </summary>
            public District District
            {
                get;
                set;
            }

            /// <summary>
            /// Do we need to generate the Police Station in this district?
            /// </summary>
            public bool GeneratePoliceStation
            {
                get;
                set;
            }

            /// <summary>
            /// Do we need to generate the Hospital in this district?
            /// </summary>
            public bool GenerateHospital
            {
                get;
                set;
            }

            public int MapWidth
            {
                get { return m_MapWidth; }
                set
                {
                    if (value <= 0 || value > RogueGame.MAP_MAX_WIDTH) throw new ArgumentOutOfRangeException("MapWidth");
                    m_MapWidth = value;
                }
            }

            public int MapHeight
            {
                get { return m_MapHeight; }
                set
                {
                    if (value <= 0 || value > RogueGame.MAP_MAX_WIDTH) throw new ArgumentOutOfRangeException("MapHeight");
                    m_MapHeight = value;
                }
            }

            public int MinBlockSize
            {
                get { return m_MinBlockSize; }
                set
                {
                    if (value < 4 || value > 32) throw new ArgumentOutOfRangeException("MinBlockSize must be [4..32]");
                    m_MinBlockSize = value;
                }
            }

            public int WreckedCarChance
            {
                get { return m_WreckedCarChance; }
                set
                {
                    if (value < 0 || value > 100) throw new ArgumentOutOfRangeException("WreckedCarChance must be [0..100]");
                    m_WreckedCarChance = value;
                }
            }

            public int ShopBuildingChance
            {
                get { return m_ShopBuildingChance; }
                set
                {
                    if (value < 0 || value > 100) throw new ArgumentOutOfRangeException("ShopBuildingChance must be [0..100]");
                    m_ShopBuildingChance = value;
                }
            }

            public int ParkBuildingChance
            {
                get { return m_ParkBuildingChance; }
                set
                {
                    if (value < 0 || value > 100) throw new ArgumentOutOfRangeException("ParkBuildingChance must be [0..100]");
                    m_ParkBuildingChance = value;

                }
            }

            public int CHARBuildingChance
            {
                get { return m_CHARBuildingChance; }
                set
                {
                    if (value < 0 || value > 100) throw new ArgumentOutOfRangeException("CHARBuildingChance must be [0..100]");
                    m_CHARBuildingChance = value;

                }
            }

            public int PostersChance
            {
                get { return m_PostersChance; }
                set
                {
                    if (value < 0 || value > 100) throw new ArgumentOutOfRangeException("PostersChance must be [0..100]");
                    m_PostersChance = value;

                }
            }

            public int TagsChance
            {
                get { return m_TagsChance; }
                set
                {
                    if (value < 0 || value > 100) throw new ArgumentOutOfRangeException("TagsChance must be [0..100]");
                    m_TagsChance = value;

                }
            }

            public int ItemInShopShelfChance
            {
                get { return m_ItemInShopShelfChance; }
                set
                {
                    if (value < 0 || value > 100) throw new ArgumentOutOfRangeException("ItemInShopShelfChance must be [0..100]");
                    m_ItemInShopShelfChance = value;

                }
            }


            public int PolicemanChance
            {
                get { return m_PolicemanChance; }
                set
                {
                    if (value < 0 || value > 100) throw new ArgumentOutOfRangeException("PolicemanChance must be [0..100]");
                    m_PolicemanChance = value;

                }
            }
            #endregion
        }

        public class Block
        {
            /// <summary>
            /// Rectangle enclosing the whole block.
            /// </summary>
            public Rectangle Rectangle { get; set; }

            /// <summary>
            /// Rectangle enclosing the building : the blocks minus the walkway ring.
            /// </summary>
            public Rectangle BuildingRect { get; set; }

            /// <summary>
            /// Rectangle enclosing the inside of the building : the building minus the walls ring.
            /// </summary>
            public Rectangle InsideRect { get; set; }


            public Block(Rectangle rect)
            {
                ResetRectangle(rect);
            }

            public Block(Block copyFrom)
            {
                this.Rectangle = copyFrom.Rectangle;
                this.BuildingRect = copyFrom.BuildingRect;
                this.InsideRect = copyFrom.InsideRect;
            }

            public void ResetRectangle(Rectangle rect)
            {
                this.Rectangle = rect;
                this.BuildingRect = new Rectangle(rect.Left + 1, rect.Top + 1, rect.Width - 2, rect.Height - 2);
                this.InsideRect = new Rectangle(this.BuildingRect.Left + 1, this.BuildingRect.Top + 1, this.BuildingRect.Width - 2, this.BuildingRect.Height - 2);
            }
        }

        protected enum ShopType : byte
        {
            _FIRST,

            GENERAL_STORE = _FIRST,
            GROCERY,
            SPORTSWEAR,
            PHARMACY,
            CONSTRUCTION,
            GUNSHOP,
            HUNTING,

            _COUNT
        }

        protected enum CHARBuildingType : byte
        {
            NONE,
            AGENCY,
            OFFICE
        }

        // alpha10
        protected enum HouseOutsideRoomType : byte
        {
            _FIRST,

            GARDEN = _FIRST,
            PARKING_LOT,

            _COUNT
        }
        #endregion

        #region Constants
        public static readonly Parameters DEFAULT_PARAMS = new Parameters()
        {
            MapWidth = RogueGame.MAP_MAX_WIDTH,
            MapHeight = RogueGame.MAP_MAX_HEIGHT,
            MinBlockSize = 11, // 12 for 75x75 map size; 10 gives to many small buildings.
            WreckedCarChance = 10,
            ShopBuildingChance = 10,
            ParkBuildingChance = 10,
            CHARBuildingChance = 10,
            PostersChance = 2,
            TagsChance = 2,
            ItemInShopShelfChance = 100,
            PolicemanChance = 15
        };

        const int PARK_TREE_CHANCE = 25;
        const int PARK_BENCH_CHANCE = 5;
        const int PARK_ITEM_CHANCE = 5;
        const int PARK_SHED_CHANCE = 75;  // alpha10.1
        const int PARK_SHED_WIDTH = 5;  // alpha10
        const int PARK_SHED_HEIGHT = 5;  // alpha10

        const int MAX_CHAR_GUARDS_PER_OFFICE = 3;

        const int SEWERS_ITEM_CHANCE = 1;
        const int SEWERS_JUNK_CHANCE = 10;
        const int SEWERS_TAG_CHANCE = 10;
        const int SEWERS_IRON_FENCE_PER_BLOCK_CHANCE = 50; // 8 fences average on std maps size 75x75.
        const int SEWERS_ROOM_CHANCE = 20;

        const int SUBWAY_TAGS_POSTERS_CHANCE = 20;

        const int HOUSE_LIVINGROOM_ITEMS_ON_TABLE = 2;
        const int HOUSE_KITCHEN_ITEMS_ON_TABLE = 2;
        const int HOUSE_KITCHEN_ITEMS_IN_FRIDGE = 3;
        const int HOUSE_BASEMENT_CHANCE = 30;
        const int HOUSE_BASEMENT_OBJECT_CHANCE_PER_TILE = 10;
        const int HOUSE_BASEMENT_PILAR_CHANCE = 20;
        const int HOUSE_BASEMENT_WEAPONS_CACHE_CHANCE = 20;
        const int HOUSE_BASEMENT_ZOMBIE_RAT_CHANCE = 5; // per tile.
        // alpha10 new house stuff
        const int HOUSE_OUTSIDE_ROOM_NEED_MIN_ROOMS = 4;
        const int HOUSE_OUTSIDE_ROOM_CHANCE = 75;
        const int HOUSE_GARDEN_TREE_CHANCE = 10;  // per tile
        const int HOUSE_PARKING_LOT_CAR_CHANCE = 10;  // per tile
        // alpha10.1 new house floorplan: apartements
        const int HOUSE_IS_APARTMENTS_CHANCE = 50;

        const int SHOP_BASEMENT_CHANCE = 30;
        const int SHOP_BASEMENT_SHELF_CHANCE_PER_TILE = 5;
        const int SHOP_BASEMENT_ITEM_CHANCE_PER_SHELF = 33;
        const int SHOP_WINDOW_CHANCE = 30;
        const int SHOP_BASEMENT_ZOMBIE_RAT_CHANCE = 5; // per tile.
        #endregion

        #region Fields
        Parameters m_Params = DEFAULT_PARAMS;
        protected DiceRoller m_DiceRoller;

        /// <summary>
        /// Blocks on surface map since during current generation.
        /// </summary>
        List<Block> m_SurfaceBlocks;
        #endregion

        #region Properties
        public Parameters Params
        {
            get { return m_Params; }
            set { m_Params = value; }
        }
        #endregion

        public BaseTownGenerator(RogueGame game, Parameters parameters)
            : base(game)
        {
            m_Params = parameters;
            m_DiceRoller = new DiceRoller(game.Session.Seed);
        }












        #region Special Locations




        #endregion



    }
}
