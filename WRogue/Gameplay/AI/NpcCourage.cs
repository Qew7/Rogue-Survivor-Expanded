using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.AI;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Gameplay.AI
{
    // A decision about this encounter, recalculated from observable conditions each turn.
    static class NpcCourage
    {
        public struct Assessment
        {
            public int Resolve, Threat;
            public bool Mortal;
        }

        public static bool CanFear(Actor actor)
        {
            return actor != null && !actor.IsPlayer && !actor.Model.Abilities.IsUndead &&
                actor.Model.Abilities.IsIntelligent && actor.Faction.ID != (int)GameFactions.IDs.TheFerals;
        }

        public static bool ImmediateMortalThreat(RogueGame game, Actor actor, List<Percept> enemies)
        {
            return Assess(game, actor, enemies).Mortal;
        }

        public static int Resolve(RogueGame game, Actor actor, List<Percept> enemies, out int threat)
        {
            Assessment assessment = Assess(game, actor, enemies);
            threat = assessment.Threat;
            return assessment.Resolve;
        }

        public static Assessment Assess(RogueGame game, Actor actor, List<Percept> enemies)
        {
            Assessment assessment = new Assessment();
            if (!CanFear(actor)) { assessment.Resolve = 100; return assessment; }
            int score = game.NpcContent.FactionPolicy(actor.Faction.ID).Courage +
                PersonalitySystem.Bias(actor, DecisionKind.Courage, registry: game.NpcContent.Personalities);
            int maxHp = Math.Max(1, game.Rules.ActorMaxHPs(actor));
            int maxSta = Math.Max(1, game.Rules.ActorMaxSTA(actor));
            score += 5 - 35 * Math.Max(0, maxHp - actor.HitPoints) / maxHp;
            score += 5 - 25 * Math.Max(0, maxSta - actor.StaminaPoints) / maxSta;

            ItemRangedWeapon ranged = actor.GetEquippedRangedWeapon();
            bool hasAmmo = ranged != null && HasAmmo(actor, ranged);
            if (hasAmmo) score += ranged.Ammo > 0 ? 16 : 8;
            else if (actor.GetEquippedWeapon() != null) score += 5;
            int firearms = Skill(actor, Skills.IDs.FIREARMS), bows = Skill(actor, Skills.IDs.BOWS);
            bool firearmReady = hasAmmo && ((ItemRangedWeaponModel)ranged.Model).IsFireArm;
            bool bowReady = hasAmmo && ((ItemRangedWeaponModel)ranged.Model).IsBow;
            score += firearmReady ? 4 * firearms : -3 * firearms;
            score += bowReady ? 4 * bows : -3 * bows;
            score += 3 * Skill(actor, Skills.IDs.MARTIAL_ARTS) + 2 * Skill(actor, Skills.IDs.STRONG);

            if (actor.Model.Abilities.HasSanity)
            {
                int maxSanity = Math.Max(1, game.Rules.ActorMaxSanity(actor));
                int loss = Math.Min(25, 25 * Math.Max(0, maxSanity - actor.Sanity) / maxSanity);
                score += PersonalitySystem.HasTrait(actor, "maniac") || PersonalitySystem.HasTrait(actor, "berserker") ? loss : -loss;
            }
            if (actor.Personality != null)
                foreach (MemoryInstance memory in actor.Personality.Memories)
                    if (memory.Id == "frightened_escape") { score -= 12; break; }

            Actor leader = actor.HasLeader ? actor.Leader : actor;
            int groupSupport = leader != actor ? Companion(game, actor, leader) : 0;
            if (leader.Followers != null)
                foreach (Actor follower in leader.Followers)
                    if (follower != actor) groupSupport += Companion(game, actor, follower);
            score += Math.Max(-40, Math.Min(24, groupSupport));
            score = Math.Max(-100, Math.Min(100, score));

            if (enemies != null)
                foreach (Percept percept in enemies)
                {
                    Actor enemy = percept.Percepted as Actor;
                    if (enemy == null || enemy.IsDead || enemy.Location.Map != actor.Location.Map ||
                        percept.Turn != actor.Location.Map.LocalTime.TurnCounter) continue;
                    int distance = game.Rules.GridDistance(actor.Location.Position, enemy.Location.Position);
                    ItemRangedWeapon gun = enemy.GetEquippedRangedWeapon();
                    bool shoots = gun != null && gun.Ammo > 0 && distance <= enemy.CurrentRangedAttack.Range;
                    if (shoots && enemy.CurrentRangedAttack.DamageValue >= actor.HitPoints ||
                        distance <= 1 && enemy.CurrentMeleeAttack.DamageValue >= actor.HitPoints)
                        assessment.Mortal = true;
                    if (distance > 3 && !shoots) continue;
                    int damage = Math.Max(1, shoots ? enemy.CurrentRangedAttack.DamageValue : enemy.CurrentMeleeAttack.DamageValue);
                    int danger = Math.Min(70, 20 + 30 * damage / maxHp +
                        (damage >= actor.HitPoints ? 45 : damage * 2 >= actor.HitPoints ? 20 : 0));
                    assessment.Threat += shoots ? danger : distance <= 1 ? danger : distance == 2 ? danger * 2 / 3 : danger / 3;
                }
            assessment.Threat = Math.Min(100, assessment.Threat);
            assessment.Resolve = score - assessment.Threat;
            return assessment;
        }

        static int Skill(Actor actor, Skills.IDs id)
        { return actor.Sheet.SkillTable.GetSkillLevel((int)id); }

        static bool HasAmmo(Actor actor, ItemRangedWeapon weapon)
        {
            if (weapon.Ammo > 0) return true;
            foreach (Item item in actor.Inventory.Items)
            {
                ItemAmmo ammo = item as ItemAmmo;
                if (ammo != null && ammo.AmmoType == weapon.AmmoType) return true;
            }
            return false;
        }

        static int Companion(RogueGame game, Actor actor, Actor companion)
        {
            if (companion.IsDead || companion.IsSleeping || companion.Location.Map != actor.Location.Map ||
                !NpcIntentSystem.CanSee(game, actor, companion)) return 0;
            int maxHp = Math.Max(1, game.Rules.ActorMaxHPs(companion));
            int support = companion.HitPoints * 2 < maxHp ? -8 : 6;
            if (companion.Activity == Activity.FLEEING) support -= 16;
            else if (companion.Personality != null)
                foreach (MemoryInstance memory in companion.Personality.Memories)
                    if (memory.Id == "frightened_escape") { support -= 10; break; }
            return support;
        }
    }

    // Records panic only after a retreat action really changes the NPC's position.
    sealed class ActionFearRetreat : ActorAction
    {
        readonly ActorAction retreat;
        readonly Actor threat;
        public ActionFearRetreat(Actor actor, RogueGame game, ActorAction retreat, Actor threat)
            : base(actor, game) { this.retreat = retreat; this.threat = threat; }
        public override bool IsLegal() { return retreat.IsLegal(); }
        public override void Perform()
        {
            Location before = m_Actor.Location;
            retreat.Perform();
            if (before.Map == m_Actor.Location.Map && before.Position == m_Actor.Location.Position) return;
            if (!NpcCourage.CanFear(m_Actor) || m_Actor.Personality == null) return;
            foreach (MemoryInstance memory in m_Actor.Personality.Memories)
                if (memory.Id == "frightened_escape") return;
            Location place = m_Actor.Location;
            PersonalitySystem.Report(m_Game, new SignificantEvent("fled_in_fear", m_Actor, threat,
                place.Map, place.Position, place.Map.LocalTime.TurnCounter));
        }
    }
}
