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
        #region Damaging, Killing & Disarming actors
        void InflictDamage(Actor actor, int dmg)
        {
            // HP.
            actor.HitPoints -= dmg;
            if (CancelMouseMoveOnDamage(actor, dmg))
                AddMessage(new Message("Mouse movement stopped: you took damage.", m_Session.WorldTime.TurnCounter, Color.Red));

            // Stamina.
            if (actor.Model.Abilities.CanTire)
            {
                actor.StaminaPoints -= dmg;
            }

            // Body armor breaks?
            Item torsoItem = actor.GetEquippedItem(DollPart.TORSO);
            if (torsoItem != null && torsoItem is ItemBodyArmor)
            {
                if (m_Rules.RollChance(Rules.BODY_ARMOR_BREAK_CHANCE))
                {
                    // do it.
                    OnUnequipItem(actor, torsoItem);
                    actor.Inventory.RemoveAllQuantity(torsoItem);

                    // message.
                    if (IsVisibleToPlayer(actor))
                    {
                        AddMessage(MakeMessage(actor, String.Format(": {0} breaks and is now useless!", torsoItem.TheName)));
                        RedrawPlayScreen();
                        AnimDelay(actor.IsPlayer ? DELAY_NORMAL : DELAY_SHORT);
                    }
                }
            }

            // If sleeping, wake up dude!
            if (actor.IsSleeping)
                DoWakeUp(actor);
        }

        // alpha10 drop corpse optional
        public void KillActor(Actor killer, Actor deadGuy, string reason, bool canDropCorpse = true)
        {
            // Sanity check.
#if false
            for some reason, this can happen with starved actors. no f*****g idea why since this is the only place where we set the dead flag.
            if (deadGuy.IsDead)
                throw new InvalidOperationException(String.Format("killing deadGuy that is already dead : killer={0} deadGuy={1} reason={2}", (
                    killer == null ? "N/A" : killer.TheName), deadGuy.TheName, reason));
#endif

            // Set dead flag.
            deadGuy.IsDead = true;

            // force to stop dragging corpses.
            DoStopDraggingCorpses(deadGuy);

            // untrigger all traps here.
            UntriggerAllTrapsHere(deadGuy.Location);

            // living killing undead = restore sanity.
            if (killer != null && !killer.Model.Abilities.IsUndead && killer.Model.Abilities.HasSanity && deadGuy.Model.Abilities.IsUndead)
                RegenActorSanity(killer, Rules.SANITY_RECOVER_KILL_UNDEAD);

            // death of bonded leader/follower hits sanity.
            if (deadGuy.HasLeader)
            {
                if (m_Rules.HasActorBondWith(deadGuy.Leader, deadGuy))
                {
                    SpendActorSanity(deadGuy.Leader, Rules.SANITY_HIT_BOND_DEATH);
                    if (IsVisibleToPlayer(deadGuy.Leader))
                    {
                        if (deadGuy.Leader.IsPlayer && !deadGuy.Leader.IsBotPlayer) ClearMessages();
                        AddMessage(MakeMessage(deadGuy.Leader, String.Format("{0} deeply disturbed by {1} sudden death!",
                            Conjugate(deadGuy.Leader, VERB_BE), deadGuy.Name)));
                        if (deadGuy.Leader.IsPlayer && !deadGuy.Leader.IsBotPlayer) AddMessagePressEnter();
                    }
                }
            }
            else if (deadGuy.CountFollowers > 0)
            {
                foreach (Actor fo in deadGuy.Followers)
                {
                    if (m_Rules.HasActorBondWith(fo, deadGuy))
                    {
                        SpendActorSanity(fo, Rules.SANITY_HIT_BOND_DEATH);
                        if (IsVisibleToPlayer(fo))
                        {
                            if (fo.IsPlayer && !fo.IsBotPlayer) ClearMessages();
                            AddMessage(MakeMessage(fo, String.Format("{0} deeply disturbed by {1} sudden death!",
                                Conjugate(fo, VERB_BE), deadGuy.Name)));
                            if (fo.IsPlayer && !fo.IsBotPlayer) AddMessagePressEnter();
                        }
                    }
                }
            }

            // Unique actor?
            if (deadGuy.IsUnique)
            {
                if (killer != null)
                    m_Session.Scoring.AddEvent(deadGuy.Location.Map.LocalTime.TurnCounter,
                        String.Format("* {0} was killed by {1} {2}! *", deadGuy.TheName, killer.Model.Name, killer.TheName));
                else
                    m_Session.Scoring.AddEvent(deadGuy.Location.Map.LocalTime.TurnCounter,
                        String.Format("* {0} died by {1}! *", deadGuy.TheName, reason));
            }

            // Player dead?
            // BEFORE removing followers & dropping items.
            if (deadGuy == m_Player)
                PlayerDied(killer, reason);

            // Remove followers.
            deadGuy.RemoveAllFollowers();

            // Remove from leader.
            #region
            if (deadGuy.Leader != null)
            {
                // player's follower killed : scoring and message.
                if (deadGuy.Leader.IsPlayer)
                {
                    string deathEvent;
                    if (killer != null)
                        deathEvent = String.Format("Follower {0} was killed by {1} {2}!", deadGuy.TheName, killer.Model.Name, killer.TheName);
                    else
                        deathEvent = String.Format("Follower {0} died by {1}!", deadGuy.TheName, reason);
                    m_Session.Scoring.AddEvent(deadGuy.Location.Map.LocalTime.TurnCounter, deathEvent);
                }

                deadGuy.Leader.RemoveFollower(deadGuy);
            }
            #endregion

            // Remove aggressor & self defence relations.
            bool wasMurder = (killer != null && m_Rules.IsMurder(killer, deadGuy));
            deadGuy.RemoveAllAgressorSelfDefenceRelations();

            // Remove from map.
            deadGuy.Location.Map.RemoveActor(deadGuy);

            // Drop some inventory items.
            #region
            if (deadGuy.Inventory != null && !deadGuy.Inventory.IsEmpty)
            {
                int deadItemsCount = deadGuy.Inventory.CountItems;
                Item[] dropThem = new Item[deadItemsCount];
                for (int i = 0; i < dropThem.Length; i++)
                    dropThem[i] = deadGuy.Inventory[i];
                for (int i = 0; i < dropThem.Length; i++)
                {
                    Item it = dropThem[i];
                    int chance = (it is ItemAmmo || it is ItemFood) ? Rules.VICTIM_DROP_AMMOFOOD_ITEM_CHANCE : Rules.VICTIM_DROP_GENERIC_ITEM_CHANCE;
                    if (it.Model.IsUnbreakable || it.IsUnique || m_Rules.RollChance(chance))
                        DropItem(deadGuy, it);
                }
            }
            #endregion

            // Blood splat/Remains
            if (!deadGuy.Model.Abilities.IsUndead)
                SplatterBlood(deadGuy.Location.Map, deadGuy.Location.Position);
#if false
            disabled: avoid unecessary large saved games
            if (deadGuy.Model.Abilities.IsUndead)
                UndeadRemains(deadGuy.Location.Map, deadGuy.Location.Position);
            else
                SplatterBlood(deadGuy.Location.Map, deadGuy.Location.Position);
#endif

            // Corpse?
            if (Rules.HasCorpses(m_Session.GameMode))
            {
                if (!deadGuy.Model.Abilities.IsUndead && canDropCorpse)
                {
                    DropCorpse(deadGuy);
                }
            }

            // One more kill
            if (killer != null)
                ++killer.KillsCount;

            // Player scoring
            if (killer == m_Player)
                PlayerKill(deadGuy);

            // Undead level up?
            #region
            if (killer != null && Rules.HasEvolution(m_Session.GameMode))
            {
                if (killer.Model.Abilities.IsUndead)
                {
                    #region
                    // check for evolution.
                    ActorModel levelUpModel = CheckUndeadEvolution(killer);
                    if (levelUpModel != null)
                    {
                        // Remember skills if any.
                        SkillTable savedSkills = null;
                        if (killer.Sheet.SkillTable != null && killer.Sheet.SkillTable.Skills != null)
                            savedSkills = new SkillTable(killer.Sheet.SkillTable.Skills);

                        // Do the transformation.
                        killer.Model = levelUpModel;

                        // If player, make sure it is setup properly.
                        if (killer.IsPlayer)
                            PrepareActorForPlayerControl(killer);

                        // If had skills, give them back.
                        if (savedSkills != null)
                        {
                            foreach (Skill s in savedSkills.Skills)
                                for (int i = 0; i < s.Level; i++)
                                {
                                    killer.Sheet.SkillTable.AddOrIncreaseSkill(s.ID);
                                    OnSkillUpgrade(killer, (Skills.IDs)s.ID);
                                }
                            m_TownGenerator.RecomputeActorStartingStats(killer);
                        }

                        // Message.
                        if (IsVisibleToPlayer(killer))
                        {
                            AddOverlay(new OverlayRect(Color.Yellow, new Rectangle(MapToScreen(killer.Location.Position), new Size(TILE_SIZE, TILE_SIZE))));
                            AddMessage(MakeMessage(killer, String.Format("{0} a {1} horror!", Conjugate(killer, VERB_TRANSFORM_INTO), levelUpModel.Name)));
                            RedrawPlayScreen();
                            AnimDelay(DELAY_LONG);
                            ClearOverlays();
                        }
                    }
                    #endregion
                }
            }
            #endregion

            // Trust : leader killing a follower target or adjacent enemy.
            if (killer != null && killer.CountFollowers > 0)
            {
                foreach (Actor fo in killer.Followers)
                {
                    bool gainTrust = false;
                    if (fo.TargetActor == deadGuy || (m_Rules.AreEnemies(fo, deadGuy) && m_Rules.IsAdjacent(fo.Location, deadGuy.Location)))
                        gainTrust = true;

                    if (gainTrust)
                    {
                        DoSay(fo, killer, "That was close! Thanks for the help!!", Sayflags.IS_FREE_ACTION);
                        ModifyActorTrustInLeader(fo, Rules.TRUST_LEADER_KILL_ENEMY, true);
                    }
                }
            }

            // Murder?
            #region
            if (wasMurder)
            {
                // one more murder.
                ++killer.MurdersCounter;

                // if player, log.
                if (killer.IsPlayer)
                    m_Session.Scoring.AddEvent(m_Session.WorldTime.TurnCounter, String.Format("Murdered {0} a {1}!", deadGuy.TheName, deadGuy.Model.Name));
                // message.
                if (IsVisibleToPlayer(killer))
                    AddMessage(MakeMessage(killer, String.Format("murdered {0}!!", deadGuy.Name)));

                // check for npcs law enforcers witnessing the murder.
                Map map = killer.Location.Map;
                Point killerPos = killer.Location.Position;
                foreach (Actor a in map.Actors)
                {
                    // check ability and state/relationship
                    if (!a.Model.Abilities.IsLawEnforcer || a.IsDead || a.IsSleeping || a.IsPlayer ||
                        a == killer || a == deadGuy || a.Leader == killer || killer.Leader == a)
                        continue;

                    // do as less computations as possible : we don't need all the actor f*****g fov, just the line to the murderer.

                    // fov range check.
                    if (m_Rules.GridDistance(a.Location.Position, killerPos) > m_Rules.ActorFOV(a, map.LocalTime, m_Session.World.Weather))
                        continue;

                    // LOS check.
                    if (!LOS.CanTraceViewLine(a.Location, killerPos))
                        continue;

                    // we see the murderer!
                    // make enemy and emote.
                    DoSay(a, killer, String.Format("MURDER! {0} HAS KILLED {1}!", killer.TheName, deadGuy.TheName), Sayflags.IS_FREE_ACTION | Sayflags.IS_IMPORTANT);
                    DoMakeAggression(a, killer);
                }
            }
            #endregion

            // Emote: a law enforcer killing a murderer feels warm and fuzzy inside.
            #region
            if (killer != null && deadGuy.MurdersCounter > 0 && killer.Model.Abilities.IsLawEnforcer && !killer.Faction.IsEnemyOf(deadGuy.Faction))
            {
                if (killer.IsPlayer)
                    AddMessage(new Message("You feel like you did your duty with killing a murderer.", m_Session.WorldTime.TurnCounter, Color.White));
                else
                    DoSay(killer, deadGuy, "Good riddance, murderer!", Sayflags.IS_FREE_ACTION | Sayflags.IS_DANGER);
            }
            #endregion

            //////////////////////////////////////////////
            // Player or Player Followers Killing Uniques
            //////////////////////////////////////////////
            #region
            // The Sewers Thing
            if (deadGuy == m_Session.UniqueActors.TheSewersThing.TheActor)
            {
                if (killer == m_Player || killer.Leader == m_Player)
                {
                    // scoring.
                    m_Session.Scoring.SetCompletedAchievement(Achievement.IDs.KILLED_THE_SEWERS_THING);

                    // achievement!
                    ShowNewAchievement(Achievement.IDs.KILLED_THE_SEWERS_THING);
                }
            }
            #endregion
        }

        // alpha10
        /// <summary>
        ///
        /// </summary>
        /// <param name="actor"></param>
        /// <returns>the disarmed item or null if actor had no equipped item</returns>
        Item Disarm(Actor actor)
        {
            Item disarmIt = null;

            // pick equipped item to disarm : prefer weapon, then any right handed item(?), then left handed.
            disarmIt = actor.GetEquippedWeapon();
            if (disarmIt == null)
            {
                disarmIt = actor.GetEquippedItem(DollPart.RIGHT_HAND);
                if (disarmIt == null)
                {
                    disarmIt = actor.GetEquippedItem(DollPart.LEFT_HAND);
                }
            }

            if (disarmIt == null)
                return null;

            // unequip, remove from inv and drop item in a random adjacent tile
            // if none possible, will drop on same tile (which then has no almost no gameplay effect
            // because the actor can take it back asap at no ap cost... unless he dies)
            DoUnequipItem(actor, disarmIt, false);
            actor.Inventory.RemoveAllQuantity(disarmIt);
            List<Point> dropTiles = new List<Point>(8);
            actor.Location.Map.ForEachAdjacentInMap(actor.Location.Position,
                (pt) =>
                {
                    // checking if can drop there is eq to checking if can throw it there
                    if (!actor.Location.Map.IsBlockingThrow(pt.X, pt.Y))
                        dropTiles.Add(pt);
                });
            Point dropOnTile;
            if (dropTiles.Count > 0)
                dropOnTile = dropTiles[m_Rules.Roll(0, dropTiles.Count)];
            else
                dropOnTile = actor.Location.Position;
            actor.Location.Map.DropItemAt(disarmIt, dropOnTile);

            // done
            return disarmIt;
        }
        #endregion
        #region Undeads leveling up
        ActorModel CheckUndeadEvolution(Actor undead)
        {
            // check option & game mode.
            if (!s_Options.AllowUndeadsEvolution || !Rules.HasEvolution(m_Session.GameMode))
                return null;

            // evolve?
            bool evolve = false;
            switch (undead.Model.ID)
            {
                // zombie master 4 kills  & Day > X -> zombie lord
                case (int)GameActors.IDs.UNDEAD_ZOMBIE_MASTER:
                    {
                        if (undead.KillsCount < 4)
                            return null;
                        if (undead.Location.Map.LocalTime.Day < ZOMBIE_LORD_EVOLUTION_MIN_DAY && !undead.IsPlayer)
                            return null;
                        evolve = true;
                        break;
                    }

                // zombie lord 8 kills -> zombie prince.
                case (int)GameActors.IDs.UNDEAD_ZOMBIE_LORD:
                    {
                        if (undead.KillsCount < 8)
                            return null;
                        evolve = true;
                        break;
                    }

                // skeleton 2 kills -> red eyed skeleton
                case (int)GameActors.IDs.UNDEAD_SKELETON:
                    {
                        if (undead.KillsCount < 2)
                            return null;
                        evolve = true;
                        break;
                    }
                // red eye skeleton 4 kills -> red skeleton
                case (int)GameActors.IDs.UNDEAD_RED_EYED_SKELETON:
                    {
                        if (undead.KillsCount < 4)
                            return null;
                        evolve = true;
                        break;
                    }

                // zombie -> dark eyed zombie
                case (int)GameActors.IDs.UNDEAD_ZOMBIE:
                    evolve = true;
                    break;

                // dark eyed zombie -> dark zombie
                case (int)GameActors.IDs.UNDEAD_DARK_EYED_ZOMBIE:
                    evolve = true;
                    break;

                // zombified 2 kills -> neophyte
                case (int)GameActors.IDs.UNDEAD_MALE_ZOMBIFIED:
                case (int)GameActors.IDs.UNDEAD_FEMALE_ZOMBIFIED:
                    {
                        if (undead.KillsCount < 2)
                            return null;
                        evolve = true;
                        break;
                    }

                // neophyte 4 kills & Day > X -> disciple
                case (int)GameActors.IDs.UNDEAD_MALE_NEOPHYTE:
                case (int)GameActors.IDs.UNDEAD_FEMALE_NEOPHYTE:
                    {
                        if (undead.KillsCount < 4)
                            return null;
                        if (undead.Location.Map.LocalTime.Day < DISCIPLE_EVOLUTION_MIN_DAY && !undead.IsPlayer)
                            return null; ;
                        evolve = true;
                        break;
                    }

                default:
                    evolve = false;
                    break;
            }

            // evolve vs no evolution.
            if (evolve)
            {
                GameActors.IDs evolutionID = NextUndeadEvolution((GameActors.IDs)undead.Model.ID);
                if (evolutionID == (GameActors.IDs)undead.Model.ID)
                    return null;
                else
                    return GameActors[evolutionID];
            }
            else
                return null;
        }

        public GameActors.IDs NextUndeadEvolution(GameActors.IDs fromModelID)
        {
            switch (fromModelID)
            {
                case GameActors.IDs.UNDEAD_SKELETON: return GameActors.IDs.UNDEAD_RED_EYED_SKELETON;
                case GameActors.IDs.UNDEAD_RED_EYED_SKELETON: return GameActors.IDs.UNDEAD_RED_SKELETON;

                case GameActors.IDs.UNDEAD_ZOMBIE: return GameActors.IDs.UNDEAD_DARK_EYED_ZOMBIE;
                case GameActors.IDs.UNDEAD_DARK_EYED_ZOMBIE: return GameActors.IDs.UNDEAD_DARK_ZOMBIE;

                case GameActors.IDs.UNDEAD_FEMALE_ZOMBIFIED: return GameActors.IDs.UNDEAD_FEMALE_NEOPHYTE;
                case GameActors.IDs.UNDEAD_MALE_ZOMBIFIED: return GameActors.IDs.UNDEAD_MALE_NEOPHYTE;
                case GameActors.IDs.UNDEAD_FEMALE_NEOPHYTE: return GameActors.IDs.UNDEAD_FEMALE_DISCIPLE;
                case GameActors.IDs.UNDEAD_MALE_NEOPHYTE: return GameActors.IDs.UNDEAD_MALE_DISCIPLE;

                case GameActors.IDs.UNDEAD_ZOMBIE_MASTER: return GameActors.IDs.UNDEAD_ZOMBIE_LORD;
                case GameActors.IDs.UNDEAD_ZOMBIE_LORD: return GameActors.IDs.UNDEAD_ZOMBIE_PRINCE;

                default: return fromModelID;
            }
        }
        #endregion
        #region Player death & Post mortem
        void PlayerDied(Actor killer, string reason)
        {
            // stop sim thread.
            StopSimThread(true);   // alpha10 abort allowed when dying

            // mouse.
            m_UI.UI_SetCursor(null);

            // music.
            m_MusicManager.Stop();
            m_MusicManager.Play(GameMusics.PLAYER_DEATH, MusicPriority.PRIORITY_EVENT);

            ///////////
            // Scoring
            ///////////
            #region
            m_Session.Scoring.TurnsSurvived = m_Session.WorldTime.TurnCounter;
            m_Session.Scoring.SetKiller(killer);
            if (m_Player.CountFollowers > 0)
            {
                foreach (Actor fo in m_Player.Followers)
                    m_Session.Scoring.AddFollowerWhenDied(fo);
            }

            List<Zone> zone = m_Player.Location.Map.GetZonesAt(m_Player.Location.Position.X, m_Player.Location.Position.Y);
            if (zone == null)
            {
                m_Session.Scoring.DeathPlace = m_Player.Location.Map.Name;
            }
            else
            {
                string zoneName = zone[0].Name;
                m_Session.Scoring.DeathPlace = String.Format("{0} at {1}", m_Player.Location.Map.Name, zoneName);
            }
            if (killer != null)
                m_Session.Scoring.DeathReason = String.Format("{0} by {1} {2}",
                    m_Rules.IsMurder(killer, m_Player) ? "Murdered" : "Killed", killer.Model.Name, killer.TheName);
            else
                m_Session.Scoring.DeathReason = String.Format("Death by {0}", reason);
            m_Session.Scoring.AddEvent(m_Session.WorldTime.TurnCounter, "Died.");
            #endregion

            /////////////////////////////////////////
            // Tip, Message, screenshot & permadeath.
            /////////////////////////////////////////
            #region
            int iTip = m_Rules.Roll(0, GameTips.TIPS.Length);
            AddOverlay(new OverlayPopup(new string[] { "TIP OF THE DEAD", "Did you know that...", GameTips.TIPS[iTip] }, Color.White, Color.White, POPUP_FILLCOLOR, new Point(0, 0)));

            ClearMessages();
            AddMessage(new Message("**** YOU DIED! ****", m_Session.WorldTime.TurnCounter, Color.Red));
            if (killer != null)
                AddMessage(new Message(String.Format("Killer : {0}.", killer.TheName), m_Session.WorldTime.TurnCounter, Color.Red));
            AddMessage(new Message(String.Format("Reason : {0}.", reason), m_Session.WorldTime.TurnCounter, Color.Red));
            if (m_Player.Model.Abilities.IsUndead)
                AddMessage(new Message("You die one last time... Game over!", m_Session.WorldTime.TurnCounter, Color.Red));
            else
                AddMessage(new Message("You join the realm of the undeads... Game over!", m_Session.WorldTime.TurnCounter, Color.Red));

            // if permadeath on delete save file.
            if (s_Options.IsPermadeathOn)
                DeleteSavedGame(GetUserSave());

            // screenshot.
            if (s_Options.IsDeathScreenshotOn)
            {
                RedrawPlayScreen();
                string shotname = DoTakeScreenshot();
                if (shotname == null)
                    AddMessage(MakeErrorMessage("could not save death screenshot."));
                else
                    AddMessage(new Message(String.Format("Death screenshot saved : {0}.", shotname), m_Session.WorldTime.TurnCounter, Color.Red));
            }

            AddMessagePressEnter();
            #endregion

            // post mortem.
            HandlePostMortem();

            // music.
            m_MusicManager.Stop();

            // alpha10.1 bot release control
#if DEBUG
            BotReleaseControl();
#endif
        }

        string TimeSpanToString(TimeSpan rt)
        {
            // alpha10 shortened
            string timeDays = rt.Days == 0 ? "" : String.Format("{0} d ", rt.Days);
            string timeHours = rt.Hours == 0 ? "" : String.Format("{0:D2} h ", rt.Hours);
            string timeMinutes = rt.Minutes == 0 ? "" : String.Format("{0:D2} m ", rt.Minutes);
            string timeSeconds = rt.Seconds == 0 ? "" : String.Format("{0:D2} s", rt.Seconds);
            return String.Format("{0}{1}{2}{3}", timeDays, timeHours, timeMinutes, timeSeconds);
        }

        void HandlePostMortem()
        {
            ////////////////
            // Prepare data.
            ////////////////
            WorldTime deathTime = new WorldTime();
            deathTime.TurnCounter = m_Session.Scoring.TurnsSurvived;
            bool isMale = m_Player.Model.DollBody.IsMale;
            string heOrShe = isMale ? "He" : "She";
            string hisOrHer = HisOrHer(m_Player);
            string himOrHer = isMale ? "him" : "her";
            string name = m_Player.TheName.Replace("(YOU) ", "");
            TimeSpan rt = m_Session.Scoring.RealLifePlayingTime;
            string realTimeString = TimeSpanToString(rt);
            m_Session.Scoring.Side = m_Player.Model.Abilities.IsUndead ? DifficultySide.FOR_UNDEAD : DifficultySide.FOR_SURVIVOR;
            m_Session.Scoring.DifficultyRating = Scoring.ComputeDifficultyRating(s_Options, m_Session.Scoring.Side, m_Session.Scoring.ReincarnationNumber);

            ////////////////////////////////////
            // Format scoring into a text file.
            ///////////////////////////////////
            TextFile graveyard = new TextFile();

            graveyard.Append(String.Format("ROGUE SURVIVOR {0}", SetupConfig.GAME_VERSION));
            graveyard.Append("POST MORTEM");

            #region Summary
            graveyard.Append(String.Format("{0} was {1} and {2}.", name, AorAn(m_Player.Model.Name), AorAn(m_Player.Faction.MemberName)));
            graveyard.Append(String.Format("{0} survived to see {1}.", heOrShe, deathTime.ToString()));
            graveyard.Append(String.Format("{0}'s spirit guided {1} for {2}.", name, himOrHer, realTimeString));
            if (m_Session.Scoring.ReincarnationNumber > 0)
                graveyard.Append(String.Format("{0} was reincarnation {1}.", heOrShe, m_Session.Scoring.ReincarnationNumber));
            graveyard.Append(" ");
            #endregion

            graveyard.Append("> SCORING");
            #region
            graveyard.Append(String.Format("{0} scored a total of {1} points.", heOrShe, m_Session.Scoring.TotalPoints));
            graveyard.Append(String.Format("- difficulty rating of {0}%.", (int)(100 * m_Session.Scoring.DifficultyRating)));
            graveyard.Append(String.Format("- {0} base points for survival.", m_Session.Scoring.SurvivalPoints));
            graveyard.Append(String.Format("- {0} base points for kills.", m_Session.Scoring.KillPoints));
            graveyard.Append(String.Format("- {0} base points for achievements.", m_Session.Scoring.AchievementPoints));
            graveyard.Append(" ");
            #endregion

            graveyard.Append("> ACHIEVEMENTS");
            #region
            foreach (Achievement ach in m_Session.Scoring.Achievements)
            {
                if (ach.IsDone)
                    graveyard.Append(String.Format("- {0} for {1} points!", ach.Name, ach.ScoreValue));
                else
                    graveyard.Append(String.Format("- Fail : {0}.", ach.TeaseName));
            }
            if (m_Session.Scoring.CompletedAchievementsCount == 0)
            {
                graveyard.Append("Didn't achieve anything notable. And then died.");
                graveyard.Append(String.Format("(unlock all the {0} achievements to win this game version)", Scoring.MAX_ACHIEVEMENTS));
            }
            else
            {
                graveyard.Append(String.Format("Total : {0}/{1}.", m_Session.Scoring.CompletedAchievementsCount, Scoring.MAX_ACHIEVEMENTS));
                if (m_Session.Scoring.CompletedAchievementsCount >= Scoring.MAX_ACHIEVEMENTS)
                {
                    graveyard.Append("*** You achieved everything! You can consider having won this version of the game! CONGRATULATIONS! ***");
                }
                else
                    graveyard.Append("(unlock all the achievements to win this game version)");
                graveyard.Append("(later versions of the game will feature real winning conditions and multiple endings...)");
            }
            graveyard.Append(" ");
            #endregion

            graveyard.Append("> DEATH");
            #region
            graveyard.Append(String.Format("{0} in {1}.", m_Session.Scoring.DeathReason, m_Session.Scoring.DeathPlace));
            graveyard.Append(" ");
            #endregion

            graveyard.Append("> KILLS");
            #region
            if (m_Session.Scoring.HasNoKills)
            {
                graveyard.Append(String.Format("{0} was a pacifist. Or too scared to fight.", heOrShe));
            }
            else
            {
                // models kill list.
                foreach (Scoring.KillData killData in m_Session.Scoring.Kills)
                {
                    string modelName = killData.Amount > 1 ? Models.Actors[killData.ActorModelID].PluralName : Models.Actors[killData.ActorModelID].Name;
                    graveyard.Append(String.Format("{0,4} {1}.", killData.Amount, modelName));
                }
            }
            // murders? only livings.
            if (!m_Player.Model.Abilities.IsUndead)
            {
                if (m_Player.MurdersCounter > 0)
                {
                    graveyard.Append(String.Format("{0} committed {1} murder{2}!", heOrShe, m_Player.MurdersCounter, m_Player.MurdersCounter > 1 ? "s" : ""));
                }
            }

            graveyard.Append(" ");
            #endregion

            graveyard.Append("> FUN FACTS!");
            #region
            graveyard.Append(String.Format("While {0} has died, others are still having fun!", name));
            string[] funFacts = CompileDistrictFunFacts(m_Player.Location.Map.District);
            for (int i = 0; i < funFacts.Length; i++)
                graveyard.Append(funFacts[i]);
            graveyard.Append("");
            #endregion

            graveyard.Append("> SKILLS");
            #region
            if (m_Player.Sheet.SkillTable.Skills == null)
            {
                graveyard.Append(String.Format("{0} was a jack of all trades. Or an incompetent.", heOrShe));
            }
            else
            {
                foreach (Skill sk in m_Player.Sheet.SkillTable.Skills)
                {
                    graveyard.Append(String.Format("{0}-{1}.", sk.Level, Skills.Name(sk.ID)));
                }
            }
            graveyard.Append(" ");
            #endregion

            graveyard.Append("> INVENTORY");
            #region
            if (m_Player.Inventory.IsEmpty)
            {
                graveyard.Append(String.Format("{0} was humble. Or dirt poor.", heOrShe));
            }
            else
            {
                foreach (Item it in m_Player.Inventory.Items)
                {
                    string desc = DescribeItemShort(it);
                    if (it.IsEquipped)
                        graveyard.Append(String.Format("- {0} (equipped).", desc));
                    else
                        graveyard.Append(String.Format("- {0}.", desc));
                }
            }
            graveyard.Append(" ");
            #endregion

            graveyard.Append("> FOLLOWERS");
            #region
            if (m_Session.Scoring.FollowersWhendDied == null || m_Session.Scoring.FollowersWhendDied.Count == 0)
            {
                graveyard.Append(String.Format("{0} was doing fine alone. Or everyone else was dead.", heOrShe));
            }
            else
            {
                // names.
                StringBuilder sb = new StringBuilder(String.Format("{0} was leading", heOrShe));
                bool firstFo = true;
                int i = 0;
                int count = m_Session.Scoring.FollowersWhendDied.Count;
                foreach (Actor fo in m_Session.Scoring.FollowersWhendDied)
                {
                    if (firstFo)
                        sb.Append(" ");
                    else
                    {
                        if (i == count)
                            sb.Append(".");
                        else if (i == count - 1)
                            sb.Append(" and ");
                        else
                            sb.Append(", ");
                    }
                    sb.Append(fo.TheName);
                    ++i;
                    firstFo = false;
                }
                sb.Append(".");
                graveyard.Append(sb.ToString());

                // skills.
                foreach (Actor fo in m_Session.Scoring.FollowersWhendDied)
                {
                    graveyard.Append(String.Format("{0} skills : ", fo.Name));
                    if (fo.Sheet.SkillTable != null && fo.Sheet.SkillTable.Skills != null)
                    {
                        foreach (Skill sk in fo.Sheet.SkillTable.Skills)
                        {
                            graveyard.Append(String.Format("{0}-{1}.", sk.Level, Skills.Name(sk.ID)));
                        }
                    }
                }
            }
            graveyard.Append(" ");
            #endregion

            graveyard.Append("> EVENTS");
            #region
            if (m_Session.Scoring.HasNoEvents)
            {
                graveyard.Append(String.Format("{0} had a quiet life. Or dull and boring.", heOrShe));
            }
            else
            {
                foreach (Scoring.GameEventData ev in m_Session.Scoring.Events)
                {
                    WorldTime evTime = new WorldTime();
                    evTime.TurnCounter = ev.Turn;
                    graveyard.Append(String.Format("- {0,13} : {1}", evTime.ToString(), ev.Text));
                }
            }
            graveyard.Append(" ");
            #endregion

            graveyard.Append("> CUSTOM OPTIONS");
            #region
            graveyard.Append(String.Format("- difficulty rating of {0}%.", (int)(100 * m_Session.Scoring.DifficultyRating)));
            if (s_Options.IsPermadeathOn)
                graveyard.Append(String.Format("- {0} : yes.", GameOptions.Name(GameOptions.IDs.GAME_PERMADEATH)));
            if (!s_Options.AllowUndeadsEvolution && Rules.HasEvolution(m_Session.GameMode)) // alpha10 only if manually disabled
                graveyard.Append(String.Format("- {0} : {1}.", GameOptions.Name(GameOptions.IDs.GAME_ALLOW_UNDEADS_EVOLUTION), s_Options.AllowUndeadsEvolution ? "yes" : "no"));
            if (s_Options.CitySize != GameOptions.DEFAULT_CITY_SIZE)
                graveyard.Append(String.Format("- {0} : {1}.", GameOptions.Name(GameOptions.IDs.GAME_CITY_SIZE), s_Options.CitySize));
            if (s_Options.DayZeroUndeadsPercent != GameOptions.DEFAULT_DAY_ZERO_UNDEADS_PERCENT)
                graveyard.Append(String.Format("- {0} : {1}%.", GameOptions.Name(GameOptions.IDs.GAME_DAY_ZERO_UNDEADS_PERCENT), s_Options.DayZeroUndeadsPercent));
            if (s_Options.DistrictSize != GameOptions.DEFAULT_DISTRICT_SIZE)
                graveyard.Append(String.Format("- {0} : {1}.", GameOptions.Name(GameOptions.IDs.GAME_DISTRICT_SIZE), s_Options.DistrictSize));
            if (s_Options.MaxCivilians != GameOptions.DEFAULT_MAX_CIVILIANS)
                graveyard.Append(String.Format("- {0} : {1}.", GameOptions.Name(GameOptions.IDs.GAME_MAX_CIVILIANS), s_Options.MaxCivilians));
            if (s_Options.MaxUndeads != GameOptions.DEFAULT_MAX_UNDEADS)
                graveyard.Append(String.Format("- {0} : {1}.", GameOptions.Name(GameOptions.IDs.GAME_MAX_UNDEADS), s_Options.MaxUndeads));
            if (!s_Options.NPCCanStarveToDeath)
                graveyard.Append(String.Format("- {0} : {1}.", GameOptions.Name(GameOptions.IDs.GAME_NPC_CAN_STARVE_TO_DEATH), s_Options.NPCCanStarveToDeath ? "yes" : "no"));
            if (s_Options.StarvedZombificationChance != GameOptions.DEFAULT_STARVED_ZOMBIFICATION_CHANCE)
                graveyard.Append(String.Format("- {0} : {1}%.", GameOptions.Name(GameOptions.IDs.GAME_STARVED_ZOMBIFICATION_CHANCE), s_Options.StarvedZombificationChance));
            if (!s_Options.RevealStartingDistrict)
                graveyard.Append(String.Format("- {0} : {1}.", GameOptions.Name(GameOptions.IDs.GAME_REVEAL_STARTING_DISTRICT), s_Options.RevealStartingDistrict ? "yes" : "no"));
            if (s_Options.SimulateDistricts != GameOptions.DEFAULT_SIM_DISTRICTS)
                graveyard.Append(String.Format("- {0} : {1}.", GameOptions.Name(GameOptions.IDs.GAME_SIMULATE_DISTRICTS), GameOptions.Name(s_Options.SimulateDistricts)));
            if (s_Options.SimulateWhenSleeping)
                graveyard.Append(String.Format("- {0} : {1}.", GameOptions.Name(GameOptions.IDs.GAME_SIMULATE_SLEEP), s_Options.SimulateWhenSleeping ? "yes" : "no"));
            if (s_Options.ZombieInvasionDailyIncrease != GameOptions.DEFAULT_ZOMBIE_INVASION_DAILY_INCREASE)
                graveyard.Append(String.Format("- {0} : {1}%.", GameOptions.Name(GameOptions.IDs.GAME_ZOMBIE_INVASION_DAILY_INCREASE), s_Options.ZombieInvasionDailyIncrease));
            if (s_Options.ZombificationChance != GameOptions.DEFAULT_ZOMBIFICATION_CHANCE)
                graveyard.Append(String.Format("- {0} : {1}%.", GameOptions.Name(GameOptions.IDs.GAME_ZOMBIFICATION_CHANCE), s_Options.ZombificationChance));
            if (s_Options.MaxReincarnations != GameOptions.DEFAULT_MAX_REINCARNATIONS)
                graveyard.Append(String.Format("- {0} : {1}.", GameOptions.Name(GameOptions.IDs.GAME_MAX_REINCARNATIONS), s_Options.MaxReincarnations));
            graveyard.Append(" ");
            #endregion

            graveyard.Append("> R.I.P");
            #region
            graveyard.Append(String.Format("May {0} soul rest in peace.", HisOrHer(m_Player)));
            graveyard.Append(String.Format("For {0} body is now a meal for evil.", HisOrHer(m_Player)));
            graveyard.Append("The End.");
            #endregion

            /////////////////////
            // Save to graveyard
            /////////////////////
            int gx, gy;
            gx = gy = 0;
            m_UI.UI_Clear(Color.Black);
            m_UI.UI_DrawStringBold(Color.Yellow, "Saving post mortem to graveyard...", 0, 0);
            gy += BOLD_LINE_SPACING;
            m_UI.UI_Repaint();
            string graveName = GetUserNewGraveyardName();
            string graveFile = GraveFilePath(graveName);
            if (!graveyard.Save(graveFile))
            {
                m_UI.UI_DrawStringBold(Color.Red, "Could not save to graveyard.", 0, gy);
                gy += BOLD_LINE_SPACING;
            }
            else
            {
                m_UI.UI_DrawStringBold(Color.Yellow, "Grave saved to :", 0, gy);
                gy += BOLD_LINE_SPACING;
                m_UI.UI_DrawString(Color.White, graveFile, 0, gy);
                gy += BOLD_LINE_SPACING;
            }
            DrawFootnote(Color.White, "press ENTER");
            m_UI.UI_Repaint();
            WaitEnter();

            ///////////////////////////////
            // Display grave as text file.
            ///////////////////////////////
            graveyard.FormatLines(TEXTFILE_CHARS_PER_LINE);
            int iLine = 0;
            bool loop = false;
            do
            {
                // header.
                m_UI.UI_Clear(Color.Black);
                gx = gy = 0;
                DrawHeader();
                gy += BOLD_LINE_SPACING;

                // text.
                int linesThisPage = 0;
                m_UI.UI_DrawStringBold(Color.White, "---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+", 0, gy);
                gy += BOLD_LINE_SPACING;
                while (linesThisPage < TEXTFILE_LINES_PER_PAGE && iLine < graveyard.FormatedLines.Count)
                {
                    string line = graveyard.FormatedLines[iLine];
                    m_UI.UI_DrawStringBold(Color.White, line, gx, gy);
                    gy += BOLD_LINE_SPACING;
                    ++iLine;
                    ++linesThisPage;
                }

                // foot.
                m_UI.UI_DrawStringBold(Color.White, "---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+", 0, CANVAS_HEIGHT - 2 * BOLD_LINE_SPACING);
                if (iLine < graveyard.FormatedLines.Count)
                    DrawFootnote(Color.White, "press ENTER for more");
                else
                    DrawFootnote(Color.White, "press ENTER to leave");

                // wait.
                m_UI.UI_Repaint();
                WaitEnter();

                // loop?
                loop = (iLine < graveyard.FormatedLines.Count);
            }
            while (loop);

            /////////////
            // Hi Score?
            /////////////
            StringBuilder skillsSb = new StringBuilder();
            if (m_Player.Sheet.SkillTable.Skills != null)
            {
                foreach (Skill sk in m_Player.Sheet.SkillTable.Skills)
                {
                    skillsSb.AppendFormat("{0}-{1} ", sk.Level, Skills.Name(sk.ID));
                }
            }
            HiScore newHiScore = HiScore.FromScoring(name, m_Session.Scoring, skillsSb.ToString());
            if (m_HiScoreTable.Register(newHiScore))
            {
                SaveHiScoreTable();
                HandleHiScores(true);
            }
        }

        #endregion
    }
}
