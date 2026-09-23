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
                // 0. Raise the deads; Check infections (non STD)
                #region
                bool hasCorpses = Rules.HasCorpses(m_Session.GameMode);
                bool hasInfection = Rules.HasInfection(m_Session.GameMode);
                if (hasCorpses || hasInfection)
                {
                    #region Corpses
                    if (hasCorpses && map.CountCorpses > 0)
                    {
                        // decide who zombify or rots.
                        List<Corpse> tryZombifyCorpses = new List<Corpse>(map.CountCorpses);
                        List<Corpse> rottenCorpses = new List<Corpse>(map.CountCorpses);
                        foreach (Corpse c in map.Corpses)
                        {
                            // zombify?
                            int chanceZombify = m_Rules.CorpseZombifyChance(c, map.LocalTime);
                            if (m_Rules.RollChance(chanceZombify))
                            {
                                // zombify this one.
                                tryZombifyCorpses.Add(c);
                                continue;
                            }
                            // or rot away?
                            InflictDamageToCorpse(c, Rules.CorpseDecayPerTurn(c));
                            if (c.HitPoints <= 0)
                            {
                                rottenCorpses.Add(c);
                                continue;
                            }
                        }
                        // zombify!
                        if (tryZombifyCorpses.Count > 0)
                        {
                            List<Corpse> zombifiedCorpses = new List<Corpse>(tryZombifyCorpses.Count);
                            foreach (Corpse c in tryZombifyCorpses)
                            {
                                // only one actor per tile!
                                if (map.GetActorAt(c.Position) == null)
                                {
                                    float corpseState = (float)c.HitPoints / (float)c.MaxHitPoints;
                                    int zombifiedHP = (int)(corpseState * m_Rules.ActorMaxHPs(c.DeadGuy));
                                    zombifiedCorpses.Add(c);
                                    Actor zombified = Zombify(null, c.DeadGuy, false);

                                    if (IsVisibleToPlayer(map, c.Position))
                                    {
                                        AddMessage(new Message(String.Format("The corpse of {0} rise again!!", c.DeadGuy.Name), map.LocalTime.TurnCounter, Color.Red));
                                        // FIXME --
                                        // alpha10 this will be a sfx not music
                                        m_MusicManager.Play(GameSounds.UNDEAD_RISE, MusicPriority.PRIORITY_EVENT);
                                    }
                                }
                            }
                            foreach (Corpse c in zombifiedCorpses)
                                DestroyCorpse(c, map);
                        }
                        // rot! (message only)
                        if (m_Player != null && m_Player.Location.Map == map)
                        {
                            foreach (Corpse c in rottenCorpses)
                            {
                                DestroyCorpse(c, map);
                                if (IsVisibleToPlayer(map, c.Position))
                                    AddMessage(new Message(String.Format("The corpse of {0} turns into dust.", c.DeadGuy.Name), map.LocalTime.TurnCounter, Color.Purple));
                            }
                        }
                    }
                    #endregion

                    #region Infection effects
                    if (hasInfection)
                    {
                        List<Actor> infectedToKill = null;
                        foreach (Actor a in map.Actors)
                        {
                            if (a.Infection >= Rules.INFECTION_LEVEL_1_WEAK && !a.Model.Abilities.IsUndead)
                            {
                                int infectionP = m_Rules.ActorInfectionPercent(a);

                                #region
                                if (m_Rules.Roll(0, 1000) < m_Rules.InfectionEffectTriggerChance1000(infectionP))
                                {
                                    bool isVisible = IsVisibleToPlayer(a);
                                    bool isPlayer = a.IsPlayer;  // alpha10.1 consistency fix
                                    bool isBot = a.IsBotPlayer;  // alpha10.1 handle bot

                                    // if sleeping, wake up.
                                    if (a.IsSleeping)
                                        DoWakeUp(a);

                                    // apply effect.
                                    bool killHim = false;
                                    if (infectionP >= Rules.INFECTION_LEVEL_5_DEATH)
                                    {
                                        killHim = true;
                                    }
                                    else if (infectionP >= Rules.INFECTION_LEVEL_4_BLEED)
                                    {
                                        DoVomit(a);
                                        a.HitPoints -= Rules.INFECTION_LEVEL_4_BLEED_HP;
                                        if (CancelMouseMoveOnDamage(a, Rules.INFECTION_LEVEL_4_BLEED_HP))
                                            AddMessage(new Message("Mouse movement stopped: you took damage.", m_Session.WorldTime.TurnCounter, Color.Red));
                                        if (isVisible)
                                        {
                                            if (isPlayer) ClearMessages();
                                            AddMessage(MakeMessage(a, String.Format("{0} blood.", Conjugate(a, VERB_VOMIT)), Color.Purple));
                                            if (isPlayer && !isBot)
                                            {
                                                AddMessagePressEnter();
                                                ClearMessages();
                                            }
                                        }
                                        if (a.HitPoints <= 0)
                                            killHim = true;
                                    }
                                    else if (infectionP >= Rules.INFECTION_LEVEL_3_VOMIT)
                                    {
                                        DoVomit(a);
                                        if (isVisible)
                                        {
                                            if (isPlayer) ClearMessages();
                                            AddMessage(MakeMessage(a, String.Format("{0}.", Conjugate(a, VERB_VOMIT)), Color.Purple));
                                            if (isPlayer && !isBot)
                                            {
                                                AddMessagePressEnter();
                                                ClearMessages();
                                            }
                                        }
                                    }
                                    else if (infectionP >= Rules.INFECTION_LEVEL_2_TIRED)
                                    {
                                        SpendActorStaminaPoints(a, Rules.INFECTION_LEVEL_2_TIRED_STA);
                                        a.SleepPoints -= Rules.INFECTION_LEVEL_2_TIRED_SLP;
                                        if (a.SleepPoints < 0) a.SleepPoints = 0;
                                        if (isVisible)
                                        {
                                            if (isPlayer) ClearMessages();
                                            AddMessage(MakeMessage(a, String.Format("{0} sick and tired.", Conjugate(a, VERB_FEEL)), Color.Purple));
                                            if (isPlayer && !isBot)
                                            {
                                                AddMessagePressEnter();
                                                ClearMessages();
                                            }
                                        }
                                    }
                                    else if (infectionP >= Rules.INFECTION_LEVEL_1_WEAK)
                                    {
                                        SpendActorStaminaPoints(a, Rules.INFECTION_LEVEL_1_WEAK_STA);
                                        if (isVisible)
                                        {
                                            if (isPlayer) ClearMessages();
                                            AddMessage(MakeMessage(a, String.Format("{0} sick and weak.", Conjugate(a, VERB_FEEL)), Color.Purple));
                                            if (isPlayer && !isBot)
                                            {
                                                AddMessagePressEnter();
                                                ClearMessages();
                                            }
                                        }
                                    }

                                    // if it kills him, remember.
                                    if (killHim)
                                    {
                                        if (infectedToKill == null) infectedToKill = new List<Actor>(map.CountActors);
                                        infectedToKill.Add(a);
                                    }
                                } // trigged effect
                                #endregion
                            } // is infected
                        } // each actor

                        // kill infected to kill (duh)
                        if (infectedToKill != null)
                        {
                            foreach (Actor a in infectedToKill)
                            {
                                if (IsVisibleToPlayer(a))
                                    AddMessage(MakeMessage(a, String.Format("{0} of infection!", Conjugate(a, VERB_DIE))));
                                KillActor(null, a, "infection");
                                // if player, force zombify NOW.
                                if (a.IsPlayer)
                                {
                                    // remove player corpse!
                                    map.TryRemoveCorpseOf(a);
                                    // zombify player!
                                    Zombify(null, a, false);

                                    // show
                                    AddMessage(MakeMessage(a, Conjugate(a, "turn") + " into a Zombie!"));
                                    RedrawPlayScreen();
                                    AnimDelay(DELAY_LONG);
                                }
                            }
                        }
                    }
                    #endregion
                }  // non STD game.
                #endregion

                // 1. Update odors.
                // alpha10 obsolete     1.1 Odor suppression/generation.
                #region
                //List<OdorScent> scentGenerated = new List<OdorScent>();
                //foreach (OdorScent scent in map.Scents)
                //{
                //    switch (scent.Odor)
                //    {
                //        case Odor.PERFUME_LIVING_SUPRESSOR:
                //            // remember to suppress living scent.
                //            scentGenerated.Add(new OdorScent(Odor.LIVING, -scent.Strength, scent.Position));
                //            break;

                //        case Odor.PERFUME_LIVING_GENERATOR:
                //            // remember to generate living scent here.
                //            scentGenerated.Add(new OdorScent(Odor.LIVING, scent.Strength, scent.Position));
                //            break;
                //    }
                //}
                //foreach (OdorScent genScent in scentGenerated)
                //    map.ModifyScentAt(genScent.Odor, genScent.Strength, genScent.Position);
                #endregion

                //      1.2 Odors decay.
                #region
                List<OdorScent> scentGarbage = null;

                // decay map scents
                foreach (OdorScent scent in map.Scents)
                {
                    // alpha10
                    int decay = m_Rules.OdorsDecay(map, scent.Position, m_Session.World.Weather);

                    // decay.
                    map.ModifyScentAt(scent.Odor, -decay, scent.Position);

                    // garbage?
                    if (scent.Strength < OdorScent.MIN_STRENGTH)
                    {
                        if (scentGarbage == null) scentGarbage = new List<OdorScent>(1);
                        scentGarbage.Add(scent);
                    }
                }
                if (scentGarbage != null)
                {
                    foreach (OdorScent scent in scentGarbage)
                        map.RemoveScent(scent);
                    scentGarbage = null;
                }
                #endregion

                //      1.3 Actors scents.
                #region
                foreach (Actor actor in map.Actors)
                {
                    // alpha10
                    DropActorScents(actor);
                    DecayActorScents(actor);
                }
                #endregion

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
                        --actor.FoodPoints;
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
                        if (!actor.Model.Abilities.IsUndead && Rules.HasImmediateZombification(m_Session.GameMode) && m_Rules.RollChance(s_Options.StarvedZombificationChance))
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

            // -- Check timers.
            #region
            if (map.CountTimers > 0)
            {
                List<TimedTask> timersGarbage = null;
                foreach (TimedTask t in map.Timers)
                {
                    t.Tick(map);
                    if (t.IsCompleted)
                    {
                        if (timersGarbage == null) timersGarbage = new List<TimedTask>(map.CountTimers);
                        timersGarbage.Add(t);
                    }
                }
                if (timersGarbage != null)
                {
                    foreach (TimedTask t in timersGarbage)
                        map.RemoveTimer(t);
                }
            }
            #endregion

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
