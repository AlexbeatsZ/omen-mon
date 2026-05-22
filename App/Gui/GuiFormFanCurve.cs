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

    // Unified Tmax -> fan level manager.
    public class GuiFormFanCurve : Form {

        private Button BtnCancel;
        private Button BtnDelete;
        private Button BtnNew;
        private Button BtnSave;
        private ComboBox CmbProgram;
        private DataGridView Grid;
        private Label LblHint;
        private Panel Chart;

        public string ProgramName { get; private set; }

        public GuiFormFanCurve(string programName) {

            this.ProgramName = programName;

            this.BtnCancel = new Button();
            this.BtnDelete = new Button();
            this.BtnNew = new Button();
            this.BtnSave = new Button();
            this.CmbProgram = new ComboBox();
            this.Grid = new DataGridView();
            this.LblHint = new Label();
            this.Chart = new Panel();

            Initialize();
            LoadProgramList();
            LoadProgram(this.ProgramName);

        }

        private void Initialize() {

            this.Text = "风扇方案管理";
            this.Icon = OmenMon.Resources.Icon;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new Size(620, 430);

            this.Chart.Location = new Point(12, 12);
            this.Chart.Size = new Size(596, 190);
            this.Chart.BorderStyle = BorderStyle.FixedSingle;
            this.Chart.BackColor = Color.White;
            this.Chart.Paint += EventChartPaint;

            this.LblHint.Location = new Point(12, 208);
            this.LblHint.Size = new Size(596, 20);
            this.LblHint.Text = "最高温度 Tmax -> 统一风扇挡位";
            this.LblHint.TextAlign = ContentAlignment.MiddleLeft;

            this.Grid.Location = new Point(12, 232);
            this.Grid.Size = new Size(596, 92);
            this.Grid.AllowUserToAddRows = false;
            this.Grid.AllowUserToDeleteRows = false;
            this.Grid.AllowUserToResizeRows = false;
            this.Grid.RowHeadersVisible = true;
            this.Grid.RowHeadersWidth = 86;
            this.Grid.ColumnHeadersVisible = false;
            this.Grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            this.Grid.EditMode = DataGridViewEditMode.EditOnEnter;
            this.Grid.CellValueChanged += delegate { this.Chart.Invalidate(); };
            this.Grid.CurrentCellDirtyStateChanged += delegate {
                if(this.Grid.IsCurrentCellDirty)
                    this.Grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            Label labelProgram = new Label();
            labelProgram.Location = new Point(12, 340);
            labelProgram.Size = new Size(44, 22);
            labelProgram.Text = "方案:";
            labelProgram.TextAlign = ContentAlignment.MiddleLeft;

            this.CmbProgram.DropDownStyle = ComboBoxStyle.DropDownList;
            this.CmbProgram.Location = new Point(58, 340);
            this.CmbProgram.Size = new Size(226, 21);
            this.CmbProgram.SelectionChangeCommitted += delegate {
                if(this.CmbProgram.SelectedItem != null)
                    LoadProgram((string) this.CmbProgram.SelectedItem);
            };

            this.BtnNew.Location = new Point(290, 338);
            this.BtnNew.Size = new Size(75, 24);
            this.BtnNew.Text = "新建";
            this.BtnNew.Click += EventActionNew;

            this.BtnSave.Location = new Point(371, 338);
            this.BtnSave.Size = new Size(75, 24);
            this.BtnSave.Text = "保存";
            this.BtnSave.Click += EventActionSave;

            this.BtnDelete.Location = new Point(452, 338);
            this.BtnDelete.Size = new Size(75, 24);
            this.BtnDelete.Text = "删除";
            this.BtnDelete.Click += EventActionDelete;

            this.BtnCancel.Location = new Point(533, 338);
            this.BtnCancel.Size = new Size(75, 24);
            this.BtnCancel.Text = "关闭";
            this.BtnCancel.Click += delegate {
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            this.Controls.Add(this.Chart);
            this.Controls.Add(this.LblHint);
            this.Controls.Add(this.Grid);
            this.Controls.Add(labelProgram);
            this.Controls.Add(this.CmbProgram);
            this.Controls.Add(this.BtnNew);
            this.Controls.Add(this.BtnSave);
            this.Controls.Add(this.BtnDelete);
            this.Controls.Add(this.BtnCancel);

        }

        private void LoadProgramList() {

            this.CmbProgram.BeginUpdate();
            this.CmbProgram.Items.Clear();
            foreach(string name in Config.FanProgram.Keys)
                this.CmbProgram.Items.Add(name);
            this.CmbProgram.EndUpdate();

        }

        private void LoadProgram(string name) {

            if(!Config.FanProgram.ContainsKey(name))
                return;

            this.ProgramName = name;
            this.CmbProgram.SelectedItem = name;
            this.Grid.Columns.Clear();
            this.Grid.Rows.Clear();

            FanProgramData program = Config.FanProgram[name];
            bool merged = false;

            foreach(byte temperature in program.Level.Keys)
                this.Grid.Columns.Add("T" + temperature.ToString(), "");

            this.Grid.Rows.Add(2);
            this.Grid.Rows[0].HeaderCell.Value = "温度";
            this.Grid.Rows[1].HeaderCell.Value = "风扇挡位";

            int i = 0;
            foreach(byte temperature in program.Level.Keys) {
                byte cpu = program.Level[temperature][0];
                byte gpu = program.Level[temperature][1];
                if(cpu != gpu)
                    merged = true;

                this.Grid.Rows[0].Cells[i].Value = temperature.ToString();
                this.Grid.Rows[1].Cells[i].Value = Math.Max(cpu, gpu).ToString();
                i++;
            }

            this.LblHint.Text = merged ?
                "最高温度 Tmax -> 统一风扇挡位；旧方案 CPU/GPU 挡位不同，已按较高挡位合并显示" :
                "最高温度 Tmax -> 统一风扇挡位";

            this.Chart.Invalidate();

        }

        private void EventActionNew(object sender, EventArgs e) {

            string baseName = "Custom";
            string name = baseName;
            int i = 1;
            while(Config.FanProgram.ContainsKey(name))
                name = baseName + i++.ToString();

            SortedDictionary<byte, byte[]> levels = new SortedDictionary<byte, byte[]>();
            byte[] t = new byte[] { 0, 60, 70, 78, 85, 90, 95 };
            byte[] f = new byte[] { 21, 21, 25, 30, 40, 48, 55 };
            for(int n = 0; n < t.Length; n++)
                levels[t[n]] = new byte[] { f[n], f[n] };

            Config.FanProgram[name] = new FanProgramData(
                name,
                BiosData.FanMode.Default,
                BiosData.GpuPowerLevel.Medium,
                levels);

            LoadProgramList();
            LoadProgram(name);

        }

        private void EventActionDelete(object sender, EventArgs e) {

            if(this.CmbProgram.SelectedItem == null)
                return;

            string name = (string) this.CmbProgram.SelectedItem;
            if(IsBuiltInProgram(name)) {
                MessageBox.Show(this, "内置默认方案不允许删除。", "删除方案",
                    MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if(MessageBox.Show(this, "删除方案: " + name + "?", "删除方案",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            Config.FanProgram.Remove(name);
            Config.Save();
            LoadProgramList();
            if(this.CmbProgram.Items.Count > 0)
                LoadProgram((string) this.CmbProgram.Items[0]);

        }

        private void EventActionSave(object sender, EventArgs e) {

            try {
                SortedDictionary<byte, byte[]> levels = ReadLevels();
                string name = (string) this.CmbProgram.SelectedItem;
                FanProgramData old = Config.FanProgram[name];

                Config.FanProgram[name] = new FanProgramData(
                    name,
                    old.FanMode,
                    old.GpuPower,
                    levels);

                Config.Save();
                this.ProgramName = name;
                this.DialogResult = DialogResult.OK;
                LoadProgram(name);

            } catch(Exception ex) {
                MessageBox.Show(this, ex.Message, "保存风扇方案",
                    MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }

        }

        private SortedDictionary<byte, byte[]> ReadLevels() {

            SortedDictionary<byte, byte[]> levels = new SortedDictionary<byte, byte[]>();
            int lastTemperature = -1;
            bool anyNonZero = false;
            bool highTempLowFan = false;

            for(int i = 0; i < this.Grid.Columns.Count; i++) {
                int temperature = Convert.ToInt32(this.Grid.Rows[0].Cells[i].Value);
                int level = Convert.ToInt32(this.Grid.Rows[1].Cells[i].Value);

                if(temperature < 0 || temperature > 100)
                    throw new ArgumentOutOfRangeException("温度必须是 0-100 范围内整数。");
                if(temperature <= lastTemperature)
                    throw new ArgumentOutOfRangeException("温度节点必须严格递增。");
                if(level < 0 || level > 255)
                    throw new ArgumentOutOfRangeException("风扇挡位必须是 0-255 范围内整数。");

                if(level != 0) {
                    anyNonZero = true;
                    level = Conv.GetConstrained(level, Config.FanLevelMin, Config.FanLevelMax);
                }
                if(temperature >= 80 && level < 40)
                    highTempLowFan = true;

                levels[(byte) temperature] = new byte[] { (byte) level, (byte) level };
                lastTemperature = temperature;
            }

            if(levels.Count == 0 || !anyNonZero)
                throw new ArgumentOutOfRangeException("不允许保存全 0 风扇挡位。");

            if(highTempLowFan
                && MessageBox.Show(this, "80°C 以上风扇挡位仍低于 40，确定保存?",
                    "保存风扇方案", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                throw new OperationCanceledException("已取消保存。");

            return levels;

        }

        private void EventChartPaint(object sender, PaintEventArgs e) {

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(46, 20, this.Chart.Width - 68, this.Chart.Height - 52);
            using(Pen axis = new Pen(Color.FromArgb(120, 120, 120)))
                e.Graphics.DrawRectangle(axis, r);

            using(Font font = new Font(SystemFonts.MessageBoxFont.FontFamily, 8)) {
                e.Graphics.DrawString("Tmax", font, Brushes.DimGray, r.Right - 30, r.Bottom + 6);
                e.Graphics.DrawString("Level", font, Brushes.DimGray, 6, 4);
            }

            List<Point> points = new List<Point>();
            for(int i = 0; i < this.Grid.Columns.Count; i++)
                try {
                    int temperature = Convert.ToInt32(this.Grid.Rows[0].Cells[i].Value);
                    int level = Convert.ToInt32(this.Grid.Rows[1].Cells[i].Value);
                    points.Add(new Point(
                        r.Left + temperature * r.Width / 100,
                        r.Bottom - Conv.GetConstrained(level, 0, Config.FanLevelMax) * r.Height / Config.FanLevelMax));
                } catch { }

            if(points.Count == 0)
                return;

            using(Pen line = new Pen(Color.FromArgb(0, 102, 204), 3)) {
                for(int i = 0; i < points.Count; i++) {
                    if(i == 0) {
                        e.Graphics.DrawLine(line, r.Left, points[i].Y, points[i].X, points[i].Y);
                    } else {
                        e.Graphics.DrawLine(line, points[i - 1].X, points[i - 1].Y, points[i].X, points[i - 1].Y);
                        e.Graphics.DrawLine(line, points[i].X, points[i - 1].Y, points[i].X, points[i].Y);
                    }
                }
                e.Graphics.DrawLine(line, points[points.Count - 1].X, points[points.Count - 1].Y, r.Right, points[points.Count - 1].Y);
            }

        }

        private bool IsBuiltInProgram(string name) {
            return name == "Silent"
                || name == "OmenBalanced"
                || name == "OmenPerformance"
                || name == "OmenEco"
                || name == "Power"
                || name == "CoolBoost";
        }

    }

}
