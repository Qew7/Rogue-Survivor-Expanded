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
        #region Describing a game element
        string[] DescribeStuffAt(Map map, Point mapPos)
        {
            // Actor?
            Actor actor = map.GetActorAt(mapPos);
            if (actor != null)
            {
                return DescribeActor(actor);
            }

            // Object/Items?
            MapObject obj = map.GetMapObjectAt(mapPos);
            if (obj != null)
            {
                return DescribeMapObject(obj, map, mapPos);
            }

            // Items?
            Inventory inv = map.GetItemsAt(mapPos);
            if (inv != null && !inv.IsEmpty)
            {
                return DescribeInventory(inv);
            }

            // Corpses?
            List<Corpse> corpses = map.GetCorpsesAt(mapPos);
            if (corpses != null)
            {
                return DescribeCorpses(corpses);
            }

            // Nothing to describe!
            return null;
        }

        string[] DescribeActor(Actor actor)
        {
            List<string> lines = new List<string>(10);

            // 1. Name-Faction(Gang), Model, SpawnTime, Order & Leader(trust if player), (Murder counter if player law enforcer);
            //    Enemy & Self-Defence.
            if (actor.Faction != null)
            {
                if (actor.IsInAGang)
                    lines.Add(String.Format("{0}, {1}-{2}.", Capitalize(actor.Name), actor.Faction.MemberName, GameGangs.NAMES[actor.GangID]));
                else
                    lines.Add(String.Format("{0}, {1}.", Capitalize(actor.Name), actor.Faction.MemberName));
            }
            else
                lines.Add(String.Format("{0}.", Capitalize(actor.Name)));
            lines.Add(String.Format("{0}.", Capitalize(actor.Model.Name)));

            lines.Add(String.Format("{0} since {1}.", actor.Model.Abilities.IsUndead ? "Undead" : "Staying alive", new WorldTime(actor.SpawnTime).ToString()));
            AIController ai = actor.Controller as AIController;
            if (ai != null && ai.Order != null)
            {
                lines.Add(String.Format("Order : {0}.", ai.Order.ToString()));
            }
            if (actor.HasLeader)
            {
                if (actor.Leader.IsPlayer)
                {
                    if (actor.TrustInLeader >= Rules.TRUST_BOND_THRESHOLD)
                        lines.Add(String.Format("Trust : BOND."));
                    else if (actor.TrustInLeader >= Rules.TRUST_MAX)
                        lines.Add("Trust : MAX.");
                    else
                        lines.Add(String.Format("Trust : {0}/T:{1}-B:{2}.", actor.TrustInLeader, Rules.TRUST_TRUSTING_THRESHOLD, Rules.TRUST_BOND_THRESHOLD));
                    OrderableAI orderAI = ai as OrderableAI;
                    if (orderAI != null)
                    {
                        if (orderAI.DontFollowLeader)
                            lines.Add("Ordered to not follow you.");
                    }
                    // gauges.
                    lines.Add(String.Format("Foo : {0} {1}h", actor.FoodPoints, FoodToHoursUntilHungry(actor.FoodPoints)));
                    lines.Add(String.Format("Slp : {0} {1}h", actor.SleepPoints, m_Rules.SleepToHoursUntilSleepy(actor.SleepPoints, actor.Location.Map.LocalTime.IsNight)));
                    lines.Add(String.Format("San : {0} {1}h", actor.Sanity, m_Rules.SanityToHoursUntilUnstable(actor)));
                    lines.Add(String.Format("Inf : {0} {1}%", actor.Infection, m_Rules.ActorInfectionPercent(actor)));
                }
                else
                    lines.Add(String.Format("Leader : {0}.", Capitalize(actor.Leader.Name)));
            }

            // show murder counter if trusting follower or player is a law enforcer.
            if (actor.MurdersCounter > 0 && m_Player.Model.Abilities.IsLawEnforcer)
            {
                lines.Add("WANTED FOR MURDER!");
                lines.Add(String.Format("{0} murder{1}!", actor.MurdersCounter, actor.MurdersCounter > 1 ? "s" : ""));
            }
            else if (actor.HasLeader && actor.Leader.IsPlayer && m_Rules.IsActorTrustingLeader(actor))
            {
                if (actor.MurdersCounter > 0)
                    lines.Add(String.Format("* Confess {0} murder{1}! *", actor.MurdersCounter, actor.MurdersCounter > 1 ? "s" : ""));
                else
                    lines.Add("Has committed no murders.");
            }
            if (actor.IsAggressorOf(m_Player))
                lines.Add("Aggressed you.");
            if (m_Player.IsSelfDefenceFrom(actor))
                lines.Add(String.Format("You can kill {0} in self-defence.", HimOrHer(actor)));
            if (m_Player.IsAggressorOf(actor))
                lines.Add(String.Format("You aggressed {0}.", HimOrHer(actor)));
            if (actor.IsSelfDefenceFrom(m_Player))
                lines.Add("Killing you would be self-defence.");
            if (!m_Player.Faction.IsEnemyOf(actor.Faction) && m_Rules.AreGroupEnemies(m_Player, actor)) // alpha10
                lines.Add("You are enemies through groups.");

            lines.Add("");

            // 2. Activity & Hunger/Sleep/Sanity
            string activityLine = DescribeActorActivity(actor);
            if (activityLine != null)
                lines.Add(activityLine);
            else
                lines.Add(" ");  // blank activity line
            if (actor.Model.Abilities.HasToSleep)
            {
                if (m_Rules.IsActorExhausted(actor))
                    lines.Add("Exhausted!");
                else if (m_Rules.IsActorSleepy(actor))
                    lines.Add("Sleepy.");
            }
            if (actor.Model.Abilities.HasToEat)
            {
                if (m_Rules.IsActorStarving(actor))
                    lines.Add("Starving!");
                else if (m_Rules.IsActorHungry(actor))
                    lines.Add("Hungry.");
            }
            else if (actor.Model.Abilities.IsRotting)
            {
                if (m_Rules.IsRottingActorStarving(actor))
                    lines.Add("Starving!");
                else if (m_Rules.IsRottingActorHungry(actor))
                    lines.Add("Hungry.");
            }
            if (actor.Model.Abilities.HasSanity)
            {
                if (m_Rules.IsActorInsane(actor))
                    lines.Add("Insane!");
                else if (m_Rules.IsActorDisturbed(actor))
                    lines.Add("Disturbed.");
            }

            // 3. Speed
            lines.Add(String.Format("Spd : {0:F2}", (float)m_Rules.ActorSpeed(actor) / (float)Rules.BASE_SPEED));

            // 4. HP & STA.
            StringBuilder sb = new StringBuilder();
            int maxHP = m_Rules.ActorMaxHPs(actor);
            if (actor.HitPoints != maxHP)
                sb.Append(String.Format("HP  : {0:D2}/{1:D2}", actor.HitPoints, maxHP));
            else
                sb.Append(String.Format("HP  : {0:D2} MAX", actor.HitPoints));
            if (actor.Model.Abilities.CanTire)
            {
                int maxSTA = m_Rules.ActorMaxSTA(actor);
                if (actor.StaminaPoints != maxSTA)
                    sb.Append(String.Format("   STA : {0}/{1}", actor.StaminaPoints, maxSTA));
                else
                    sb.Append(string.Format("   STA : {0} MAX", actor.StaminaPoints));
            }
            lines.Add(sb.ToString());

            // 5. Attack, Dmg, Defence.
            Attack attack = m_Rules.ActorMeleeAttack(actor, actor.CurrentMeleeAttack, null);
            lines.Add(String.Format("Atk : {0:D2} Dmg : {1:D2}", attack.HitValue, attack.DamageValue));
            Defence defence = m_Rules.ActorDefence(actor, actor.CurrentDefence);
            lines.Add(String.Format("Def : {0:D2}", defence.Value));
            lines.Add(String.Format("Arm : {0}/{1}", defence.Protection_Hit, defence.Protection_Shot));
            lines.Add(" ");

            // 6. Flavor
            lines.Add(actor.Model.FlavorDescription);
            lines.Add(" ");

            // 7. Skills
            if (actor.Sheet.SkillTable != null && actor.Sheet.SkillTable.CountSkills > 0)
            {
                foreach (Skill sk in actor.Sheet.SkillTable.Skills)
                    lines.Add(String.Format("{0}-{1}", sk.Level, Skills.Name(sk.ID)));
                lines.Add(" ");
            }

            // alpha10
            // 8. Unusual abilities
            // unusual abilities for undeads
            if (actor.Model.Abilities.IsUndead)
            {
                // fov
                lines.Add(string.Format("- FOV : {0}.", actor.Model.StartingSheet.BaseViewRange));

                // smell rating
                int smell = (int)(100 * m_Rules.ActorSmell(actor));  // appliyes z-tracker skill
                lines.Add(
                    smell == 0 ? "- Has no sense of smell." :
                    smell < 50 ? "- Has poor sense of smell." :
                    smell < 100 ? "- Has good sense of smell." :
                    "- Has excellent sense of smell.");

                // grab?
                if (actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.Z_GRAB) > 0)
                    lines.Add("- Z-Grab : this undead can grab its victims.");

                if (actor.Model.Abilities.IsUndeadMaster) lines.Add("- Other undeads follow this undead tracks.");
                else if (smell > 0) lines.Add("- This undead will follow zombie masters tracks.");
                if (actor.Model.Abilities.IsIntelligent) lines.Add("- This undead is intelligent.");
                if (actor.Model.Abilities.CanDisarm) lines.Add("- This undead can disarm.");
                if (actor.Model.Abilities.CanJump)
                {
                    if (actor.Model.Abilities.CanJumpStumble) lines.Add("- This undead can jump but may stumble.");
                    else lines.Add("- This undead can jump.");
                }
                if (m_Rules.HasActorPushAbility(actor)) lines.Add("- This undead can push.");
                if (actor.Model.Abilities.ZombieAI_Explore) lines.Add("- This undead will explore.");

                // things some of them cannot do
                if (!actor.Model.Abilities.IsRotting) lines.Add("- This undead will not rot.");
                if (!actor.Model.Abilities.CanBashDoors) lines.Add("- This undead cannot bash doors.");
                if (!actor.Model.Abilities.CanBreakObjects) lines.Add("- This undead cannot break objects.");
                if (!actor.Model.Abilities.CanZombifyKilled) lines.Add("- This undead cannot infect livings.");
                if (!actor.Model.Abilities.AI_CanUseAIExits) lines.Add("- This undead live in this map.");
            }
            // misc unusual abilities
            if (actor.Model.Abilities.IsLawEnforcer) lines.Add("- Is a law enforcer.");
            if (actor.Model.Abilities.IsSmall) lines.Add("- Is small and can sneak through things.");

            // 9. Inventory.
            if (actor.Inventory != null && !actor.Inventory.IsEmpty)
            {
                lines.Add(String.Format("Items {0}/{1} : ", actor.Inventory.CountItems, m_Rules.ActorMaxInv(actor)));
                lines.AddRange(DescribeInventory(actor.Inventory));
            }

            // done.
            return lines.ToArray();
        }

        string DescribeActorActivity(Actor actor)
        {
            if (actor.IsPlayer)
                return null;

            switch (actor.Activity)
            {
                case Activity.IDLE:
                    return null;

                case Activity.CHASING:
                    if (actor.TargetActor == null)
                        return "Chasing!";
                    else
                        return String.Format("Chasing {0}!", actor.TargetActor.Name);

                case Activity.FIGHTING:
                    if (actor.TargetActor == null)
                        return "Fighting!";
                    else
                        return String.Format("Fighting {0}!", actor.TargetActor.Name);

                case Activity.TRACKING:
                    return "Tracking!";

                case Activity.FLEEING:
                    return "Fleeing!";

                case Activity.FLEEING_FROM_EXPLOSIVE:
                    return "Fleeing from explosives!";

                case Activity.FOLLOWING:
                    if (actor.TargetActor == null)
                        return "Following.";
                    else
                    {
                        // alpha10
                        if (actor.Leader == actor.TargetActor)
                            return string.Format("Following {0} leader.", HisOrHer(actor));
                        return string.Format("Following {0}.", actor.TargetActor.Name);
                    }

                case Activity.FOLLOWING_ORDER:
                    return "Following orders.";

                case Activity.SLEEPING:
                    return "Sleeping.";

                default:
                    throw new ArgumentException("unhandled activity " + actor.Activity);
            }
        }

        string DescribePlayerFollowerStatus(Actor follower)
        {
            string desc;

            BaseAI foAI = follower.Controller as BaseAI;
            if (foAI.Order == null)
                desc = "(no orders)";
            else
                desc = foAI.Order.ToString();
            desc += String.Format("(trust:{0})", follower.TrustInLeader);

            return desc;
        }

        string[] DescribeMapObject(MapObject obj, Map map, Point mapPos)
        {
            List<string> lines = new List<string>(4);

            // 1. Name
            lines.Add(String.Format("{0}.", obj.AName));

            // 2. Special flags.
            if (obj.IsJumpable)
                lines.Add("Can be jumped on.");
            if (obj.IsCouch)
                lines.Add("Is a couch.");
            if (obj.GivesWood)
                lines.Add("Can be dismantled for wood.");
            if (obj.IsMovable)
                lines.Add("Can be moved.");
            if (obj.StandOnFovBonus)
                lines.Add("Increases view range.");

            // 3. Common Status: Break, Fire.
            //    Concrete MapObjects status.
            StringBuilder sb = new StringBuilder();
            if (obj.BreakState == MapObject.Break.BROKEN)
                sb.Append("Broken! ");
            if (obj.FireState == MapObject.Fire.ONFIRE)
                sb.Append("On fire! ");
            else if (obj.FireState == MapObject.Fire.ASHES)
                sb.Append("Burnt to ashes! ");
            lines.Add(sb.ToString());
            if (obj is PowerGenerator)
            {
                PowerGenerator powGen = obj as PowerGenerator;
                if (powGen.IsOn)
                    lines.Add("Currently ON.");
                else
                    lines.Add("Currently OFF.");
                float powerRatio = m_Rules.ComputeMapPowerRatio(obj.Location.Map);
                lines.Add(String.Format("The power gauge reads {0}%.", (int)(100 * powerRatio)));
            }
            else if (obj is Board)
            {
                lines.Add("The text reads : ");
                lines.AddRange((obj as Board).Text);
            }

            // 4. HitPoints & Barricade
            if (obj.MaxHitPoints > 0)
            {
                if (obj.HitPoints < obj.MaxHitPoints)
                    lines.Add(String.Format("HP        : {0}/{1}", obj.HitPoints, obj.MaxHitPoints));
                else
                    lines.Add(String.Format("HP        : {0} MAX", obj.HitPoints));

                DoorWindow door = obj as DoorWindow;
                if (door != null)
                {
                    if (door.BarricadePoints < Rules.BARRICADING_MAX)
                        lines.Add(String.Format("Barricades: {0}/{1}", door.BarricadePoints, Rules.BARRICADING_MAX));
                    else
                        lines.Add(String.Format("Barricades: {0} MAX", door.BarricadePoints));
                }
            }

            // 5. Weight?
            if (obj.Weight > 0)
            {
                lines.Add(String.Format("Weight    : {0}", obj.Weight));
            }

            // 6. Items there
            Inventory inv = map.GetItemsAt(mapPos);
            if (inv != null && !inv.IsEmpty)
            {
                lines.AddRange(DescribeInventory(inv));
            }

            return lines.ToArray();
        }

        string[] DescribeInventory(Inventory inv)
        {
            List<string> lines = new List<string>(inv.CountItems);

            foreach (Item it in inv.Items)
            {
                if (it.IsEquipped)
                    lines.Add(String.Format("- {0} (equipped)", DescribeItemShort(it)));
                else
                    lines.Add(String.Format("- {0}", DescribeItemShort(it)));
            }

            return lines.ToArray();
        }

        string[] DescribeCorpses(List<Corpse> corpses)
        {
            List<string> lines = new List<string>(corpses.Count + 2);

            if (corpses.Count > 1)
                lines.Add("There are corpses there...");
            else
                lines.Add("There is a corpse here.");
            lines.Add(" ");

            foreach (Corpse c in corpses)
            {
                lines.Add(String.Format("- Corpse of {0}.", c.DeadGuy.Name));
            }
            return lines.ToArray();
        }

        string[] DescribeCorpseLong(Corpse c, bool isInPlayerTile)
        {
            List<string> lines = new List<string>(10);

            // 1. Corpse of XXX
            lines.Add(String.Format("Corpse of {0}.", c.DeadGuy.Name));
            lines.Add(" ");

            // 2. Necrology infos.
            int necrology = m_Player.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.NECROLOGY);

            string deadSince = "???";
            if (necrology > 0)
                deadSince = WorldTime.MakeTimeDurationMessage(m_Session.WorldTime.TurnCounter - c.Turn);
            lines.Add(String.Format("Death     : {0}.", deadSince));

            string infectionEst = "???";
            if (necrology >= Rules.SKILL_NECROLOGY_LEVEL_FOR_INFECTION)
            {
                int infectionP = m_Rules.ActorInfectionPercent(c.DeadGuy);
                if (infectionP == 0) infectionEst = "0/7 - none";
                else if (infectionP < 5) infectionEst = "1/7 - traces";
                else if (infectionP < 15) infectionEst = "2/7 - minor";
                else if (infectionP < 30) infectionEst = "3/7 - low";
                else if (infectionP < 55) infectionEst = "4/7 - average";
                else if (infectionP < 70) infectionEst = "5/7 - important";
                else if (infectionP < 99) infectionEst = "6/7 - great";
                else infectionEst = "7/7 - total";
            }
            lines.Add(String.Format("Infection : {0}.", infectionEst));

            string riseEst = "???";
            if (necrology >= Rules.SKILL_NECROLOGY_LEVEL_FOR_RISE)
            {
                int riseP = 2 * m_Rules.CorpseZombifyChance(c, c.DeadGuy.Location.Map.LocalTime, false);
                if (riseP < 5) riseEst = "0/6 - extremely unlikely";
                else if (riseP < 20) riseEst = "1/6 - unlikely";
                else if (riseP < 40) riseEst = "2/6 - possible";
                else if (riseP < 60) riseEst = "3/6 - likely";
                else if (riseP < 80) riseEst = "4/6 - very likely";
                else if (riseP < 99) riseEst = "5/6 - most likely";
                else riseEst = "6/6 - certain";
            }
            lines.Add(String.Format("Rise      : {0}.", riseEst));
            lines.Add(" ");

            // 3. Decay
            int rotLevel = Rules.CorpseRotLevel(c);
            switch (rotLevel)
            {
                case 5: lines.Add("The corpse is about to crumble to dust."); break;
                case 4: lines.Add("The corpse is almost entirely rotten."); break;
                case 3: lines.Add("The corpse is badly damaged."); break;
                case 2: lines.Add("The corpse is damaged."); break;
                case 1: lines.Add("The corpse is bruised and smells."); break;
                case 0: lines.Add("The corpse looks fresh."); break;
                default: throw new Exception("unhandled rot level");
            }

            // 3. Medic info.
            string reviveEst = "???";
            int medic = m_Player.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.MEDIC);
            if (medic >= Rules.SKILL_MEDIC_LEVEL_FOR_REVIVE_EST)
            {
                int reviveP = m_Rules.CorpseReviveChance(m_Player, c);
                if (reviveP == 0) reviveEst = "impossible";
                else if (reviveP < 5) reviveEst = "0/6 - extremely unlikely";
                else if (reviveP < 20) reviveEst = "1/6 - unlikely";
                else if (reviveP < 40) reviveEst = "2/6 - possible";
                else if (reviveP < 60) reviveEst = "3/6 - likely";
                else if (reviveP < 80) reviveEst = "4/6 - very likely";
                else if (reviveP < 99) reviveEst = "5/6 - most likely";
                else reviveEst = "6/6 - certain";
            }
            lines.Add(String.Format("Revive    : {0}.", reviveEst));

            // 5. Special keys.
            if (isInPlayerTile)
            {
                lines.Add(" ");
                lines.Add("----");
                lines.Add("LBM to start/stop dragging.");
                lines.Add(String.Format("RBM to {0}.", m_Player.Model.Abilities.IsUndead ? "eat" : "butcher"));
                if (!m_Player.Model.Abilities.IsUndead)
                {
                    lines.Add(String.Format("to eat: <{0}>", s_KeyBindings.Get(PlayerCommand.EAT_CORPSE).ToString()));
                    lines.Add(String.Format("to revive : <{0}>", s_KeyBindings.Get(PlayerCommand.REVIVE_CORPSE).ToString()));
                }
            }

            return lines.ToArray();
        }
        #region Items
        string DescribeItemShort(Item it)
        {
            string name = it.Quantity > 1 ? it.Model.PluralName : it.AName;

            if (it is ItemFood)
            {
                ItemFood food = it as ItemFood;
                if (m_Rules.IsFoodSpoiled(food, m_Session.WorldTime.TurnCounter))
                    name += " (spoiled)";
                else if (m_Rules.IsFoodExpired(food, m_Session.WorldTime.TurnCounter))
                    name += " (expired)";
            }
            else if (it is ItemRangedWeapon)
            {
                ItemRangedWeapon rw = it as ItemRangedWeapon;
                name += String.Format(" ({0}/{1})", rw.Ammo, (rw.Model as ItemRangedWeaponModel).MaxAmmo);
            }
            else if (it is ItemTrap)
            {
                ItemTrap trap = it as ItemTrap;
                if (trap.IsActivated) name += "(activated)";
                if (trap.IsTriggered) name += "(triggered)";
                if (trap.Owner == m_Player) name += "(yours)";  // alpha10
            }

            if (it.Quantity > 1)
                return String.Format("{0} {1}", it.Quantity, name);
            else
                return name;
        }

        string[] DescribeItemLong(Item it, bool isPlayerInventory, int iSlot)
        {
            List<string> lines = new List<string>();
            bool isDefaultUse = true; // alpha10

            // 1. Name & stacking.
            if (it.Model.IsStackable)
            {
                lines.Add(String.Format("{0} {1}/{2}", DescribeItemShort(it), it.Quantity, it.Model.StackingLimit));
            }
            else
                lines.Add(DescribeItemShort(it));

            // 2. Special flags.
            // unbreakable?
            if (it.Model.IsUnbreakable)
            {
                lines.Add("Unbreakable.");
            }

            // 3. Item specific stuff...
            string inInvAdditionalDesc = null;
            if (it is ItemWeapon)
            {
                lines.AddRange(DescribeItemWeapon(it as ItemWeapon));
                if (it is ItemRangedWeapon)
                {
                    isDefaultUse = false;
                    inInvAdditionalDesc = String.Format("to fire : <{0}>", s_KeyBindings.Get(PlayerCommand.FIRE_MODE).ToString());
                }
            }
            else if (it is ItemFood)
            {
                lines.AddRange(DescribeItemFood(it as ItemFood));
            }
            else if (it is ItemMedicine)
            {
                lines.AddRange(DescribeItemMedicine(it as ItemMedicine));
            }
            else if (it is ItemBarricadeMaterial)
            {
                lines.AddRange(DescribeItemBarricadeMaterial(it as ItemBarricadeMaterial));
                isDefaultUse = false;
                inInvAdditionalDesc = String.Format("to build : <{0}>/<{1}>/<{2}>",
                    s_KeyBindings.Get(PlayerCommand.BARRICADE_MODE).ToString(), s_KeyBindings.Get(PlayerCommand.BUILD_SMALL_FORTIFICATION).ToString(),
                    s_KeyBindings.Get(PlayerCommand.BUILD_LARGE_FORTIFICATION).ToString());
            }
            else if (it is ItemBodyArmor)
            {
                lines.AddRange(DescribeItemBodyArmor(it as ItemBodyArmor));
            }
            else if (it is ItemSprayPaint)
            {
                lines.AddRange(DescribeItemSprayPaint(it as ItemSprayPaint));
                isDefaultUse = false;
                inInvAdditionalDesc = String.Format("to spray : <{0}>", s_KeyBindings.Get(PlayerCommand.USE_SPRAY).ToString());
            }
            else if (it is ItemSprayScent)
            {
                lines.AddRange(DescribeItemSprayScent(it as ItemSprayScent));
                isDefaultUse = false;
                inInvAdditionalDesc = String.Format("to spray : <{0}>", s_KeyBindings.Get(PlayerCommand.USE_SPRAY).ToString());
            }
            else if (it is ItemLight)
            {
                lines.AddRange(DescribeItemLight(it as ItemLight));
            }
            else if (it is ItemTracker)
            {
                lines.AddRange(DescribeItemTracker(it as ItemTracker));
            }
            else if (it is ItemAmmo)
            {
                lines.AddRange(DescribeItemAmmo(it as ItemAmmo));
                isDefaultUse = false;
                inInvAdditionalDesc = string.Format("to reload : <LMB> or <Ctrl-{0}>", iSlot + 1);
            }
            else if (it is ItemExplosive)
            {
                lines.AddRange(DescribeItemExplosive(it as ItemExplosive));
                inInvAdditionalDesc = String.Format("to throw : <{0}>", s_KeyBindings.Get(PlayerCommand.FIRE_MODE).ToString());
            }
            else if (it is ItemTrap)
            {
                lines.AddRange(DescribeItemTrap(it as ItemTrap));
                // alpha10
                if ((it as ItemTrap).TrapModel.ActivatesWhenDropped)
                    inInvAdditionalDesc = "to activate trap : drop it";
                else
                    inInvAdditionalDesc = "to activate trap : use it";
            }
            else if (it is ItemEntertainment)
            {
                lines.AddRange(DescribeItemEntertainment(it as ItemEntertainment));
            }

            // 3. Flavor description
            lines.Add(" ");
            lines.Add(it.Model.FlavorDescription);

            // 4. Special keys.
            // alpha10 added more special keys very few players know about!
            if (isPlayerInventory)
            {
                lines.Add(" ");
                lines.Add("----");
                if (it.Model.IsEquipable)
                    lines.Add(string.Format("to {0} : <LMB> or <Ctrl-{1}>", it.IsEquipped ? "unequip" : "equip", iSlot + 1));
                else if (isDefaultUse)
                    lines.Add(string.Format("to use : <LMB> or <Ctrl-{0}>", iSlot + 1));
                if (!it.IsEquipped)
                    lines.Add("to drop : <RMB>");
                lines.Add(String.Format("to give : <{0}>", s_KeyBindings.Get(PlayerCommand.GIVE_ITEM).ToString()));
                if (inInvAdditionalDesc != null)
                    lines.Add(inInvAdditionalDesc);
            }
            else
            {
                lines.Add(" ");
                lines.Add("----");
                lines.Add(string.Format("to take : <LMB> or <Shift-{0}>", iSlot + 1));
            }

            // done.
            return lines.ToArray();
        }

        string[] DescribeItemExplosive(ItemExplosive ex)
        {
            List<string> lines = new List<string>();

            ItemExplosiveModel m = ex.Model as ItemExplosiveModel;
            ItemPrimedExplosive primed = ex as ItemPrimedExplosive;

            lines.Add("> explosive");

            // 1. Explosive attack.
            if (m.BlastAttack.CanDamageObjects)
                lines.Add("Can damage objects.");
            if (m.BlastAttack.CanDestroyWalls)
                lines.Add("Can destroy walls.");

            if (primed != null)
                lines.Add(String.Format("Fuse          : {0} turn(s) left!", primed.FuseTimeLeft));
            else
                lines.Add(String.Format("Fuse          : {0} turn(s)", m.FuseDelay));
            lines.Add(String.Format("Blast radius  : {0}", m.BlastAttack.Radius));

            // 2. Damage for each distance.
            StringBuilder sb = new StringBuilder();
            for (int blastRadius = 0; blastRadius <= m.BlastAttack.Radius; blastRadius++)
            {
                sb.Append(String.Format("{0};", m_Rules.BlastDamage(blastRadius, m.BlastAttack)));
            }
            lines.Add(String.Format("Blast damages : {0}", sb.ToString()));

            // 3. Specialized explosives.
            // grenade?
            ItemGrenade grenade = ex as ItemGrenade;
            if (grenade != null)
            {
                lines.Add("> grenade");

                ItemGrenadeModel greModel = grenade.Model as ItemGrenadeModel;
                int rng = m_Rules.ActorMaxThrowRange(m_Player, greModel.MaxThrowDistance);
                if (rng != greModel.MaxThrowDistance)
                    lines.Add(String.Format("Throwing rng  : {0} ({1})", rng, greModel.MaxThrowDistance));
                else
                    lines.Add(String.Format("Throwing rng  : {0}", rng));
            }

            // 4. Primed?
            if (primed != null)
            {
                lines.Add("PRIMED AND READY TO EXPLODE!");
            }

            return lines.ToArray();
        }

        string[] DescribeItemWeapon(ItemWeapon w)
        {
            List<string> lines = new List<string>();

            ItemWeaponModel m = w.Model as ItemWeaponModel;

            lines.Add("> weapon");

            // 1. Attack
            lines.Add(String.Format("Atk : +{0}", m.Attack.HitValue));
            lines.Add(String.Format("Dmg : +{0}", m.Attack.DamageValue));
            // alpha10
            if (m.Attack.StaminaPenalty != 0)
                lines.Add(String.Format("Sta : -{0}", m.Attack.StaminaPenalty));
            if (m.Attack.DisarmChance != 0)
                lines.Add(String.Format("Disarm : +{0}%", m.Attack.DisarmChance));

            // 2. Melee vs Ranged items
            ItemMeleeWeapon mw = w as ItemMeleeWeapon;
            if (mw != null)
            {
                if (mw.IsFragile)
                    lines.Add("Breaks easily.");
                // alpha10 tool
                if (mw.IsTool)
                {
                    lines.Add("Is a tool.");
                    int toolBashDmg = mw.ToolBashDamageBonus;
                    if (toolBashDmg != 0)
                        lines.Add(string.Format("Tool Dmg   : +{0} = +{1}", toolBashDmg, toolBashDmg + m.Attack.DamageValue));
                    float toolBuild = mw.ToolBuildBonus;
                    if (toolBuild != 0)
                        lines.Add(string.Format("Tool Build : +{0}%", (int)(100 * toolBuild)));
                }
            }
            else
            {
                ItemRangedWeapon rw = w as ItemRangedWeapon;
                if (rw != null)
                {
                    ItemRangedWeaponModel rm = w.Model as ItemRangedWeaponModel;
                    if (rm.IsFireArm)
                        lines.Add("> firearm");
                    else if (rm.IsBow)
                        lines.Add("> bow");
                    else
                        lines.Add("> ranged weapon");

                    // alpha10
                    lines.Add(string.Format("Rapid Fire Atk: {0} {1}", rm.RapidFireHit1Value, rm.RapidFireHit2Value));

                    lines.Add(string.Format("Rng  : {0}-{1}", rm.Attack.Range, rm.Attack.EfficientRange));
                    if (rw.Ammo < rm.MaxAmmo)
                        lines.Add(string.Format("Amo  : {0}/{1}", rw.Ammo, rm.MaxAmmo));
                    else
                        lines.Add(string.Format("Amo  : {0} MAX", rw.Ammo));
                    lines.Add(string.Format("Type : {0}", DescribeAmmoType(rm.AmmoType)));
                }
            }

            // done.
            return lines.ToArray();
        }

        string DescribeAmmoType(AmmoType at)
        {
            switch (at)
            {
                case AmmoType.BOLT: return "bolts";
                case AmmoType.HEAVY_PISTOL: return "heavy pistol bullets";
                case AmmoType.HEAVY_RIFLE: return "heavy rifle bullets";
                case AmmoType.LIGHT_PISTOL: return "light pistol bullets";
                case AmmoType.LIGHT_RIFLE: return "light rifle bullets";
                case AmmoType.SHOTGUN: return "shotgun cartridge";
                default:
                    throw new ArgumentOutOfRangeException("unhandled ammo type");
            }
        }

        string[] DescribeItemAmmo(ItemAmmo am)
        {
            List<string> lines = new List<string>();

            lines.Add("> ammo");

            // 1. Ammo type
            lines.Add(string.Format("Type : {0}", DescribeAmmoType(am.AmmoType)));

            return lines.ToArray();
        }

        string[] DescribeItemFood(ItemFood f)
        {
            List<string> lines = new List<string>();

            ItemFoodModel m = f.Model as ItemFoodModel;

            lines.Add("> food");

            // 1. Fresh/Expired, Best-Before
            if (f.IsPerishable)
            {
                if (m_Rules.IsFoodStillFresh(f, m_Session.WorldTime.TurnCounter))
                    lines.Add("Fresh.");
                else if (m_Rules.IsFoodExpired(f, m_Session.WorldTime.TurnCounter))
                    lines.Add("*Expired*");
                else if (m_Rules.IsFoodSpoiled(f, m_Session.WorldTime.TurnCounter))
                    lines.Add("**SPOILED**");
                lines.Add(String.Format("Best-Before : {0}", f.BestBefore.ToString()));
            }
            else
                lines.Add("Always fresh.");


            // 2. Nutrition
            int nutrition = m_Rules.FoodItemNutrition(f, m_Session.WorldTime.TurnCounter);
            int nutritionForPlayer = (m_Player == null ? nutrition : m_Rules.ActorItemNutritionValue(m_Player, nutrition));
            if (nutritionForPlayer == m.Nutrition)
                lines.Add(String.Format("Nutrition   : +{0}", nutrition));
            else
                lines.Add(String.Format("Nutrition   : +{0} (+{1})", nutritionForPlayer, nutrition));

            return lines.ToArray();
        }

        string[] DescribeItemMedicine(ItemMedicine med)
        {
            List<string> lines = new List<string>();

            ItemMedicineModel m = med.Model as ItemMedicineModel;

            lines.Add("> medicine");

            // alpha10 dont add lines for zero values

            int healingForPlayer = (m_Player == null ? m.Healing : m_Rules.ActorMedicineEffect(m_Player, m.Healing));
            if (m.Healing != 0)
            {
                if (healingForPlayer == m.Healing)
                    lines.Add(String.Format("Healing : +{0}", m.Healing));
                else
                    lines.Add(String.Format("Healing : +{0} (+{1})", healingForPlayer, m.Healing));
            }

            int staminaForPlayer = (m_Player == null ? m.StaminaBoost : m_Rules.ActorMedicineEffect(m_Player, m.StaminaBoost));
            if (m.StaminaBoost != 0)
            {
                if (staminaForPlayer == m.StaminaBoost)
                    lines.Add(String.Format("Stamina : +{0}", m.StaminaBoost));
                else
                    lines.Add(String.Format("Stamina : +{0} (+{1})", staminaForPlayer, m.StaminaBoost));
            }

            int sleepForPlayer = (m_Player == null ? m.SleepBoost : m_Rules.ActorMedicineEffect(m_Player, m.SleepBoost));
            if (m.SleepBoost != 0)
            {
                if (sleepForPlayer == m.SleepBoost)
                    lines.Add(String.Format("Sleep   : +{0}", m.SleepBoost));
                else
                    lines.Add(String.Format("Sleep   : +{0} (+{1})", sleepForPlayer, m.SleepBoost));
            }

            int sanForPlayer = (m_Player == null ? m.SanityCure : m_Rules.ActorMedicineEffect(m_Player, m.SanityCure));
            if (m.SanityCure != 0)
            {
                if (sanForPlayer == m.SanityCure)
                    lines.Add(String.Format("Sanity  : +{0}", m.SanityCure));
                else
                    lines.Add(String.Format("Sanity  : +{0} (+{1})", sanForPlayer, m.SanityCure));
            }

            if (m_Session.GamePreset.Infection)
            {
                int cureForPlayer = (m_Player == null ? m.InfectionCure : m_Rules.ActorMedicineEffect(m_Player, m.InfectionCure));
                if (m.InfectionCure != 0)
                {
                    if (cureForPlayer == m.InfectionCure)
                        lines.Add(String.Format("Cure    : +{0}", m.InfectionCure));
                    else
                        lines.Add(String.Format("Cure    : +{0} (+{1})", cureForPlayer, m.InfectionCure));
                }
            }

            return lines.ToArray();
        }

        string[] DescribeItemBarricadeMaterial(ItemBarricadeMaterial bm)
        {
            List<string> lines = new List<string>();

            ItemBarricadeMaterialModel m = bm.Model as ItemBarricadeMaterialModel;

            lines.Add("> barricade material");

            // 1. Barricading value.
            int barForPlayer = (m_Player == null ? m.BarricadingValue : m_Rules.ActorBarricadingPoints(m_Player, m.BarricadingValue));
            if (barForPlayer == m.BarricadingValue)
                lines.Add(String.Format("Barricading : +{0}", m.BarricadingValue));
            else
                lines.Add(String.Format("Barricading : +{0} (+{1})", barForPlayer, m.BarricadingValue));

            return lines.ToArray();
        }

        string[] DescribeItemBodyArmor(ItemBodyArmor b)
        {
            List<string> lines = new List<string>();

            lines.Add("> body armor");

            // 1. Protection value.
            lines.Add(string.Format("Protection vs Hits  : +{0}", b.Protection_Hit));
            lines.Add(string.Format("Protection vs Shots : +{0}", b.Protection_Shot));
            lines.Add(string.Format("Encumbrance         : -{0} DEF", b.Encumbrance));
            lines.Add(string.Format("Weight              : -{0:F2} SPD", 0.01f * b.Weight));

            // 2. Unsuspicious effects.
            List<string> unsuspicious = new List<string>();
            List<string> suspicious = new List<string>();
            if (b.IsFriendlyForCops()) unsuspicious.Add("Cops");
            if (b.IsHostileForCops()) suspicious.Add("Cops");
            foreach (GameGangs.IDs gang in GameGangs.BIKERS)
            {
                if (b.IsHostileForBiker(gang)) suspicious.Add(GameGangs.NAMES[(int)gang]);
                if (b.IsFriendlyForBiker(gang)) unsuspicious.Add(GameGangs.NAMES[(int)gang]);
            }
            // alpha10 fixed rule & desc mismatch
            //foreach (GameGangs.IDs gang in GameGangs.GANGSTAS)
            //{
            //    if (b.IsHostileForBiker(gang)) suspicious.Add(GameGangs.NAMES[(int)gang]);
            //    if (b.IsFriendlyForBiker(gang)) unsuspicious.Add(GameGangs.NAMES[(int)gang]);
            //}
            if (unsuspicious.Count > 0)
            {
                lines.Add("Unsuspicious to:");
                foreach (string s in unsuspicious)
                    lines.Add("- " + s);
            }
            if (suspicious.Count > 0)
            {
                lines.Add("Suspicious to:");
                foreach (string s in suspicious)
                    lines.Add("- " + s);
            }

            return lines.ToArray();
        }

        string[] DescribeItemSprayPaint(ItemSprayPaint sp)
        {
            List<string> lines = new List<string>();

            ItemSprayPaintModel m = sp.Model as ItemSprayPaintModel;

            lines.Add("> spray paint");

            // 1. Paint
            if (sp.PaintQuantity < m.MaxPaintQuantity)
                lines.Add(String.Format("Paint : {0}/{1}", sp.PaintQuantity, m.MaxPaintQuantity));
            else
                lines.Add(String.Format("Paint : {0} MAX", sp.PaintQuantity));

            return lines.ToArray();
        }

        string[] DescribeItemSprayScent(ItemSprayScent sp)
        {
            List<string> lines = new List<string>();

            ItemSprayScentModel m = sp.Model as ItemSprayScentModel;

            lines.Add("> spray scent");

            // 1. Spray.
            if (sp.SprayQuantity < m.MaxSprayQuantity)
                lines.Add(String.Format("Spray    : {0}/{1}", sp.SprayQuantity, m.MaxSprayQuantity));
            else
                lines.Add(String.Format("Spray    : {0} MAX", sp.SprayQuantity));

            // alpha10
            // 2. Odor & Strength
            lines.Add(string.Format("Odor     : {0}", Capitalize(sp.Odor.ToString().ToLower())));
            lines.Add(string.Format("Strength : {0}h", sp.Strength / WorldTime.TURNS_PER_HOUR));

            return lines.ToArray();
        }


        string[] DescribeItemLight(ItemLight lt)
        {
            List<string> lines = new List<string>();

            ItemLightModel m = lt.Model as ItemLightModel;

            lines.Add("> light");

            // 1. Batteries
            lines.Add(DescribeBatteries(lt.Batteries, m.MaxBatteries));

            // 2. FoV
            lines.Add(String.Format("FOV       : +{0}", lt.FovBonus));

            return lines.ToArray();
        }

        string[] DescribeItemTracker(ItemTracker tr)
        {
            List<string> lines = new List<string>();

            ItemTrackerModel m = tr.Model as ItemTrackerModel;

            lines.Add("> tracker");

            // 1. Batteries
            lines.Add(DescribeBatteries(tr.Batteries, m.MaxBatteries));
            // alpha10 range if applicable
            // TODO -- should be an tracker item property, hardcoding is baaaad -_-
            if (tr.CanTrackUndeads)
                lines.Add(string.Format("Range: {0}", Rules.ZTRACKINGRADIUS));
            else
                lines.Add("Range: whole map");

            // alpha10
            // 2. Clock
            if (tr.HasClock)
            {
                lines.Add(" ");
                if (tr.Batteries == 0)
                    lines.Add("Out of batteries, can't give the time.");
                else if (!tr.IsEquipped)
                    lines.Add("Equip the item to read the time.");
                else
                    lines.Add(string.Format("The clock reads: {0}h, {1}", m_Session.WorldTime.Hour, DescribeDayPhase(m_Session.WorldTime.Phase)));
            }

            return lines.ToArray();
        }

        string[] DescribeItemTrap(ItemTrap tr)
        {
            List<string> lines = new List<string>();

            ItemTrapModel m = tr.Model as ItemTrapModel;

            lines.Add("> trap");

            // 1. Status
            if (tr.IsActivated)
            {
                lines.Add("** Activated! **");
                // alpha10
                if (m_Rules.IsSafeFromTrap(tr, m_Player))
                {
                    lines.Add("You will safely avoid this trap.");
                    if (tr.Owner != null)
                        lines.Add(string.Format("Trap setup by {0}.", tr.Owner.Name));
                }
            }
            else if (tr.IsTriggered)
            {
                // alpha10
                lines.Add("** Triggered! **");
                if (m_Rules.IsSafeFromTrap(tr, m_Player))
                {
                    lines.Add("You will safely avoid this trap.");
                    if (tr.Owner != null)
                        lines.Add(string.Format("Trap setup by {0}.", tr.Owner.Name));
                }
            }
            // alpha10
            lines.Add(string.Format("Trigger chance for you : {0}%.", m_Rules.GetTrapTriggerChance(tr, m_Player)));

            // 2. Flags
            if (m.IsOneTimeUse) lines.Add("Desactives when triggered.");
            if (m.IsNoisy) lines.Add(String.Format("Makes {0} noise.", m.NoiseName));
            if (m.UseToActivate) lines.Add("Use to activate.");
            // if (m.IsFlammable) lines.Add("Can be put on fire.");

            // 3. Stats
            lines.Add(String.Format("Damage  : {0} x{1} = {2}", m.Damage, tr.Quantity, tr.Quantity * m.Damage));  // alpha10
            lines.Add(String.Format("Trigger : {0}% x{1} = {2}%", m.TriggerChance, tr.Quantity, tr.Quantity * m.TriggerChance));  // alpha10
            lines.Add(String.Format("Break   : {0}%", m.BreakChance));
            if (m.BlockChance > 0) lines.Add(String.Format("Block   : {0}%", m.BlockChance));
            if (m.BreakChanceWhenEscape > 0) lines.Add(String.Format("{0}% to break on escape", m.BreakChanceWhenEscape));

            return lines.ToArray();
        }

        string[] DescribeItemEntertainment(ItemEntertainment ent)
        {
            List<String> lines = new List<string>();

            ItemEntertainmentModel m = ent.EntertainmentModel;

            lines.Add("> entertainment");

            // player bored?
            if (m_Player != null && ent.IsBoringFor(m_Player)) // alpha10 boring items item centric
                lines.Add("* BORED OF IT! *");

            // San & Bore chance.
            lines.Add(String.Format("Sanity : +{0}", m.Value));
            lines.Add(String.Format("Boring : {0}%", m.BoreChance));

            return lines.ToArray();
        }

        string DescribeBatteries(int batteries, int maxBatteries)
        {
            int hours = BatteriesToHours(batteries);
            if (batteries < maxBatteries)
                return String.Format("Batteries : {0}/{1} ({2}h)", batteries, maxBatteries, hours);
            else
                return String.Format("Batteries : {0} MAX ({1}h)", batteries, hours);
        }
        #endregion

        string DescribeSkillShort(Skills.IDs id)
        {
            switch (id)
            {
                case Skills.IDs.AGILE:
                    return String.Format("+{0} melee ATK, +{1} DEF", Rules.SKILL_AGILE_ATK_BONUS, Rules.SKILL_AGILE_DEF_BONUS);
                case Skills.IDs.AWAKE:
                    return String.Format("+{0}% max SLP, +{1}% SLP regen ", (int)(100 * Rules.SKILL_AWAKE_SLEEP_BONUS), (int)(100 * Rules.SKILL_AWAKE_SLEEP_REGEN_BONUS));
                case Skills.IDs.BOWS:
                    return String.Format("bows +{0} ATK, +{1} DMG", Rules.SKILL_BOWS_ATK_BONUS, Rules.SKILL_BOWS_DMG_BONUS);
                case Skills.IDs.CARPENTRY:
                    return String.Format("build, -{0} mat. at lvl 3, +{1}% barricading", Rules.SKILL_CARPENTRY_LEVEL3_BUILD_BONUS, (int)(100 * Rules.SKILL_CARPENTRY_BARRICADING_BONUS));
                case Skills.IDs.CHARISMATIC:
                    return String.Format("+{0} trust per turn, +{1}% trade rolls, steal followers", Rules.SKILL_CHARISMATIC_TRUST_BONUS, Rules.SKILL_CHARISMATIC_TRADE_BONUS);  // alpha10.1 steal followers
                case Skills.IDs.FIREARMS:
                    return String.Format("firearms +{0} ATK, +{1} DMG", Rules.SKILL_FIREARMS_ATK_BONUS, Rules.SKILL_FIREARMS_DMG_BONUS);
                case Skills.IDs.HARDY:
                    return String.Format("sleeping anywhere heals, +{0}% chance to heal when sleeping", Rules.SKILL_HARDY_HEAL_CHANCE_BONUS);
                case Skills.IDs.HAULER:
                    return String.Format("+{0} inventory slots", Rules.SKILL_HAULER_INV_BONUS);
                case Skills.IDs.HIGH_STAMINA:
                    return String.Format("+{0} STA", Rules.SKILL_HIGH_STAMINA_STA_BONUS);
                case Skills.IDs.LEADERSHIP:
                    return String.Format("+{0} max Followers", Rules.SKILL_LEADERSHIP_FOLLOWER_BONUS);
                case Skills.IDs.LIGHT_EATER:
                    return String.Format("+{0}% max FOO, +{1}% items food points", (int)(100 * Rules.SKILL_LIGHT_EATER_MAXFOOD_BONUS), (int)(100 * Rules.SKILL_LIGHT_EATER_FOOD_BONUS));
                case Skills.IDs.LIGHT_FEET:
                    return String.Format("+{0}% to avoid and escape traps", Rules.SKILL_LIGHT_FEET_TRAP_BONUS);
                case Skills.IDs.LIGHT_SLEEPER:
                    return String.Format("+{0}% noise wake up chance", Rules.SKILL_LIGHT_SLEEPER_WAKEUP_CHANCE_BONUS);
                case Skills.IDs.MARTIAL_ARTS:
                    return String.Format("unarmed only +{0} ATK, +{1} DMG, +{2}% disarm", Rules.SKILL_MARTIAL_ARTS_ATK_BONUS, Rules.SKILL_MARTIAL_ARTS_DMG_BONUS, Rules.SKILL_MARTIAL_ARTS_DISARM_BONUS);
                case Skills.IDs.MEDIC:
                    return String.Format("+{0}% medicine items effects, +{1}% revive ", (int)(100 * Rules.SKILL_MEDIC_BONUS), Rules.SKILL_MEDIC_REVIVE_BONUS);
                case Skills.IDs.NECROLOGY:
                    return String.Format("+{0}/+{1} DMG vs undeads/corpses, data on corpses", Rules.SKILL_NECROLOGY_UNDEAD_BONUS, Rules.SKILL_NECROLOGY_CORPSE_BONUS);
                case Skills.IDs.STRONG:
                    return String.Format("+{0} melee DMG, +{1}% resist disarming, +{2} throw range", Rules.SKILL_STRONG_DMG_BONUS, Rules.SKILL_STRONG_RESIST_DISARM_BONUS, Rules.SKILL_STRONG_THROW_BONUS);
                case Skills.IDs.STRONG_PSYCHE:
                    return String.Format("+{0}% SAN threshold", (int)(100 * Rules.SKILL_STRONG_PSYCHE_LEVEL_BONUS));
                case Skills.IDs.TOUGH:
                    return String.Format("+{0} HP", Rules.SKILL_TOUGH_HP_BONUS);
                case Skills.IDs.UNSUSPICIOUS:
                    return String.Format("+{0}% unnoticed by law enforcers and gangs", Rules.SKILL_UNSUSPICIOUS_BONUS);

                case Skills.IDs.Z_AGILE:
                    return String.Format("+{0} melee ATK, +{1} DEF, can jump", Rules.SKILL_ZAGILE_ATK_BONUS, Rules.SKILL_ZAGILE_DEF_BONUS);
                case Skills.IDs.Z_EATER:
                    return String.Format("+{0}% eating HP regen", (int)(100 * Rules.SKILL_ZEATER_REGEN_BONUS));
                case Skills.IDs.Z_GRAB:
                    return String.Format("can grab enemies, +{0}% per level", Rules.SKILL_ZGRAB_CHANCE);
                case Skills.IDs.Z_INFECTOR:
                    return String.Format("+{0}% infection damage", (int)(100 * Rules.SKILL_ZINFECTOR_BONUS));
                case Skills.IDs.Z_LIGHT_EATER:
                    return String.Format("+{0}% max ROT, +{1}% from eating", (int)(100 * Rules.SKILL_ZLIGHT_EATER_MAXFOOD_BONUS), (int)(100 * Rules.SKILL_ZLIGHT_EATER_FOOD_BONUS));
                case Skills.IDs.Z_LIGHT_FEET:
                    return String.Format("+{0}% to avoid traps", Rules.SKILL_ZLIGHT_FEET_TRAP_BONUS);
                case Skills.IDs.Z_STRONG:
                    return String.Format("+{0} melee DMG, can push", Rules.SKILL_ZSTRONG_DMG_BONUS);
                case Skills.IDs.Z_TOUGH:
                    return String.Format("+{0} HP", Rules.SKILL_ZTOUGH_HP_BONUS);
                case Skills.IDs.Z_TRACKER:
                    return String.Format("+{0}% smell", (int)(100 * Rules.SKILL_ZTRACKER_SMELL_BONUS));

                default:
                    throw new ArgumentOutOfRangeException("unhandled skill id");
            }
        }

        string DescribeDayPhase(DayPhase phase)
        {
            switch (phase)
            {
                case DayPhase.AFTERNOON: return "Afternoon";
                case DayPhase.DEEP_NIGHT: return "Deep Night";
                case DayPhase.EVENING: return "Evening";
                case DayPhase.MIDDAY: return "Midday";
                case DayPhase.MIDNIGHT: return "Midnight";
                case DayPhase.MORNING: return "Morning";
                case DayPhase.SUNRISE: return "Sunrise";
                case DayPhase.SUNSET: return "Sunset";

                default: throw new ArgumentOutOfRangeException("unhandled dayphase");
            }
        }

        string DescribeWeather(Weather weather)
        {
            switch (weather)
            {
                case Weather.CLOUDY: return "Cloudy";
                case Weather.HEAVY_RAIN: return "Heavy rain";
                case Weather.RAIN: return "Rain";
                case Weather.CLEAR: return "Clear";

                default:
                    throw new ArgumentOutOfRangeException("unhandled weather");
            }
        }

        Color WeatherColor(Weather weather)
        {
            switch (weather)
            {
                case Weather.CLOUDY: return Color.Gray;
                case Weather.HEAVY_RAIN: return Color.Blue;
                case Weather.RAIN: return Color.LightBlue;
                case Weather.CLEAR: return Color.Yellow;

                default:
                    throw new ArgumentOutOfRangeException("unhandled weather");
            }
        }

        int BatteriesToHours(int batteries)
        {
            return batteries / WorldTime.TURNS_PER_HOUR;
        }

        int FoodToHoursUntilHungry(int food)
        {
            int left = food - Session.Get.GamePreset.HungerPoints;
            if (left <= 0)
                return 0;
            return left / WorldTime.TURNS_PER_HOUR;
        }

        int FoodToHoursUntilRotHungry(int food)
        {
            int left = food - Session.Get.GamePreset.RotPoints;
            if (left <= 0)
                return 0;
            return left / WorldTime.TURNS_PER_HOUR;
        }

        public bool IsAlmostHungry(Actor actor)
        {
            if (!actor.Model.Abilities.HasToEat)
                return false;
            return FoodToHoursUntilHungry(actor.FoodPoints) <= 3;
        }

        public bool IsAlmostRotHungry(Actor actor)
        {
            if (!actor.Model.Abilities.IsRotting)
                return false;
            return FoodToHoursUntilRotHungry(actor.FoodPoints) <= 3;
        }
        #endregion
    }
}
