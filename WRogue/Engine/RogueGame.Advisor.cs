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
        #region Advisor hints.
        void HandleAdvisor(Actor player)
        {
            ///////////////////////////////
            // If all hints given, say so.
            ///////////////////////////////
            if (s_Hints.HasAdvisorGivenAllHints())
            {
                ShowAdvisorMessage(
                    "YOU KNOW THE BASICS!",
                    new string[] {
                        "The Advisor has given you all the hints.",
                        "You can disable the advisor in the options.",
                        "Read the manual or discover the rest of the game by yourself.",
                        "Good luck and have fun!",
                        String.Format("To REDEFINE THE KEYS : <{0}>.", s_KeyBindings.Get(PlayerCommand.KEYBINDING_MODE).ToString()),
                        String.Format("To CHANGE OPTIONS    : <{0}>.", s_KeyBindings.Get(PlayerCommand.OPTIONS_MODE).ToString()),
                        String.Format("To READ THE MANUAL   : <{0}>.", s_KeyBindings.Get(PlayerCommand.HELP_MODE).ToString())
                    });
                return;
            }

            /////////////////////////////////
            // Show the first appliable hint.
            /////////////////////////////////

            for (int i = (int)AdvisorHint._FIRST; i < (int)AdvisorHint._COUNT; i++)
            {
                if (s_Hints.IsAdvisorHintGiven((AdvisorHint)i))
                    continue;
                if (IsAdvisorHintAppliable((AdvisorHint)i))
                {
                    AdvisorGiveHint((AdvisorHint)i);
                    return;
                }
            }

            // no hint.
            ShowAdvisorMessage("No hint available.",
                new string[] {
                    "The Advisor has now new hint for you in this situation.",
                    "You will see a popup when he has something to say.",
                    String.Format("To REDEFINE THE KEYS : <{0}>.", s_KeyBindings.Get(PlayerCommand.KEYBINDING_MODE).ToString()),
                    String.Format("To CHANGE OPTIONS    : <{0}>.", s_KeyBindings.Get(PlayerCommand.OPTIONS_MODE).ToString()),
                    String.Format("To READ THE MANUAL   : <{0}>.", s_KeyBindings.Get(PlayerCommand.HELP_MODE).ToString())
                });
        }

        // alpha10 obsolete
        //bool HasAdvisorAnyHintToGive()
        //{
        //    for (int i = (int)AdvisorHint._FIRST; i < (int)AdvisorHint._COUNT; i++)
        //    {
        //        if (s_Hints.IsAdvisorHintGiven((AdvisorHint)i))
        //            continue;
        //        if (IsAdvisorHintAppliable((AdvisorHint)i))
        //            return true;
        //    }

        //    return false;
        //}

        // alpha10
        /// <summary>
        ///
        /// </summary>
        /// <returns>-1 if none</returns>
        int GetAdvisorFirstAvailableHint()
        {
            for (int i = (int)AdvisorHint._FIRST; i < (int)AdvisorHint._COUNT; i++)
            {
                if (s_Hints.IsAdvisorHintGiven((AdvisorHint)i))
                    continue;
                if (IsAdvisorHintAppliable((AdvisorHint)i))
                    return i;
            }

            return -1;
        }

        void AdvisorGiveHint(AdvisorHint hint)
        {
            /////////////////
            // Mark as given
            /////////////////
            s_Hints.SetAdvisorHintAsGiven(hint);

            ////////////////
            // Save status.
            ////////////////
            SaveHints();

            /////////
            // Show
            /////////
            ShowAdvisorHint(hint);
        }

        bool IsAdvisorHintAppliable(AdvisorHint hint)
        {
            Map map = m_Player.Location.Map;
            Point pos = m_Player.Location.Position;

            switch (hint)
            {
                case AdvisorHint.ACTOR_MELEE:   // adjacent to an enemy.
                    return IsAdjacentToEnemy(map, pos, m_Player);

                case AdvisorHint.BARRICADE:  // barricading.
                    return map.HasAnyAdjacentInMap(pos, (pt) =>
                        {
                            DoorWindow door = map.GetMapObjectAt(pt) as DoorWindow;
                            if (door == null)
                                return false;
                            return m_Rules.CanActorBarricadeDoor(m_Player, door);
                        });

                case AdvisorHint.BUILD_FORTIFICATION: // building fortifications.
                    return map.HasAnyAdjacentInMap(pos, (pt) =>
                    {
                        return m_Rules.CanActorBuildFortification(m_Player, pt, false);
                    });

                case AdvisorHint.CELLPHONES:
                    return m_Player.Inventory.GetFirstByModel(GameItems.CELL_PHONE) != null;

                case AdvisorHint.CITY_INFORMATION:  // city information, wait a bit...
                    return map.LocalTime.Hour >= 12;

                case AdvisorHint.CORPSE:
                    return !m_Player.Model.Abilities.IsUndead && map.GetCorpsesAt(pos) != null;

                case AdvisorHint.CORPSE_EAT:
                    return m_Player.Model.Abilities.IsUndead && map.GetCorpsesAt(pos) != null;

                case AdvisorHint.DOORWINDOW_OPEN:   // can open an adj door/window.
                    return map.HasAnyAdjacentInMap(pos, (pt) =>
                        {
                            DoorWindow door = map.GetMapObjectAt(pt) as DoorWindow;
                            if (door == null)
                                return false;
                            return m_Rules.IsOpenableFor(m_Player, door);
                        });

                case AdvisorHint.DOORWINDOW_CLOSE:   // can close an open door/window.
                    return map.HasAnyAdjacentInMap(pos, (pt) =>
                    {
                        DoorWindow door = map.GetMapObjectAt(pt) as DoorWindow;
                        if (door == null)
                            return false;
                        return m_Rules.IsClosableFor(m_Player, door);
                    });

                case AdvisorHint.EXIT_STAIRS_LADDERS:  // using stairs, laders.
                    return map.GetExitAt(pos) != null;

                case AdvisorHint.EXIT_LEAVING_DISTRICT: // leaving the district.
                    {
                        foreach (Direction d in Direction.COMPASS)
                        {
                            Point pt = pos + d;
                            if (map.IsInBounds(pt))
                                continue;
                            if (map.GetExitAt(pt) != null)
                                return true;
                        }
                        return false;
                    }

                case AdvisorHint.FLASHLIGHT:
                    return m_Player.Inventory.HasItemOfType(typeof(ItemLight));

                case AdvisorHint.GAME_SAVE_LOAD:    // saving/loading. wait a bit...
                    return map.LocalTime.Hour >= 7;

                case AdvisorHint.GRENADE:
                    {
                        Inventory inv = m_Player.Inventory;
                        if (inv == null || inv.IsEmpty)
                            return false;
                        return inv.HasItemOfType(typeof(ItemGrenade));
                    }

                case AdvisorHint.ITEM_GRAB_CONTAINER: // can take an item from an adjacent container.
                    return map.HasAnyAdjacentInMap(pos, (pt) => m_Rules.CanActorGetItemFromContainer(m_Player, pt));

                case AdvisorHint.ITEM_GRAB_FLOOR:   // can take an item from the flor.
                    {
                        Inventory invThere = map.GetItemsAt(pos);
                        if (invThere == null)
                            return false;
                        foreach (Item it in invThere.Items)
                            if (m_Rules.CanActorGetItem(m_Player, it))
                                return true;
                        return false;
                    }

                case AdvisorHint.ITEM_EQUIP:  // equip an item.
                    {
                        Inventory inv = m_Player.Inventory;
                        if (inv == null || inv.IsEmpty)
                            return false;
                        foreach (Item it in inv.Items)
                            if (!it.IsEquipped && m_Rules.CanActorEquipItem(m_Player, it))
                                return true;
                        return false;
                    }

                case AdvisorHint.ITEM_UNEQUIP:  // unequip an item.
                    {
                        Inventory inv = m_Player.Inventory;
                        if (inv == null || inv.IsEmpty)
                            return false;
                        foreach (Item it in inv.Items)
                            if (m_Rules.CanActorUnequipItem(m_Player, it))
                                return true;
                        return false;
                    }

                case AdvisorHint.ITEM_DROP: // dropping an item.
                    {
                        Inventory inv = m_Player.Inventory;
                        if (inv == null || inv.IsEmpty)
                            return false;
                        foreach (Item it in inv.Items)
                            if (m_Rules.CanActorDropItem(m_Player, it))
                                return true;
                        return false;
                    }

                case AdvisorHint.ITEM_TYPE_BARRICADING: // barricading material.
                    {
                        Inventory inv = m_Player.Inventory;
                        if (inv == null || inv.IsEmpty)
                            return false;
                        return inv.HasItemOfType(typeof(ItemBarricadeMaterial));
                    }

                case AdvisorHint.ITEM_USE: // using an item.
                    {
                        Inventory inv = m_Player.Inventory;
                        if (inv == null || inv.IsEmpty)
                            return false;
                        foreach (Item it in inv.Items)
                            if (m_Rules.CanActorUseItem(m_Player, it))
                                return true;
                        return false;
                    }

                case AdvisorHint.KEYS_OPTIONS:  // redefining keys & options.
                    return true;

                case AdvisorHint.LEADING_CAN_RECRUIT:   // can recruit follower.
                    return map.HasAnyAdjacentInMap(pos, (pt) =>
                        {
                            Actor other = map.GetActorAt(pt);
                            if (other == null)
                                return false;
                            return m_Rules.CanActorTakeLead(m_Player, other);
                        });

                case AdvisorHint.LEADING_GIVE_ORDERS:   // give orders to followers.
                    return m_Player.CountFollowers > 0;

                case AdvisorHint.LEADING_NEED_SKILL:    // could recruit...
                    return map.HasAnyAdjacentInMap(pos, (pt) =>
                        {
                            Actor other = map.GetActorAt(pt);
                            if (other == null)
                                return false;
                            return !m_Rules.AreEnemies(m_Player, other);
                        });

                case AdvisorHint.LEADING_SWITCH_PLACE:  // switch place.
                    return map.HasAnyAdjacentInMap(pos, (pt) =>
                    {
                        Actor other = map.GetActorAt(pt);
                        if (other == null)
                            return false;
                        return m_Rules.CanActorSwitchPlaceWith(m_Player, other);
                    });

                case AdvisorHint.MOUSE_LOOK:    // always!
                    return map.LocalTime.TurnCounter >= 2;  // don't spam at turn 0.

                case AdvisorHint.MOVE_BASIC:    // always!
                    return true;

                case AdvisorHint.MOVE_JUMP:  // can jump.
                    return !m_Rules.IsActorTired(m_Player) &&
                        map.HasAnyAdjacentInMap(pos, (pt) =>
                        {
                            MapObject obj = map.GetMapObjectAt(pt);
                            if (obj == null)
                                return false;
                            return obj.IsJumpable;
                        });

                case AdvisorHint.MOVE_RUN:   // running.
                    return map.LocalTime.TurnCounter >= 5 && m_Rules.CanActorRun(m_Player);  // don't spam at turn 0.

                case AdvisorHint.MOVE_RESTING: // resting.
                    return m_Rules.IsActorTired(m_Player);

                case AdvisorHint.NIGHT: // night effects, wait a bit.
                    return map.LocalTime.TurnCounter >= 1 * WorldTime.TURNS_PER_HOUR;

                case AdvisorHint.NPC_TRADE: // trading.
                    return map.HasAnyAdjacentInMap(pos, (pt) =>
                        {
                            Actor other = map.GetActorAt(pt);
                            if (other == null)
                                return false;
                            return m_Rules.CanActorInitiateTradeWith(m_Player, other);
                        });

                case AdvisorHint.NPC_GIVING_ITEM: // giving items.
                    {
                        Inventory inv = m_Player.Inventory;
                        if (inv == null || inv.IsEmpty)
                            return false;
                        return map.HasAnyAdjacentInMap(pos, (pt) =>
                            {
                                Actor other = map.GetActorAt(pt);
                                if (other == null)
                                    return false;
                                return !m_Rules.AreEnemies(m_Player, other);
                            });
                    }

                case AdvisorHint.NPC_SHOUTING:  // shouting.
                    return map.HasAnyAdjacentInMap(pos, (pt) =>
                        {
                            Actor other = map.GetActorAt(pt);
                            if (other == null)
                                return false;
                            return other.IsSleeping && !m_Rules.AreEnemies(m_Player, other);
                        });

                case AdvisorHint.OBJECT_BREAK: // breaking around.
                    return map.HasAnyAdjacentInMap(pos, (pt) =>
                        {
                            MapObject obj = map.GetMapObjectAt(pt);
                            if (obj == null)
                                return false;
                            return m_Rules.IsBreakableFor(m_Player, obj);
                        });

                case AdvisorHint.OBJECT_PUSH:   // pushable around.
                    return map.HasAnyAdjacentInMap(pos, (pt) =>
                    {
                        MapObject obj = map.GetMapObjectAt(pt);
                        if (obj == null)
                            return false;
                        return m_Rules.CanActorPush(m_Player, obj);
                    });

                case AdvisorHint.RAIN:  // rainy weather, wait a bit.
                    return m_Rules.IsWeatherRain(m_Session.World.Weather) && map.LocalTime.TurnCounter >= 2 * WorldTime.TURNS_PER_HOUR;

                case AdvisorHint.SPRAYS_PAINT:    // using spraypaint.
                    return m_Player.Inventory.HasItemOfType(typeof(ItemSprayPaint));

                case AdvisorHint.SPRAYS_SCENT:    // using scent sprays.
                    return m_Player.Inventory.HasItemOfType(typeof(ItemSprayScent));

                case AdvisorHint.STATE_HUNGRY:
                    return m_Rules.IsActorHungry(m_Player);

                case AdvisorHint.STATE_SLEEPY:
                    return m_Rules.IsActorSleepy(m_Player);

                case AdvisorHint.WEAPON_FIRE: // can fire a weapon.
                    {
                        ItemRangedWeapon rw = m_Player.GetEquippedWeapon() as ItemRangedWeapon;
                        if (rw == null)
                            return false;
                        return rw.Ammo >= 0;
                    }

                case AdvisorHint.WEAPON_RELOAD: // reloading a weapon.
                    {
                        ItemRangedWeapon rw = m_Player.GetEquippedWeapon() as ItemRangedWeapon;
                        if (rw == null)
                            return false;
                        Inventory inv = m_Player.Inventory;
                        if (inv == null || inv.IsEmpty)
                            return false;
                        foreach (Item it in inv.Items)
                            if (it is ItemAmmo && m_Rules.CanActorUseItem(m_Player, it))
                                return true;
                        return false;
                    }

                // alpha10 new hints

                case AdvisorHint.SANITY:  // sanity
                    return m_Player.Sanity < 0.80f * m_Rules.ActorMaxSanity(m_Player);

                case AdvisorHint.INFECTION:
                    return m_Player.Infection > 0;

                case AdvisorHint.TRAPS:
                    return m_Player.Inventory.HasItemOfType(typeof(ItemTrap));

                default:
                    throw new ArgumentOutOfRangeException("unhandled hint");
            }
        }

        void GetAdvisorHintText(AdvisorHint hint, out string title, out string[] body)
        {
            switch (hint)
            {
                case AdvisorHint.ACTOR_MELEE:
                    title = "ATTACK AN ENEMY IN MELEE";
                    body = new string[] {
                            "You are next to an enemy.",
                            "To ATTACK him, try to MOVE on him."};
                    break;

                case AdvisorHint.BARRICADE:
                    title = "BARRICADING A DOOR/WINDOW";
                    body = new string[] {
                            "You can barricade an adjacent door or window.",
                            "Barricading uses material such as planks.",
                            String.Format("To BARRICADE : <{0}>.", s_KeyBindings.Get(PlayerCommand.BARRICADE_MODE).ToString())
                        };
                    break;

                case AdvisorHint.BUILD_FORTIFICATION:
                    title = "BUILDING FORTIFICATIONS";
                    body = new string[] {
                            "You can now build fortifications thanks to the carpentry skill.",
                            "You need enough barricading materials.",
                            String.Format("To BUILD SMALL FORTIFICATIONS : <{0}>.", s_KeyBindings.Get(PlayerCommand.BUILD_SMALL_FORTIFICATION).ToString()),
                            String.Format("To BUILD LARGE FORTIFICATIONS : <{0}>.", s_KeyBindings.Get(PlayerCommand.BUILD_LARGE_FORTIFICATION).ToString())
                        };
                    break;

                case AdvisorHint.CELLPHONES:
                    title = "CELLPHONES";
                    body = new string[] {
                            "You have found a cellphone.",
                            "Cellphones are useful to keep contact with your follower(s).",
                            "You and your follower(s) must have a cellphone equipped.",
                            "You can recharge cellphones at power generators."
                        };
                    break;

                case AdvisorHint.CITY_INFORMATION:
                    title = "CITY INFORMATION";
                    body = new string[] {
                            "You know the layout of your town.",
                            "You aso know the most notable locations.",
                            String.Format("To VIEW THE CITY INFORMATION : <{0}>.", s_KeyBindings.Get(PlayerCommand.CITY_INFO).ToString())
                        };
                    break;

                // alpha10 merged corpses hints
                case AdvisorHint.CORPSE:
                    title = "CORPSES";
                    body = new string[] {
                            "You are standing on a CORPSE.",
                            "Corpses will slowly rot away but may resurrect as zombies.",
                            "You can BUTCHER a corpse as a way to prevent that.",
                            "You can also DRAG corpses to move them.",
                            "You can try to REVIVE corpses if you have the medic skill and a medikit.",
                            "If you are desperate and starving you can resort to cannibalism by EATING corpses.",
                            "To act, hover the mouse on it in the corpse list and...",
                            "TO BUTCHER the CORPSE : <RMB>",
                            "TO DRAG the CORPSE : <LMB>",
                            string.Format("TO REVIVE the CORPSE : <{0}>", s_KeyBindings.Get(PlayerCommand.REVIVE_CORPSE).ToString()),
                            string.Format("TO EAT the CORPSE : <{0}>", s_KeyBindings.Get(PlayerCommand.EAT_CORPSE).ToString())
                    };
                    break;

                case AdvisorHint.CORPSE_EAT:
                    title = "EATING CORPSES";
                    body = new string[] {
                            "You can eat a corpse to regain health.",
                            String.Format("TO EAT A CORPSE : <RMB> on it in the corpse list.")
                    };
                    break;

                case AdvisorHint.DOORWINDOW_OPEN:
                    title = "OPENING A DOOR/WINDOW";
                    body = new string[] {
                            "You are next to a closed door or window.",
                            "To OPEN it, try to MOVE on it."
                        };
                    break;

                case AdvisorHint.DOORWINDOW_CLOSE:
                    title = "CLOSING A DOOR/WINDOW";
                    body = new string[] {
                            "You are next to an open door or window.",
                            String.Format("To CLOSE : <{0}>.", s_KeyBindings.Get(PlayerCommand.CLOSE_DOOR).ToString())
                        };
                    break;

                case AdvisorHint.EXIT_STAIRS_LADDERS:
                    title = "USING STAIRS & LADDERS";
                    body = new string[] {
                            "You are standing on stairs or a ladder.",
                            "You can use this exit to go on another map.",
                            String.Format("To USE THE EXIT : <{0}>.", s_KeyBindings.Get(PlayerCommand.USE_EXIT).ToString())
                        };
                    break;

                case AdvisorHint.FLASHLIGHT:
                    title = "LIGHTING";
                    body = new string[] {
                            "You have found a lighting item, such as a flashlight.",
                            "Equip the item to increase your view distance (FoV).",
                            "Standing next to someone with a light on has the same effect.",
                            "You can recharge flashlights at power generators."
                        };
                    break;

                case AdvisorHint.GAME_SAVE_LOAD:
                    title = "SAVING AND LOADING GAME";
                    body = new string[] {
                            "Now could be a good time to save your game.",
                            "You can have only one save game active.",
                            String.Format("To SAVE THE GAME : <{0}>.", s_KeyBindings.Get(PlayerCommand.SAVE_GAME).ToString()),
                            String.Format("To LOAD THE GAME : <{0}>.", s_KeyBindings.Get(PlayerCommand.LOAD_GAME).ToString()),
                            "You can also load the game from the main menu.",
                            "Saving or loading can take a bit of time, please be patient.",
                            "Or consider turning some game options to lower settings."
                        };
                    break;

                case AdvisorHint.EXIT_LEAVING_DISTRICT:
                    title = "LEAVING THE DISTRICT";
                    body = new string[] {
                            "You are next to a district EXIT.",
                            "You can leave this district by MOVING into the exit."
                        };
                    break;

                case AdvisorHint.GRENADE:
                    title = "GRENADES";
                    body = new string[] {
                            "You have found a grenade.",
                            "To THROW a GRENADE, EQUIP it and FIRE it.",
                            String.Format("To FIRE : <{0}>.", s_KeyBindings.Get(PlayerCommand.FIRE_MODE).ToString())
                        };
                    break;

                case AdvisorHint.ITEM_GRAB_CONTAINER:
                    title = "TAKING AN ITEM FROM A CONTAINER";
                    body = new string[] {
                            "You are next to a container, such as a wardrobe or a shelf.",
                            "You can TAKE the item there by MOVING into the object."
                        };
                    break;

                case AdvisorHint.ITEM_GRAB_FLOOR:
                    title = "TAKING AN ITEM FROM THE FLOOR";
                    body = new string[] {
                            "You are standing on a stack of items.",
                            "The items are listed on the right panel in the ground inventory.",
                            "To TAKE an item, move your mouse on the item on the ground inventory and <LMB>.",
                            "Shortcut : <Ctrl-item slot number>."
                        };
                    break;

                case AdvisorHint.ITEM_DROP:
                    title = "DROPPING AN ITEM";
                    body = new string[] {
                            "You can drop items from your inventory.",
                            "To DROP an item, <RMB> on it.",
                            "The item must be unequiped first."
                        };
                    break;

                case AdvisorHint.ITEM_EQUIP:
                    title = "EQUIPING AN ITEM";
                    body = new string[] {
                            "You have an equipable item in your inventory.",
                            "Typical equipable items are weapons, lights and phones.",
                            "To EQUIP the item, <LMB> on it in your inventory.",
                            "Shortcut : <Ctrl-item slot number>"
                        };
                    break;

                case AdvisorHint.ITEM_TYPE_BARRICADING:
                    title = "ITEM - BARRICADING MATERIAL";
                    body = new string[] {
                            "You have some barricading materials, such as planks.",
                            "Barricading material is used when you barricade doors/windows or build fortifications.",
                            "To build fortifications you need the CARPENTRY skill."
                        };
                    break;

                case AdvisorHint.ITEM_UNEQUIP:
                    title = "UNEQUIPING AN ITEM";
                    body = new string[] {
                            "You have equiped an item.",
                            "The item is displayed with a green background.",
                            "To UNEQUIP the item, <LMB> on it in your inventory.",
                            "Shortcut: <Ctrl-item slot number>"
                        };
                    break;

                case AdvisorHint.ITEM_USE:
                    title = "USING AN ITEM";
                    body = new string[] {
                            "You can use one of your item.",
                            "Typical usable items are food, medecine and ammunition.",
                            "To USE the item, <LMB> on it in your inventory.",
                            "Shortcut: <Ctrl-item slot number>"
                        };
                    break;

                case AdvisorHint.KEYS_OPTIONS:
                    title = "KEYS & OPTIONS";
                    body = new string[] {
                            String.Format("You can view and redefine the KEYS by pressing <{0}>.", s_KeyBindings.Get(PlayerCommand.KEYBINDING_MODE).ToString()),
                            String.Format("You can change OPTIONS by pressing <{0}>.", s_KeyBindings.Get(PlayerCommand.OPTIONS_MODE).ToString()),
                            "Some option changes will only take effect when starting a new game.",
                            "Keys and Options are saved."
                        };
                    break;

                case AdvisorHint.LEADING_CAN_RECRUIT:
                    title = "LEADING - RECRUITING";
                    body = new string[] {
                            "You can recruit a follower next to you!",
                            String.Format("To RECRUIT : <{0}>.", s_KeyBindings.Get(PlayerCommand.LEAD_MODE).ToString())
                        };
                    break;

                case AdvisorHint.LEADING_GIVE_ORDERS:
                    title = "LEADING - GIVING ORDERS";
                    body = new string[] {
                            "You can give orders and directives to your follower.",
                            "You can also fire your followers.",
                            String.Format("To GIVE ORDERS : <{0}>.", s_KeyBindings.Get(PlayerCommand.ORDER_MODE).ToString()),
                            String.Format("To FIRE YOUR FOLLOWER : <{0}>.", s_KeyBindings.Get(PlayerCommand.LEAD_MODE).ToString())
                        };
                    break;

                case AdvisorHint.LEADING_NEED_SKILL:
                    title = "LEADING - LEADERSHIP SKILL";
                    body = new string[] {
                            "You can try to recruit a follower if you have the LEADERSHIP skill.",
                            "The higher the skill, the more followers you can recruit."
                        };
                    break;

                case AdvisorHint.LEADING_SWITCH_PLACE:
                    title = "LEADING - SWITCHING PLACE";
                    body = new string[] {
                            "You can switch place with followers next to you.",
                            String.Format("To SWITCH PLACE : <{0}>.", s_KeyBindings.Get(PlayerCommand.SWITCH_PLACE).ToString())
                        };
                    break;

                case AdvisorHint.MOUSE_LOOK:
                    title = "LOOKING WITH THE MOUSE";
                    body = new string[] {
                            "You can LOOK at actors and objects on the map.",
                            "Move the MOUSE over something interesting.",
                            "You will get a detailed description of the actor or object.",
                            "This is useful to learn the game or assessing the tactical situation."
                        };
                    break;

                case AdvisorHint.MOVE_BASIC:
                    title = "MOVEMENT - DIRECTIONS";
                    body = new string[] {
                            "MOVE your character around with the movements keys.",
                            "The default keys are your NUMPAD numbers.",
                            "",
                            "7 8 9",
                            "4 - 6",
                            "1 2 3",
                            "",
                            "5 makes you WAIT one turn.",
                            "The move keys are the most important ones.",
                            "When asked for a DIRECTION, press a MOVE key.",
                            "Be sure to remember that!",
                            "...and remember to keep NumLock on!"
                        };
                    break;

                case AdvisorHint.MOVE_JUMP:
                    title = "MOVEMENT - JUMPING";
                    body = new string[] {
                            "You can JUMP on or over an obstacle next to you.",
                            "Typical jumpable objects are cars, fences and furniture.",
                            "The object is described with 'Can be jumped on'.",
                            "Some enemies can't jump and won't be able to follow you.",
                            "Jumping is tiring and spends stamina.",
                            "To jump, just MOVE on the obstacle."
                        };
                    break;

                case AdvisorHint.MOVE_RUN:
                    title = "MOVEMENT - RUNNING";
                    body = new string[] {
                            "You can RUN to move faster.",
                            "Running is tiring and spend stamina.",
                            String.Format("To TOGGLE RUNNING : <{0}>.", s_KeyBindings.Get(PlayerCommand.RUN_TOGGLE).ToString())
                        };
                    break;

                case AdvisorHint.MOVE_RESTING:
                    title = "MOVEMENT - RESTING";
                    body = new string[] {
                            "You are TIRED because you lost too much STAMINA.",
                            "Being tired is bad for you!",
                            "You move slowly.",
                            "You can't do tiring activities such as running, fighting and jumping.",
                            "You always recover a bit of stamina each turn.",
                            "But you can REST to recover stamina faster.",
                            String.Format("To REST/WAIT : <{0}>.", s_KeyBindings.Get(PlayerCommand.WAIT_OR_SELF).ToString())
                        };
                    break;

                case AdvisorHint.NIGHT:
                    title = "NIGHT TIME";
                    body = new string[] {
                            "It is night. Night time is penalizing for livings.",
                            "They tire faster (stamina and sleep) and don't see very far.",
                            "Undeads are not penalized by night at all."
                        };
                    break;

                case AdvisorHint.NPC_GIVING_ITEM:
                    title = "GIVING ITEMS";
                    body = new string[] {
                            "You can GIVE ITEMS to other actors.",
                            String.Format("To GIVE AN ITEM : move the mouse over your item and press <{0}>.", s_KeyBindings.Get(PlayerCommand.GIVE_ITEM).ToString())
                        };
                    break;

                case AdvisorHint.NPC_SHOUTING:
                    title = "SHOUTING";
                    body = new string[] {
                            "Someone is sleeping near you.",
                            "You can SHOUT to try to wake him or her up.",
                            "Other actors can also shout to wake their friends up when they see danger.",
                            String.Format("To SHOUT : <{0}>.", s_KeyBindings.Get(PlayerCommand.SHOUT).ToString())
                        };
                    break;

                case AdvisorHint.NPC_TRADE:
                    title = "TRADING";
                    body = new string[] {
                            "You can TRADE with an actor next to you.",
                            "Actor that can trade with you have a $ icon on the map.",
                            "Trading means exhanging items.",
                            "To ask for a TRADE offer, just try to MOVE into the actor and accept or refuse the offer.",
                            "You can also initiate a more detailled trade negociation.",
                            String.Format("To NEGOCIATE A TRADE : press <{0}> and select an npc with the directions.", s_KeyBindings.Get(PlayerCommand.NEGOCIATE_TRADE).ToString())
                        };
                    break;

                case AdvisorHint.OBJECT_BREAK:
                    title = "BREAKING OBJECTS";
                    body = new string[] {
                            "You can try to BREAK an object around you.",
                            "Typical breakable objects are furnitures, doors and windows.",
                            String.Format("To BREAK : <{0}>.", s_KeyBindings.Get(PlayerCommand.BREAK_MODE).ToString())
                        };
                    break;

                case AdvisorHint.OBJECT_PUSH:  // alpha10 also pulling and mention shoving actors
                    title = "PUSHING/PULLING OBJECTS";
                    body = new string[] {
                            "You can PUSH/PULL an OBJECT around you.",
                            "Only MOVABLE objects can be pushed/pulled.",
                            "Movable objects will be described as 'Can be moved'",
                            "You can also PUSH/PULL ACTORS around you.",
                            String.Format("To PUSH : <{0}>.", s_KeyBindings.Get(PlayerCommand.PUSH_MODE).ToString()),
                            String.Format("To PULL : <{0}>.", s_KeyBindings.Get(PlayerCommand.PULL_MODE).ToString())
                        };
                    break;

                case AdvisorHint.RAIN:
                    title = "RAIN";
                    body = new string[] {
                            "It is raining. Rain has various effects.",
                            "Livings vision is reduced.",
                            "Firearms have more chance to jam.",
                            "Scents evaporate faster."
                        };
                    break;

                case AdvisorHint.SPRAYS_PAINT:
                    title = "SPRAYS - SPRAYPAINT";
                    body = new string[] {
                            "You have found a can of spraypaint.",
                            "You can tag a symbol on walls and floors.",
                            "This is useful to mark some places and locations.",
                            String.Format("To SPRAY : equip the spray and press <{0}>.", s_KeyBindings.Get(PlayerCommand.USE_SPRAY).ToString())
                        };
                    break;

                case AdvisorHint.SPRAYS_SCENT:
                    title = "SPRAYS - SCENT SPRAY";
                    body = new string[] {
                            "You have found a scent spray.",
                            "You can spray some perfurme on yourself or another adjacent actor.",
                            "This is useful to confuse the undeads because they hunt using their smell.",
                            String.Format("To SPRAY : equip the spray and press <{0}>.", s_KeyBindings.Get(PlayerCommand.USE_SPRAY).ToString())
                        };
                    break;

                case AdvisorHint.STATE_HUNGRY:
                    title = "STATE - HUNGRY";
                    body = new string[] {
                            "You are HUNGRY.",
                            "If you become starved you can die!",
                            "You should EAT soon.",
                            "To eat, just USE a food item, such as groceries.",
                            "Read the manual for more explanations on hunger."
                        };
                    break;

                case AdvisorHint.STATE_SLEEPY:
                    title = "STATE - SLEEPY";
                    body = new string[] {
                            "You are SLEEPY.",
                            "This is bad for you!",
                            "You have a number of penalties.",
                            "You should find a place to SLEEP.",
                            "Couches are good places to sleep.",
                            String.Format("To SLEEP : <{0}>.", s_KeyBindings.Get(PlayerCommand.SLEEP).ToString()),
                            "Read the manual for more explanations on sleep."
                        };
                    break;

                case AdvisorHint.WEAPON_FIRE:
                    title = "FIRING A WEAPON";
                    body = new string[] {
                            "You can fire your equiped ranged weapon.",
                            "You need to have valid targets.",
                            "To fire on a target you need ammunitions and a clear line of fine.",
                            "The target must be within the weapon range.",
                            "The closer the target is, the easier it is to hit and it does slightly more damage.",
                            String.Format("To FIRE : <{0}>.", s_KeyBindings.Get(PlayerCommand.FIRE_MODE).ToString()),
                            "When firing you can switch to rapid fire mode : you will shoot twice but at reduced accuracy.",
                            "Remember you need to have visible enemies to fire at.",
                            "Read the manual for more explanation about firing and ranged weapons."
                        };
                    break;

                case AdvisorHint.WEAPON_RELOAD:
                    title = "RELOADING A WEAPON";
                    body = new string[] {
                            "You can reload your equiped ranged weapon.",
                            "To RELOAD, just USE a compatible ammo item.",
                        };
                    break;

                // alpha10 new hints

                case AdvisorHint.SANITY:  // sanity
                    title = "SANITY";
                    body = new string[] {
                        "You should care about your SANITY.",
                        "If it gets too low, you can go insane.",
                        "Living in this horrible world and seing horrible things will lower your sanity.",
                        "You can recover sanity by :",
                        "- Talking to people.",
                        "- Having followers you trust.",
                        "- Killing undeads.",
                        "- Using entertainment items.",
                        "- Taking pills."
                    };
                    break;

                case AdvisorHint.INFECTION:
                    title = "INFECTION";
                    body = new string[] {
                        "You are INFECTED!",
                        "Most undeads bites are infectious.",
                        "A low infection value will make you sick.",
                        "A full infection value is death.",
                        "Infection only worsen when you are biten.",
                        "Cure the infection with appropriate meds."
                    };
                    break;

                case AdvisorHint.TRAPS:
                    title = "TRAPS";
                    body = new string[] {
                        "You are carrying TRAPS.",
                        "Traps are a good way to protect places.",
                        "Drop activated traps on tiles.",
                        "Some traps are activated by dropping them.",
                        "Other traps need to be activated before being dropped.",
                        "You are always safe from your own traps.",
                        "Traps layed by your followers are also safe."
                    };
                    break;

                default:
                    throw new ArgumentOutOfRangeException("unhandled hint");
            }
        }

        void ShowAdvisorHint(AdvisorHint hint)
        {
            string title;
            string[] body;

            GetAdvisorHintText(hint, out title, out body);
            ShowAdvisorMessage(title, body);
        }

        void ShowAdvisorMessage(string title, string[] lines)
        {
            // clear.
            ClearMessages();
            ClearOverlays();

            // tell.
            string[] text = new string[lines.Length + 2];
            text[0] = "HINT : " + title;
            Array.Copy(lines, 0, text, 1, lines.Length);
            text[lines.Length + 1] = String.Format("(hint {0}/{1})", s_Hints.CountAdvisorHintsGiven(), (int)AdvisorHint._COUNT);
            AddOverlay(new OverlayPopup(text, Color.White, Color.White, Color.Black, new Point(0, 0)));

            // wait.
            ClearMessages();
            AddMessage(new Message("You can disable the advisor in the options screen.", m_Session.WorldTime.TurnCounter, Color.White));
            AddMessage(new Message(String.Format("To show the options screen : <{0}>.", s_KeyBindings.Get(PlayerCommand.OPTIONS_MODE).ToString()), m_Session.WorldTime.TurnCounter, Color.White));
            AddMessagePressEnter();

            // clear.
            ClearMessages();
            ClearOverlays();
            RedrawPlayScreen();
        }
        #endregion
        #region Input helpers
        void WaitKeyOrMouse(out KeyEventArgs key, out Point mousePos, out MouseButtons? mouseButtons)
        {
            PlayerInputEvent input = m_InputReader.Read(null);
            key = input.Key;
            mousePos = input.MousePosition;
            mouseButtons = input.MouseButtons;
        }

        /// <summary>
        /// Loop input until Exit/Cancel or a direction command is issued.
        /// </summary>
        /// <returns>null if Exit/Cancel</returns>
        Direction WaitDirectionOrCancel()
        {
            for (; ; )
            {
                KeyEventArgs key;
                Point mousePos;
                MouseButtons? buttons;
                WaitKeyOrMouse(out key, out mousePos, out buttons);
                if (key != null)
                {
                    if (key.KeyCode == Keys.Escape) return null;
                    Direction keyboardDirection = CommandToDirection(InputTranslator.KeyToCommand(key));
                    if (keyboardDirection != null) return keyboardDirection;
                }
                else if (m_Player != null)
                {
                    if (buttons == MouseButtons.Right) return null;
                    Direction mouseDirection = DirectionFromMouseTarget(m_Player.Location.Position,
                        MouseToMap(mousePos), m_MapViewRect, buttons);
                    if (mouseDirection != null) return mouseDirection;
                }
            }
        }

        internal static Direction DirectionFromMouseTarget(Point origin, Point target,
            Rectangle view, MouseButtons? buttons)
        {
            if (buttons != MouseButtons.Left || !view.Contains(target)) return null;
            Point offset = new Point(target.X - origin.X, target.Y - origin.Y);
            if (offset.X == 0 && offset.Y == 0) return Direction.NEUTRAL;
            return Direction.FromVector(offset);
        }

        void WaitEnter()
        {
            for (; ; )
            {
                KeyEventArgs inKey = m_UI.UI_WaitKey();
                if (inKey.KeyCode == Keys.Enter)
                    return;
            }
        }

        void WaitEscape()
        {
            for (; ; )
            {
                KeyEventArgs inKey = m_UI.UI_WaitKey();
                if (inKey.KeyCode == Keys.Escape)
                    return;
            }
        }

        /// <summary>
        /// -1 if no choice.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        int KeyToChoiceNumber(Keys key)
        {
            switch (key)
            {
                case Keys.NumPad0:
                case Keys.D0:
                    return 0;

                case Keys.NumPad1:
                case Keys.D1:
                    return 1;

                case Keys.NumPad2:
                case Keys.D2:
                    return 2;

                case Keys.NumPad3:
                case Keys.D3:
                    return 3;

                case Keys.NumPad4:
                case Keys.D4:
                    return 4;

                case Keys.NumPad5:
                case Keys.D5:
                    return 5;

                case Keys.NumPad6:
                case Keys.D6:
                    return 6;

                case Keys.NumPad7:
                case Keys.D7:
                    return 7;

                case Keys.NumPad8:
                case Keys.D8:
                    return 8;

                case Keys.NumPad9:
                case Keys.D9:
                    return 9;

                default:
                    return -1;
            }
        }

        bool WaitYesOrNo()
        {
            for (; ; )
            {
                KeyEventArgs inKey = m_UI.UI_WaitKey();
                if (inKey.KeyCode == Keys.Y)
                    return true;
                else if (inKey.KeyCode == Keys.N || inKey.KeyCode == Keys.Escape)
                    return false;
            }
        }
        #endregion
    }
}
