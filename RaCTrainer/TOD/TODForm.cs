using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace racman.TOD
{
    public partial class TODForm : Form
    {
        static ModLoaderForm modLoaderForm;
        public Form InputDisplay;
        public tod game;

        private bool isStartingAutosplitter;
        public Form ConfigureCombos;

        public TODForm(tod game)
        {
            this.game = game;
            InitializeComponent();

            if (this.game.HasInputDisplay)
            {
                this.game.SetupInputDisplayMemorySubs();
            }
        }

        private void patchLoaderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if ((Application.OpenForms["ModLoaderForm"] as ModLoaderForm) != null)
            {
                modLoaderForm.Activate();
            }
            else
            {
                modLoaderForm = new ModLoaderForm();
                modLoaderForm.Show();
            }
        }

        private void memoryUtilitiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MemoryForm memoryForm = new MemoryForm();
            memoryForm.Show();
        }

        private void levelFlagsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FormLevelFlags form = new FormLevelFlags(game, 0x10211398, tod.LevelFlags, tod.LevelFlagPlanetOrder);
            form.Show();
        }

        private void planets_comboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var api = game.api;
            var pid = api.getCurrentPID();

            api.WriteMemory(pid, tod.addr.savePlanetId, new byte[] { (byte)planets_comboBox.SelectedIndex });
        }

        private void TODForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            game.api.Disconnect();
            if (!isStartingAutosplitter)
                Application.Exit();
        }

        private void buttonStartAutosplitter_Click(object sender, EventArgs e)
        {
            /*
             * The old autosplitter asked which ASS/GASS route was being used,
             * then disconnected its memory subscriptions around autoscrollers.
             * The SPRX autosplitter now survives game exits itself, and split
             * route filtering is handled by LiveSplit, so that flow is no
             * longer needed.
             *
             * var choiceForm = new CategoryChoiceForm();
             * choiceForm.ShowDialog();
             * ...
             */
            if (game.api is Ratchetron)
            {
                isStartingAutosplitter = true;
                Close();

                try
                {
                    func.PrepareSPRX(AttachPS3Form.ip, "tod-autosplitter.sprx", 5);

                    ToDAutosplitterForm autosplitterForm =
                        new ToDAutosplitterForm(AttachPS3Form.ip);
                    autosplitterForm.Show();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Failed to start autosplitter:\n" + ex.Message,
                        "Autosplitter Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    Application.Exit();
                }
            }
            else
            {
                MessageBox.Show(
                    "SPRX autosplitter is not supported on RPCS3.",
                    "Autosplitter Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void DieButtonClick(object sender, EventArgs e)
        {
            game.KillYourself();
        }

        private void ChallegeModeButtonClick(object sender, EventArgs e)
        {
            game.SetChallengeMode();
        }

        private void ResetAllGoldBoltsClick(object sender, EventArgs e)
        {
            game.ResetAllGoldBolts();
        }

        private void GodRatchetClick(object sender, EventArgs e)
        {
            game.SetGodRatchet();
        }

        private void ResetAllRYNOPlans(object sender, EventArgs e)
        {
            game.ResetRYNOPlans();
        }

        private void PlayerValuesFormClick(object sender, EventArgs e)
        {
            PlayerValues form = new PlayerValues(game);
            form.Show();
        }

        private void ArmorSkinsFormClick(object sender, EventArgs e)
        {
            ArmorSkinsForm form = new ArmorSkinsForm(game);
            form.Show();
        }

        private void WeaponsFormClick(object sender, EventArgs e)
        {
            WeaponForm form = new WeaponForm(game); 
            form.Show();
        }

        private void GadgetsFormClick(object sender, EventArgs e)
        {
            GadgetForm form = new GadgetForm(game);
            form.Show();
        }

        private void ResetGroovitronStorageClick(object sender, EventArgs e)
        {
            game.ResetGoldenGrovitronStorage();
        }

        private void SavePositionClick(object sender, EventArgs e)
        {
            int temp = RadioButtonChoice();
            if (temp == -1)
                return;
            game.SavePosition(temp);
        }
        private void LoadPositionClick(object sender, EventArgs e)
        {
            int temp = RadioButtonChoice();
            if (temp == -1)
                return;
            game.LoadPosition(temp);
        }

        private int RadioButtonChoice()
        {
            if (radioButton1.Checked)
            {
                return 0;
            }
            else if (radioButton2.Checked)
            {
                return 1;
            }
            else if (radioButton3.Checked)
            {
                return 2;
            }
            else
            {
                MessageBox.Show("Choose a button you beech");
                return -1;
            }
        }

        private void InputViewerClick(object sender, EventArgs e)
        {
            if (InputDisplay == null)
            {
                InputDisplay = new InputDisplay();
                InputDisplay.FormClosed += InputDisplay_FormClosed;
                InputDisplay.Show();
            }
        }

        private void InputDisplay_FormClosed(object sender, FormClosedEventArgs e)
        {
            InputDisplay = null;
        }

        private void configureButtonCombosToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ConfigureCombos == null)
            {
                ConfigureCombos = new ConfigureCombos();
                ConfigureCombos.FormClosed += ConfigureCombos_FormClosed;
                ConfigureCombos.Show();
                game.InputsTimer.Enabled = false;
            }
        }

        private void ConfigureCombos_FormClosed(object sender, FormClosedEventArgs e)
        {
            ConfigureCombos = null;
            if (checkBox1.Checked)
                game.InputsTimer.Enabled = true;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
                game.InputsTimer.Enabled = true;
            else
                game.InputsTimer.Enabled = false;
        }

        private void configureButtonCombosToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            if (ConfigureCombos == null)
            {
                ConfigureCombos = new ConfigureCombos();
                ConfigureCombos.FormClosed += ConfigureCombos_FormClosed;
                ConfigureCombos.Show();
                game.InputsTimer.Enabled = false;
            }
        }
    }
}
