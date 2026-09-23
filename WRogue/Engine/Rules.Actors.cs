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
        #region Actors relations
        // alpha10 refactored and rewrote
        /// <summary>
        /// Check if both actors are enemies.
        /// - enemy factions
        /// - enemy gangs
        /// - personal enemies
        /// - group enemies (if checkGroups)
        /// Symetrical, don't need to call for actorB,actorA.
        /// </summary>
        /// <param name="actor"></param>
        /// <param name="actorB"></param>
        /// <param name="checkGroups"></param>
        /// <returns></returns>
        public bool AreEnemies(Actor actorA, Actor actorB, bool checkGroups=true)
        {
            if (actorA == null || actorB == null)
                return false;
            if (actorA == actorB)  // alpha10 silly fix
                return false;

            // Enemy factions? (symetrical)
            if (actorA.Faction.IsEnemyOf(actorB.Faction))
                return true;

            // Enemy gangs? (symetrical)
            if (actorA.Faction == actorB.Faction && actorA.IsInAGang && actorB.IsInAGang && actorA.GangID != actorB.GangID)
                    return true;

            // alpha10
            // Personal enemies? (symetrical)
            if (ArePersonalEnemies(actorA, actorB))
                return true;

            // alpha10
            // Enemy of groups (symetrical)
            if (checkGroups && AreGroupEnemies(actorA, actorB))
                return true;

            // Not enemies.
            return false;
        }

        // alpha10
        /// <summary>
        /// Check if they are in an agressor-selfdefence reliation.
        /// Symetrical, don't need to call for actorB,actorA.
        /// </summary>
        /// <param name="actorA"></param>
        /// <param name="actorB"></param>
        /// <returns></returns>
        public bool ArePersonalEnemies(Actor actorA, Actor actorB)
        {
            if (actorA == null || actorB == null)
                return false;
            if (actorA == actorB)
                return false;

            if (actorA.IsAggressorOf(actorB))
                return true;

            if (actorA.IsSelfDefenceFrom(actorB))
                return true;

            // doesnt need to check for target as the relation is symetrical (aggressor of <-> self defence from)
            return false;
        }

        // alpha10
        /// <summary>
        /// Check if they are enmemies through group relations :
        /// - my leader enemies are my enemies
        /// - my mates enemies are my enemies.
        /// - my follower enemies are my enemies.
        /// Symetrical, don't need to call for actorB,actorA.
        /// </summary>
        /// <param name="actorA"></param>
        /// <param name="actorB"></param>
        /// <returns></returns>
        public bool AreGroupEnemies(Actor actorA, Actor actorB)
        {
            if (actorA == null || actorB == null)
                return false;
            if (actorA == actorB)
                return false;

            // my leader enemies are my enemies.
            // my mates enemies are my enemies.
            bool IsEnemyOfMyLeaderOrMates(Actor groupActor, Actor target)
            {
                if (AreEnemies(groupActor.Leader, target, false))
                    return true;
                foreach (Actor mate in groupActor.Leader.Followers)
                    if (mate != groupActor && AreEnemies(mate, target, false))
                        return true;
                return false;
            }

            // my followers enemies are my enemies
            bool IsEnemyOfMyFollowers(Actor groupActor, Actor target)
            {
                foreach (Actor follower in groupActor.Followers)
                    if (AreEnemies(follower, target, false))
                        return true;
                return false;
            }

            // check A group
            if (actorA.HasLeader)
                if (IsEnemyOfMyLeaderOrMates(actorA, actorB))
                    return true;
            if (actorA.CountFollowers > 0)
                if (IsEnemyOfMyFollowers(actorA, actorB))
                    return true;

            // check B group
            if (actorB.HasLeader)
                if (IsEnemyOfMyLeaderOrMates(actorB, actorA))
                    return true;
            if (actorB.CountFollowers > 0)
                if (IsEnemyOfMyFollowers(actorB, actorA))
                    return true;

            // nope
            return false;
        }

        public bool IsMurder(Actor killer, Actor victim)
        {
            if (killer == null || victim == null)
                return false;

            // killing an undead is never a murder (doh!)
            if (victim.Model.Abilities.IsUndead)
                return false;

            // a law enforcer killing a murderer is not a murder.
            if (killer.Model.Abilities.IsLawEnforcer && victim.MurdersCounter > 0)
                return false;

            // killing between enemy factions is allowed.
            if (killer.Faction.IsEnemyOf(victim.Faction))
                return false;

            // killing in self defence is not a murder.
            if (killer.IsSelfDefenceFrom(victim))
                return false;

            // all other cases are murders!
            return true;
        }
        #endregion
        #region Stats affected by Skills & Status effects
        public int ActorSpeed(Actor actor)
        {
            float speed = actor.Doll.Body.Speed;

            // stamina.
            if (IsActorTired(actor))
                speed *= 2f / 3f;

            // sleep.
            if (IsActorExhausted(actor))
                speed /= 2f;
            else if (IsActorSleepy(actor))
                speed *= 2f / 3f;

            // wearing armor.
            ItemBodyArmor armor = actor.GetEquippedItem(DollPart.TORSO) as ItemBodyArmor;
            if (armor != null)
                speed -= armor.Weight;

            // dragging corpses.
            if (actor.DraggedCorpse != null)
                speed /= 2f;

            // done, speed must be >= 0.
            return Math.Max((int)speed, 0);
        }

        public int ActorMaxHPs(Actor actor)
        {
            int skillBonus = (SKILL_TOUGH_HP_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.TOUGH))
                + (SKILL_ZTOUGH_HP_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.Z_TOUGH));

            return actor.Sheet.BaseHitPoints + skillBonus;
        }

        public int ActorMaxSTA(Actor actor)
        {
            int skillBonus = (SKILL_HIGH_STAMINA_STA_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.HIGH_STAMINA));

            return actor.Sheet.BaseStaminaPoints + skillBonus;
        }

        public int ActorItemNutritionValue(Actor actor, int baseValue)
        {
            int skillBonus = (int)(baseValue * SKILL_LIGHT_EATER_FOOD_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.LIGHT_EATER));

            return baseValue + skillBonus;
        }

        public int ActorMaxFood(Actor actor)
        {
            int skillBonus = (int)(actor.Sheet.BaseFoodPoints * SKILL_LIGHT_EATER_MAXFOOD_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.LIGHT_EATER));

            return actor.Sheet.BaseFoodPoints + skillBonus;
        }

        public int ActorMaxRot(Actor actor)
        {
            int skillBonus = (int)(actor.Sheet.BaseFoodPoints * SKILL_ZLIGHT_EATER_MAXFOOD_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.Z_LIGHT_EATER));

            return actor.Sheet.BaseFoodPoints + skillBonus;
        }

        public int ActorMaxSleep(Actor actor)
        {
            int skillBonus = (int)(actor.Sheet.BaseSleepPoints * SKILL_AWAKE_SLEEP_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.AWAKE));

            return (actor.Sheet.BaseSleepPoints + skillBonus);
        }

        public int ActorSleepRegen(Actor actor, bool isOnCouch)
        {
            int baseRegen = isOnCouch ? SLEEP_COUCH_SLEEPING_REGEN : SLEEP_NOCOUCH_SLEEPING_REGEN;
            int skillBonus = (int)(baseRegen * SKILL_AWAKE_SLEEP_REGEN_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.AWAKE));

            return baseRegen + skillBonus;
        }

        public int ActorMaxSanity(Actor actor)
        {
            return actor.Sheet.BaseSanity;
        }

        public int ActorDisturbedLevel(Actor actor)
        {
            float factor = 1.0f - SKILL_STRONG_PSYCHE_LEVEL_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.STRONG_PSYCHE);
            return (int)(SANITY_UNSTABLE_LEVEL * factor);
        }

        public int ActorMaxInv(Actor actor)
        {
            int skillBonus = SKILL_HAULER_INV_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.HAULER);

            return actor.Sheet.BaseInventoryCapacity + skillBonus;
        }

        public int ActorDamageBonusVsUndeads(Actor actor)
        {
            return SKILL_NECROLOGY_UNDEAD_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.NECROLOGY);
        }

        // alpha10 added mapobject param
        public Attack ActorMeleeAttack(Actor actor, Attack baseAttack, Actor target, MapObject objToBreak=null)
        {
            float hit = baseAttack.HitValue;
            float dmg = baseAttack.DamageValue;
            int disarmBonus = 0;  // alpha10

            // skills bonuses.
            int hitBonus = SKILL_AGILE_ATK_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.AGILE) +
                            SKILL_ZAGILE_ATK_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.Z_AGILE);
            int dmgBonus = SKILL_STRONG_DMG_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.STRONG) +
                            SKILL_ZSTRONG_DMG_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.Z_STRONG);
            // martial arts apply only if no weapon equipped.
            if (actor.GetEquippedWeapon() == null)
            {
                int ma = actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.MARTIAL_ARTS);
                if (ma > 0)
                {
                    hitBonus += SKILL_MARTIAL_ARTS_ATK_BONUS * ma;
                    dmgBonus += SKILL_MARTIAL_ARTS_DMG_BONUS * ma;
                    disarmBonus += SKILL_MARTIAL_ARTS_DISARM_BONUS * ma;
                }
            }

            // necrology vs undeads.
            if (target != null && target.Model.Abilities.IsUndead)
                dmgBonus += ActorDamageBonusVsUndeads(actor);

            // alpha10
            // add tool damage bonus vs map objects
            if (objToBreak != null)
            {
                ItemMeleeWeapon eqMw = actor.GetEquippedMeleeWeapon();
                if (eqMw != null)
                {
                    dmgBonus += eqMw.ToolBashDamageBonus;
                }
            }

            hit += hitBonus;
            dmg += dmgBonus;

            // alpha10
            // disarm chance.
            float disarmChance = MELEE_DISARM_BASE_CHANCE;
            disarmChance += disarmBonus;
            // defender strong resist disarm
            if (target != null)
                disarmChance -= SKILL_STRONG_RESIST_DISARM_BONUS * target.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.STRONG);

            // sleepiness penalties.
            if (IsActorExhausted(actor))
            {
                hit /= 2f;
                disarmChance /= 2f;
            }
            else if (IsActorSleepy(actor))
            {
                hit *= 3f / 4f;
                disarmChance *= 3f / 4f;
            }

            // done.
            return Attack.MeleeAttack(baseAttack.Verb, (int)hit, (int)dmg, baseAttack.StaminaPenalty, (int)disarmChance);
        }

        public Attack ActorRangedAttack(Actor actor, Attack baseAttack, int distance, Actor target)
        {
            int hitMod = 0;
            int dmgBonus = 0;

            // skill bonuses.
            switch (baseAttack.Kind)
            {
                case AttackKind.BOW:
                    hitMod = SKILL_BOWS_ATK_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.BOWS);
                    dmgBonus = SKILL_BOWS_DMG_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.BOWS);
                    break;
                case AttackKind.FIREARM:
                    hitMod = SKILL_FIREARMS_ATK_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.FIREARMS);
                    dmgBonus = SKILL_FIREARMS_DMG_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.FIREARMS);
                    break;
            }

            if (target != null && target.Model.Abilities.IsUndead)
                dmgBonus += ActorDamageBonusVsUndeads(actor);

            // distance vs range penalties/bonus.
            int efficientRange = baseAttack.EfficientRange;
            // alpha10 distance as % modifier instead of flat bonus
            float distanceMod = 1;
            if (distance != efficientRange)
            {
                float distanceScale = (efficientRange - distance) / (float)baseAttack.Range;
                // bigger effect (penalty) beyond efficient range
                if (distance > efficientRange)
                    distanceScale *= 2;

                distanceMod = 1 + distanceScale;
            }
            float hit = (baseAttack.HitValue + hitMod) * distanceMod;
            float rapidHit1 = (baseAttack.Hit2Value + hitMod) * distanceMod;
            float rapidHit2 = (baseAttack.Hit3Value + hitMod) * distanceMod;

            float dmg = baseAttack.DamageValue + dmgBonus;

            // sleep penalty.
            if (IsActorExhausted(actor))
            {
                hit *= FIRING_WHEN_SLP_EXHAUSTED;
                rapidHit1 *= FIRING_WHEN_SLP_EXHAUSTED;
                rapidHit2 *= FIRING_WHEN_SLP_EXHAUSTED;
            }
            else if (IsActorSleepy(actor))
            {
                hit *= FIRING_WHEN_SLP_SLEEPY;
                rapidHit1 *= FIRING_WHEN_SLP_SLEEPY;
                rapidHit2 *= FIRING_WHEN_SLP_SLEEPY;
            }

            // stamina penalty.
            if (IsActorTired(actor))
            {
                hit *= FIRING_WHEN_STA_TIRED;
                rapidHit1 *= FIRING_WHEN_STA_TIRED;
                rapidHit2 *= FIRING_WHEN_STA_TIRED;
            }
            else if (actor.StaminaPoints < ActorMaxSTA(actor))
            {
                hit *= FIRING_WHEN_STA_NOT_FULL;
                rapidHit1 *= FIRING_WHEN_STA_NOT_FULL;
                rapidHit2 *= FIRING_WHEN_STA_NOT_FULL;
            }

            // return attack.
            return Attack.RangedAttack(baseAttack.Kind, baseAttack.Verb, (int)hit, (int)rapidHit1, (int)rapidHit2, (int)dmg, baseAttack.Range);
        }

        // alpha10
        /// <summary>
        /// Estimate chances to hit with a ranged attack. <br></br>
        /// Simulate a large number of rolls attack vs defence and returns % of hits.
        /// </summary>
        /// <param name="actor"></param>
        /// <param name="target"></param>
        /// <param name="shotCounter">0 for normal shot, 1 for 1st rapid fire shot, 2 for 2nd rapid fire shot</param>
        /// <returns>[0..100]</returns>
        public int ComputeChancesRangedHit(Actor actor, Actor target, int shotCounter)
        {
            Attack attack = ActorRangedAttack(actor, actor.CurrentRangedAttack, GridDistance(actor.Location.Position, target.Location.Position), target);
            Defence defence = ActorDefence(target, target.CurrentDefence);

            int hitValue = (shotCounter == 0 ? attack.HitValue : shotCounter == 1 ? attack.Hit2Value : attack.Hit3Value);
            int defValue = defence.Value;

            const int ROLLS = 1000;
            int hits = 0;
            for (int i = 0; i < ROLLS; i++)
            {
                int atkRoll = RollSkill(hitValue);
                int defRoll = RollSkill(defValue);
                if (atkRoll > defRoll)
                    hits++;
            }

            int percent = (100 * hits) / ROLLS;
            return percent;
        }

        public int ActorMaxThrowRange(Actor actor, int baseRange)
        {
            int bonus = SKILL_STRONG_THROW_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.STRONG);

            return baseRange + bonus;
        }

        public Defence ActorDefence(Actor actor, Defence baseDefence)
        {
            // Sleeping actors are defenceless.
            if (actor.IsSleeping)
                return new Defence(0, 0, 0);

            // Base value + skill.
            int defBonus = SKILL_AGILE_DEF_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.AGILE) +
                SKILL_ZAGILE_DEF_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.Z_AGILE);
            float def = baseDefence.Value + defBonus;

            // Sleepy effect.
            if (IsActorExhausted(actor))
                def /= 2f;
            else if (IsActorSleepy(actor))
                def *= 3f / 4f;

            // done.
            return new Defence((int)def, baseDefence.Protection_Hit, baseDefence.Protection_Shot);
        }

        public int ActorMedicineEffect(Actor actor, int baseEffect)
        {
            int effectBonus = (int)(Math.Ceiling(SKILL_MEDIC_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.MEDIC) * baseEffect));
            return baseEffect + effectBonus;
        }

        public int ActorHealChanceBonus(Actor actor)
        {
            int chanceBonus = SKILL_HARDY_HEAL_CHANCE_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.HARDY);
            return chanceBonus;
        }

        public int ActorBarricadingPoints(Actor actor, int baseBarricadingPoints)
        {
            int barBonus = 0;

            // carpentry skill
            barBonus += (int)(baseBarricadingPoints * SKILL_CARPENTRY_BARRICADING_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.CARPENTRY));

            // alpha10
            // tool build bonus
            ItemMeleeWeapon eqMw = actor.GetEquippedMeleeWeapon();
            if (eqMw != null && eqMw.ToolBuildBonus != 0)
            {
                barBonus += (int)(baseBarricadingPoints * eqMw.ToolBuildBonus);
            }

            return baseBarricadingPoints + barBonus;
        }

        public int ActorMaxFollowers(Actor actor)
        {
            return SKILL_LEADERSHIP_FOLLOWER_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.LEADERSHIP);
        }

        public int ActorFOV(Actor actor, WorldTime time, Weather weather)
        {
            // Sleeping actors have no FOV.
            if (actor.IsSleeping)
                return 0;

            // base value.
            int FOV = actor.Sheet.BaseViewRange;

            // lighting/weather
            Lighting light = actor.Location.Map.Lighting;
            switch (light)
            {
                case Lighting.DARKNESS:
                    // FoV in darkness depends on actor.
                    FOV = DarknessFov(actor);
                    break;
                case Lighting.LIT:
                    // nothing to do, unmodified base FOV.
                    break;
                case Lighting.OUTSIDE:
                    // night & weather penalty
                    FOV -= NightFovPenalty(actor, time);
                    FOV -= WeatherFovPenalty(actor, weather);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("unhandled lighting");
            }

            // sleep penalty.
            if (IsActorExhausted(actor))
                FOV -= 2;
            else if (IsActorSleepy(actor))
                FOV -= 1;

            // light equipped or standing next to someone with light.
            // works only in darkness or during the night.
            if (light == Lighting.DARKNESS || (light == Lighting.OUTSIDE && time.IsNight))
            {
                int lightBonus = 0;

                lightBonus = GetLightBonusEquipped(actor);
                if (lightBonus == 0)
                {
                    Map map = actor.Location.Map;
                    if (map.HasAnyAdjacentInMap(actor.Location.Position,
                        (pt) =>
                        {
                            Actor other = map.GetActorAt(pt);
                            if (other == null)
                                return false;
                            return HasLightOnEquipped(other);
                        }))
                        lightBonus = 1;
                }
                FOV += lightBonus;
            }

            // standing on some map objects.
            MapObject mobj = actor.Location.Map.GetMapObjectAt(actor.Location.Position);
            if (mobj != null && mobj.StandOnFovBonus)
                ++FOV;

            // done.
            FOV = Math.Max(MINIMAL_FOV, FOV);
            return FOV;
        }

        public float ActorSmell(Actor actor)
        {
            return (1.0f + SKILL_ZTRACKER_SMELL_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.Z_TRACKER)) * actor.Model.StartingSheet.BaseSmellRating;
        }

        public int ActorSmellThreshold(Actor actor)
        {
            // sleeping actors can't smell.
            if (actor.IsSleeping)
                return -1;

            // actor model base value.
            float smellRating = ActorSmell(actor);
            int minSmell = 1 + OdorScent.MAX_STRENGTH - (int)(smellRating * OdorScent.MAX_STRENGTH);
            return minSmell;
        }

        bool HasLightOnEquipped(Actor actor)
        {
            ItemLight light = actor.GetEquippedItem(DollPart.LEFT_HAND) as ItemLight;
            return (light != null && light.Batteries > 0);
        }

        int GetLightBonusEquipped(Actor actor)
        {
            ItemLight light = actor.GetEquippedItem(DollPart.LEFT_HAND) as ItemLight;
            return light == null || light.Batteries <= 0 ? 0 : light.FovBonus;
        }

        public int ActorLoudNoiseWakeupChance(Actor actor, int noiseDistance)
        {
            int baseChance = LOUD_NOISE_BASE_WAKEUP_CHANCE;
            int skillBonus = SKILL_LIGHT_SLEEPER_WAKEUP_CHANCE_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.LIGHT_SLEEPER);
            int distBonus = Math.Max(0, (LOUD_NOISE_RADIUS - noiseDistance) * LOUD_NOISE_DISTANCE_BONUS);

            return baseChance + skillBonus + distBonus;
        }

        public int ActorBarricadingMaterialNeedForFortification(Actor builder, bool isLarge)
        {
            int baseCost = isLarge ? 4 : 2;
            int skillBonus = (builder.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.CARPENTRY) >= 3 ? SKILL_CARPENTRY_LEVEL3_BUILD_BONUS : 0);
            return Math.Max(1, baseCost - skillBonus);
        }

        public int ActorTrustIncrease(Actor actor)
        {
            int skillBonus = SKILL_CHARISMATIC_TRUST_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.CHARISMATIC);

            return TRUST_BASE_INCREASE + skillBonus;
        }

        public int ActorCharismaticTradeChance(Actor actor)
        {
            return SKILL_CHARISMATIC_TRADE_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.CHARISMATIC);
        }

        public int ActorUnsuspicousChance(Actor observer, Actor actor)
        {
            // base = unsuspicious skill.
            int baseChance = SKILL_UNSUSPICIOUS_BONUS * actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.UNSUSPICIOUS);

            // bonus.
            int bonus = 0;

            // wearing some outfit.
            ItemBodyArmor armor = actor.GetEquippedItem(DollPart.TORSO) as ItemBodyArmor;
            if (armor != null)
            {
                if (observer.Faction.ID == (int)GameFactions.IDs.ThePolice)
                {
                    if (armor.IsHostileForCops())
                        bonus -= UNSUSPICIOUS_BAD_OUTFIT_PENALTY;
                    else if (armor.IsFriendlyForCops())
                        bonus += UNSUSPICIOUS_GOOD_OUTFIT_BONUS;
                }
                else if (observer.Faction.ID == (int)GameFactions.IDs.TheBikers)
                {
                    if (armor.IsHostileForBiker((GameGangs.IDs)observer.GangID))
                        bonus -= UNSUSPICIOUS_BAD_OUTFIT_PENALTY;
                    else if (armor.IsFriendlyForBiker((GameGangs.IDs)observer.GangID))
                        bonus += UNSUSPICIOUS_GOOD_OUTFIT_BONUS;
                }
            }

            return baseChance + bonus;
        }

        public int ActorSpotMurdererChance(Actor spotter, Actor murderer)
        {
            int spotterBonus = MURDER_SPOTTING_MURDERCOUNTER_BONUS * murderer.MurdersCounter;
            int distancePenalty = MURDERER_SPOTTING_DISTANCE_PENALTY * GridDistance(spotter.Location.Position, murderer.Location.Position);

            return MURDERER_SPOTTING_BASE_CHANCE + spotterBonus - distancePenalty;
        }

#if false
        // alpha10  previous attempt
        // FIXME -- needing to pass game as arg is uggly. shouldnt be any reference to game in rules but we need to
        // call an ai method...
        public int ScoreNpcItemTradeValue(RogueGame game, Actor npc, Item it, bool fromNpcInventory, Actor otherTrader)
        {
            // base score
            int score = (npc.Controller as BaseAI).ScoreItemValue(game, it, fromNpcInventory);

            // modify by respective charismatic skills
            // a charismatic actor will make the opposing trader over-estimate the actor items.
            int addScore = 0;
            if (fromNpcInventory)
                addScore = (score * npc.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.CHARISMATIC) * SKILL_CHARISMATIC_TRADE_BONUS) / 100;
            else
                addScore = (score * otherTrader.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.CHARISMATIC) * SKILL_CHARISMATIC_TRADE_BONUS) / 100;

            score += addScore;

            return score;
        }
#endif
        #endregion
    }
}
