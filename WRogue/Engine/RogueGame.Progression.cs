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
        #region Stats
        int CountLivings(Map map)
        {
            if (map == null)
                throw new ArgumentNullException("map");

            int count = 0;
            foreach (Actor a in map.Actors)
                if (!a.Model.Abilities.IsUndead)
                    ++count;

            return count;
        }

        int CountActors(Map map, Predicate<Actor> predFn)
        {
            if (map == null)
                throw new ArgumentNullException("map");

            int count = 0;
            foreach (Actor a in map.Actors)
                if (predFn(a))
                    ++count;

            return count;
        }

        int CountFaction(Map map, Faction f)
        {
            if (map == null)
                throw new ArgumentNullException("map");

            int count = 0;
            foreach (Actor a in map.Actors)
                if (a.Faction == f)
                    ++count;

            return count;
        }

        int CountUndeads(Map map)
        {
            if (map == null)
                throw new ArgumentNullException("map");

            int count = 0;
            foreach (Actor a in map.Actors)
                if (a.Model.Abilities.IsUndead)
                    ++count;

            return count;
        }

        int CountFoodItemsNutrition(Map map)
        {
            if (map == null)
                throw new ArgumentNullException("map");

            // food items on ground.
            int groundNutrition = 0;
            foreach (Inventory inv in map.GroundInventories)
            {
                if (inv.IsEmpty)
                    continue;
                foreach (Item it in inv.Items)
                {
                    if (it is ItemFood)
                        groundNutrition += m_Rules.FoodItemNutrition(it as ItemFood, map.LocalTime.TurnCounter);
                }
            }
            // food items carried by actors.
            int carriedNutrition = 0;
            foreach (Actor a in map.Actors)
            {
                Inventory inv = a.Inventory;
                if (inv == null || inv.IsEmpty)
                    continue;
                foreach (Item it in inv.Items)
                {
                    if (it is ItemFood)
                        carriedNutrition += m_Rules.FoodItemNutrition(it as ItemFood, map.LocalTime.TurnCounter);
                }
            }

            return groundNutrition + carriedNutrition;
        }

        bool HasActorOfModelID(Map map, GameActors.IDs actorModelID)
        {
            if (map == null)
                throw new ArgumentNullException("map");

            foreach (Actor a in map.Actors)
                if (a.Model.ID == (int)actorModelID)
                    return true;

            return false;
        }
        #endregion
        #region New day/night, Scoring, Advancement
        void OnNewNight()
        {
            UpdatePlayerFOV(m_Player);

            //----- Upgrade Player (undead only once every 2 nights)
            if (m_Player.Model.Abilities.IsUndead && m_Player.Location.Map.LocalTime.Day % 2 == 1)
            {
                // Mode.
                ClearOverlays();
                AddOverlay(new OverlayPopup(UPGRADE_MODE_TEXT, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, Point.Empty));

                // music.
                m_MusicManager.Stop();
                m_MusicManager.Play(GameMusics.INTERLUDE, MusicPriority.PRIORITY_EVENT);

                // Message.
                ClearMessages();
                AddMessage(new Message("You will hunt another day!", m_Session.WorldTime.TurnCounter, Color.Green));
                UpdatePlayerFOV(m_Player);
                if (!m_Player.IsBotPlayer)
                    AddMessagePressEnter();

                // Upgrade time!
                // alpha10.1 handle bot skill upgrade, bot followers will upgrade as npcs
                if (m_Player.IsBotPlayer)
                {
                    HandleNPCSkillUpgrade(m_Player);
                }
                else
                {
                    HandlePlayerDecideUpgrade(m_Player);
                    HandlePlayerFollowersUpgrade();
                }

                // Resume play.
                ClearMessages();
                AddMessage(new Message("Welcome to the night.", m_Session.WorldTime.TurnCounter, Color.White));
                ClearOverlays();
                RedrawPlayScreen();

                // music
                m_MusicManager.Stop();
            }
        }

        void OnNewDay()
        {
            /////////////////////////
            // Normal day processing
            /////////////////////////

            //----- Upgrade Player (living only)
            if (!m_Player.Model.Abilities.IsUndead)
            {
                // Mode.
                ClearOverlays();
                AddOverlay(new OverlayPopup(UPGRADE_MODE_TEXT, MODE_TEXTCOLOR, MODE_BORDERCOLOR, MODE_FILLCOLOR, Point.Empty));

                // music.
                m_MusicManager.Stop();
                m_MusicManager.Play(GameMusics.INTERLUDE, MusicPriority.PRIORITY_EVENT);

                // Message.
                ClearMessages();
                AddMessage(new Message("You survived another night!", m_Session.WorldTime.TurnCounter, Color.Green));
                UpdatePlayerFOV(m_Player);
                if (!m_Player.IsBotPlayer)
                    AddMessagePressEnter();

                // Upgrade time!
                // alpha10.1 handle bot skill upgrade, bot followers will upgrade as npcs
                if (m_Player.IsBotPlayer)
                {
                    HandleNPCSkillUpgrade(m_Player);
                }
                else
                {
                    HandlePlayerDecideUpgrade(m_Player);
                    HandlePlayerFollowersUpgrade();
                }

                // Resume play.
                ClearMessages();
                AddMessage(new Message("Welcome to tomorrow.", m_Session.WorldTime.TurnCounter, Color.White));
                ClearOverlays();
                RedrawPlayScreen();

                // music
                m_MusicManager.Stop();
            }

            // alpha10 obsolete
            //// Check weather change.
            //CheckWeatherChange();

            //////////////////////////////
            // New day achievements.
            // 1. Reached day X (living only)
            //////////////////////////////
            // 1. Reached day X (living only)
            if (!m_Player.Model.Abilities.IsUndead)
            {
                if (m_Session.WorldTime.Day == 7)
                {
                    // scoring.
                    m_Session.Scoring.SetCompletedAchievement(Achievement.IDs.REACHED_DAY_07);

                    // achievement!
                    ShowNewAchievement(Achievement.IDs.REACHED_DAY_07);
                }
                else if (m_Session.WorldTime.Day == 14)
                {
                    // scoring.
                    m_Session.Scoring.SetCompletedAchievement(Achievement.IDs.REACHED_DAY_14);

                    // achievement!
                    ShowNewAchievement(Achievement.IDs.REACHED_DAY_14);
                }
                else if (m_Session.WorldTime.Day == 21)
                {
                    // scoring.
                    m_Session.Scoring.SetCompletedAchievement(Achievement.IDs.REACHED_DAY_21);

                    // achievement!
                    ShowNewAchievement(Achievement.IDs.REACHED_DAY_21);
                }
                else if (m_Session.WorldTime.Day == 28)
                {
                    // scoring.
                    m_Session.Scoring.SetCompletedAchievement(Achievement.IDs.REACHED_DAY_28);

                    // achievement!
                    ShowNewAchievement(Achievement.IDs.REACHED_DAY_28);
                }
            }
        }

        void HandlePlayerDecideUpgrade(Actor upgradeActor)
        {
            // roll N skills to updgrade.
            List<Skills.IDs> upgradeChoices = RollSkillsToUpgrade(upgradeActor, 3 * 100);

            // "you" vs follower name.
            string youName = upgradeActor == m_Player ? "You" : upgradeActor.Name;

            // loop.
            bool loop = true;
            do
            {
                OverlayPopupTitle popup = null;

                ///////////////////
                // 1. Redraw
                // 2. Read input
                // 3. Handle input
                ///////////////////

                // 1. Redraw
                ClearMessages();
                AddMessage(new Message(youName + " can improve or learn one of these skills. Choose wisely.", m_Session.WorldTime.TurnCounter, Color.Green));

                if (upgradeChoices.Count == 0)
                {
                    AddMessage(MakeErrorMessage(youName + " can't learn anything new!"));
                }
                else
                {
                    List<string> popupLines = new List<string>();
                    popupLines.Add(" ");

                    for (int iChoice = 0; iChoice < upgradeChoices.Count; iChoice++)
                    {
                        Skills.IDs sk = upgradeChoices[iChoice];
                        int level = upgradeActor.Sheet.SkillTable.GetSkillLevel((int)sk);
                        string text = string.Format("{0}. {1} {2}/{3}", iChoice + 1, Skills.Name(sk), level + 1, Skills.MaxSkillLevel(sk));
                        AddMessage(new Message(text, m_Session.WorldTime.TurnCounter, Color.LightGreen));

                        popupLines.Add(text);
                        popupLines.Add("    " + DescribeSkillShort(sk));
                        popupLines.Add(" ");
                    }

                    popupLines.Add("ESC. don't upgrade");

                    if (upgradeActor != m_Player)
                    {
                        popupLines.Add(" ");
                        popupLines.Add(upgradeActor.Name + " current skills");
                        foreach (Skill sk in upgradeActor.Sheet.SkillTable.Skills)
                        {
                            popupLines.Add(string.Format("{0} {1}", Skills.Name(sk.ID), sk.Level));
                        }
                    }

                    popup = new OverlayPopupTitle(upgradeActor == m_Player ? "Select skill to upgrade" : "Select skill to upgrade for " + upgradeActor.Name, Color.White, popupLines.ToArray(), Color.White, Color.White, Color.Black, new Point(64, 64));
                    AddOverlay(popup);
                }
                AddMessage(new Message("ESC if you don't want to upgrade.", m_Session.WorldTime.TurnCounter, Color.White));
                RedrawPlayScreen();

                // 2. Read input
                KeyEventArgs inKey = m_UI.UI_WaitKey();

                // 3. Handle input
                PlayerCommand command = InputTranslator.KeyToCommand(inKey);
                if (inKey.KeyCode == Keys.Escape)// command == PlayerCommand.EXIT_OR_CANCEL)
                {
                    loop = false;
                    if (popup != null) RemoveOverlay(popup);
                    RedrawPlayScreen();
                }
                else
                {
                    // get choice.
                    int choice = KeyToChoiceNumber(inKey.KeyCode);

                    if (choice >= 1 && choice <= upgradeChoices.Count)
                    {
                        // upgrade skill.
                        Skills.IDs skID = upgradeChoices[choice - 1];
                        Skill sk = SkillUpgrade(upgradeActor, skID);

                        // message & scoring.
                        if (sk.Level == 1)
                        {
                            AddMessage(new Message(String.Format("{0} learned skill {1}.", upgradeActor.Name, Skills.Name(sk.ID)), m_Session.WorldTime.TurnCounter, Color.LightGreen));
                            m_Session.Scoring.AddEvent(m_Session.WorldTime.TurnCounter, String.Format("{0} learned skill {1}.", upgradeActor.Name, Skills.Name(sk.ID)));
                        }
                        else
                        {
                            AddMessage(new Message(String.Format("{0} improved skill {1} to level {2}.", upgradeActor.Name, Skills.Name(sk.ID), sk.Level), m_Session.WorldTime.TurnCounter, Color.LightGreen));
                            m_Session.Scoring.AddEvent(m_Session.WorldTime.TurnCounter, String.Format("{0} improved skill {1} to level {2}.", upgradeActor.Name, Skills.Name(sk.ID), sk.Level));
                        }
                        AddMessagePressEnter();
                        if (popup != null) RemoveOverlay(popup);
                        RedrawPlayScreen();
                        loop = false;
                    }
                }

            } while (loop);
        }

        void HandlePlayerFollowersUpgrade()
        {
            // if no followers, nothing to do.
            if (m_Player.CountFollowers == 0)
                return;

            // Message.
            ClearMessages();
            AddMessage(new Message("Your followers learned new skills at your side!", m_Session.WorldTime.TurnCounter, Color.Green));
            AddMessagePressEnter();

            // Do it.
            foreach (Actor follower in m_Player.Followers)
            {
                // player pick for the follower.
                HandlePlayerDecideUpgrade(follower);
            }

        }

        void HandleLivingNPCsUpgrade(Map map)
        {
            foreach (Actor a in map.Actors)
            {
                // ignore player, we do it separatly.
                if (a == m_Player)
                    continue;
                // ignore player followers (upgraded already)
                if (a.Leader == m_Player)
                    continue;
                // not undeads!
                if (a.Model.Abilities.IsUndead)
                    continue;

                // do it!
                HandleNPCSkillUpgrade(a);  // alpha10.1
            }
        }

        // alpha10.1 factorized to handle bot skill upgrade
        void HandleNPCSkillUpgrade(Actor a)
        {
            List<Skills.IDs> upgradeFrom = RollSkillsToUpgrade(a, 3 * 100);
            Skills.IDs? chosenSkill = NPCPickSkillToUpgrade(a, upgradeFrom);
            if (chosenSkill == null)
                return;
            // upgrade it!
            SkillUpgrade(a, chosenSkill.Value);
        }

        void HandleUndeadNPCsUpgrade(Map map)
        {
            foreach (Actor a in map.Actors)
            {
                // ignore player, we do it separatly.
                if (a == m_Player)
                    continue;
                // ignore player followers (upgraded already)
                if (a.Leader == m_Player)
                    continue;
                // undeads only, and some branches only.
                if (!a.Model.Abilities.IsUndead)
                    continue;
                if (!s_Options.SkeletonsUpgrade && GameActors.IsSkeletonBranch(a.Model))
                    continue;
                if (!s_Options.RatsUpgrade && GameActors.IsRatBranch(a.Model))
                    continue;
                if (!s_Options.ShamblersUpgrade && GameActors.IsShamblerBranch(a.Model))
                    continue;

                // do it!
                List<Skills.IDs> upgradeFrom = RollSkillsToUpgrade(a, 3 * 100);
                Skills.IDs? chosenSkill = NPCPickSkillToUpgrade(a, upgradeFrom);
                if (chosenSkill == null)
                    continue;
                // upgrade it!
                SkillUpgrade(a, chosenSkill.Value);
            }
        }

        List<Skills.IDs> RollSkillsToUpgrade(Actor actor, int maxTries)
        {
            int count = (actor.Model.Abilities.IsUndead ? Rules.UNDEAD_UPGRADE_SKILLS_TO_CHOOSE_FROM : Rules.UPGRADE_SKILLS_TO_CHOOSE_FROM);
            List<Skills.IDs> list = new List<Skills.IDs>(count);

            for (int i = 0; i < count; i++)
            {
                Skills.IDs? newSk;
                int attempt = 0;
                do
                {
                    ++attempt;
                    newSk = RollRandomSkillToUpgrade(actor, maxTries);
                    if (newSk == null)
                        return list;
                } while (list.Contains(newSk.Value) && attempt < maxTries);

                list.Add(newSk.Value);
            }

            return list;
        }

        Skills.IDs? NPCPickSkillToUpgrade(Actor npc, List<Skills.IDs> chooseFrom)
        {
            if (chooseFrom == null || chooseFrom.Count == 0)
                return null;

            // Compute skill utilities and get best utility.
            int N = chooseFrom.Count;
            int[] utilities = new int[N];
            int bestUtility = -1;
            for (int i = 0; i < N; i++)
            {
                utilities[i] = NPCSkillUtility(npc, chooseFrom[i]);
                if (utilities[i] > bestUtility)
                    bestUtility = utilities[i];
            }

            // Randomly choose on of the best.
            List<Skills.IDs> bestSkills = new List<Skills.IDs>(N);
            for (int i = 0; i < N; i++)
                if (utilities[i] == bestUtility)
                    bestSkills.Add(chooseFrom[i]);
            return bestSkills[m_Rules.Roll(0, bestSkills.Count)];
        }

        int NPCSkillUtility(Actor actor, Skills.IDs skID)
        {
            const int USELESS_UTIL = 0;
            const int LOW_UTIL = 1;
            const int AVG_UTIL = 2;
            const int HI_UTIL = 3;

            if (actor.Model.Abilities.IsUndead)
            {
                // undeads.
                switch (skID)
                {
                    // useful one.
                    case Skills.IDs.Z_GRAB:
                    case Skills.IDs.Z_INFECTOR:
                    case Skills.IDs.Z_LIGHT_EATER:
                        return HI_UTIL;

                    // ok ones.
                    case Skills.IDs.Z_AGILE:
                    case Skills.IDs.Z_STRONG:
                    case Skills.IDs.Z_TOUGH:
                    case Skills.IDs.Z_TRACKER:
                        return AVG_UTIL;

                    // meh ones.
                    case Skills.IDs.Z_EATER:
                    case Skills.IDs.Z_LIGHT_FEET:
                        return LOW_UTIL;

                    default:
                        return USELESS_UTIL;
                }
            }
            else
            {
                switch (skID)
                {
                    case Skills.IDs.AGILE:
                        return AVG_UTIL;

                    case Skills.IDs.AWAKE:
                        // useful only if has to sleep.
                        return actor.Model.Abilities.HasToSleep ? HI_UTIL : USELESS_UTIL;

                    case Skills.IDs.BOWS:
                        {
                            // useful only if has bow weapon.
                            if (actor.Inventory != null)
                            {
                                foreach (Item it in actor.Inventory.Items)
                                    if (it is ItemRangedWeapon)
                                    {
                                        if ((it.Model as ItemRangedWeaponModel).IsBow)
                                            return HI_UTIL;
                                    }
                            }
                            return USELESS_UTIL;
                        }

                    case Skills.IDs.CARPENTRY:
                        return LOW_UTIL;

                    case Skills.IDs.CHARISMATIC:
                        // useful only if leader.
                        return actor.CountFollowers > 0 ? LOW_UTIL : USELESS_UTIL;

                    case Skills.IDs.FIREARMS:
                        {
                            // useful only if has firearm weapon.
                            if (actor.Inventory != null)
                            {
                                foreach (Item it in actor.Inventory.Items)
                                    if (it is ItemRangedWeapon)
                                    {
                                        if ((it.Model as ItemRangedWeaponModel).IsFireArm)
                                            return HI_UTIL;
                                    }
                            }
                            return USELESS_UTIL;
                        }

                    case Skills.IDs.HARDY:
                        // useful only if has to sleep.
                        return actor.Model.Abilities.HasToSleep ? HI_UTIL : USELESS_UTIL;

                    case Skills.IDs.HAULER:
                        return HI_UTIL;

                    case Skills.IDs.HIGH_STAMINA:
                        return HI_UTIL;  // alpha10; was previously rated as avg

                    case Skills.IDs.LEADERSHIP:
                        // useful only if not follower.
                        return actor.HasLeader ? USELESS_UTIL : LOW_UTIL;

                    case Skills.IDs.LIGHT_EATER:
                        // useful only if has to eat.
                        return actor.Model.Abilities.HasToEat ? HI_UTIL : USELESS_UTIL;

                    case Skills.IDs.LIGHT_FEET:
                        return AVG_UTIL;

                    case Skills.IDs.LIGHT_SLEEPER:
                        // useful only if has to sleep.
                        return actor.Model.Abilities.HasToSleep ? AVG_UTIL : USELESS_UTIL;

                    case Skills.IDs.MARTIAL_ARTS:
                        {
                            // useless if any weapon in inventory.
                            if (actor.Inventory != null)
                            {
                                foreach (Item it in actor.Inventory.Items)
                                {
                                    if (it is ItemWeapon)
                                        return LOW_UTIL;
                                }
                            }
                            return AVG_UTIL;
                        }

                    case Skills.IDs.MEDIC:
                        return LOW_UTIL;

                    case Skills.IDs.NECROLOGY:
                        return LOW_UTIL; // alpha10 ; was previously rated as useless

                    case Skills.IDs.STRONG:
                        return AVG_UTIL;

                    case Skills.IDs.STRONG_PSYCHE:
                        // useful only if has sanity.
                        return actor.Model.Abilities.HasSanity ? HI_UTIL : USELESS_UTIL;

                    case Skills.IDs.TOUGH:
                        return HI_UTIL;

                    case Skills.IDs.UNSUSPICIOUS:
                        // useful only if murderer and not law enforcer.
                        return actor.MurdersCounter > 0 && !actor.Model.Abilities.IsLawEnforcer ? LOW_UTIL : USELESS_UTIL;

                    default:
                        return USELESS_UTIL;
                }
            }
        }

        Skills.IDs? RollRandomSkillToUpgrade(Actor actor, int maxTries)
        {
            int attempt = 0;
            int skID;
            bool isUndead = actor.Model.Abilities.IsUndead;

            do
            {
                ++attempt;
                skID = isUndead ? (int)Skills.RollUndead(Rules.DiceRoller) : (int)Skills.RollLiving(Rules.DiceRoller);
            }
            while (actor.Sheet.SkillTable.GetSkillLevel(skID) >= Skills.MaxSkillLevel(skID) && attempt < maxTries);

            if (attempt >= maxTries)
                return null;
            else
                return (Skills.IDs)skID;
        }

        void DoLooseRandomSkill(Actor actor)
        {
            int[] skills = actor.Sheet.SkillTable.SkillsList;
            if (skills == null) return;

            // pick a skill.
            int iSkill = m_Rules.Roll(0, skills.Length);
            Skills.IDs lostSkill = (Skills.IDs)skills[iSkill];

            // regress.
            actor.Sheet.SkillTable.DecOrRemoveSkill((int)lostSkill);

            // message.
            if (IsVisibleToPlayer(actor))
                AddMessage(MakeMessage(actor, String.Format("regressed in {0}!", Skills.Name(lostSkill))));
        }

        public Skill SkillUpgrade(Actor actor, Skills.IDs id)
        {
            actor.Sheet.SkillTable.AddOrIncreaseSkill((int)id);
            Skill sk = actor.Sheet.SkillTable.GetSkill((int)id);
            OnSkillUpgrade(actor, id);

            return sk;
        }

        public void OnSkillUpgrade(Actor actor, Skills.IDs id)
        {
            switch (id)
            {
                case Skills.IDs.HAULER:
                    if (actor.Inventory != null)
                        actor.Inventory.MaxCapacity = m_Rules.ActorMaxInv(actor);
                    break;

                default:
                    // no special upkeep to do.
                    break;
            }
        }

        void ChangeWeather()
        {
            bool canSeeWeather = m_Rules.CanActorSeeSky(m_Player); // alpha10

            // roll & annouce new weather.
            string desc;
            Weather newWeather;
            switch (m_Session.World.Weather)
            {
                case Weather.CLEAR:
                    newWeather = Weather.CLOUDY;
                    desc = "Clouds are covering the sun.";
                    break;

                case Weather.CLOUDY:
                    if (m_Rules.RollChance(50))
                    {
                        newWeather = Weather.CLEAR;
                        desc = "The sky is clear again.";
                    }
                    else
                    {
                        newWeather = Weather.RAIN;
                        desc = "Rain is starting to fall.";
                    }
                    break;

                case Weather.RAIN:
                    if (m_Rules.RollChance(50))
                    {
                        newWeather = Weather.CLOUDY;
                        desc = "The rain has stopped.";
                    }
                    else
                    {
                        newWeather = Weather.HEAVY_RAIN;
                        desc = "The weather is getting worse!";
                    }
                    break;

                case Weather.HEAVY_RAIN:
                    newWeather = Weather.RAIN;
                    desc = "The rain is less heavy.";
                    break;

                default:
                    throw new ArgumentOutOfRangeException("unhandled weather");
            }

            // change.
            m_Session.World.Weather = newWeather;

            // message.
            if (canSeeWeather)
                AddMessage(new Message(desc, m_Session.WorldTime.TurnCounter, Color.White));

            // scoring.
            m_Session.Scoring.AddEvent(m_Session.WorldTime.TurnCounter, String.Format("The weather changed to {0}.", DescribeWeather(m_Session.World.Weather)));
        }

        /// <summary>
        /// Add kill to scoring record.
        /// </summary>
        /// <param name="victim"></param>
        void PlayerKill(Actor victim)
        {
            // scoring.
            m_Session.Scoring.AddKill(m_Player, victim, m_Session.WorldTime.TurnCounter);
        }
        #endregion
    }
}
