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
        #region Making enemies & Attacking
        public void DoMakeAggression(Actor aggressor, Actor target)
        {
            // no need if in enemy factions.
            if (aggressor.Faction.IsEnemyOf(target.Faction))
                return;

            bool alreadyEnemies = aggressor.IsAggressorOf(target) || target.IsAggressorOf(aggressor);

            // if target is AI and has not aggressor as enemy, emote.
            if (!target.IsPlayer && !target.IsSleeping && !aggressor.IsAggressorOf(target) && !target.IsAggressorOf(aggressor))
                DoSay(target, aggressor, "BASTARD! TRAITOR!", Sayflags.IS_FREE_ACTION | Sayflags.IS_DANGER);

            // aggressor and selfdefence
            aggressor.MarkAsAgressorOf(target);
            target.MarkAsSelfDefenceFrom(aggressor);


            // then handle special cases.
            // make enemy of all faction actors on maps:
            // 1. Making an enemy of cops.
            // 2. Making an enemy of soldiers.
            #region
            if (!target.IsSleeping)
            {
                Faction tFaction = target.Faction;
                // 1. Making an enemy of cops.
                if (tFaction == GameFactions.ThePolice)
                {
                    // only non-law enforcers or murderers make enemies of cops by attacking cops.
                    if (!aggressor.Model.Abilities.IsLawEnforcer || m_Rules.IsMurder(aggressor, target))
                        OnMakeEnemyOfCop(aggressor, target, alreadyEnemies);
                }
                // 2. Making an enemy of soldiers.
                else if (tFaction == GameFactions.TheArmy)
                {
                    OnMakeEnemyOfSoldier(aggressor, target, alreadyEnemies);
                }
            }
            #endregion
        }

        // FIXME factorize common code with OnMakeEnemyOfSoldier
        void OnMakeEnemyOfCop(Actor aggressor, Actor cop, bool wasAlreadyEnemy)
        {
            // say.
            if (!wasAlreadyEnemy)
                DoSay(cop, aggressor, String.Format("TO DISTRICT PATROLS : {0} MUST DIE!", aggressor.TheName), Sayflags.IS_FREE_ACTION | Sayflags.IS_DANGER);

            // make enemy of all cops in the district.
            MakeEnemyOfTargetFactionInDistrict(aggressor, cop,
                (a) =>
                {
                    if (a.IsPlayer && a != cop && !a.IsSleeping && !m_Rules.AreEnemies(a, aggressor))
                    {
                        int turn = m_Session.WorldTime.TurnCounter;
                        ClearMessages();
                        AddMessage(new Message("You get a message from your police radio.", turn, Color.White));
                        AddMessage(new Message(String.Format("{0} is armed and dangerous. Shoot on sight!", aggressor.TheName), turn, Color.White));
                        AddMessage(new Message(String.Format("Current location : {0}@{1},{2}", aggressor.Location.Map.Name, aggressor.Location.Position.X, aggressor.Location.Position.Y), turn, Color.White));
                        if (!a.IsBotPlayer)
                            AddMessagePressEnter();
                    }
                });
        }

        // FIXME factorize common code with OnMakeEnemyOfCop
        void OnMakeEnemyOfSoldier(Actor aggressor, Actor soldier, bool wasAlreadyEnemy)
        {
            // say.
            if (!wasAlreadyEnemy)
                DoSay(soldier, aggressor, String.Format("TO DISTRICT SQUADS : {0} MUST DIE!", aggressor.TheName), Sayflags.IS_FREE_ACTION | Sayflags.IS_DANGER);

            // make enemy of all cops in the district.
            MakeEnemyOfTargetFactionInDistrict(aggressor, soldier,
                (a) =>
                {
                    if (a.IsPlayer && a != soldier && !a.IsSleeping && !m_Rules.AreEnemies(a, aggressor))
                    {
                        int turn = m_Session.WorldTime.TurnCounter;
                        ClearMessages();
                        AddMessage(new Message("You get a message from your army radio.", turn, Color.White));
                        AddMessage(new Message(String.Format("{0} is armed and dangerous. Shoot on sight!", aggressor.Name), turn, Color.White));
                        AddMessage(new Message(String.Format("Current location : {0}@{1},{2}", aggressor.Location.Map.Name, aggressor.Location.Position.X, aggressor.Location.Position.Y), turn, Color.White));
                        if (!a.IsBotPlayer)
                            AddMessagePressEnter();
                    }
                });
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="aggressor"></param>
        /// <param name="target"></param>
        /// <param name="fn">action to call on faction actor BEFORE making agressor an enemy.</param>
        void MakeEnemyOfTargetFactionInDistrict(Actor aggressor, Actor target, Action<Actor> fn)
        {
            Faction tFaction = target.Faction;
            foreach (Map m in target.Location.Map.District.Maps)
            {
                foreach (Actor a in m.Actors)
                {
                    if (a == aggressor || a == target)
                        continue;
                    if (a.Faction != tFaction)
                        continue;
                    if (a.Leader == aggressor)
                        continue;

                    // perform additional action on actor.
                    if (fn != null)
                        fn(a);

                    // aggression & self defence.
                    aggressor.MarkAsAgressorOf(a);
                    a.MarkAsSelfDefenceFrom(aggressor);
                }
            }
        }

#if false
        obsolete Actor.HasActorAsPersonalEnemy checks for leaders & followers.
        void MakeEnemyOfGroup(Actor a, IEnumerable<Actor> group)
        {
            if (group == null || a == null)
                return;

            foreach (Actor member in group)
            {
                a.MarkAsPersonalEnemy(member);
                member.MarkAsPersonalEnemy(a);
            }
        }

        void MakeEnemiesGroupsSub(IEnumerable<Actor> groupA, IEnumerable<Actor> groupB)
        {
            if (groupA == null || groupB == null)
                return;

            // O(n^2) beauty
            foreach (Actor a in groupA)
                foreach (Actor b in groupB)
                {
                    a.MarkAsPersonalEnemy(b);
                    b.MarkAsPersonalEnemy(a);
                }
        }
#endif

        public void DoMeleeAttack(Actor attacker, Actor defender)
        {
            // set activiy & target.
            attacker.Activity = Activity.FIGHTING;
            attacker.TargetActor = defender;

            // if not already enemies, attacker is aggressor.
            if (!m_Rules.AreEnemies(attacker, defender))
                DoMakeAggression(attacker, defender);

            // get attack & defence.
            Attack attack = m_Rules.ActorMeleeAttack(attacker, attacker.CurrentMeleeAttack, defender);
            Defence defence = m_Rules.ActorDefence(defender, defender.CurrentDefence);

            // spend APs & STA.
            SpendActorActionPoints(attacker, Rules.BASE_ACTION_COST);
            SpendActorStaminaPoints(attacker, Rules.STAMINA_COST_MELEE_ATTACK + attack.StaminaPenalty);

            // resolve attack.
            int hitRoll = m_Rules.RollSkill(attack.HitValue);
            int defRoll = m_Rules.RollSkill(defence.Value);

            // loud noise.
            OnLoudNoise(attacker.Location.Map, attacker.Location.Position, "Nearby fighting");

            // if defender is long waiting player, force stop.
            if (m_IsPlayerLongWait && defender.IsPlayer)
            {
                m_IsPlayerLongWaitForcedStop = true;
            }

            // show/hear.
            bool isDefVisible = IsVisibleToPlayer(defender);
            bool isAttVisible = IsVisibleToPlayer(attacker);
            bool isPlayer = attacker.IsPlayer || defender.IsPlayer;
            bool isBot = attacker.IsBotPlayer || defender.IsBotPlayer;  // alpha10.1 handle bot

            if (!isDefVisible && !isAttVisible && !isPlayer &&
                m_Rules.RollChance(PLAYER_HEAR_FIGHT_CHANCE))
            {
                AddMessageIfAudibleForPlayer(attacker.Location, MakePlayerCentricMessage("You hear fighting", attacker.Location.Position));
            }

            if (isAttVisible)
            {
                AddOverlay(new OverlayRect(Color.Yellow, new Rectangle(MapToScreen(attacker.Location.Position), new Size(TILE_SIZE, TILE_SIZE))));
                AddOverlay(new OverlayRect(Color.Red, new Rectangle(MapToScreen(defender.Location.Position), new Size(TILE_SIZE, TILE_SIZE))));
                AddOverlay(new OverlayImage(MapToScreen(attacker.Location.Position), GameImages.ICON_MELEE_ATTACK));
            }

            // Hit vs Missed
            if (hitRoll > defRoll)
            {
                // alpha10
                // roll for attacker disarming defender
                if (attacker.Model.Abilities.CanDisarm && m_Rules.RollChance(attack.DisarmChance))
                {
                    Item disarmIt = Disarm(defender);
                    if (disarmIt != null)
                    {
                        // show
                        if (isDefVisible)
                        {
                            if (isPlayer)
                                ClearMessages();
                            AddMessage(MakeMessage(attacker, Conjugate(attacker, VERB_DISARM), defender));
                            AddMessage(new Message(string.Format("{0} is sent flying!", disarmIt.TheName), attacker.Location.Map.LocalTime.TurnCounter));
                            if (isPlayer && !isBot)
                            {
                                AddMessagePressEnter();
                            }
                            else
                            {
                                RedrawPlayScreen();
                                AnimDelay(DELAY_SHORT);
                            }
                        }
                    }
                }

                // roll damage - double potential if def is sleeping.
                int dmgRoll = m_Rules.RollDamage(defender.IsSleeping ? attack.DamageValue * 2 : attack.DamageValue) - defence.Protection_Hit;
                // damage?
                if (dmgRoll > 0)
                {
                    // inflict dmg.
                    InflictDamage(defender, dmgRoll);

                    // regen HP/Rot and infection?
                    if (attacker.Model.Abilities.CanZombifyKilled && !defender.Model.Abilities.IsUndead)
                    {
                        RegenActorHitPoints(attacker, Rules.ActorBiteHpRegen(attacker, dmgRoll));
                        attacker.FoodPoints = Math.Min(attacker.FoodPoints + m_Rules.ActorBiteNutritionValue(attacker, dmgRoll), m_Rules.ActorMaxRot(attacker));
                        if (isAttVisible)
                        {
                            AddMessage(MakeMessage(attacker, Conjugate(attacker, VERB_FEAST_ON), defender, " flesh !"));
                        }
                        InfectActor(defender, Rules.InfectionForDamage(attacker, dmgRoll));
                    }

                    // Killed?
                    if (defender.HitPoints <= 0) // def killed!
                    {
                        // show.
                        if (isAttVisible || isDefVisible)
                        {
                            AddMessage(MakeMessage(attacker, Conjugate(attacker, defender.Model.Abilities.IsUndead ? VERB_DESTROY : m_Rules.IsMurder(attacker, defender) ? VERB_MURDER : VERB_KILL), defender, " !"));
                            AddOverlay(new OverlayImage(MapToScreen(defender.Location.Position), GameImages.ICON_KILLED));
                            RedrawPlayScreen();
                            AnimDelay(DELAY_LONG);
                        }

                        // kill.
                        KillActor(attacker, defender, "hit");

                        // cause insanity?
                        if (attacker.Model.Abilities.IsUndead && !defender.Model.Abilities.IsUndead)
                            SeeingCauseInsanity(attacker, attacker.Location, Rules.SANITY_HIT_EATEN_ALIVE, String.Format("{0} eaten alive", defender.Name));

                        // turn victim into zombie; always turn player into zombie NOW if killed by zombifier or if was infected.
                        if (Rules.HasImmediateZombification(m_Session.GameMode) || defender == m_Player)
                        {
                            if (attacker.Model.Abilities.CanZombifyKilled && !defender.Model.Abilities.IsUndead && m_Rules.RollChance(s_Options.ZombificationChance))
                            {
                                if (defender.IsPlayer)
                                {
                                    // remove player corpse.
                                    defender.Location.Map.TryRemoveCorpseOf(defender);
                                }
                                // add new zombie.
                                Zombify(attacker, defender, false);

                                // show
                                if (isDefVisible)
                                {
                                    AddMessage(MakeMessage(attacker, Conjugate(attacker, "turn"), defender, " into a Zombie!"));
                                    RedrawPlayScreen();
                                    AnimDelay(DELAY_LONG);
                                }
                            }
                            else if (defender == m_Player && !defender.Model.Abilities.IsUndead && defender.Infection > 0)
                            {
                                // remove player corpse.
                                defender.Location.Map.TryRemoveCorpseOf(defender);
                                // zombify player!
                                Zombify(null, defender, false);

                                // show
                                AddMessage(MakeMessage(defender, Conjugate(defender, "turn") + " into a Zombie!"));
                                RedrawPlayScreen();
                                AnimDelay(DELAY_LONG);
                            }
                        }
                    }
                    else
                    {
                        // show
                        if (isAttVisible || isDefVisible)
                        {
                            AddMessage(MakeMessage(attacker, Conjugate(attacker, attack.Verb), defender, String.Format(" for {0} damage.", dmgRoll)));
                            AddOverlay(new OverlayImage(MapToScreen(defender.Location.Position), GameImages.ICON_MELEE_DAMAGE));
                            AddOverlay(new OverlayText(MapToScreen(defender.Location.Position).Add(DAMAGE_DX, DAMAGE_DY), Color.White, dmgRoll.ToString(), Color.Black));
                            RedrawPlayScreen();
                            AnimDelay(isPlayer ? DELAY_NORMAL : DELAY_SHORT);
                        }
                    }
                }
                else
                {
                    if (isAttVisible || isDefVisible)
                    {
                        AddMessage(MakeMessage(attacker, Conjugate(attacker, attack.Verb), defender, " for no effect."));
                        AddOverlay(new OverlayImage(MapToScreen(defender.Location.Position), GameImages.ICON_MELEE_MISS));
                        RedrawPlayScreen();
                        AnimDelay(isPlayer ? DELAY_NORMAL : DELAY_SHORT);
                    }
                }

            }   // end of hit
            else // miss
            {
                // show
                if (isAttVisible || isDefVisible)
                {
                    AddMessage(MakeMessage(attacker, Conjugate(attacker, VERB_MISS), defender));
                    AddOverlay(new OverlayImage(MapToScreen(defender.Location.Position), GameImages.ICON_MELEE_MISS));
                    RedrawPlayScreen();
                    AnimDelay(isPlayer ? DELAY_NORMAL : DELAY_SHORT);
                }
            }

            // weapon break?
            ItemMeleeWeapon meleeWeapon = attacker.GetEquippedWeapon() as ItemMeleeWeapon;
            if (meleeWeapon != null && !(meleeWeapon.Model as ItemMeleeWeaponModel).IsUnbreakable)
            {
                if (m_Rules.RollChance(meleeWeapon.IsFragile ? Rules.MELEE_WEAPON_FRAGILE_BREAK_CHANCE : Rules.MELEE_WEAPON_BREAK_CHANCE))
                {
                    // do it.
                    // stackable weapons : only break ONE.
                    OnUnequipItem(attacker, meleeWeapon);
                    if (meleeWeapon.Quantity > 1)
                        --meleeWeapon.Quantity;
                    else
                        attacker.Inventory.RemoveAllQuantity(meleeWeapon);

                    // message.
                    if (isAttVisible)
                    {
                        AddMessage(MakeMessage(attacker, String.Format(": {0} breaks and is now useless!", meleeWeapon.TheName)));
                        RedrawPlayScreen();
                        AnimDelay(isPlayer ? DELAY_NORMAL : DELAY_SHORT);
                    }
                }
            }

            // alpha10 bug fix; clear overlays only if action is visible
            if (isAttVisible || isDefVisible)
                ClearOverlays();
        }

        public void DoRangedAttack(Actor attacker, Actor defender, List<Point> LoF, FireMode mode)
        {
            // if not enemies, aggression.
            if (!m_Rules.AreEnemies(attacker, defender))
                DoMakeAggression(attacker, defender);

            // resolve, depending on mode.
            switch (mode)
            {
                case FireMode.DEFAULT:
                    // spend AP.
                    SpendActorActionPoints(attacker, Rules.BASE_ACTION_COST);

                    // do attack.
                    DoSingleRangedAttack(attacker, defender, LoF, 0);
                    break;

                case FireMode.RAPID:
                    // spend AP.
                    SpendActorActionPoints(attacker, Rules.BASE_ACTION_COST);

                    // 1st attack
                    DoSingleRangedAttack(attacker, defender, LoF, 1);

                    // 2nd attack.
                    // special cases:
                    // - target was killed by 1st attack.
                    // - no more ammo.
                    ItemRangedWeapon w = attacker.GetEquippedWeapon() as ItemRangedWeapon;
                    if (defender.IsDead)
                    {
                        // spend 2nd shot ammo.
                        --w.Ammo;

                        // shoot at nothing.
                        Attack attack = attacker.CurrentRangedAttack;
                        AddMessage(MakeMessage(attacker, String.Format("{0} at nothing.", Conjugate(attacker, attack.Verb))));
                    }
                    else if (w.Ammo <= 0)
                    {
                        // fail silently.
                        return;
                    }
                    else
                    {
                        // perform attack normally.
                        DoSingleRangedAttack(attacker, defender, LoF, 2);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException("unhandled mode");
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="attacker"></param>
        /// <param name="defender"></param>
        /// <param name="LoF"></param>
        /// <param name="shotCounter">0 for normal shot, 1 for 1st rapid fire shot, 2 for 2nd rapid fire shot</param>
        void DoSingleRangedAttack(Actor attacker, Actor defender, List<Point> LoF, int shotCounter)
        {
            // set activiy & target.
            attacker.Activity = Activity.FIGHTING;
            attacker.TargetActor = defender;

            // get attack & defence.
            int targetDistance = m_Rules.GridDistance(attacker.Location.Position, defender.Location.Position);
            Attack attack = m_Rules.ActorRangedAttack(attacker, attacker.CurrentRangedAttack, targetDistance, defender);
            Defence defence = m_Rules.ActorDefence(defender, defender.CurrentDefence);

            // spend STA.
            SpendActorStaminaPoints(attacker, attack.StaminaPenalty);

            // Firearms weapon jam?
            if (attack.Kind == AttackKind.FIREARM)
            {
                int jamChances = m_Rules.IsWeatherRain(m_Session.World.Weather) ? Rules.FIREARM_JAM_CHANCE_RAIN : Rules.FIREARM_JAM_CHANCE_NO_RAIN;
                if (m_Rules.RollChance(jamChances))
                {
                    if (IsVisibleToPlayer(attacker))
                    {
                        AddMessage(MakeMessage(attacker, " : weapon jam!"));
                        return;
                    }
                }
            }

            // spend ammo.
            ItemRangedWeapon weapon = attacker.GetEquippedWeapon() as ItemRangedWeapon;
            if (weapon == null)
                throw new InvalidOperationException("DoSingleRangedAttack but no equipped ranged weapon");
            --weapon.Ammo;

            // check we are firing through something and it intercepts the attack.
            if (DoCheckFireThrough(attacker, LoF))
            {
                return;
            }

            // if defender is long waiting player, force stop.
            if (m_IsPlayerLongWait && defender.IsPlayer)
            {
                m_IsPlayerLongWaitForcedStop = true;
            }

            // resolve attack.
            int hitValue = (shotCounter == 0 ? attack.HitValue : shotCounter == 1 ? attack.Hit2Value : attack.Hit3Value);
            int hitRoll = m_Rules.RollSkill(hitValue);
            int defRoll = m_Rules.RollSkill(defence.Value);

            // show/hear.
            bool isDefVisible = IsVisibleToPlayer(defender.Location);
            bool isAttVisible = IsVisibleToPlayer(attacker.Location);
            bool isPlayer = attacker.IsPlayer || defender.IsPlayer;

            if (!isDefVisible && !isAttVisible && !isPlayer &&
                m_Rules.RollChance(PLAYER_HEAR_FIGHT_CHANCE))
            {
                AddMessageIfAudibleForPlayer(attacker.Location, MakePlayerCentricMessage("You hear firing", attacker.Location.Position));
            }

            if (isAttVisible)
            {
                AddOverlay(new OverlayRect(Color.Yellow, new Rectangle(MapToScreen(attacker.Location.Position), new Size(TILE_SIZE, TILE_SIZE))));
                AddOverlay(new OverlayRect(Color.Red, new Rectangle(MapToScreen(defender.Location.Position), new Size(TILE_SIZE, TILE_SIZE))));
                AddOverlay(new OverlayImage(MapToScreen(attacker.Location.Position), GameImages.ICON_RANGED_ATTACK));
            }

            // Hit vs Missed
            #region
            if (hitRoll > defRoll)
            {
                // roll damage - double potential if def is sleeping.
                int dmgRoll = m_Rules.RollDamage(defender.IsSleeping ? attack.DamageValue * 2 : attack.DamageValue) - defence.Protection_Shot;
                if (dmgRoll > 0)
                {
                    // inflict dmg.
                    InflictDamage(defender, dmgRoll);

                    // Killed?
                    if (defender.HitPoints <= 0) // def killed!
                    {
                        // show.
                        if (isDefVisible)
                        {
                            AddMessage(MakeMessage(attacker, Conjugate(attacker, defender.Model.Abilities.IsUndead ? VERB_DESTROY : m_Rules.IsMurder(attacker, defender) ? VERB_MURDER : VERB_KILL), defender, " !"));
                            AddOverlay(new OverlayImage(MapToScreen(defender.Location.Position), GameImages.ICON_KILLED));
                            RedrawPlayScreen();
                            AnimDelay(DELAY_LONG);
                        }

                        // kill.
                        KillActor(attacker, defender, "shot");
                    }
                    else
                    {
                        // show
                        if (isDefVisible)
                        {
                            AddMessage(MakeMessage(attacker, Conjugate(attacker, attack.Verb), defender, String.Format(" for {0} damage.", dmgRoll)));
                            AddOverlay(new OverlayImage(MapToScreen(defender.Location.Position), GameImages.ICON_RANGED_DAMAGE));
                            AddOverlay(new OverlayText(MapToScreen(defender.Location.Position).Add(DAMAGE_DX, DAMAGE_DY), Color.White, dmgRoll.ToString(), Color.Black));
                            RedrawPlayScreen();
                            AnimDelay(isPlayer ? DELAY_NORMAL : DELAY_SHORT);
                        }
                    }
                }
                else
                {
                    if (isDefVisible)
                    {
                        AddMessage(MakeMessage(attacker, Conjugate(attacker, attack.Verb), defender, " for no effect."));
                        AddOverlay(new OverlayImage(MapToScreen(defender.Location.Position), GameImages.ICON_RANGED_MISS));
                        RedrawPlayScreen();
                        AnimDelay(isPlayer ? DELAY_NORMAL : DELAY_SHORT);
                    }
                }

            }   // end of hit
            else // miss
            {
                // show
                if (isDefVisible)
                {
                    AddMessage(MakeMessage(attacker, Conjugate(attacker, VERB_MISS), defender));
                    AddOverlay(new OverlayImage(MapToScreen(defender.Location.Position), GameImages.ICON_RANGED_MISS));
                    RedrawPlayScreen();
                    AnimDelay(isPlayer ? DELAY_NORMAL : DELAY_SHORT);
                }
            }
            #endregion

            // alpha10 bug fix; clear overlays only if action is visible
            if (isAttVisible || isDefVisible)
                ClearOverlays();
        }

        bool DoCheckFireThrough(Actor attacker, List<Point> LoF)
        {
            // check if we are firing through an object that blocks the LoF and breaks.
            foreach (Point pt in LoF)
            {
                MapObject mapObj = attacker.Location.Map.GetMapObjectAt(pt);
                if (mapObj == null)
                    continue;
                if (mapObj.BreaksWhenFiredThrough &&
                    mapObj.BreakState != MapObject.Break.BROKEN &&      // not if already broken.
                    !mapObj.IsWalkable)                                 // not if not blocking.
                {
                    // message.
                    bool isAttVisible = IsVisibleToPlayer(attacker);
                    bool isObjVisible = IsVisibleToPlayer(mapObj);
                    if (isAttVisible || isObjVisible)
                    {
                        if (isAttVisible)
                        {
                            AddOverlay(new OverlayRect(Color.Yellow, new Rectangle(MapToScreen(attacker.Location.Position), new Size(TILE_SIZE, TILE_SIZE))));
                            AddOverlay(new OverlayImage(MapToScreen(attacker.Location.Position), GameImages.ICON_RANGED_ATTACK));
                        }
                        if (isObjVisible)
                            AddOverlay(new OverlayRect(Color.Red, new Rectangle(MapToScreen(pt), new Size(TILE_SIZE, TILE_SIZE))));

                        AnimDelay(attacker.IsPlayer ? DELAY_NORMAL : DELAY_SHORT);
                    }

                    // destroy that object.
                    DoDestroyObject(mapObj);

                    // fire intercepted.
                    return true;
                }
            }

            // Line Of Fire completly clear, process normally.
            return false;
        }

        public void DoThrowGrenadeUnprimed(Actor actor, Point targetPos)
        {
            // get grenade.
            ItemGrenade grenade = actor.GetEquippedWeapon() as ItemGrenade;
            if (grenade == null)
                throw new InvalidOperationException("throwing grenade but no grenade equiped ");

            // spend AP.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);

            // consume grenade.
            actor.Inventory.Consume(grenade);

            // drop primed grenade at target position.
            Map map = actor.Location.Map;
            ItemGrenadePrimed primedGrenade = new ItemGrenadePrimed(m_GameItems[grenade.PrimedModelID]);
            map.DropItemAt(primedGrenade, targetPos);

            // message about throwing.
            bool isVisible = IsVisibleToPlayer(actor) || IsVisibleToPlayer(actor.Location.Map, targetPos);
            if (isVisible)
            {
                AddOverlay(new OverlayRect(Color.Yellow, new Rectangle(MapToScreen(actor.Location.Position), new Size(TILE_SIZE, TILE_SIZE))));
                AddOverlay(new OverlayRect(Color.Red, new Rectangle(MapToScreen(targetPos), new Size(TILE_SIZE, TILE_SIZE))));
                AddMessage(MakeMessage(actor, String.Format("{0} a {1}!", Conjugate(actor, VERB_THROW), grenade.Model.SingleName)));
                RedrawPlayScreen();
                AnimDelay(DELAY_LONG);
                ClearOverlays();
                RedrawPlayScreen();
            }
        }

        public void DoThrowGrenadePrimed(Actor actor, Point targetPos)
        {
            // get grenade.
            ItemGrenadePrimed primedGrenade = actor.GetEquippedWeapon() as ItemGrenadePrimed;
            if (primedGrenade == null)
                throw new InvalidOperationException("throwing primed grenade but no primed grenade equiped ");

            // spend AP.
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);

            // remove grenade from inventory.
            actor.Inventory.RemoveAllQuantity(primedGrenade);

            // drop primed grenade at target position.
            actor.Location.Map.DropItemAt(primedGrenade, targetPos);

            // message about throwing.
            bool isVisible = IsVisibleToPlayer(actor) || IsVisibleToPlayer(actor.Location.Map, targetPos);
            if (isVisible)
            {
                AddOverlay(new OverlayRect(Color.Yellow, new Rectangle(MapToScreen(actor.Location.Position), new Size(TILE_SIZE, TILE_SIZE))));
                AddOverlay(new OverlayRect(Color.Red, new Rectangle(MapToScreen(targetPos), new Size(TILE_SIZE, TILE_SIZE))));
                AddMessage(MakeMessage(actor, String.Format("{0} back a {1}!", Conjugate(actor, VERB_THROW), primedGrenade.Model.SingleName)));
                RedrawPlayScreen();
                AnimDelay(DELAY_LONG);
                ClearOverlays();
                RedrawPlayScreen();
            }
        }

        void ShowBlastImage(Point screenPos, BlastAttack attack, int damage)
        {
            float alpha = 0.1f + (float)damage / (float)attack.Damage[0];
            if (alpha > 1) alpha = 1;
            AddOverlay(new OverlayTransparentImage(alpha, screenPos, GameImages.ICON_BLAST));
            AddOverlay(new OverlayText(screenPos, Color.Red, damage.ToString(), Color.Black));
        }

        void DoBlast(Location location, BlastAttack blastAttack)
        {
            // noise.
            OnLoudNoise(location.Map, location.Position, "A loud EXPLOSION");

            // blast icon vs audio.
            bool isVisible = IsVisibleToPlayer(location);
            if (isVisible)
            {
                ShowBlastImage(MapToScreen(location.Position), blastAttack, blastAttack.Damage[0]);
                RedrawPlayScreen();
                AnimDelay(DELAY_LONG);
                RedrawPlayScreen();
            }
            else if (m_Rules.RollChance(PLAYER_HEAR_EXPLOSION_CHANCE))
            {
                AddMessageIfAudibleForPlayer(location, MakePlayerCentricMessage("You hear an explosion", location.Position));
            }

            // ground zero explosion.
            ApplyExplosionDamage(location, 0, blastAttack);

            // explosion wave.
            for (int waveDistance = 1; waveDistance <= blastAttack.Radius; waveDistance++)
            {
                // do it.
                bool anyVisible = ApplyExplosionWave(location, waveDistance, blastAttack);

                // show.
                if (anyVisible)
                {
                    isVisible = true; // alpha10
                    RedrawPlayScreen();
                    AnimDelay(DELAY_NORMAL);
                }
            }

            // alpha10 bug fix; clear overlays only if action is visible
            if (isVisible)
                ClearOverlays();
        }

        bool ApplyExplosionWave(Location center, int waveDistance, BlastAttack blast)
        {
            bool anyVisible = false;
            Map map = center.Map;

            Point pt = new Point();
            int xmin = center.Position.X - waveDistance;
            int xmax = center.Position.X + waveDistance;
            int ymin = center.Position.Y - waveDistance;
            int ymax = center.Position.Y + waveDistance;

            // north.
            if (ymin >= 0)
            {
                pt.Y = ymin;
                for (int x = xmin; x <= xmax; x++)
                {
                    pt.X = x;
                    anyVisible |= ApplyExplosionWaveSub(center, pt, waveDistance, blast);
                }
            }

            // south.
            if (ymax < map.Height)
            {
                pt.Y = ymax;
                for (int x = xmin; x <= xmax; x++)
                {
                    pt.X = x;
                    anyVisible |= ApplyExplosionWaveSub(center, pt, waveDistance, blast);
                }
            }

            // west.
            // do dont west corners twice!
            // hence the ymin + 1 and < ymax checks.
            if (xmin >= 0)
            {
                pt.X = xmin;
                for (int y = ymin + 1; y < ymax; y++)
                {
                    pt.Y = y;
                    anyVisible |= ApplyExplosionWaveSub(center, pt, waveDistance, blast);
                }
            }

            // east.
            // don't do east corners twice!
            // hence the ymin + 1 and < ymax checks.
            if (xmax < map.Width)
            {
                pt.X = xmax;
                for (int y = ymin + 1; y < ymax; y++)
                {
                    pt.Y = y;
                    anyVisible |= ApplyExplosionWaveSub(center, pt, waveDistance, blast);
                }
            }

            // return if any explosion was visible.
            return anyVisible;
        }

        bool ApplyExplosionWaveSub(Location blastCenter, Point pt, int waveDistance, BlastAttack blast)
        {
            if (blastCenter.Map.IsInBounds(pt) &&
                LOS.CanTraceFireLine(blastCenter, pt, waveDistance, null))
            {
                // do damage.
                int damage = ApplyExplosionDamage(new Location(blastCenter.Map, pt), waveDistance, blast);

                // show if visible.
                if (IsVisibleToPlayer(blastCenter.Map, pt))
                {
                    ShowBlastImage(MapToScreen(pt), blast, damage);
                    return true;
                }
                else
                    return false;
            }

            return false;
        }

        int ApplyExplosionDamage(Location location, int distanceFromBlast, BlastAttack blast)
        {
            Map map = location.Map;

            int modifiedDamage = m_Rules.BlastDamage(distanceFromBlast, blast);

            // if no damage, don't bother.
            if (modifiedDamage <= 0)
                return 0;

            // damage actor / carried explosives chain reaction.
            #region
            Actor victim = map.GetActorAt(location.Position);
            if (victim != null)
            {
                // carried explosives chain reaction.
                #region
                Inventory carriedItems = victim.Inventory;
                ExplosionChainReaction(carriedItems, location);
                #endregion

                // damage.
                #region
                int dmgToVictim = modifiedDamage - (victim.CurrentDefence.Protection_Hit + victim.CurrentDefence.Protection_Shot) / 2;
                if (dmgToVictim > 0)
                {
                    // inflict.
                    InflictDamage(victim, dmgToVictim);

                    // message.
                    if (IsVisibleToPlayer(victim))
                    {
                        AddMessage(new Message(String.Format("{0} is hit for {1} damage!", victim.Name, dmgToVictim), map.LocalTime.TurnCounter, Color.Crimson));
                    }

                    // die? do not kill someone who is already dead, this could happen because of multiple explosions in a single turn.
                    if (victim.HitPoints <= 0 && !victim.IsDead)
                    {
                        // kill him.
                        KillActor(null, victim, String.Format("explosion {0} damage", dmgToVictim));

                        // message?
                        if (IsVisibleToPlayer(victim))
                        {
                            AddMessage(new Message(String.Format("{0} dies in the explosion!", victim.Name), map.LocalTime.TurnCounter, Color.Crimson));
                        }
                    }
                }
                else
                    AddMessage(new Message(String.Format("{0} is hit for no damage.", victim.Name), map.LocalTime.TurnCounter, Color.White));
                #endregion
            }
            #endregion

            // destroy items / ground explosives chain reaction.
            #region
            Inventory groundInv = map.GetItemsAt(location.Position);
            if (groundInv != null)
            {
                // ground explosives chain reaction.
                #region
                ExplosionChainReaction(groundInv, location);
                #endregion

                // pick items to destroy - don't destroy explosives ready to go, we need them for the chain reaction.
                #region
                // the more damage, the more chance.
                // never destroy uniques or unbreakables.
                int destroyChance = modifiedDamage;
                List<Item> destroyItems = new List<Item>(groundInv.CountItems);
                foreach (Item it in groundInv.Items)
                {
                    if (it.IsUnique || it.Model.IsUnbreakable)
                        continue;
                    if (it is ItemPrimedExplosive)
                    {
                        if ((it as ItemPrimedExplosive).FuseTimeLeft <= 0)
                            continue;
                    }
                    if (!m_Rules.RollChance(destroyChance))
                        continue;
                    destroyItems.Add(it);
                }

                // do it.
                foreach (Item it in destroyItems)
                    map.RemoveItemAt(it, location.Position);
                destroyItems = null;
                #endregion
            }
            #endregion

            // damage objects?
            #region
            if (blast.CanDamageObjects)
            {
                MapObject obj = map.GetMapObjectAt(location.Position);
                if (obj != null)
                {
                    DoorWindow door = obj as DoorWindow;
                    // damage only breakables or barricaded door/windows.
                    if (obj.IsBreakable || (door != null && door.IsBarricaded))
                    {
                        int damageToObject = modifiedDamage;

                        // barricaded doors absorb part of the damage.
                        if (door != null && door.IsBarricaded)
                        {
                            int barricadeDamage = Math.Min(door.BarricadePoints, damageToObject);
                            door.BarricadePoints -= barricadeDamage;
                            damageToObject -= barricadeDamage;
                        }

                        // then directly damage the object.
                        if (damageToObject >= 0)
                        {
                            obj.HitPoints -= damageToObject;
                            if (obj.HitPoints <= 0)
                                DoDestroyObject(obj);
                        }
                    }
                }
            }
            #endregion

            // damage corpses?
            #region
            List<Corpse> corpses = map.GetCorpsesAt(location.Position);
            if (corpses != null)
            {
                foreach (Corpse c in corpses)
                    InflictDamageToCorpse(c, modifiedDamage);
            }
            #endregion

            // destroy walls?
            if (blast.CanDestroyWalls)
            {
                throw new NotImplementedException("blast.destroyWalls");
            }

            // return damage done.
            return modifiedDamage;
        }

        void ExplosionChainReaction(Inventory inv, Location location)
        {
            if (inv == null || inv.IsEmpty)
                return;

            // set each explosive item ready to explode.
            List<ItemExplosive> removedExplosives = null;
            List<ItemPrimedExplosive> addedExplosives = null;
            foreach (Item it in inv.Items)
            {
                // explosive?
                ItemExplosive explosive = it as ItemExplosive;
                if (explosive == null)
                    continue;

                // if a primed explosive, just force fuse to zero.
                ItemPrimedExplosive primedExplosive = explosive as ItemPrimedExplosive;
                if (primedExplosive != null)
                {
                    primedExplosive.FuseTimeLeft = 0;
                    continue;
                }

                // unprimed explosive, prime it, force fuse to zero and drop it at location.
                if (removedExplosives == null)
                    removedExplosives = new List<ItemExplosive>();
                if (addedExplosives == null)
                    addedExplosives = new List<ItemPrimedExplosive>();

                removedExplosives.Add(explosive);
                // add as many primed explosives at explosive quantity (stackables explosives).
                for (int nbPrimedToDrop = 0; nbPrimedToDrop < it.Quantity; nbPrimedToDrop++)
                {
                    primedExplosive = new ItemPrimedExplosive(m_GameItems[explosive.PrimedModelID]);
                    primedExplosive.FuseTimeLeft = 0;
                    addedExplosives.Add(primedExplosive);
                }
            }

            // remove explosives from inventory.
            if (removedExplosives != null)
            {
                foreach (Item removeIt in removedExplosives)
                    inv.RemoveAllQuantity(removeIt);
            }

            // drop primed explosives.
            if (addedExplosives != null)
            {
                foreach (Item addIt in addedExplosives)
                    location.Map.DropItemAt(addIt, location.Position);
            }
        }
        #endregion
    }
}
