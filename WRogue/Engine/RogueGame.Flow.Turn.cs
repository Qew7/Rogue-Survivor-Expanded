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
        void NextMapTurn(Map map, SimFlags sim)
        {
            bool isLoDetail = (sim & SimFlags.LODETAIL_TURN) != 0;

            ////////////////////////////////////////
            // (the following are skipped in lodetail turns)
            // 0. Raise the deads; Check infections (non STD)
            // 1. Update odors.
            //      alpha10 obsolete 1.1 Odor suppression/generation.
            //      1.2 Odors decay.
            //      1.3 Actors scents.  // alpha10
            // 2. Regen actors AP & STA
            // 3. Stop tired actors from running.
            // 4. Actor gauges & states :
            //    Hunger, Sleep, Sanity, Leader Trust.
            //      4.1 May kill starved actors.
            //      4.2 Handle sleeping actors.
            //      4.3 Exhausted actors might collapse.
            // 5. Check batteries : lights, trackers.
            // 6. Check explosives.
            // 7. Check fires.
            // (the following are always performed)
            // - Check timers.
            // - Advance local time.
            // - Check for NPC upgrade.
            ////////////////////////////////////////

            // TEST CORPSES
#if false
            if (map.LocalTime.TurnCounter < 2 && map == m_Player.Location.Map)
            {
                for (int i = 0; i < 10; i++)
                {
                    Actor deadGuy = m_TownGenerator.CreateNewCivilian(0, 0, 0);
                    Point p = new Point(1, 1);
                    if (map.GetActorAt(p.Add(m_Player.Location.Position)) == null)
                    {
                        map.PlaceActorAt(deadGuy, p.Add(m_Player.Location.Position));
                        KillActor(null, deadGuy, "TEST CORPSES");
                    }
                }
            }

            foreach (Actor a in map.Actors)
            {
                if (a.IsPlayer) continue;
                if (a.Model.Abilities.IsUndead) continue;
                KillActor(null, a, "TEST CORPSES");
                break;
            }
#endif
            if (!isLoDetail)
            {
                AdvanceCorpsesAndInfection(map);

                AdvanceMapScents(map);

                // 2. Regen actors AP & STA
                #region
                // regen.
                foreach (Actor actor in map.Actors)
                {
                    if (!actor.IsSleeping)
                        actor.ActionPoints += m_Rules.ActorSpeed(actor);

                    if (actor.StaminaPoints < m_Rules.ActorMaxSTA(actor))
                        RegenActorStaminaPoints(actor, Rules.STAMINA_REGEN_PER_TURN);
                }
                // reset actor index.
                map.CheckNextActorIndex = 0;
                #endregion

                // 3. Stop tired actors from running.
                #region
                foreach (Actor actor in map.Actors)
                {
                    if (actor.IsRunning)
                    {
                        if (actor.StaminaPoints < Rules.STAMINA_MIN_FOR_ACTIVITY)
                        {
                            actor.IsRunning = false;
                            if (actor == m_Player)
                            {
                                AddMessage(MakeMessage(actor, String.Format("{0} too tired to continue running!", Conjugate(actor, VERB_BE))));
                                RedrawPlayScreen();
                            }
                        }
                    }
                }
                #endregion

                // 4. Actor gauges & states
                #region
                List<Actor> actorsStarvedToDeath = null;
                foreach (Actor actor in map.Actors)
                {
                    // hunger && rot.
                    #region
                    if (actor.Model.Abilities.HasToEat)
                    {
                        // food points loss.
                        --actor.FoodPoints;
                        if (actor.FoodPoints < 0) actor.FoodPoints = 0;

                        // May kill starved actors.
                        if (m_Rules.IsActorStarving(actor))
                        {
                            // kill him?
                            if (m_Rules.RollChance(Rules.FOOD_STARVING_DEATH_CHANCE))
                            {
                                if (actor.IsPlayer || s_Options.NPCCanStarveToDeath)
                                {
                                    if (actorsStarvedToDeath == null)
                                        actorsStarvedToDeath = new List<Actor>();
                                    actorsStarvedToDeath.Add(actor);
                                }
                            }
                        }
                    }
                    else if (actor.Model.Abilities.IsRotting)
                    {
                        // rot.
                        int rotLoss = m_Session.GamePreset.RotDecayPercent / 100;
                        if (m_Rules.RollChance(m_Session.GamePreset.RotDecayPercent % 100)) rotLoss++;
                        actor.FoodPoints -= rotLoss;
                        if (actor.FoodPoints < 0) actor.FoodPoints = 0;

                        // rot effects.
                        if (m_Rules.IsRottingActorStarving(actor))
                        {
                            // loose 1 HP.
                            if (m_Rules.Roll(0, 1000) < Rules.ROT_STARVING_HP_CHANCE)
                            {
                                if (IsVisibleToPlayer(actor))
                                {
                                    AddMessage(MakeMessage(actor, "is rotting away."));
                                }
                                if (--actor.HitPoints <= 0)
                                {
                                    if (actorsStarvedToDeath == null)
                                        actorsStarvedToDeath = new List<Actor>();
                                    actorsStarvedToDeath.Add(actor);
                                }
                            }
                        }
                        else if (m_Rules.IsRottingActorHungry(actor))
                        {
                            // loose a skill.
                            if (m_Rules.Roll(0, 1000) < Rules.ROT_HUNGRY_SKILL_CHANCE)
                                DoLooseRandomSkill(actor);
                        }
                    }
                    #endregion

                    // sleep.
                    #region
                    if (actor.Model.Abilities.HasToSleep)
                    {
                        // sleep vs sleep points loss.
                        if (actor.IsSleeping)
                        {
                            // sleeping.
                            // nightmare?
                            if (m_Rules.IsActorDisturbed(actor) && m_Rules.RollChance(Rules.SANITY_NIGHTMARE_CHANCE))
                            {
                                // wake up, shout, lose sleep and sta.
                                DoWakeUp(actor);
                                DoShout(actor, "NO! LEAVE ME ALONE!");
                                actor.SleepPoints -= Rules.SANITY_NIGHTMARE_SLP_LOSS;
                                if (actor.SleepPoints < 0) actor.SleepPoints = 0;
                                SpendActorSanity(actor, Rules.SANITY_NIGHTMARE_SAN_LOSS);
                                SpendActorStaminaPoints(actor, Rules.SANITY_NIGHTMARE_STA_LOSS);
                                // msg.
                                if (IsVisibleToPlayer(actor))
                                    AddMessage(MakeMessage(actor, String.Format("{0} from a horrible nightmare!", Conjugate(actor, VERB_WAKE_UP))));
                                // if player, sfx.
                                if (actor.IsPlayer)
                                {
                                    // FIXME replace with sfx
                                    // alpha10
                                    m_MusicManager.Stop();
                                    m_MusicManager.Play(GameSounds.NIGHTMARE, MusicPriority.PRIORITY_EVENT);
                                }
                            }
                        }
                        else
                        {
                            // awake.
                            --actor.SleepPoints;
                            if (map.LocalTime.IsNight)
                                --actor.SleepPoints;
                            if (actor.SleepPoints < 0) actor.SleepPoints = 0;
                        }

                        //      4.2 Handle sleeping actors.
                        #region
                        if (actor.IsSleeping)
                        {
                            bool isOnCouch = m_Rules.IsOnCouch(actor);
                            // activity.
                            actor.Activity = Activity.SLEEPING;

                            // regen sleep pts.
                            int sleepRegen = m_Rules.ActorSleepRegen(actor, isOnCouch);
                            actor.SleepPoints += sleepRegen;
                            actor.SleepPoints = Math.Min(actor.SleepPoints, m_Rules.ActorMaxSleep(actor));

                            // heal?
                            if (actor.HitPoints < m_Rules.ActorMaxHPs(actor))
                            {
                                int healChance = (isOnCouch ? Rules.SLEEP_ON_COUCH_HEAL_CHANCE : 0);
                                healChance += m_Rules.ActorHealChanceBonus(actor);
                                if (m_Rules.RollChance(healChance))
                                    RegenActorHitPoints(actor, Rules.SLEEP_HEAL_HITPOINTS);
                            }

                            // wake up?
                            // wake up if hungry or fully slept.
                            bool wakeUp = m_Rules.IsActorHungry(actor) || actor.SleepPoints >= m_Rules.ActorMaxSleep(actor);
                            if (wakeUp)
                            {
                                DoWakeUp(actor);
                            }
                            else
                            {
                                if (actor.IsPlayer)
                                {
                                    // check music.
                                    if (m_MusicManager.Music != GameMusics.SLEEP)
                                    {
                                        m_MusicManager.Stop();
                                        m_MusicManager.PlayLooping(GameMusics.SLEEP, MusicPriority.PRIORITY_EVENT);
                                    }
                                    // message.
                                    AddMessage(new Message("...zzZZZzzZ...", map.LocalTime.TurnCounter, Color.DarkCyan));
                                    RedrawPlayScreen();
                                    // give some time to sim thread.
                                    if (s_Options.SimThread)
                                        Thread.Sleep(10);
                                }
                                else if (m_Rules.RollChance(MESSAGE_NPC_SLEEP_SNORE_CHANCE) && IsVisibleToPlayer(actor))
                                {
                                    AddMessage(MakeMessage(actor, String.Format("{0}.", Conjugate(actor, VERB_SNORE))));
                                    RedrawPlayScreen();
                                }
                            }
                        }
                        #endregion

                        //      4.3 Exhausted actors might collapse.
                        #region
                        if (m_Rules.IsActorExhausted(actor))
                        {
                            if (m_Rules.RollChance(Rules.SLEEP_EXHAUSTION_COLLAPSE_CHANCE))
                            {
                                // do it
                                DoStartSleeping(actor);

                                // message.
                                if (IsVisibleToPlayer(actor))
                                {
                                    AddMessage(MakeMessage(actor, String.Format("{0} from exhaustion !!", Conjugate(actor, VERB_COLLAPSE))));
                                    RedrawPlayScreen();
                                }

                                // player?
                                if (actor == m_Player)
                                {
                                    UpdatePlayerFOV(m_Player);
                                    ComputeViewRect(m_Player.Location.Position);
                                    RedrawPlayScreen();
                                }
                            }
                        }
                        #endregion
                    }
                    #endregion

                    // sanity.
                    #region
                    if (actor.Model.Abilities.HasSanity)
                    {
                        // sanity loss.
                        if (--actor.Sanity <= 0) actor.Sanity = 0;
                    }
                    #endregion

                    // leader trust & leader/follower bond.
                    #region
                    if (actor.HasLeader)
                    {
                        // trust.
                        ModifyActorTrustInLeader(actor, m_Rules.ActorTrustIncrease(actor.Leader), false);
                        // bond with leader.
                        if (m_Rules.HasActorBondWith(actor, actor.Leader) && m_Rules.RollChance(Rules.SANITY_RECOVER_BOND_CHANCE))
                        {
                            RegenActorSanity(actor, Rules.SANITY_RECOVER_BOND);
                            RegenActorSanity(actor.Leader, Rules.SANITY_RECOVER_BOND);
                            if (IsVisibleToPlayer(actor))
                                AddMessage(MakeMessage(actor, String.Format("{0} reassured knowing {1} is with {2}.",
                                            Conjugate(actor, VERB_FEEL), actor.Leader.Name, HimOrHer(actor))));
                            if (IsVisibleToPlayer(actor.Leader))
                                AddMessage(MakeMessage(actor.Leader, String.Format("{0} reassured knowing {1} is with {2}.",
                                            Conjugate(actor.Leader, VERB_FEEL), actor.Name, HimOrHer(actor.Leader))));
                        }
                    }
                    #endregion

                }
                #endregion
                #region Kill (zombify) starved actors.
                if (actorsStarvedToDeath != null)
                {
                    foreach (Actor actor in actorsStarvedToDeath)
                    {
                        // message.
                        if (IsVisibleToPlayer(actor))
                        {
                            AddMessage(MakeMessage(actor, String.Format("{0} !!", Conjugate(actor, VERB_DIE_FROM_STARVATION))));
                            RedrawPlayScreen();
                        }

                        // kill.
                        KillActor(null, actor, "starvation");

                        // zombify?
                        if (!actor.Model.Abilities.IsUndead && m_Session.GamePreset.ImmediateZombification && m_Rules.RollChance(s_Options.StarvedZombificationChance))
                        {
                            // remove morpse!
                            map.TryRemoveCorpseOf(actor);
                            // zombify!
                            Zombify(null, actor, false);
                            // show.
                            if (IsVisibleToPlayer(actor))
                            {
                                AddMessage(MakeMessage(actor, String.Format("{0} into a Zombie!", Conjugate(actor, "turn"))));
                                RedrawPlayScreen();
                                AnimDelay(DELAY_LONG);
                            }
                        }
                    }
                }
                #endregion

                // 5. Check batteries : lights, trackers.
                #region
                foreach (Actor actor in map.Actors)
                {
                    Item leftItem = actor.GetEquippedItem(DollPart.LEFT_HAND);
                    if (leftItem == null)
                        continue;

                    // light?
                    ItemLight light = leftItem as ItemLight;
                    if (light != null)
                    {
                        if (light.Batteries > 0)
                        {
                            --light.Batteries;
                            if (light.Batteries <= 0)
                            {
                                if (IsVisibleToPlayer(actor))
                                {
                                    AddMessage(MakeMessage(actor, String.Format(": {0} light goes off.", light.TheName)));
                                }
                            }
                        }
                        continue;
                    }

                    // tracker?
                    ItemTracker tracker = leftItem as ItemTracker;
                    if (tracker != null)
                    {
                        if (tracker.Batteries > 0)
                        {
                            --tracker.Batteries;
                            if (tracker.Batteries <= 0)
                            {
                                if (IsVisibleToPlayer(actor))
                                {
                                    AddMessage(MakeMessage(actor, String.Format(": {0} goes off.", tracker.TheName)));
                                }
                            }
                        }
                        continue;
                    }
                }
                #endregion

                // 6. Check explosives.
                #region
                // 6.1 Update fuses.
                bool hasExplosivesToExplode = false;
                #region
                // on ground.
                foreach (Inventory groundInv in map.GroundInventories)
                {
                    // update each explosive fuse there,
                    // remember which should explode.
                    foreach (Item it in groundInv.Items)
                    {
                        ItemPrimedExplosive primed = it as ItemPrimedExplosive;
                        if (primed == null)
                            continue;

                        // primed explosive, burn fuse.
                        --primed.FuseTimeLeft;
                        if (primed.FuseTimeLeft <= 0)
                            hasExplosivesToExplode = true;
                    }
                }

                // on actors.
                foreach (Actor actor in map.Actors)
                {
                    Inventory inv = actor.Inventory;
                    if (inv == null || inv.IsEmpty)
                        continue;

                    // update each explosive fuse there,
                    // remember which should explode.
                    foreach (Item it in inv.Items)
                    {
                        ItemPrimedExplosive primed = it as ItemPrimedExplosive;
                        if (primed == null)
                            continue;

                        // primed explosive, burn fuse.
                        --primed.FuseTimeLeft;
                        if (primed.FuseTimeLeft <= 0)
                            hasExplosivesToExplode = true;
                    }
                }
                #endregion

                // 6.2 Explode.
                #region
                if (hasExplosivesToExplode)
                {
                    bool hasExplodedSomething = false;
                    do
                    {
                        // nothing exploded by default.
                        hasExplodedSomething = false;

                        // on ground.
                        if (!hasExplodedSomething)
                        {
                            foreach (Inventory groundInv in map.GroundInventories)
                            {
                                Point? pos = map.GetGroundInventoryPosition(groundInv);
                                if (pos == null)
                                    throw new InvalidOperationException("explosives : GetGroundInventoryPosition returned null point");

                                foreach (Item it in groundInv.Items)
                                {
                                    ItemPrimedExplosive primed = it as ItemPrimedExplosive;
                                    if (primed == null)
                                        continue;

                                    if (primed.FuseTimeLeft <= 0)
                                    {
                                        // boom!
                                        map.RemoveItemAt(primed, pos.Value);
                                        DoBlast(new Location(map, pos.Value), (primed.Model as ItemExplosiveModel).BlastAttack);
                                        hasExplodedSomething = true;
                                        break;
                                    }
                                }

                                if (hasExplodedSomething)
                                    break;
                            }
                        }

                        // on actors.
                        if (!hasExplodedSomething)
                        {
                            foreach (Actor actor in map.Actors)
                            {
                                Inventory inv = actor.Inventory;
                                if (inv == null || inv.IsEmpty)
                                    continue;

                                foreach (Item it in inv.Items)
                                {
                                    ItemPrimedExplosive primed = it as ItemPrimedExplosive;
                                    if (primed == null)
                                        continue;

                                    if (primed.FuseTimeLeft <= 0)
                                    {
                                        // boom!
                                        actor.Inventory.RemoveAllQuantity(primed);
                                        DoBlast(new Location(map, actor.Location.Position), (primed.Model as ItemExplosiveModel).BlastAttack);
                                        hasExplodedSomething = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    while (hasExplodedSomething);
                }
                #endregion
                #endregion

                // 7. Check fires.
                #region
                // 7.1 Rain has a chance to put out fires.
                // FIXME there still the weather bug when simulating = weather used is current world weather, not map weather.
                #region
                // Check?
                if (m_Rules.IsWeatherRain(m_Session.World.Weather) && m_Rules.RollChance(Rules.FIRE_RAIN_TEST_CHANCE))
                {
                    // 7.1.1 Burning objects?
                    foreach (MapObject obj in map.MapObjects)
                    {
                        if (obj.IsOnFire && m_Rules.RollChance(Rules.FIRE_RAIN_PUT_OUT_CHANCE))
                        {
                            // do it.
                            UnapplyOnFire(obj);
                            // tell.
                            if (IsVisibleToPlayer(obj))
                            {
                                AddMessage(new Message("The rain has put out a fire.", map.LocalTime.TurnCounter));
                            }
                        }
                    }
                }
                #endregion
                #endregion
            }   // skipped in lodetail turns.

            AdvanceMapTimers(map);

            // -- Advance local time.
            #region
            bool wasLocalNight = map.LocalTime.IsNight;
            ++map.LocalTime.TurnCounter;
            bool isLocalDay = !map.LocalTime.IsNight;
            #endregion

            // -- Check for NPC upgrade.
            #region
            if (wasLocalNight && isLocalDay)
            {
                HandleLivingNPCsUpgrade(map);
            }
            else if (s_Options.ZombifiedsUpgradeDays != GameOptions.ZupDays.OFF && !wasLocalNight && !isLocalDay && GameOptions.IsZupDay(s_Options.ZombifiedsUpgradeDays, map.LocalTime.Day))
            {
                HandleUndeadNPCsUpgrade(map);
            }
            #endregion
        }

    }
}
