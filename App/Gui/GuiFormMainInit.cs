  //\\   OmenMon: Hardware Monitoring & Control Utility
 //  \\  Copyright © 2023-2024 Piotr Szczepański * License: GPL3
     //  https://omenmon.github.io/

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using OmenMon.Library;

namespace OmenMon.AppGui {

    public partial class GuiFormMain : Form {

#region Form Components
        private Button BtnCpuApply;
        private Button BtnFanManage;
        private ButtonEx BtnFanSet;
        private Button BtnGpuApply;
        private Button BtnKbdColorPresetDel;
        private Button BtnKbdColorPresetSet;
        private Button BtnLogClear;
        private Button BtnLogCopy;
        private Button BtnLogExport;
        private CheckBox ChkKbdBacklight;
        private ComboBox CmbCpuPlan;
        private ComboBox CmbFanPlan;
        private ComboBox CmbGpuPlan;
        private ComboBox CmbKbdColorPreset;
        private GroupBox GrpConsole;
        private GroupBox GrpFanBars;
        private GroupBox GrpKbd;
        private GroupBox GrpPlans;
        private GroupBox GrpSys;
        private Label LblFan0Cap;
        private Label LblFan0Val;
        private Label LblFan1Cap;
        private Label LblFan1Val;
        private Label LblCpuPlanState;
        private Label LblFanPlanState;
        private Label LblGpuPlanState;
        internal PictureBox PicKbd;
        private RichTextBox RtfLog;
        private RichTextBox RtfSysInfo;
        private TabControl TabMain;
        private TabPage TabPerformance;
        private TabPage TabLighting;
        private TextBox TxtKbdColorVal;
        private ToolTip Tip;
        private TrackBar TrkFan0Lvl;
        private TrackBar TrkFan1Lvl;
#endregion

#region Initialization
        private void Initialize() {

            this.FormClosing += Config.GuiCloseWindowExit ?
                Context.Menu.EventActionExit : new FormClosingEventHandler(EventFormClosing);
            this.VisibleChanged += EventFormVisibleChanged;
            this.HelpButtonClicked += EventActionHelp;

            this.FigureFont = new Font(
                GdiFont.Get(0),
                Config.GuiFigureFontSize,
                FontStyle.Regular,
                GraphicsUnit.Pixel);

            this.BtnCpuApply = new Button();
            this.BtnFanManage = new Button();
            this.BtnFanSet = new ButtonEx();
            this.BtnGpuApply = new Button();
            this.BtnKbdColorPresetDel = new Button();
            this.BtnKbdColorPresetSet = new Button();
            this.BtnLogClear = new Button();
            this.BtnLogCopy = new Button();
            this.BtnLogExport = new Button();
            this.ChkKbdBacklight = new CheckBox();
            this.CmbCpuPlan = new ComboBox();
            this.CmbFanPlan = new ComboBox();
            this.CmbGpuPlan = new ComboBox();
            this.CmbKbdColorPreset = new ComboBox();
            this.GrpConsole = new GroupBox();
            this.GrpFanBars = new GroupBox();
            this.GrpKbd = new GroupBox();
            this.GrpPlans = new GroupBox();
            this.GrpSys = new GroupBox();
            this.LblFan0Cap = new Label();
            this.LblFan0Val = new Label();
            this.LblFan1Cap = new Label();
            this.LblFan1Val = new Label();
            this.LblCpuPlanState = new Label();
            this.LblFanPlanState = new Label();
            this.LblGpuPlanState = new Label();
            this.PicKbd = new PictureBox();
            this.RtfLog = new RichTextBox();
            this.RtfSysInfo = new RichTextBox();
            this.TabMain = new TabControl();
            this.TabPerformance = new TabPage();
            this.TabLighting = new TabPage();
            this.Tip = new ToolTip(this.Components);
            this.TrkFan0Lvl = new TrackBar();
            this.TrkFan1Lvl = new TrackBar();
            this.TxtKbdColorVal = new TextBox();

            this.TabPerformance.SuspendLayout();
            this.TabLighting.SuspendLayout();
            this.TabMain.SuspendLayout();
            this.GrpSys.SuspendLayout();
            this.GrpConsole.SuspendLayout();
            this.GrpFanBars.SuspendLayout();
            this.GrpPlans.SuspendLayout();
            this.GrpKbd.SuspendLayout();
            this.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize) this.TrkFan0Lvl).BeginInit();
            ((System.ComponentModel.ISupportInitialize) this.TrkFan1Lvl).BeginInit();
            ((System.ComponentModel.ISupportInitialize) this.PicKbd).BeginInit();

            this.GrpSys.Location = new Point(8, 8);
            this.GrpSys.Size = new Size(386, 222);
            this.GrpSys.Text = "系统状态 / 传感器信息";

            this.RtfSysInfo.BackColor = SystemColors.Control;
            this.RtfSysInfo.BorderStyle = BorderStyle.None;
            this.RtfSysInfo.Cursor = Cursors.Arrow;
            this.RtfSysInfo.DetectUrls = false;
            this.RtfSysInfo.Location = new Point(10, 20);
            this.RtfSysInfo.ReadOnly = true;
            this.RtfSysInfo.Size = new Size(366, 190);
            this.RtfSysInfo.TabStop = false;
            this.RtfSysInfo.WordWrap = true;
            this.RtfSysInfo.Text = "";
            this.GrpSys.Controls.Add(this.RtfSysInfo);

            this.GrpConsole.Location = new Point(8, 236);
            this.GrpConsole.Size = new Size(386, 236);
            this.GrpConsole.Text = "控制台输出 / 输入反馈";

            this.RtfLog.BackColor = Color.FromArgb(250, 250, 250);
            this.RtfLog.BorderStyle = BorderStyle.FixedSingle;
            this.RtfLog.DetectUrls = false;
            this.RtfLog.Font = new Font("Consolas", 9F);
            this.RtfLog.Location = new Point(10, 20);
            this.RtfLog.ReadOnly = true;
            this.RtfLog.Size = new Size(366, 174);
            this.RtfLog.WordWrap = false;

            this.BtnLogClear.Location = new Point(139, 202);
            this.BtnLogClear.Size = new Size(75, 24);
            this.BtnLogClear.Text = "清空";
            this.BtnLogClear.Click += EventActionLogClear;

            this.BtnLogCopy.Location = new Point(220, 202);
            this.BtnLogCopy.Size = new Size(75, 24);
            this.BtnLogCopy.Text = "复制";
            this.BtnLogCopy.Click += EventActionLogCopy;

            this.BtnLogExport.Location = new Point(301, 202);
            this.BtnLogExport.Size = new Size(75, 24);
            this.BtnLogExport.Text = "导出";
            this.BtnLogExport.Click += EventActionLogExport;

            this.GrpConsole.Controls.Add(this.RtfLog);
            this.GrpConsole.Controls.Add(this.BtnLogClear);
            this.GrpConsole.Controls.Add(this.BtnLogCopy);
            this.GrpConsole.Controls.Add(this.BtnLogExport);

            this.GrpFanBars.Location = new Point(402, 8);
            this.GrpFanBars.Size = new Size(154, 464);
            this.GrpFanBars.Text = "风扇挡位";

            this.LblFan0Cap.Location = new Point(22, 24);
            this.LblFan0Cap.Size = new Size(48, 18);
            this.LblFan0Cap.Text = "CPU";
            this.LblFan0Cap.TextAlign = ContentAlignment.MiddleCenter;

            this.LblFan1Cap.Location = new Point(84, 24);
            this.LblFan1Cap.Size = new Size(48, 18);
            this.LblFan1Cap.Text = "GPU";
            this.LblFan1Cap.TextAlign = ContentAlignment.MiddleCenter;

            this.TrkFan0Lvl.AutoSize = false;
            this.TrkFan0Lvl.Location = new Point(26, 48);
            this.TrkFan0Lvl.Maximum = Config.FanLevelMax;
            this.TrkFan0Lvl.Minimum = Config.FanLevelMin;
            this.TrkFan0Lvl.Orientation = Orientation.Vertical;
            this.TrkFan0Lvl.Size = new Size(38, 350);
            this.TrkFan0Lvl.TickFrequency = 5;

            this.TrkFan1Lvl.AutoSize = false;
            this.TrkFan1Lvl.Location = new Point(88, 48);
            this.TrkFan1Lvl.Maximum = Config.FanLevelMax;
            this.TrkFan1Lvl.Minimum = Config.FanLevelMin;
            this.TrkFan1Lvl.Orientation = Orientation.Vertical;
            this.TrkFan1Lvl.Size = new Size(38, 350);
            this.TrkFan1Lvl.TickFrequency = 5;
            this.TrkFan1Lvl.TickStyle = TickStyle.TopLeft;

            this.LblFan0Val.Font = this.FigureFont;
            this.LblFan0Val.Location = new Point(18, 404);
            this.LblFan0Val.Size = new Size(58, 28);
            this.LblFan0Val.TextAlign = ContentAlignment.MiddleCenter;

            this.LblFan1Val.Font = this.FigureFont;
            this.LblFan1Val.Location = new Point(80, 404);
            this.LblFan1Val.Size = new Size(58, 28);
            this.LblFan1Val.TextAlign = ContentAlignment.MiddleCenter;

            this.GrpFanBars.Controls.Add(this.LblFan0Cap);
            this.GrpFanBars.Controls.Add(this.LblFan1Cap);
            this.GrpFanBars.Controls.Add(this.TrkFan0Lvl);
            this.GrpFanBars.Controls.Add(this.TrkFan1Lvl);
            this.GrpFanBars.Controls.Add(this.LblFan0Val);
            this.GrpFanBars.Controls.Add(this.LblFan1Val);

            this.GrpPlans.Location = new Point(564, 8);
            this.GrpPlans.Size = new Size(326, 464);
            this.GrpPlans.Text = "方案控制";

            AddPlanSection(this.GrpPlans, "风扇方案", this.CmbFanPlan, this.BtnFanSet, this.BtnFanManage, this.LblFanPlanState, 28);
            AddPlanSection(this.GrpPlans, "CPU 方案", this.CmbCpuPlan, this.BtnCpuApply, null, this.LblCpuPlanState, 158);
            AddPlanSection(this.GrpPlans, "GPU 方案", this.CmbGpuPlan, this.BtnGpuApply, null, this.LblGpuPlanState, 288);

            this.BtnFanSet.Text = "应用";
            this.BtnFanSet.HighlightColorDark = Color.FromArgb(Config.GuiColorWarmDark);
            this.BtnFanSet.HighlightColorLight = Color.FromArgb(Config.GuiColorWarmLite);
            this.BtnFanSet.HighlightGradientMode = LinearGradientMode.BackwardDiagonal;
            this.BtnFanSet.HighlightRadius = 2;
            this.BtnFanSet.HighlightWidth = 5;
            this.BtnFanManage.Text = "管理";
            this.BtnCpuApply.Text = "应用";
            this.BtnGpuApply.Text = "应用";

            this.CmbFanPlan.DropDownStyle = ComboBoxStyle.DropDownList;
            this.CmbCpuPlan.DropDownStyle = ComboBoxStyle.DropDownList;
            this.CmbGpuPlan.DropDownStyle = ComboBoxStyle.DropDownList;

            this.TabPerformance.Controls.Add(this.GrpSys);
            this.TabPerformance.Controls.Add(this.GrpConsole);
            this.TabPerformance.Controls.Add(this.GrpFanBars);
            this.TabPerformance.Controls.Add(this.GrpPlans);
            this.TabPerformance.Location = new Point(4, 22);
            this.TabPerformance.Name = "TabPerformance";
            this.TabPerformance.Padding = new Padding(3);
            this.TabPerformance.Size = new Size(898, 482);
            this.TabPerformance.Text = Config.Locale.Get(Config.L_GUI_MAIN + "TabPerformance");
            this.TabPerformance.UseVisualStyleBackColor = true;

            this.ChkKbdBacklight.Location = new Point(10, 20);
            this.ChkKbdBacklight.Size = new Size(95, 21);
            this.ChkKbdBacklight.Text = Config.Locale.Get(Config.L_GUI_MAIN + Gui.G_KBD + "Backlight");

            this.CmbKbdColorPreset.DropDownStyle = ComboBoxStyle.DropDownList;
            this.CmbKbdColorPreset.Location = new Point(112, 20);
            this.CmbKbdColorPreset.Size = new Size(195, 21);

            this.BtnKbdColorPresetDel.Location = new Point(313, 19);
            this.BtnKbdColorPresetDel.Size = new Size(34, 23);
            this.BtnKbdColorPresetDel.Text = "-";

            this.BtnKbdColorPresetSet.Location = new Point(353, 19);
            this.BtnKbdColorPresetSet.Size = new Size(34, 23);
            this.BtnKbdColorPresetSet.Text = "+";

            this.TxtKbdColorVal.Location = new Point(393, 20);
            this.TxtKbdColorVal.Size = new Size(180, 20);

            this.PicKbd.Location = new Point(10, 52);
            this.PicKbd.Size = new Size(560, 180);
            this.PicKbd.SizeMode = PictureBoxSizeMode.Zoom;

            this.GrpKbd.Location = new Point(8, 8);
            this.GrpKbd.Size = new Size(586, 246);
            this.GrpKbd.Text = Config.Locale.Get(Config.L_GUI_MAIN + Gui.G_KBD).Replace("&", "&&");
            this.GrpKbd.Controls.Add(this.ChkKbdBacklight);
            this.GrpKbd.Controls.Add(this.CmbKbdColorPreset);
            this.GrpKbd.Controls.Add(this.BtnKbdColorPresetDel);
            this.GrpKbd.Controls.Add(this.BtnKbdColorPresetSet);
            this.GrpKbd.Controls.Add(this.TxtKbdColorVal);
            this.GrpKbd.Controls.Add(this.PicKbd);

            this.TabLighting.Controls.Add(this.GrpKbd);
            this.TabLighting.Location = new Point(4, 22);
            this.TabLighting.Name = "TabLighting";
            this.TabLighting.Padding = new Padding(3);
            this.TabLighting.Size = new Size(898, 482);
            this.TabLighting.Text = Config.Locale.Get(Config.L_GUI_MAIN + "TabLighting");
            this.TabLighting.UseVisualStyleBackColor = true;

            this.TabMain.Controls.Add(this.TabPerformance);
            this.TabMain.Controls.Add(this.TabLighting);
            this.TabMain.Location = new Point(6, 6);
            this.TabMain.Size = new Size(906, 508);
            this.TabMain.SelectedIndex = 0;

            this.Controls.Add(this.TabMain);

            this.AutoScaleDimensions = new SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.HelpButton = true;
            this.Icon = OmenMon.Resources.Icon;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = Gui.T_FRM + "Main";
            this.Size = new Size(930, 558);
            this.SizeGripStyle = SizeGripStyle.Hide;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = Config.Locale.Get(Config.L_GUI_MAIN + "Title");

            this.Tip.InitialDelay = 0;
            this.Tip.ReshowDelay = 0;
            this.Tip.AutoPopDelay = 5000;
            this.Tip.SetToolTip(this.CmbFanPlan, "选择风扇方案后点击应用。");
            this.Tip.SetToolTip(this.BtnFanManage, "打开统一 Tmax 风扇曲线管理。");
            this.Tip.SetToolTip(this.TrkFan0Lvl, "定速方案下可拖动；其他模式只读显示。");
            this.Tip.SetToolTip(this.TrkFan1Lvl, "定速方案下可拖动；其他模式只读显示。");

            ((System.ComponentModel.ISupportInitialize) this.PicKbd).EndInit();
            ((System.ComponentModel.ISupportInitialize) this.TrkFan0Lvl).EndInit();
            ((System.ComponentModel.ISupportInitialize) this.TrkFan1Lvl).EndInit();

            this.GrpSys.ResumeLayout(false);
            this.GrpConsole.ResumeLayout(false);
            this.GrpFanBars.ResumeLayout(false);
            this.GrpPlans.ResumeLayout(false);
            this.GrpKbd.ResumeLayout(false);
            this.GrpKbd.PerformLayout();
            this.TabPerformance.ResumeLayout(false);
            this.TabLighting.ResumeLayout(false);
            this.TabMain.ResumeLayout(false);
            this.ResumeLayout(false);

            this.CmbFanPlan.SelectionChangeCommitted += EventFanPlanChanged;
            this.CmbCpuPlan.SelectionChangeCommitted += EventCpuPlanChanged;
            this.CmbGpuPlan.SelectionChangeCommitted += EventGpuPlanChanged;
            this.TrkFan0Lvl.ValueChanged += EventFanTrkChanged;
            this.TrkFan1Lvl.ValueChanged += EventFanTrkChanged;
            this.BtnFanSet.Click += EventActionFanSet;
            this.BtnFanManage.Click += EventActionFanProgEdit;
            this.BtnCpuApply.Click += EventActionCpuApply;
            this.BtnGpuApply.Click += EventActionGpuApply;

            this.ChkKbdBacklight.Click += EventActionBacklight;
            this.CmbKbdColorPreset.SelectionChangeCommitted += EventColorPreset;
            this.TxtKbdColorVal.TextChanged += EventColorInput;
            this.BtnKbdColorPresetDel.Click += EventActionColorPresetDel;
            this.BtnKbdColorPresetSet.Click += EventActionColorPresetSet;
            this.PicKbd.MouseClick += EventColorPick;

        }

        private void AddPlanSection(
            Control parent,
            string title,
            ComboBox combo,
            Button apply,
            Button manage,
            Label state,
            int top) {

            Label label = new Label();
            label.Location = new Point(14, top);
            label.Size = new Size(280, 18);
            label.Text = title;
            label.Font = new Font(label.Font, FontStyle.Bold);

            combo.Location = new Point(16, top + 24);
            combo.Size = new Size(290, 21);

            apply.Location = new Point(16, top + 52);
            apply.Size = new Size(72, 25);

            if(manage != null) {
                manage.Location = new Point(94, top + 52);
                manage.Size = new Size(72, 25);
                parent.Controls.Add(manage);
            }

            state.Location = new Point(16, top + 84);
            state.Size = new Size(290, 34);
            state.Text = "当前: -";
            state.ForeColor = SystemColors.GrayText;

            parent.Controls.Add(label);
            parent.Controls.Add(combo);
            parent.Controls.Add(apply);
            parent.Controls.Add(state);

        }
#endregion

    }

}
