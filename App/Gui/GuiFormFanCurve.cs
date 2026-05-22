  //\\   OmenMon: Hardware Monitoring & Control Utility
 //  \\  Copyright © 2023-2024 Piotr Szczepański * License: GPL3
     //  https://omenmon.github.io/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using OmenMon.Hardware.Bios;
using OmenMon.Hardware.Platform;
using OmenMon.Library;

namespace OmenMon.AppGui {

    // Simple editor for configured fan program curves
    public class GuiFormFanCurve : Form {

#region Data
        private Button BtnCancel;
        private Button BtnSave;
        private ComboBox CmbFanMode;
        private ComboBox CmbGpuPower;
        private DataGridView Grid;
        private Label LblFanMode;
        private Label LblGpuPower;

        public string ProgramName { get; private set; }
#endregion

#region Construction
        public GuiFormFanCurve(string programName) {

            this.ProgramName = programName;

            this.BtnCancel = new Button();
            this.BtnSave = new Button();
            this.CmbFanMode = new ComboBox();
            this.CmbGpuPower = new ComboBox();
            this.Grid = new DataGridView();
            this.LblFanMode = new Label();
            this.LblGpuPower = new Label();

            Initialize();
            LoadProgram();

        }
#endregion

#region Initialization
        private void Initialize() {

            this.Text = Config.Locale.Get(Config.L_GUI_MAIN + Gui.G_FAN + "ProgEdit") + ": " + this.ProgramName;
            this.Icon = OmenMon.Resources.Icon;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new Size(420, 360);

            this.LblFanMode.Location = new Point(12, 14);
            this.LblFanMode.Size = new Size(80, 20);
            this.LblFanMode.Text = Config.Locale.Get(Config.L_CLI_PROG + "FanMode");

            this.CmbFanMode.DropDownStyle = ComboBoxStyle.DropDownList;
            this.CmbFanMode.Location = new Point(92, 11);
            this.CmbFanMode.Size = new Size(120, 21);
            this.CmbFanMode.DataSource = Enum.GetNames(typeof(BiosData.FanMode));

            this.LblGpuPower.Location = new Point(220, 14);
            this.LblGpuPower.Size = new Size(80, 20);
            this.LblGpuPower.Text = Config.Locale.Get(Config.L_CLI_PROG + "GpuPower");

            this.CmbGpuPower.DropDownStyle = ComboBoxStyle.DropDownList;
            this.CmbGpuPower.Location = new Point(300, 11);
            this.CmbGpuPower.Size = new Size(105, 21);
            this.CmbGpuPower.DataSource = Enum.GetNames(typeof(BiosData.GpuPowerLevel));

            this.Grid.Location = new Point(12, 42);
            this.Grid.Size = new Size(393, 270);
            this.Grid.AllowUserToAddRows = true;
            this.Grid.AllowUserToDeleteRows = true;
            this.Grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            this.Grid.RowHeadersWidth = 28;
            this.Grid.Columns.Add("Temperature", Config.Locale.Get(Config.L_PROG + "T") + " (C)");
            this.Grid.Columns.Add("Cpu", Config.Locale.Get(Config.L_GUI_MAIN + "Fan0"));
            this.Grid.Columns.Add("Gpu", Config.Locale.Get(Config.L_GUI_MAIN + "Fan1"));

            this.BtnSave.Location = new Point(249, 324);
            this.BtnSave.Size = new Size(75, 24);
            this.BtnSave.Text = Config.Locale.Get(Config.L_GUI + Gui.T_BTN + "Set");
            this.BtnSave.Click += EventActionSave;

            this.BtnCancel.Location = new Point(330, 324);
            this.BtnCancel.Size = new Size(75, 24);
            this.BtnCancel.Text = Config.Locale.Get(Config.L_GUI + Gui.T_BTN + "Cancel");
            this.BtnCancel.Click += delegate {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            this.Controls.Add(this.LblFanMode);
            this.Controls.Add(this.CmbFanMode);
            this.Controls.Add(this.LblGpuPower);
            this.Controls.Add(this.CmbGpuPower);
            this.Controls.Add(this.Grid);
            this.Controls.Add(this.BtnSave);
            this.Controls.Add(this.BtnCancel);

        }
#endregion

#region Data
        private void LoadProgram() {

            FanProgramData program = Config.FanProgram[this.ProgramName];
            this.CmbFanMode.SelectedItem = Enum.GetName(typeof(BiosData.FanMode), program.FanMode);
            this.CmbGpuPower.SelectedItem = Enum.GetName(typeof(BiosData.GpuPowerLevel), program.GpuPower);

            foreach(byte temperature in program.Level.Keys)
                this.Grid.Rows.Add(
                    temperature.ToString(),
                    program.Level[temperature][0].ToString(),
                    program.Level[temperature][1].ToString());

        }

        private void EventActionSave(object sender, EventArgs e) {

            try {
                SortedDictionary<byte, byte[]> levels = new SortedDictionary<byte, byte[]>();

                foreach(DataGridViewRow row in this.Grid.Rows) {
                    if(row.IsNewRow)
                        continue;

                    byte temperature = Convert.ToByte(row.Cells[0].Value);
                    byte cpu = Convert.ToByte(row.Cells[1].Value);
                    byte gpu = Convert.ToByte(row.Cells[2].Value);

                    levels[temperature] = new byte[] {
                        ConstrainFanLevel(cpu),
                        ConstrainFanLevel(gpu) };
                }

                if(levels.Count == 0)
                    throw new ArgumentOutOfRangeException();

                Config.FanProgram[this.ProgramName] =
                    new FanProgramData(
                        this.ProgramName,
                        (BiosData.FanMode) Enum.Parse(typeof(BiosData.FanMode), (string) this.CmbFanMode.SelectedItem),
                        (BiosData.GpuPowerLevel) Enum.Parse(typeof(BiosData.GpuPowerLevel), (string) this.CmbGpuPower.SelectedItem),
                        levels);

                this.DialogResult = DialogResult.OK;
                this.Close();

            } catch {

                MessageBox.Show(
                    this,
                    Config.Locale.Get(Config.L_GUI_MAIN + Gui.G_FAN + "ProgEditInvalid"),
                    Config.Locale.Get(Config.L_GUI_MAIN + Gui.G_FAN + "ProgEdit"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);

            }

        }

        private byte ConstrainFanLevel(byte value) {

            if(value == 0)
                return value;

            if(value < Config.FanLevelMin)
                return (byte) Config.FanLevelMin;

            if(value > Config.FanLevelMax)
                return (byte) Config.FanLevelMax;

            return value;

        }
#endregion

    }

}
