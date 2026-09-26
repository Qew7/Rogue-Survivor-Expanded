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
        void AdvanceCorpsesAndInfection(Map map)
        {
                // 0. Raise the deads; Check infections (non STD)
                #region
                bool hasCorpses = m_Session.GamePreset.Corpses;
                bool hasInfection = m_Session.GamePreset.Infection;
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
                            if (a.Infection > 0 && !a.Model.Abilities.IsUndead &&
                                m_Rules.ActorInfectionPercent(a) >= m_Session.GamePreset.InfectionWeakThreshold)
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
                                    if (infectionP >= m_Session.GamePreset.InfectionDeathThreshold)
                                    {
                                        killHim = true;
                                    }
                                    else if (infectionP >= m_Session.GamePreset.InfectionBleedThreshold)
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
                                    else if (infectionP >= m_Session.GamePreset.InfectionVomitThreshold)
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
                                    else if (infectionP >= m_Session.GamePreset.InfectionTiredThreshold)
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
                                    else if (infectionP >= m_Session.GamePreset.InfectionWeakThreshold)
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

        }

        void AdvanceMapScents(Map map)
        {
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

        }

        void AdvanceMapTimers(Map map)
        {
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

        }

    }
}
