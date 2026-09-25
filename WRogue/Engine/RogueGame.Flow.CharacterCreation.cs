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
        #region Character creation
        bool HandleNewCharacter()
        {
            /////////////////
            // Reset session
            /////////////////
            m_Session.Reset();
            m_Rules = new Rules(m_Session.GameDiceRoller);

            ///////////////
            // Game Mode //
            ///////////////
            if (!HandleNewGameMode())
                return false;
            m_Rules = new Rules(m_Session.GameDiceRoller);
            DiceRoller roller = m_Session.GameDiceRoller;

            ////////////////////////
            // Choose living/undead
            ////////////////////////
            bool isUndead;
            if (!HandleNewCharacterRace(roller, out isUndead))
                return false;
            m_CharGen.IsUndead = isUndead;

            /////////////////////////////
            // Choose gender/undead type
            /////////////////////////////
            if (isUndead)
            {
                GameActors.IDs modelID;
                if (!HandleNewCharacterUndeadType(roller, out modelID))
                    return false;
                m_CharGen.UndeadModel = modelID;
            }
            else
            {
                bool isMale;
                if (!HandleNewCharacterGender(roller, out isMale))
                    return false;
                m_CharGen.IsMale = isMale;
            }

            /////////////////////////////
            // Choose skill (living only)
            /////////////////////////////
            if (!isUndead)
            {
                Skills.IDs skID;
                if (!HandleNewCharacterSkill(roller, out skID))
                    return false;
                m_CharGen.StartingSkill = skID;
                // scoring : starting skill.
                m_Session.Scoring.StartingSkill = skID;
            }
            else
            {
                // udead.
            }

            // done
            return true;
        }

        bool HandleNewGameMode()
        {
            return HandleNewGamePreset();
        }

        bool HandleNewCharacterRace(DiceRoller roller, out bool isUndead)
        {
            string[] menuEntries = new string[]
            {
                "*Random*",
                "Living",
                "Undead"
            };
            string[] descs = new string[]
            {
                "(picks a race at random for you)",
                "Try to survive.",
                "Eat brains and die again."
            };

            isUndead = false;
            bool loop = true;
            bool choiceDone = false;
            int selected = 0;
            do
            {
                // display.
                m_UI.UI_Clear(Color.Black);
                int gx, gy;
                gx = gy = 0;
                m_UI.UI_DrawStringBold(Color.Yellow, String.Format("[{0}] New Character - Choose Race", m_Session.GamePreset.Name), gx, gy);
                gy += 2 * BOLD_LINE_SPACING;
                DrawMenuOrOptions(selected, Color.White, menuEntries, Color.LightGray, descs, gx, ref gy);
                gy += 2 * BOLD_LINE_SPACING;

                DrawFootnote(Color.White, "cursor to move, ENTER to select, ESC to cancel");
                m_UI.UI_Repaint();

                // get menu action.
                KeyEventArgs key = m_UI.UI_WaitKey();
                switch (key.KeyCode)
                {
                    case Keys.Up:       // move up
                        if (selected > 0) --selected;
                        else selected = menuEntries.Length - 1;
                        break;
                    case Keys.Down:     // move down
                        selected = (selected + 1) % menuEntries.Length;
                        break;

                    case Keys.Escape:
                        choiceDone = false;
                        loop = false;
                        break;

                    case Keys.Enter:    // validate
                        {
                            switch (selected)
                            {
                                case 0: // random
                                    isUndead = roller.RollChance(50);

                                    gy += BOLD_LINE_SPACING;
                                    m_UI.UI_DrawStringBold(Color.White, String.Format("Race : {0}.", isUndead ? "Undead" : "Living"), gx, gy);
                                    gy += BOLD_LINE_SPACING;
                                    m_UI.UI_DrawStringBold(Color.Yellow, "Is that OK? Y to confirm, N to cancel.", gx, gy);
                                    m_UI.UI_Repaint();
                                    if (WaitYesOrNo())
                                    {
                                        choiceDone = true;
                                        loop = false;
                                    }
                                    break;

                                case 1: // living
                                    isUndead = false;
                                    choiceDone = true;
                                    loop = false;
                                    break;

                                case 2: // undead
                                    isUndead = true;
                                    choiceDone = true;
                                    loop = false;
                                    break;
                            }
                            break;
                        }
                }

            }
            while (loop);

            // done.
            return choiceDone;
        }

        bool HandleNewCharacterGender(DiceRoller roller, out bool isMale)
        {
            ActorModel maleModel = GameActors.MaleCivilian;
            ActorModel femaleModel = GameActors.FemaleCivilian;

            string[] menuEntries = new string[]
            {
                "*Random*",
                "Male",
                "Female"
            };
            string[] descs = new string[]
            {
                "(picks a gender at random for you)",
                String.Format("HP:{0:D2}  Def:{1:D2}  Dmg:{2:D1}", maleModel.StartingSheet.BaseHitPoints, maleModel.StartingSheet.BaseDefence.Value,  maleModel.StartingSheet.UnarmedAttack.DamageValue),
                String.Format("HP:{0:D2}  Def:{1:D2}  Dmg:{2:D1}", femaleModel.StartingSheet.BaseHitPoints, femaleModel.StartingSheet.BaseDefence.Value, femaleModel.StartingSheet.UnarmedAttack.DamageValue),
            };

            isMale = true;
            bool loop = true;
            bool choiceDone = false;
            int selected = 0;
            do
            {
                // display.
                m_UI.UI_Clear(Color.Black);
                int gx, gy;
                gx = gy = 0;
                m_UI.UI_DrawStringBold(Color.Yellow, String.Format("[{0}] New Living - Choose Gender", m_Session.GamePreset.Name), gx, gy);
                gy += 2 * BOLD_LINE_SPACING;
                DrawMenuOrOptions(selected, Color.White, menuEntries, Color.LightGray, descs, gx, ref gy);
                DrawFootnote(Color.White, "cursor to move, ENTER to select, ESC to cancel");
                m_UI.UI_Repaint();

                // get menu action.
                KeyEventArgs key = m_UI.UI_WaitKey();
                switch (key.KeyCode)
                {
                    case Keys.Up:       // move up
                        if (selected > 0) --selected;
                        else selected = menuEntries.Length - 1;
                        break;
                    case Keys.Down:     // move down
                        selected = (selected + 1) % menuEntries.Length;
                        break;

                    case Keys.Escape:
                        choiceDone = false;
                        loop = false;
                        break;

                    case Keys.Enter:    // validate
                        {
                            switch (selected)
                            {
                                case 0: // random
                                    isMale = roller.RollChance(50);

                                    gy += BOLD_LINE_SPACING;
                                    m_UI.UI_DrawStringBold(Color.White, String.Format("Gender : {0}.", isMale ? "Male" : "Female"), gx, gy);
                                    gy += BOLD_LINE_SPACING;
                                    m_UI.UI_DrawStringBold(Color.Yellow, "Is that OK? Y to confirm, N to cancel.", gx, gy);
                                    m_UI.UI_Repaint();
                                    if (WaitYesOrNo())
                                    {
                                        choiceDone = true;
                                        loop = false;
                                    }
                                    break;

                                case 1: // male
                                    isMale = true;
                                    choiceDone = true;
                                    loop = false;
                                    break;

                                case 2: // female
                                    isMale = false;
                                    choiceDone = true;
                                    loop = false;
                                    break;
                            }
                            break;
                        }
                }

            }
            while (loop);

            // done.
            return choiceDone;
        }

        string DescribeUndeadModelStatLine(ActorModel m)
        {
            return String.Format("HP:{0:D3}  Spd:{1:F2}  Atk:{2:D2}  Def:{3:D2}  Dmg:{4:D2}  FoV:{5:D1}  Sml:{6:F2}",
                m.StartingSheet.BaseHitPoints, m.DollBody.Speed / 100f,
                m.StartingSheet.UnarmedAttack.HitValue, m.StartingSheet.BaseDefence.Value, m.StartingSheet.UnarmedAttack.DamageValue,
                m.StartingSheet.BaseViewRange, m.StartingSheet.BaseSmellRating);
        }

        bool HandleNewCharacterUndeadType(DiceRoller roller, out GameActors.IDs modelID)
        {
            GamePreset preset = m_Session.GamePreset;
            List<GameActors.IDs> types = new List<GameActors.IDs>();
            if (preset.Skeletons) types.Add(GameActors.IDs.UNDEAD_SKELETON);
            if (preset.Shamblers) types.Add(GameActors.IDs.UNDEAD_ZOMBIE);
            if (preset.Zombified)
            {
                types.Add(GameActors.IDs.UNDEAD_MALE_ZOMBIFIED);
                types.Add(GameActors.IDs.UNDEAD_FEMALE_ZOMBIFIED);
            }
            if (preset.ZombieMasters) types.Add(GameActors.IDs.UNDEAD_ZOMBIE_MASTER);
            if (preset.RatZombies) types.Add(GameActors.IDs.UNDEAD_RAT_ZOMBIE);
            if (types.Count == 0) throw new InvalidOperationException("Preset has no playable undead types.");

            string[] menuEntries = new string[types.Count + 1];
            string[] descs = new string[menuEntries.Length];
            menuEntries[0] = "*Random*";
            descs[0] = "(picks an available type at random for you)";
            for (int i = 0; i < types.Count; i++)
            {
                ActorModel model = GameActors[types[i]];
                menuEntries[i + 1] = model.Name;
                descs[i + 1] = DescribeUndeadModelStatLine(model);
            }
            modelID = types[0];
            int selected = 0;
            while (true)
            {
                m_UI.UI_Clear(Color.Black);
                int gx = 0, gy = 0;
                m_UI.UI_DrawStringBold(Color.Yellow, String.Format("[{0}] New Undead - Choose Type", preset.Name), gx, gy);
                gy += 2 * BOLD_LINE_SPACING;
                DrawMenuOrOptions(selected, Color.White, menuEntries, Color.LightGray, descs, gx, ref gy);
                DrawFootnote(Color.White, "cursor to move, ENTER to select, ESC to cancel");
                m_UI.UI_Repaint();
                KeyEventArgs key = m_UI.UI_WaitKey();
                if (key.KeyCode == Keys.Escape) return false;
                if (key.KeyCode == Keys.Up) selected = (selected + menuEntries.Length - 1) % menuEntries.Length;
                if (key.KeyCode == Keys.Down) selected = (selected + 1) % menuEntries.Length;
                if (key.KeyCode != Keys.Enter) continue;
                if (selected == 0)
                {
                    modelID = types[roller.Roll(0, types.Count)];
                    gy += BOLD_LINE_SPACING;
                    m_UI.UI_DrawStringBold(Color.White, String.Format("Type : {0}.", GameActors[modelID].Name), gx, gy);
                    gy += BOLD_LINE_SPACING;
                    m_UI.UI_DrawStringBold(Color.Yellow, "Is that OK? Y to confirm, N to cancel.", gx, gy);
                    m_UI.UI_Repaint();
                    if (!WaitYesOrNo()) continue;
                }
                else modelID = types[selected - 1];
                m_CharGen.IsMale = modelID != GameActors.IDs.UNDEAD_FEMALE_ZOMBIFIED;
                return true;
            }
        }

        bool HandleNewCharacterSkill(DiceRoller roller, out Skills.IDs skID)
        {
            /////////////////////////////
            // Make table of all skills.
            /////////////////////////////
            Skills.IDs[] allSkills = new Skills.IDs[(int)Skills.IDs._LAST_LIVING + 1];
            string[] menuEntries = new string[allSkills.Length + 1];
            string[] skillDesc = new string[allSkills.Length + 1];
            menuEntries[0] = "*Random*";
            skillDesc[0] = "(picks a skill at random for you)";
            for (int i = (int)Skills.IDs._FIRST_LIVING; i < (int)Skills.IDs._LAST_LIVING + 1; i++)
            {
                allSkills[i] = (Skills.IDs)i;
                menuEntries[i + 1] = Skills.Name(allSkills[i]);
                skillDesc[i + 1] = String.Format("{0} max - {1}", Skills.MaxSkillLevel(i), DescribeSkillShort(allSkills[i]));
            }

            //////////////////////////
            // Loop until choice done
            //////////////////////////
            skID = Skills.IDs._FIRST;
            bool loop = true;
            bool choiceDone = false;
            int selected = 0;
            do
            {
                // display.
                m_UI.UI_Clear(Color.Black);
                int gx, gy;
                gx = gy = 0;
                m_UI.UI_DrawStringBold(Color.Yellow, String.Format("[{0}] New {1} Character - Choose Starting Skill",
                    m_Session.GamePreset.Name,
                    m_CharGen.IsMale ? "Male" : "Female"), gx, gy);
                gy += 2 * BOLD_LINE_SPACING;
                DrawMenuOrOptions(selected, Color.White, menuEntries, Color.LightGray, skillDesc, gx, ref gy);
                DrawFootnote(Color.White, "cursor to move, ENTER to select, ESC to cancel");
                m_UI.UI_Repaint();

                // get menu action.
                KeyEventArgs key = m_UI.UI_WaitKey();
                switch (key.KeyCode)
                {
                    case Keys.Up:       // move up
                        if (selected > 0) --selected;
                        else selected = menuEntries.Length - 1;
                        break;
                    case Keys.Down:     // move down
                        selected = (selected + 1) % menuEntries.Length;
                        break;

                    case Keys.Escape:
                        choiceDone = false;
                        loop = false;
                        break;

                    case Keys.Enter:    // validate
                        if (selected == 0) // random
                            skID = Skills.RollLiving(roller);
                        else
                            skID = (Skills.IDs)(selected - 1 + (int)Skills.IDs._FIRST);

                        gy += BOLD_LINE_SPACING;
                        m_UI.UI_DrawStringBold(Color.White, String.Format("Skill : {0}.", Skills.Name(skID)), gx, gy);
                        gy += BOLD_LINE_SPACING;
                        m_UI.UI_DrawStringBold(Color.Yellow, "Is that OK? Y to confirm, N to cancel.", gx, gy);
                        m_UI.UI_Repaint();
                        if (WaitYesOrNo())
                        {
                            choiceDone = true;
                            loop = false;
                        }
                        break;
                }
            }
            while (loop);

            // done.
            return choiceDone;
        }
        #endregion
    }
}
