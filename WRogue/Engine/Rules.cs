using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

namespace djack.RogueSurvivor.Engine
{
    partial class Rules
    {
        #region Constants
        public const int BASE_ACTION_COST = 100;
        public const int BASE_SPEED = BASE_ACTION_COST;

        #region Stamina
        /// <summary>
        /// Marker for infinite stamina, value doesnt matter since !Abilities.CanTire is checked instead.
        /// </summary>
        public const int STAMINA_INFINITE = 99;

        /// <summary>
        /// Minimum stamina for doing tiring actions (melee, running, jumping...)
        /// </summary>
        public const int STAMINA_MIN_FOR_ACTIVITY = 10;

        /// <summary>
        /// Stamina cost for running.
        /// </summary>
        public const int STAMINA_COST_RUNNING = 4;

        /// <summary>
        /// Bonus to stamina when waiting.
        /// </summary>
        public const int STAMINA_REGEN_WAIT = 2;

        /// <summary>
        /// Bonus to stamina each turn.
        /// </summary>
        public const int STAMINA_REGEN_PER_TURN = 2;

        /// <summary>
        /// Stamina cost for jumping.
        /// </summary>
        public const int STAMINA_COST_JUMP = 8;

        /// <summary>
        /// Stamina cost for melee fighting.
        /// </summary>
        public const int STAMINA_COST_MELEE_ATTACK = 8;

        /// <summary>
        /// Stamina cost for moving with a dragged corpse.
        /// </summary>
        public const int STAMINA_COST_MOVE_DRAGGED_CORPSE = 8;
        #endregion

        #region Stumbling
        public const int JUMP_STUMBLE_CHANCE = 25;
        public const int JUMP_STUMBLE_ACTION_COST = BASE_ACTION_COST;
        #endregion

        #region Scents
        /// <summary>
        /// How much scent living actors drop on their tile.
        /// </summary>
        public const int LIVING_SCENT_DROP = OdorScent.MAX_STRENGTH;

        /// <summary>
        /// How much scent undead masters drop on their tile.
        /// </summary>
        public const int UNDEAD_MASTER_SCENT_DROP = OdorScent.MAX_STRENGTH;
        #endregion

        #region Barricading
        public const int BARRICADING_MAX = 2 * DoorWindow.BASE_HITPOINTS;
        #endregion

        #region FOV
        const int MINIMAL_FOV = 2;
        #endregion

        #region Day/Night and Weather effects
        public const int FOV_PENALTY_SUNSET = 1;
        public const int FOV_PENALTY_EVENING = 2;
        public const int FOV_PENALTY_MIDNIGHT = 3;
        public const int FOV_PENALTY_DEEP_NIGHT = 4;
        public const int FOV_PENALTY_SUNRISE = 2;
        public const int NIGHT_STA_PENALTY = 2;

        public const int FOV_PENALTY_RAIN = 1;
        public const int FOV_PENALTY_HEAVY_RAIN = 2;
        #endregion

        #region Weapons & Firing
        public const int MELEE_WEAPON_BREAK_CHANCE = 1;
        public const int MELEE_WEAPON_FRAGILE_BREAK_CHANCE = 3;
        public const int MELEE_DISARM_BASE_CHANCE = 5;  // alpha10
        public const int FIREARM_JAM_CHANCE_NO_RAIN = 1;
        public const int FIREARM_JAM_CHANCE_RAIN = 3;
        const float FIRING_WHEN_STA_TIRED = 0.75f;     // -25%
        const float FIRING_WHEN_STA_NOT_FULL = 0.90f;  // -10%
        // alpha10 made into constants
        const float FIRING_WHEN_SLP_EXHAUSTED = 0.50f; // -50%
        const float FIRING_WHEN_SLP_SLEEPY = 0.75f; // -25%
        #endregion

        #region Body armors
        public const int BODY_ARMOR_BREAK_CHANCE = 2;
        #endregion

        #region Hunger/Rot & Sleep & Sanity
        public const int FOOD_BASE_POINTS = WorldTime.TURNS_PER_HOUR * 48;
        public const int FOOD_HUNGRY_LEVEL = FOOD_BASE_POINTS / 2;

        public const int ROT_BASE_POINTS = WorldTime.TURNS_PER_DAY * 4;
        public const int ROT_HUNGRY_LEVEL = ROT_BASE_POINTS / 2;

        public const int SLEEP_BASE_POINTS = WorldTime.TURNS_PER_HOUR * 60;  // 60 = starting game at midnight => sleepy in late evening.
        public const int SLEEP_SLEEPY_LEVEL = SLEEP_BASE_POINTS / 2;

        public const int SANITY_BASE_POINTS = WorldTime.TURNS_PER_DAY * 4;
        public const int SANITY_UNSTABLE_LEVEL = SANITY_BASE_POINTS / 2;
        public const int SANITY_NIGHTMARE_CHANCE = 2;
        public const int SANITY_NIGHTMARE_SLP_LOSS = 2 * WorldTime.TURNS_PER_HOUR;
        public const int SANITY_NIGHTMARE_SAN_LOSS = WorldTime.TURNS_PER_HOUR;
        public const int SANITY_NIGHTMARE_STA_LOSS = 10 * STAMINA_COST_RUNNING;  // alpha10 -- worth running for 10 turns
        public const int SANITY_INSANE_ACTION_CHANCE = 5;

        public const int SANITY_HIT_BUTCHERING_CORPSE = WorldTime.TURNS_PER_HOUR;
        public const int SANITY_HIT_UNDEAD_EATING_CORPSE = 2 * WorldTime.TURNS_PER_HOUR;
        public const int SANITY_HIT_LIVING_EATING_CORPSE = 4 * WorldTime.TURNS_PER_HOUR;
        public const int SANITY_HIT_EATEN_ALIVE = 4 * WorldTime.TURNS_PER_HOUR;
        public const int SANITY_HIT_ZOMBIFY = 2 * WorldTime.TURNS_PER_HOUR;
        public const int SANITY_HIT_BOND_DEATH = 8 * WorldTime.TURNS_PER_HOUR;

        // alpha10 increased san recovery; chating & trading also recover san
        public const int SANITY_RECOVER_KILL_UNDEAD = 3 * WorldTime.TURNS_PER_HOUR;  // was 2h
        public const int SANITY_RECOVER_BOND_CHANCE = 5;
        public const int SANITY_RECOVER_BOND = 4 * WorldTime.TURNS_PER_HOUR;  // was 1h
        public const int SANITY_RECOVER_CHAT_OR_TRADE = 3 * WorldTime.TURNS_PER_HOUR;

        /// <summary>
        /// When starving, chance of dying from starvation each turn.
        /// </summary>
        public const int FOOD_STARVING_DEATH_CHANCE = 5;

        /// <summary>
        /// When eating expired food, chances of vomitting.
        /// </summary>
        public const int FOOD_EXPIRED_VOMIT_CHANCE = 25;

        /// <summary>
        /// Stamina lost for vomitting.
        /// </summary>
        public const int FOOD_VOMIT_STA_COST = 100;

        /// <summary>
        /// When rot-starving %% chance to loose an HP.
        /// </summary>
        public const int ROT_STARVING_HP_CHANCE = 5;

        /// <summary>
        /// When rot-hungry %% chance to loose a skill.
        /// </summary>
        public const int ROT_HUNGRY_SKILL_CHANCE = 5;

        /// <summary>
        /// When exhausted, chance of collapsing each turn.
        /// </summary>
        public const int SLEEP_EXHAUSTION_COLLAPSE_CHANCE = 5;

        /// <summary>
        /// When sleeping on a couch, how many sleep points per turn are regenerated.
        /// </summary>
        const int SLEEP_COUCH_SLEEPING_REGEN = 1 + SLEEP_BASE_POINTS / (12 * WorldTime.TURNS_PER_HOUR);

        /// <summary>
        /// When sleeping out of a couch, how many sleep points per turn are regenerated.
        /// </summary>
        public const int SLEEP_NOCOUCH_SLEEPING_REGEN = (2 * SLEEP_COUCH_SLEEPING_REGEN) / 3;

        /// <summary>
        /// When sleeping on a couch, chance to heal per turn.
        /// </summary>
        public const int SLEEP_ON_COUCH_HEAL_CHANCE = 5;

        /// <summary>
        /// When sleeping and healing, how many HPs regained.
        /// </summary>
        public const int SLEEP_HEAL_HITPOINTS = 2;
        #endregion

        #region Loud noises
        public const int LOUD_NOISE_RADIUS = 5;
        const int LOUD_NOISE_BASE_WAKEUP_CHANCE = 10;
        const int LOUD_NOISE_DISTANCE_BONUS = 10;
        #endregion

        #region Victims dropping items.
        public const int VICTIM_DROP_GENERIC_ITEM_CHANCE = 50;
        public const int VICTIM_DROP_AMMOFOOD_ITEM_CHANCE = 100;
        #endregion

        #region Improvised weapons
        public const int IMPROVED_WEAPONS_FROM_BROKEN_WOOD_CHANCE = 25;
        #endregion

        // alpha10 replaced with rapid fire attacks property of ranged weapons
        /*
        #region Rapid fire
        public const float RAPID_FIRE_FIRST_SHOT_ACCURACY = 0.70f;  // alpha9 was 0.50f;
        public const float RAPID_FIRE_SECOND_SHOT_ACCURACY = 0.50f;  // alpha9 was 0.30f;
        #endregion
        */

        #region Trackers
        public const int ZTRACKINGRADIUS = 6;
        #endregion

        #region Actor weight
        public const int DEFAULT_ACTOR_WEIGHT = 10;
        #endregion

        #region Things on fire
        /// <summary>
        /// When raining, chance per turn to test effects on fire on the map.
        /// 100 = will check every rain turn. Not recommended as it eats CPU.
        /// </summary>
        public const int FIRE_RAIN_TEST_CHANCE = 1;

        /// <summary>
        /// Chance for rain to put out a fire.
        /// </summary>
        public const int FIRE_RAIN_PUT_OUT_CHANCE = 10;
        #endregion

        #region Trust & Bond
        public const int TRUST_NEUTRAL = 0;
        public const int TRUST_TRUSTING_THRESHOLD = 12 * WorldTime.TURNS_PER_HOUR; // 12h of sticking together.
        public const int TRUST_MIN = -TRUST_TRUSTING_THRESHOLD;
        public const int TRUST_MAX = 4 * TRUST_TRUSTING_THRESHOLD;
        public const int TRUST_BOND_THRESHOLD = TRUST_MAX;
        public const int TRUST_BASE_INCREASE = 1;                                       // 1pt per turn.
        public const int TRUST_GOOD_GIFT_INCREASE = 3 * WorldTime.TURNS_PER_HOUR;       // 3 trust-hours gained.
        public const int TRUST_MISC_GIFT_INCREASE = TRUST_BASE_INCREASE + TRUST_GOOD_GIFT_INCREASE / 10;
        public const int TRUST_GIVE_ITEM_ORDER_PENALTY = -WorldTime.TURNS_PER_HOUR;     // 1 trust-hours lost.
        public const int TRUST_LEADER_KILL_ENEMY = 3 * WorldTime.TURNS_PER_HOUR;        // 3 trust-hours gain.
        #endregion

        #region Murder
        public const int MURDERER_SPOTTING_BASE_CHANCE = 5;
        public const int MURDERER_SPOTTING_DISTANCE_PENALTY = 1;
        public const int MURDER_SPOTTING_MURDERCOUNTER_BONUS = 5;
        #endregion

        #region Infection & Corpses
        const float INFECTION_BASE_FACTOR = 1.0f;

        public static int INFECTION_LEVEL_1_WEAK_STA = 24;//16;
        public static int INFECTION_LEVEL_2_TIRED_STA = 24;//16;
        public static int INFECTION_LEVEL_2_TIRED_SLP = 3 * WorldTime.TURNS_PER_HOUR;//2 * WorldTime.TURNS_PER_HOUR;
        public static int INFECTION_LEVEL_4_BLEED_HP = 6;//4;

        public static int INFECTION_EFFECT_TRIGGER_CHANCE_1000 = (int)(1000 * 2.0f / WorldTime.TURNS_PER_DAY);

        const int CORPSE_ZOMBIFY_BASE_CHANCE = 0;
        const int CORPSE_ZOMBIFY_DELAY = 6 * WorldTime.TURNS_PER_HOUR;
        const float CORPSE_ZOMBIFY_INFECTIONP_FACTOR = 1f; // 1% infection = X% to raise when checked.
        const float CORPSE_ZOMBIFY_NIGHT_FACTOR = 2.0f;
        const float CORPSE_ZOMBIFY_DAY_FACTOR = 0.01f;
        const float CORPSE_ZOMBIFY_TIME_FACTOR = 1f / WorldTime.TURNS_PER_DAY; // -1% per day
        const float CORPSE_EATING_NUTRITION_FACTOR = 10.0f;
        const float CORPSE_EATING_INFECTION_FACTOR = 0.1f;

        const float CORPSE_DECAY_PER_TURN = 4f / WorldTime.TURNS_PER_DAY; // -1 HP per day ~ 1 week to rot at 30 HP.
        #endregion

        #region Refugees
        public const int GIVE_RARE_ITEM_DAY = 7;
        public const int GIVE_RARE_ITEM_CHANCE = 5;
        #endregion

        // alpha10
        #region Traps
        public const int TRAP_UNDEAD_ACTOR_TRIGGER_PENALTY = 30;
        public const int TRAP_SMALL_ACTOR_AVOID_BONUS = 90;
        public const int CRUSHING_GATES_DAMAGE = 60;  // alpha10.1
        #endregion

        #region Skills limits & bonuses per level
        public const int UPGRADE_SKILLS_TO_CHOOSE_FROM = 5;
        public const int UNDEAD_UPGRADE_SKILLS_TO_CHOOSE_FROM = 2;

        /**
         * Actual skill values will be read from Skills.csv so they are not const.
         */
        // alpha10 updated default values to their currnt values in Skills.csv, was confusing.
        #region Livings

        public static int SKILL_AGILE_ATK_BONUS = 2;
        public static int SKILL_AGILE_DEF_BONUS = 4;

        public static float SKILL_AWAKE_SLEEP_BONUS = 0.10f;
        public static float SKILL_AWAKE_SLEEP_REGEN_BONUS = 0.15f;

        public static int SKILL_BOWS_ATK_BONUS = 10;
        public static int SKILL_BOWS_DMG_BONUS = 4;

        public static float SKILL_CARPENTRY_BARRICADING_BONUS = 0.15f;
        public static int SKILL_CARPENTRY_LEVEL3_BUILD_BONUS = 1;

        public static int SKILL_CHARISMATIC_TRUST_BONUS = 2;
        public static int SKILL_CHARISMATIC_TRADE_BONUS = 10;

        public static int SKILL_FIREARMS_ATK_BONUS = 10;
        public static int SKILL_FIREARMS_DMG_BONUS = 2;

        public static int SKILL_HARDY_HEAL_CHANCE_BONUS = 1;

        public static int SKILL_HAULER_INV_BONUS = 1;

        public static int SKILL_HIGH_STAMINA_STA_BONUS = 8;

        public static int SKILL_LEADERSHIP_FOLLOWER_BONUS = 1;

        public static float SKILL_LIGHT_EATER_MAXFOOD_BONUS = 0.10f;
        public static float SKILL_LIGHT_EATER_FOOD_BONUS = 0.15f;

        public static int SKILL_LIGHT_FEET_TRAP_BONUS = 15; // alpha10 prev value was 5

        public static int SKILL_LIGHT_SLEEPER_WAKEUP_CHANCE_BONUS = 20;  // alpha10 prev value was 10

        public static int SKILL_MARTIAL_ARTS_ATK_BONUS = 6;
        public static int SKILL_MARTIAL_ARTS_DMG_BONUS = 2;
        public static int SKILL_MARTIAL_ARTS_DISARM_BONUS = 10;  // alpha10

        public static float SKILL_MEDIC_BONUS = 0.15f;
        public static int SKILL_MEDIC_REVIVE_BONUS = 10;
        public static int SKILL_MEDIC_LEVEL_FOR_REVIVE_EST = 1;

        public static int SKILL_NECROLOGY_UNDEAD_BONUS = 2;
        public static int SKILL_NECROLOGY_CORPSE_BONUS = 4;
        public static int SKILL_NECROLOGY_LEVEL_FOR_INFECTION = 3;
        public static int SKILL_NECROLOGY_LEVEL_FOR_RISE = 5;

        public static float SKILL_STRONG_PSYCHE_LEVEL_BONUS = 0.15f;

        public static int SKILL_STRONG_DMG_BONUS = 2;
        public static int SKILL_STRONG_THROW_BONUS = 1;
        public static int SKILL_STRONG_RESIST_DISARM_BONUS = 5;  // alpha10

        public static int SKILL_TOUGH_HP_BONUS = 6;

        public static int SKILL_UNSUSPICIOUS_BONUS = 20;  // alpha10

        public static int UNSUSPICIOUS_BAD_OUTFIT_PENALTY = 75;  // alpha10 ; prev was 50
        public static int UNSUSPICIOUS_GOOD_OUTFIT_BONUS = 75; // alpha10 ; prev was 50

        #endregion

        #region Undeads
        public static int SKILL_ZAGILE_ATK_BONUS = 1;
        public static int SKILL_ZAGILE_DEF_BONUS = 2;

        public static int SKILL_ZSTRONG_DMG_BONUS = 2;

        public static int SKILL_ZTOUGH_HP_BONUS = 4;

        public static float SKILL_ZEATER_REGEN_BONUS = 0.20f;

        public static float SKILL_ZTRACKER_SMELL_BONUS = 0.10f;

        public static int SKILL_ZLIGHT_FEET_TRAP_BONUS = 3;

        public static int SKILL_ZGRAB_CHANCE = 4;  // alpha10 prev was 2

        public static float SKILL_ZINFECTOR_BONUS = 0.15f;

        public static float SKILL_ZLIGHT_EATER_MAXFOOD_BONUS = 0.15f;
        public static float SKILL_ZLIGHT_EATER_FOOD_BONUS = 0.10f;
        #endregion

        #endregion
        #endregion

        #region Fields
        readonly DiceRoller m_DiceRoller;
        #endregion

        #region Properties
        public DiceRoller DiceRoller { get { return m_DiceRoller; } }
        #endregion

        #region Init
        public Rules(DiceRoller diceRoller)
        {
            if (diceRoller == null)
                throw new ArgumentNullException("diceRoller");

            m_DiceRoller = diceRoller;
        }
        #endregion

        #region Rolling & Random choices
        /// <summary>
        /// Roll in range [min, max[.
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public int Roll(int min, int max)
        {
            return m_DiceRoller.Roll(min, max);
        }

        public bool RollChance(int chance)
        {
            return m_DiceRoller.RollChance(chance);
        }

        public float RollFloat()
        {
            return m_DiceRoller.RollFloat();
        }

        /// <summary>
        /// Apply a random deviation to a value.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="deviation">amount (not percentage!) of deviation from value</param>
        /// <returns></returns>
        public float Randomize(float value, float deviation)
        {
            float halfDeviation = deviation/2f;
            return value - halfDeviation * m_DiceRoller.RollFloat() + halfDeviation * m_DiceRoller.RollFloat();
        }

        public int RollX(Map map)
        {
            if (map == null)
                throw new ArgumentNullException("map");

            return m_DiceRoller.Roll(0, map.Width);
        }

        public int RollY(Map map)
        {
            if (map == null)
                throw new ArgumentNullException("map");

            return m_DiceRoller.Roll(0, map.Height);
        }

        public Direction RollDirection()
        {
            return Direction.COMPASS[m_DiceRoller.Roll(0, 8)];
        }

        /// <summary>
        /// [0,skillValue]
        /// </summary>
        /// <param name="skillValue"></param>
        /// <returns></returns>
        public int RollSkill(int skillValue)
        {
            if (skillValue <= 0)
                return 0;
            // bell curve results = less extremes => favor higher skill vs lower skill
            return (m_DiceRoller.Roll(0, skillValue + 1) + m_DiceRoller.Roll(0, skillValue + 1)) / 2;
        }

        /// <summary>
        /// [damageValue/2, damageValue]
        /// </summary>
        /// <param name="damageValue"></param>
        /// <returns></returns>
        public int RollDamage(int damageValue)
        {
            if (damageValue <= 0)
                return 0;
            return m_DiceRoller.Roll(damageValue / 2, damageValue + 1);
        }

        public Location RollNeighbourInMap(Location from)
        {
            while (true)
            {
                Location next = from + RollDirection();

                if (next.Map != from.Map)
                    continue;

                if (from.Map.IsInBounds(next.Position.X, next.Position.Y))
                    return next;
            }
        }

        public bool RollValidMoveInMap(Actor actor, int maxTries, out Location nextLocation)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");

            for (int i = 0; i < maxTries; i++)
            {
                nextLocation = RollNeighbourInMap(actor.Location);

                if (IsWalkableFor(actor, nextLocation.Map, nextLocation.Position.X, nextLocation.Position.Y))
                    return true;
            }

            nextLocation = actor.Location;
            return false;
        }

        public bool RollValidMoveDirection(Actor actor, int maxTries, out Direction direction)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");

            for (int i = 0; i < maxTries; i++)
            {
                direction = RollDirection();
                Location nextLocation = actor.Location + direction;
                if (nextLocation.Map != actor.Location.Map)
                    continue;
                if (IsWalkableFor(actor, nextLocation))
                    return true;
            }

            direction = null;
            return false;
        }

        public bool RollValidBumpDirection(Actor actor, int maxTries, out Direction direction)
        {
            if (actor == null)
                throw new ArgumentNullException("actor");

            for (int i = 0; i < maxTries; i++)
            {
                direction = RollDirection();
                Location nextLocation = actor.Location + direction;
                if (nextLocation.Map != actor.Location.Map)
                    continue;
                return true;
            }

            direction = null;
            return false;
        }
        #endregion

        #region Rules checking
















        #endregion

        #region Distances

        public bool IsAdjacent(Location a, Location b)
        {
            if (a.Map != b.Map) return false;
            return IsAdjacent(a.Position, b.Position);
        }

        public bool IsAdjacent(Point pA, Point pB)
        {
            return Math.Abs(pA.X - pB.X) < 2 && Math.Abs(pA.Y - pB.Y) < 2;
        }

        public int GridDistance(Point pA, int bX, int bY)
        {
            return Math.Max(Math.Abs(pA.X - bX), Math.Abs(pA.Y - bY));
        }

        public int GridDistance(Point pA, Point pB)
        {
            return Math.Max(Math.Abs(pA.X - pB.X), Math.Abs(pA.Y - pB.Y));
        }

        /// <summary>
        /// Standard distance formula: square root of summed squares.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        public float StdDistance(Point from, Point to)
        {
            int dX = to.X - from.X;
            int dY = to.Y - from.Y;
            int square = dX * dX + dY * dY;
            return (float)Math.Sqrt(square);
        }

        public float StdDistance(Point v)
        {
            return (float)Math.Sqrt(v.X * v.X + v.Y * v.Y);
        }

        /// <summary>
        /// Distance to use in LOS computing. Based on standard distance and modified to have a nice circle FOV.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        public float LOSDistance(Point from, Point to)
        {
            int dX = to.X - from.X;
            int dY = to.Y - from.Y;
            int square = dX * dX + dY * dY;
            return (float)Math.Sqrt(0.75f * square); // nice factor to have a smooth circle (0.90: too rough)
        }
        #endregion

        #region Actor turn ordering
        public Actor GetNextActorToAct(Map map, int turnCounter)
        {
            if (map == null)
                return null;

            int n = map.CountActors;
            for (int i = map.CheckNextActorIndex; i < n; i++)
            {
                Actor a = map.GetActor(i);
                if (a.ActionPoints > 0 && !a.IsSleeping)
                {
                    map.CheckNextActorIndex = i;
                    return a;
                }
            }

            return null;

#if false
            // old unoptimized algo : scan all actors.
            foreach (Actor actor in map.Actors)
            {
                if (actor.ActionPoints > 0 && !actor.IsSleeping)
                    return actor;
            }

            return null;
#endif
        }

        public bool IsActorBeforeInMapList(Map map, Actor actor, Actor other)
        {
            foreach (Actor a in map.Actors)
            {
                if (a == actor)
                    return true;
                if (a == other)
                    return false;
            }

            // by default, assume yes.
            return true;
        }

        public bool CanActorActThisTurn(Actor actor)
        {
            if (actor == null)
                return false;

            return actor.ActionPoints > 0;
        }

        public bool CanActorActNextTurn(Actor actor)
        {
            if (actor == null)
                return false;

            return actor.ActionPoints + ActorSpeed(actor) > 0;
        }

        /// <summary>
        /// If actor spend a turn now, will actor get the chance to act before other?
        /// </summary>
        /// <param name="actor"></param>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool WillActorActAgainBefore(Actor actor, Actor other)
        {
            // if other can still act, nope.
            if (other.ActionPoints > 0)
                return false;

            // if other will be able to act next turn BEFORE actor, nope.
            if (other.ActionPoints + ActorSpeed(other) > 0 && IsActorBeforeInMapList(actor.Location.Map, other, actor))
                return false;

            // safe!
            return true;
        }

        public bool WillOtherActTwiceBefore(Actor actor, Actor other)
        {
            if (IsActorBeforeInMapList(actor.Location.Map, actor, other))
            {
                if (other.ActionPoints > BASE_ACTION_COST)
                    return true;
                else
                    return false;
            }
            else
            {
                if (other.ActionPoints + ActorSpeed(other) > BASE_ACTION_COST)
                    return true;
                else
                    return false;
            }
        }
        #endregion



        #region Day/Night, Weather & Lighting effects
        /// <summary>
        ///
        /// </summary>
        /// <param name="actor"></param>
        /// <param name="time"></param>
        /// <returns>penalty as number > 0</returns>
        public int NightFovPenalty(Actor actor, WorldTime time)
        {
            if (actor.Model.Abilities.IsUndead)
                return 0;
            else
            {
                switch (time.Phase)
                {
                    case DayPhase.SUNSET: return FOV_PENALTY_SUNSET;
                    case DayPhase.EVENING: return FOV_PENALTY_EVENING;
                    case DayPhase.MIDNIGHT: return FOV_PENALTY_MIDNIGHT;
                    case DayPhase.DEEP_NIGHT: return FOV_PENALTY_DEEP_NIGHT;
                    case DayPhase.SUNRISE: return FOV_PENALTY_SUNRISE;
                    default: return 0;
                }
            }
        }

        public int NightStaminaPenalty(Actor actor)
        {
            if (actor.Model.Abilities.IsUndead)
                return 0;
            else
                return NIGHT_STA_PENALTY;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="actor"></param>
        /// <param name="weather"></param>
        /// <returns>penalty as number > 0</returns>
        public int WeatherFovPenalty(Actor actor, Weather weather)
        {
            if (actor.Model.Abilities.IsUndead)
                return 0;
            else
            {
                switch (weather)
                {
                    case Weather.RAIN: return FOV_PENALTY_RAIN;
                    case Weather.HEAVY_RAIN: return FOV_PENALTY_HEAVY_RAIN;
                    default: return 0;
                }
            }
        }

        public bool IsWeatherRain(Weather weather)
        {
            switch (weather)
            {
                case Weather.CLEAR:
                case Weather.CLOUDY:
                    return false;
                case Weather.HEAVY_RAIN:
                case Weather.RAIN:
                    return true;
                default: throw new ArgumentOutOfRangeException("unhandled weather");
            }
        }

        public int DarknessFov(Actor actor)
        {
            if (actor.Model.Abilities.IsUndead)
                return actor.Sheet.BaseViewRange;
            else
                return MINIMAL_FOV;
        }

        // alpha10
        public int OdorsDecay(Map map, Point pos, Weather weather)
        {
            int decay;

            // base decay
            decay = 1;

            // sewers?
            if (map == map.District.SewersMap)
            {
                decay += 2;
            }
            // outside? = weather affected.
            else if (!map.GetTileAt(pos).IsInside)  // alpha10 weather affect only outside tiles
            {
                switch (weather)
                {
                    case Weather.CLEAR:
                    case Weather.CLOUDY:
                        // default decay.
                        break;
                    case Weather.RAIN:
                        decay += 1;
                        break;
                    case Weather.HEAVY_RAIN:
                        decay += 2;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("unhandled weather");
                }
            }

            return decay;
        }

        // alpha10
        public bool CanActorSeeSky(Actor actor)
        {
            if (actor.IsDead)
                return false;
            if (actor.IsSleeping)
                return false;
            return actor.Location.Map.Lighting == Lighting.OUTSIDE;
        }

        public bool CanActorKnowTime(Actor actor)
        {
            if (actor.IsDead)
                return false;
            if (actor.IsSleeping)
                return false;
            if (actor.Location.Map.Lighting == Lighting.OUTSIDE)
                return true;

            ItemTracker eqTracker = actor.GetEquippedItem(DollPart.LEFT_HAND) as ItemTracker;
            if (eqTracker != null && eqTracker.HasClock && eqTracker.Batteries > 0)
                return true;

            return false;
        }
        #endregion

        #region Map Power rating
        /// <summary>
        /// Compute normalized % of power level on the map. Each activated PowerGenerator map object count for one power unit.
        /// </summary>
        /// <param name="map"></param>
        /// <returns>0.0 = not powered at all to 1.0 = 100% power.</returns>
        public float ComputeMapPowerRatio(Map map)
        {
            int totalOn, totalOff;

            totalOff = totalOn = 0;

            foreach (MapObject obj in map.MapObjects)
            {
                PowerGenerator powGen = obj as PowerGenerator;
                if (powGen == null)
                    continue;

                if (powGen.IsOn)
                    ++totalOn;
                else
                    ++totalOff;
            }

            int totalCount = totalOn + totalOff;
            if (totalCount == 0)
                return 0.0f;

            float ratio = (float)totalOn / (float)(totalCount);
            return ratio;
        }
        #endregion

        #region Explosions
        public int BlastDamage(int distance, BlastAttack attack)
        {
            if (distance < 0 || distance > attack.Radius)
                throw new ArgumentOutOfRangeException(String.Format("blast distance {0} out of range", distance));

            return attack.Damage[distance];
        }
        #endregion

        #region Undead Regen/Food, Infection and Corpses
        public int ActorBiteHpRegen(Actor a, int dmg)
        {
            int bonus = (int)(Rules.SKILL_ZEATER_REGEN_BONUS * a.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.Z_EATER) * dmg);
            return dmg + bonus;
        }

        public int ActorBiteNutritionValue(Actor actor, int baseValue)
        {
            float zskillFactor = SKILL_ZLIGHT_EATER_FOOD_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.Z_LIGHT_EATER);
            float skillFactor = SKILL_LIGHT_EATER_FOOD_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.LIGHT_EATER);

            return (int)(CORPSE_EATING_NUTRITION_FACTOR + zskillFactor + skillFactor) * baseValue;
        }

        public int CorpseEeatingInfectionTransmission(int infection)
        {
            return (int)(CORPSE_EATING_INFECTION_FACTOR * infection);
        }

        public int ActorInfectionHPs(Actor a)
        {
            return ActorMaxHPs(a) + ActorMaxSTA(a);
        }

        public static int InfectionForDamage(Actor infector, int dmg)
        {
            float factor = INFECTION_BASE_FACTOR + infector.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.Z_INFECTOR) * SKILL_ZINFECTOR_BONUS;
            return (int)(factor * dmg * Session.Get.GamePreset.InfectionRatePercent / 100f);
        }

        public int ActorInfectionPercent(Actor a)
        {
            return (int)(100 * a.Infection / ActorInfectionHPs(a));
        }

        public int InfectionEffectTriggerChance1000(int infectionPercent)
        {
            return (INFECTION_EFFECT_TRIGGER_CHANCE_1000 + infectionPercent / 5) *
                Session.Get.GamePreset.InfectionEffectRatePercent / 100;
        }

        public int CorpseFreshnessPercent(Corpse c)
        {
            return (int)(100 * c.HitPoints / ActorMaxHPs(c.DeadGuy));
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="c"></param>
        /// <returns>[0..5]</returns>
        public int CorpseRotLevel(Corpse c)
        {
            int freshP = CorpseFreshnessPercent(c);
            if (freshP < 5)
                return 5;
            if (freshP < 25)
                return 4;
            if (freshP < 50)
                return 3;
            if (freshP < 75)
                return 2;
            if (freshP < 90)
                return 1;
            return 0;
        }

        public static float CorpseDecayPerTurn(Corpse c)
        {
            return CORPSE_DECAY_PER_TURN * Session.Get.GamePreset.CorpseDecayPercent / 100f;
        }

        public int CorpseZombifyChance(Corpse c, WorldTime timeNow, bool checkDelay = true)
        {
            float chance;

            // delay zombification.
            int dT = timeNow.TurnCounter - c.Turn;
            if (checkDelay && dT < CORPSE_ZOMBIFY_DELAY)
                return 0;

            // compute infection P
            int infP = ActorInfectionPercent(c.DeadGuy);

            // check only every X turns, higher frequency as infP rise.
            if (checkDelay)
            {
                int freq = (infP >= 100 ? 1 : 100 / (1 + infP));
                if (timeNow.TurnCounter % freq != 0)
                    return 0;
            }

            // base chance.
            chance = CORPSE_ZOMBIFY_BASE_CHANCE + Session.Get.GamePreset.CorpseBaseRiseChance;

            // living infection
            chance += CORPSE_ZOMBIFY_INFECTIONP_FACTOR * infP;

            // less likey as time passes.
            chance -= (int)(CORPSE_ZOMBIFY_TIME_FACTOR * dT);

            // factor day & night.
            if (timeNow.IsNight)
                chance *= CORPSE_ZOMBIFY_NIGHT_FACTOR;
            else
                chance *= CORPSE_ZOMBIFY_DAY_FACTOR;

            // ok.
            int intChance = Math.Max(0, Math.Min(100, (int)(chance * Session.Get.GamePreset.CorpseRiseChance / 100f)));
            return intChance;
        }

        public int CorpseReviveChance(Actor actor, Corpse corpse)
        {
            if (!CanActorReviveCorpse(actor, corpse))
                return 0;
            int baseChance = CorpseFreshnessPercent(corpse) / 4;
            int skillBonus = actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.MEDIC) * SKILL_MEDIC_REVIVE_BONUS;
            return baseChance + skillBonus;
        }

        public int CorpseReviveHPs(Actor actor, Corpse corpse)
        {
            int baseHps = 5;
            int skillBonus = actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.MEDIC);
            return baseHps + skillBonus;
        }
        #endregion

        #region Traps
        // alpha10
        public int GetTrapTriggerChance(ItemTrap trap, Actor a)
        {
            // alpha10.1 bugfix - correctly has 0 chance to trigger safe traps (eg: followers traps etc...)
            if (IsSafeFromTrap(trap, a))
                return 0;

            int baseChance;
            int avoidBonus;

            baseChance = trap.TrapModel.TriggerChance * trap.Quantity;

            avoidBonus = 0;
            if (a.Model.Abilities.IsUndead)
                avoidBonus -= TRAP_UNDEAD_ACTOR_TRIGGER_PENALTY;
            if (a.Model.Abilities.IsSmall)
                avoidBonus += TRAP_SMALL_ACTOR_AVOID_BONUS;
            avoidBonus += a.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.LIGHT_FEET) * SKILL_LIGHT_FEET_TRAP_BONUS;
            avoidBonus += a.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.Z_LIGHT_FEET) * SKILL_ZLIGHT_FEET_TRAP_BONUS;

            return baseChance - avoidBonus;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="trap"></param>
        /// <param name="a"></param>
        /// <returns>true if trap triggers</returns>
        public bool CheckTrapTriggers(ItemTrap trap, Actor a)
        {
            // alpha10 extracted and modified trigger chance formula
            int chance = GetTrapTriggerChance(trap, a);
            return chance > 0 ? RollChance(chance) : false;
        }

        public bool CheckTrapTriggers(ItemTrap trap, MapObject mobj)
        {
            return RollChance(trap.TrapModel.TriggerChance * mobj.Weight);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="trap"></param>
        /// <param name="mobj">can be null</param>
        /// <returns></returns>
        public bool CheckTrapStepOnBreaks(ItemTrap trap, MapObject mobj = null)
        {
            int chance = trap.TrapModel.BreakChance;
            if (mobj != null) chance *= mobj.Weight;
            return RollChance(chance);
        }

        public bool CheckTrapEscapeBreaks(ItemTrap trap, Actor a)
        {
            return RollChance(trap.TrapModel.BreakChanceWhenEscape);
        }

        // alpha10
        public bool IsSafeFromTrap(ItemTrap trap, Actor a)
        {
            if (trap.BaseOwner != null && trap.BaseOwner.Owns(a))
                return true;
            if (trap.Owner == null)
                return false;
            if (trap.Owner == a)
                return true;
            return a.IsInGroupWith(trap.Owner);
        }

        public bool CheckTrapEscape(ItemTrap trap, Actor a)
        {
            // alpha10
            if (IsSafeFromTrap(trap, a))
                return true;

            int escapeBonus = 0;

            escapeBonus += a.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.LIGHT_FEET) * SKILL_LIGHT_FEET_TRAP_BONUS
                + a.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.Z_LIGHT_FEET) * SKILL_ZLIGHT_FEET_TRAP_BONUS;

            return RollChance(escapeBonus + (100 - trap.TrapModel.BlockChance * trap.Quantity));
        }

        public bool IsTrapCoveringMapObjectThere(Map map, Point pos)
        {
            MapObject mobj = map.GetMapObjectAt(pos);
            if (mobj == null) return false;
            // mobj is either walkable and not a door (eg:bed) or jumpable (eg:table,car...)
            return mobj.IsJumpable || (mobj.IsWalkable && !(mobj is DoorWindow));
        }

        public bool IsTrapTriggeringMapObjectThere(Map map, Point pos)
        {
            MapObject mobj = map.GetMapObjectAt(pos);
            if (mobj == null) return false;
            // mobj is NOT walkable and a door and NOT jumpable (eg:shelves,large fort)
            return !mobj.IsWalkable && !mobj.IsJumpable && !(mobj is DoorWindow);
        }
        #endregion

        #region Grabbing
        public int ZGrabChance(Actor grabber, Actor victim)
        {
            int zGrabLevel = grabber.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.Z_GRAB);
            return zGrabLevel * SKILL_ZGRAB_CHANCE;
        }
        #endregion

    }
}
