  //\\   OmenMon: Hardware Monitoring & Control Utility
 //  \\  Copyright © 2023-2024 Piotr Szczepański * License: GPL3
     //  https://omenmon.github.io/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using OmenMon.External;
using OmenMon.Hardware.Bios;
using OmenMon.Hardware.Platform;
using OmenMon.Library;

namespace OmenMon.AppGui {

    public partial class GuiFormMain : Form {

#region Variables
        private ColorDialogEx ColorPicker;
        private List<Object> ColorPresets;
        private List<Object> FanModes;
        private List<Object> FanPrograms;
        private List<FanPlan> FanPlans;
        private Font FigureFont;
        internal GuiKbd Kbd;
        private int LastDpi;
        private string SysInfo;
        private string SysStatus;
        private string LastOperationStatus;
        private GuiTray Context;
        private GuiLog Log;
        private System.ComponentModel.IContainer Components;
#endregion

#region Construction & Disposal
        public GuiFormMain() {

            this.Context = GuiTray.Context;
            this.Components = new System.ComponentModel.Container();
            this.ColorPresets = new List<Object>();
            this.FanModes = new List<Object>();
            this.FanPrograms = new List<Object>();
            this.FanPlans = new List<FanPlan>();
            this.LastOperationStatus = "最近操作: -";

            Initialize();
            this.Log = new GuiLog(this.RtfLog);

            this.LastDpi = (int) Gui.GetDeviceContextDpi(IntPtr.Zero);

            if(Context.Op.Platform.System.GetKbdBacklightSupport()
                && Context.Op.Platform.System.GetKbdColorSupport()) {

                this.Kbd = new GuiKbd(this.Context);
                this.PicKbd.Image = Kbd.GetImage();
                this.ColorPicker = new ColorDialogEx(UpdateKbdCallback);
                this.ColorPicker.CustomColors = Kbd.UpdateColorPicker(Config.GuiColorPickerCustom);

            } else
                this.PicKbd.Image = OmenMon.Resources.KeyboardOff;

            SetupFanCtl();
            SetupSys();
            SetupTmp();
            UpdateAll();

            this.Log.Info(Config.AppName + " " + Config.AppVersion + " GUI ready.");
            UpdateSysMsg("Welcome");

        }

        protected override void Dispose(bool isDisposing) {
            if(isDisposing && Components != null)
                Components.Dispose();
            base.Dispose(isDisposing);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) {
            if(keyData == Keys.F1) {
                GuiOp.About();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override bool ProcessDialogKey(Keys keyData) {
            if(Form.ModifierKeys == Keys.None && keyData == Keys.Escape) {
                this.Close();
                return true;
            }
            return base.ProcessDialogKey(keyData);
        }

        protected override void WndProc(ref Message m) {
            if(m.Msg == User32.WM_DPICHANGED)
                UpdateDpi(m.WParam.ToInt32() & 0xFFFF);
            base.WndProc(ref m);
        }
#endregion

#region Event Actions
        private void EventActionBacklight(object sender, EventArgs e) {
            try {
                if(Kbd != null)
                    Kbd.SetBacklight(!this.ChkKbdBacklight.Checked);
                else
                    Context.Op.Platform.System.SetKbdBacklight(!this.ChkKbdBacklight.Checked);
                this.Log.Info("SetKbdBacklight(" + (!this.ChkKbdBacklight.Checked).ToString() + ") OK");
            } catch(Exception ex) {
                this.Log.Error("SetKbdBacklight failed: " + ex.Message);
            }
            UpdateKbd();
        }

        private void EventActionColorPresetDel(object sender, EventArgs e) {
            if(this.CmbKbdColorPreset.SelectedValue == null || (string) this.CmbKbdColorPreset.SelectedValue == "")
                return;

            string name = (string) this.CmbKbdColorPreset.SelectedValue;
            if(MessageBox.Show(this, "Delete preset: " + name + "?", "Keyboard",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes) {
                Config.ColorPreset.Remove(name);
                Context.Menu.Create();
                this.CmbKbdColorPreset.DataSource = null;
                UpdateKbd();
                Config.Save();
            }
        }

        private void EventActionColorPresetSet(object sender, EventArgs e) {
            string name;
            if((name = Gui.ShowPromptInputText(
                Config.Locale.Get(Config.L_GUI_MAIN + Gui.G_KBD + "ColorPresetAdd"),
                Config.Locale.Get(Config.L_GUI_MAIN + Gui.G_KBD + "ColorPresetAddValueDefault"),
                this)) != "")
                Config.ColorPreset[name] = new BiosData.ColorTable(Kbd.GetColors(), true);

            Context.Menu.Create();
            this.CmbKbdColorPreset.DataSource = null;
            UpdateKbd();
            Config.Save();
        }

        private void EventActionFanSet(object sender, EventArgs e) {
            FanPlan plan = this.CmbFanPlan.SelectedItem as FanPlan;
            if(plan == null)
                return;

            ApplyFanPlan(plan);
        }

        private void EventActionFanProgEdit(object sender, EventArgs e) {
            string programName = null;
            if(this.CmbFanPlan.SelectedItem is FanPlan plan && plan.Kind == FanPlanKind.Curve)
                programName = plan.Name;
            else if(Config.FanProgram.Count > 0)
                programName = Config.FanProgram.Keys[0];

            if(programName == null)
                return;

            using(GuiFormFanCurve form = new GuiFormFanCurve(programName)) {
                if(form.ShowDialog(this) == DialogResult.OK) {
                    SetupFanCtl();
                    SelectFanPlan(FanPlanKind.Curve, form.ProgramName);
                    Context.Menu.Create();
                    Config.Save();
                    this.Log.Info("Fan plan list refreshed after curve manager.");
                }
            }
        }

        private void EventActionCpuApply(object sender, EventArgs e) {
            string plan = this.CmbCpuPlan.SelectedItem as string;
            this.Log.OperationBegin("Apply CPU Plan: " + plan);
            this.Log.Warn("CPU power plan is UI-only for now. No CPU PL1/PL4 BIOS write was sent; safe values need model confirmation.");
            SetLastOperation(true, "CPU 方案 " + plan + " 未写入硬件 (TODO)", "CPU: " + plan);
            this.LblCpuPlanState.Text = "当前: " + plan + " (未写入)";
        }

        private void EventActionGpuApply(object sender, EventArgs e) {
            GpuPlanItem item = this.CmbGpuPlan.SelectedItem as GpuPlanItem;
            if(item == null)
                return;

            this.Log.OperationBegin("Apply GPU Plan: " + item.Text);
            try {
                BiosData.GpuPowerData data = new BiosData.GpuPowerData(item.Level);
                this.Log.Info("SetGpuPower(" + item.Level.ToString() + ")");
                Context.Op.Platform.System.SetGpuPower(data);
                BiosData.GpuPowerData readback = Context.Op.Platform.System.GetGpuPower(true);
                string readbackText = GetGpuPowerReadback(readback);
                this.Log.OperationResult(true, readbackText);
                SetLastOperation(true, "GPU 方案 " + item.Text + " 已应用", readbackText);
                this.LblGpuPlanState.Text = "当前: " + item.Text;
            } catch(Exception ex) {
                this.Log.OperationResult(false, ex.Message);
                SetLastOperation(false, "GPU 方案应用失败", ex.Message);
            }
            UpdateSys();
        }

        private void EventActionHelp(object sender, EventArgs e) {
            GuiOp.About();
            ((System.ComponentModel.CancelEventArgs) e).Cancel = true;
        }

        private void EventActionLogClear(object sender, EventArgs e) {
            this.Log.Clear();
        }

        private void EventActionLogCopy(object sender, EventArgs e) {
            this.Log.Copy();
        }

        private void EventActionLogExport(object sender, EventArgs e) {
            using(SaveFileDialog dialog = new SaveFileDialog()) {
                dialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                dialog.FileName = "OmenMon-log-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt";
                if(dialog.ShowDialog(this) == DialogResult.OK)
                    File.WriteAllText(dialog.FileName, this.Log.Text);
            }
        }
#endregion

#region Events
        private void EventColorInput(object sender, EventArgs e) {
            if(Kbd == null)
                return;
            try {
                Kbd.SetColors(new BiosData.ColorTable(this.TxtKbdColorVal.Text));
                this.TxtKbdColorVal.ForeColor = Color.Empty;
            } catch {
                this.TxtKbdColorVal.ForeColor = Color.Red;
            }
        }

        private void EventColorPick(object sender, MouseEventArgs e) {
            if(Kbd == null || !Kbd.GetBacklight())
                return;

            this.ColorPicker.Title = Config.Locale.Get(Config.L_GUI_MAIN + "KbdColorPick" + Kbd.SetZone(e.X, e.Y).ToString());
            this.ColorPicker.Color = Color.FromArgb(Kbd.GetColor());
            this.ColorPicker.CustomColors = Kbd.UpdateColorPicker(this.ColorPicker.CustomColors);
            this.ColorPicker.ShowDialog();
        }

        private void EventColorPreset(object sender, EventArgs e) {
            if(Kbd == null || ((ComboBox) sender).SelectedValue == null)
                return;
            Context.FormMain.Kbd.SetColors(Config.ColorPreset[(string) ((ComboBox) sender).SelectedValue]);
            this.TxtKbdColorVal.Text = Kbd.GetParam();
        }

        private void EventFormClosing(object sender, FormClosingEventArgs e) {
            if(e.CloseReason == CloseReason.UserClosing) {
                e.Cancel = true;
                this.Hide();
            }
        }

        private void EventFanPlanChanged(object sender, EventArgs e) {
            this.BtnFanSet.Checked = true;
            this.LblFanPlanState.Text = "未应用更改: " + this.CmbFanPlan.Text;
            bool fixedPlan = (this.CmbFanPlan.SelectedItem as FanPlan)?.Kind == FanPlanKind.Fixed;
            this.TrkFan0Lvl.Enabled = fixedPlan;
            this.TrkFan1Lvl.Enabled = fixedPlan;
        }

        private void EventCpuPlanChanged(object sender, EventArgs e) {
            this.LblCpuPlanState.Text = "未应用更改: " + this.CmbCpuPlan.Text;
        }

        private void EventGpuPlanChanged(object sender, EventArgs e) {
            this.LblGpuPlanState.Text = "未应用更改: " + this.CmbGpuPlan.Text;
        }

        private void EventFanTrkChanged(object sender, EventArgs e) {
            if(((Control) sender).Enabled) {
                TrackBar changed = (TrackBar) sender;
                TrackBar other = changed == this.TrkFan0Lvl ? this.TrkFan1Lvl : this.TrkFan0Lvl;
                if(other.Value != changed.Value)
                    other.Value = changed.Value;
                if(this.CmbFanPlan.SelectedItem is FanPlan plan && plan.Kind == FanPlanKind.Fixed)
                    plan.FixedLevel = (byte) changed.Value;
                this.BtnFanSet.Checked = true;
                this.LblFanPlanState.Text = "未应用更改: 定速 " + changed.Value.ToString();
            }
        }

        private void EventFormVisibleChanged(object sender, EventArgs e) {
            Context.UpdateMonitorTick = 0;
            if(this.Visible)
                UpdateAll();
        }
#endregion

#region Setup
        public void SetupFanCtl() {

            this.CmbFanPlan.BeginUpdate();
            this.CmbFanPlan.DataSource = null;
            this.FanPlans.Clear();

            foreach(string name in Config.FanProgram.Keys)
                this.FanPlans.Add(new FanPlan(name, FanPlanKind.Curve, "曲线"));

            foreach(BiosData.FanMode mode in GetSupportedFirmwareFanModes())
                this.FanPlans.Add(new FanPlan(
                    GetFirmwarePlanName(mode),
                    FanPlanKind.Firmware,
                    "固件",
                    mode));
            this.FanPlans.Add(new FanPlan("定速", FanPlanKind.Fixed, "固定", null, (byte) Config.FanLevelMin));
            this.FanPlans.Add(new FanPlan("最大风扇", FanPlanKind.Max, "特殊"));

            this.CmbFanPlan.DataSource = this.FanPlans;
            this.CmbFanPlan.EndUpdate();

            this.CmbCpuPlan.Items.Clear();
            this.CmbCpuPlan.Items.AddRange(new object[] { "默认", "低功耗", "平衡", "性能" });
            this.CmbCpuPlan.SelectedIndex = 0;

            this.CmbGpuPlan.Items.Clear();
            this.CmbGpuPlan.Items.Add(new GpuPlanItem("基础功耗", BiosData.GpuPowerLevel.Minimum));
            this.CmbGpuPlan.Items.Add(new GpuPlanItem("增强功耗", BiosData.GpuPowerLevel.Medium));
            this.CmbGpuPlan.Items.Add(new GpuPlanItem("增强功耗 + Boost", BiosData.GpuPowerLevel.Maximum));
            this.CmbGpuPlan.SelectedIndex = 0;

            this.TrkFan0Lvl.Enabled = false;
            this.TrkFan1Lvl.Enabled = false;

        }

        private void SelectFanPlan(FanPlanKind kind, string name) {
            foreach(FanPlan plan in this.FanPlans)
                if(plan.Kind == kind && plan.Name == name) {
                    this.CmbFanPlan.SelectedItem = plan;
                    return;
                }
        }

        public void SetupSys() {
            this.SysInfo = "";
            this.SysStatus = "";
            UpdateSysRtf();
        }

        public void SetupTmp() {
        }
#endregion

#region Updates
        public void UpdateAll() {
            UpdateDpi(this.LastDpi);
            UpdateFan();
            UpdateFanCtl();
            UpdateKbd();
            UpdateSys();
            UpdateTmp();
        }

        private void UpdateDpi(int dpi) {
            if(Config.GuiDpiChangeResize) {
                this.SuspendLayout();
                this.AutoSize = false;
                this.Size = new Size(
                    (int) (930 + ((dpi - 96) * Config.DpiSizeAdjFactorX / 100)),
                    (int) (558 + ((dpi - 96) * Config.DpiSizeAdjFactorY / 100)));
                this.AutoSize = true;
                this.ResumeLayout(false);
                this.LastDpi = dpi;
            }
        }

        public void UpdateFan() {
            try {
                Context.Op.Platform.UpdateFans();
                int level0 = Context.Op.Platform.Fans.Fan[0].GetLevel();
                int level1 = Context.Op.Platform.Fans.Fan[1].GetLevel();
                if(!this.TrkFan0Lvl.Enabled)
                    this.TrkFan0Lvl.Value = Conv.GetConstrained(level0, this.TrkFan0Lvl.Minimum, this.TrkFan0Lvl.Maximum);
                if(!this.TrkFan1Lvl.Enabled)
                    this.TrkFan1Lvl.Value = Conv.GetConstrained(level1, this.TrkFan1Lvl.Minimum, this.TrkFan1Lvl.Maximum);
                this.LblFan0Val.Text = level0.ToString();
                this.LblFan1Val.Text = level1.ToString();

                try {
                    int rpm0 = Context.Op.Platform.Fans.Fan[0].GetSpeed();
                    int rpm1 = Context.Op.Platform.Fans.Fan[1].GetSpeed();
                    if((level0 > 0 && rpm0 == 0) || (level1 > 0 && rpm1 == 0))
                        this.LblFanPlanState.Text = "提示: 读数可能不受该机型支持";
                } catch(Exception ex) {
                    this.Log.Warn("Fan RPM readback unavailable: " + ex.Message);
                }
            } catch(Exception ex) {
                this.Log.Warn("Fan level readback unavailable: " + ex.Message);
            }
        }

        public void UpdateFanCtl() {
            try {
                if(Context.Op.Program.IsEnabled)
                    SelectFanPlan(FanPlanKind.Curve, Context.Op.Program.GetName());
                else if(Context.Op.Platform.Fans.GetMax())
                    SelectFanPlan(FanPlanKind.Max, "最大风扇");
                else {
                    BiosData.FanMode mode = Context.Op.Platform.Fans.GetMode();
                    if(mode == BiosData.FanMode.Default)
                        SelectFanPlan(FanPlanKind.Firmware, "固件 默认");
                    else if(mode == BiosData.FanMode.Performance)
                        SelectFanPlan(FanPlanKind.Firmware, "固件 性能");
                    else if(mode == BiosData.FanMode.Cool)
                        SelectFanPlan(FanPlanKind.Firmware, "固件 清凉");
                }
            } catch(Exception ex) {
                this.Log.Warn("Fan control state readback unavailable: " + ex.Message);
            }

            this.BtnFanSet.Checked = false;
            this.LblFanPlanState.Text = "当前: " + this.CmbFanPlan.Text;
        }

        public void UpdateKbd() {
            this.TxtKbdColorVal.ForeColor = Color.Empty;

            if(!Context.Op.Platform.System.GetKbdBacklightSupport()) {
                this.ChkKbdBacklight.Checked = false;
                this.ChkKbdBacklight.Enabled = false;
            } else if(Kbd != null)
                this.ChkKbdBacklight.Checked = Kbd.GetBacklight();
            else
                this.ChkKbdBacklight.Checked =
                    Context.Op.Platform.System.GetKbdBacklight() == BiosData.Backlight.On;

            if(Kbd == null || !Kbd.GetBacklight()) {
                this.CmbKbdColorPreset.DataSource = null;
                this.CmbKbdColorPreset.Enabled = false;
                this.TxtKbdColorVal.Enabled = false;
                this.TxtKbdColorVal.Text = "";
                this.BtnKbdColorPresetDel.Enabled = false;
                this.BtnKbdColorPresetSet.Enabled = false;
                this.PicKbd.Cursor = Cursors.Default;
            } else {
                this.CmbKbdColorPreset.BeginUpdate();
                this.ColorPresets.Clear();
                foreach(string name in Config.ColorPreset.Keys)
                    this.ColorPresets.Add(new {
                        Text = name.StartsWith(Config.ColorPresetDefaultPrefix) ?
                            Config.Locale.Get(Config.L_GUI_MENU + Gui.M_ACT + Gui.G_KBD + "ColorPreset" + name) : name,
                        Value = name });
                this.CmbKbdColorPreset.DataSource = this.ColorPresets;
                this.CmbKbdColorPreset.DisplayMember = "Text";
                this.CmbKbdColorPreset.ValueMember = "Value";
                this.CmbKbdColorPreset.SelectedValue = Kbd.GetPreset();
                this.CmbKbdColorPreset.Enabled = true;
                this.CmbKbdColorPreset.EndUpdate();
                this.TxtKbdColorVal.Enabled = true;
                this.TxtKbdColorVal.Text = Kbd.GetParam();
                this.BtnKbdColorPresetDel.Enabled = true;
                this.BtnKbdColorPresetSet.Enabled = true;
                this.PicKbd.Cursor = Cursors.Hand;
            }
        }

        public void UpdateKbdCallback(int color) {
            Kbd.SetColor(ColorTranslator.FromWin32(color).ToArgb());
            this.TxtKbdColorVal.Text = Kbd.GetParam();
            this.CmbKbdColorPreset.SelectedValue = Kbd.GetPreset();
        }

        public void UpdateSys() {
            try {
                Context.Op.Platform.UpdateSystem();
                this.SysInfo =
                    "机型: " + Context.Op.Platform.System.GetManufacturer() + " "
                    + Context.Op.Platform.System.GetProduct() + " "
                    + Context.Op.Platform.System.GetVersion() + Environment.NewLine
                    + "Born/BIOS: " + Context.Op.Platform.System.GetBornDate()
                    + "  主板: " + Context.Op.Platform.System.GetProduct() + Environment.NewLine
                    + "电源: " + (Context.Op.Platform.System.IsFullPower() ? "AC" : "电池")
                    + "  适配器: " + SafeGetAdapter()
                    + "  默认PL4: " + SafeGetDefaultCpuPowerLimit4() + "W" + Environment.NewLine
                    + "固件能力: " + GetFirmwareSupportSummary() + Environment.NewLine
                    + "当前风扇方案: " + this.CmbFanPlan.Text + Environment.NewLine
                    + "当前 CPU 方案: " + this.LblCpuPlanState.Text.Replace("当前: ", "") + Environment.NewLine
                    + "当前 GPU 方案: " + this.LblGpuPlanState.Text.Replace("当前: ", "") + Environment.NewLine
                    + GetTemperatureSummary() + Environment.NewLine
                    + this.LastOperationStatus + Environment.NewLine;
            } catch(Exception ex) {
                this.SysInfo = "系统状态读取失败: " + ex.Message + Environment.NewLine;
                this.Log.Warn("System readback unavailable: " + ex.Message);
            }

            UpdateSysRtf();
        }

        public void UpdateSysMsg(string message = "") {
            if(message != "")
                this.SysStatus = "状态: " + message + " @ " + DateTime.Now.ToString(Config.TimestampFormat);
            UpdateSysRtf();
        }

        private void UpdateSysRtf() {
            if(this.RtfSysInfo == null)
                return;
            this.RtfSysInfo.Text = this.SysInfo + this.SysStatus;
        }

        public void UpdateTmp() {
            try {
                Context.Op.Platform.UpdateTemperature();
                UpdateSys();
            } catch(Exception ex) {
                this.Log.Warn("Temperature readback unavailable: " + ex.Message);
            }
        }

        public void WriteLog(string message) {
            if(this.Log != null)
                this.Log.Info(message);
        }
#endregion

#region Fan Operations
        private void ApplyFanPlan(FanPlan plan) {

            this.Log.OperationBegin("Apply FanPlan: " + plan.Name);
            try {
                switch(plan.Kind) {
                    case FanPlanKind.Curve:
                        this.Log.Info("Program.Run(\"" + plan.Name + "\")");
                        if(Context.Op.Platform.Fans.GetOff()) {
                            this.Log.Info("SetOff(false)");
                            Context.Op.Platform.Fans.SetOff(false);
                        }
                        if(Context.Op.Platform.Fans.GetMax()) {
                            this.Log.Info("SetMax(false)");
                            Context.Op.Platform.Fans.SetMax(false);
                        }
                        if(!Context.Op.Program.Run(plan.Name))
                            throw new InvalidOperationException("Program.Run returned false.");
                        Context.UpdateProgramTick = 1;
                        break;

                    case FanPlanKind.Firmware:
                        ApplyFirmwareFanPlan((BiosData.FanMode) plan.FirmwareMode);
                        break;

                    case FanPlanKind.Fixed:
                        ApplyFixedFanPlan(plan.FixedLevel == null ? (byte) Config.FanLevelMin : (byte) plan.FixedLevel);
                        break;

                    case FanPlanKind.Max:
                        this.Log.Info("Program.Terminate()");
                        Context.Op.Program.Terminate();
                        this.Log.Info("SetOff(false)");
                        Context.Op.Platform.Fans.SetOff(false);
                        this.Log.Info("SetMax(true)");
                        Context.Op.FanMaxSet(true);
                        break;
                }

                string readback = GetFanReadback();
                this.Log.OperationResult(true, readback);
                SetLastOperation(true, "风扇方案 " + plan.Name + " 已应用", readback);
                this.BtnFanSet.Checked = false;
                this.LblFanPlanState.Text = "当前: " + plan.ToString();
            } catch(Exception ex) {
                this.Log.OperationResult(false, ex.Message);
                SetLastOperation(false, "风扇方案 " + plan.Name + " 应用失败", ex.Message);
            }

            UpdateFan();
            UpdateSys();
        }

        private void ApplyFirmwareFanPlan(BiosData.FanMode mode) {
            this.Log.Info("Program.Terminate()");
            Context.Op.Program.Terminate();
            this.Log.Info("SetOff(false)");
            Context.Op.Platform.Fans.SetOff(false);
            this.Log.Info("SetMax(false)");
            Context.Op.Platform.Fans.SetMax(false);
            this.Log.Info("SetLevels(255,255)");
            Context.Op.Platform.Fans.SetLevels(new byte[] { Byte.MaxValue, Byte.MaxValue });
            this.Log.Info("SetFanMode(" + mode.ToString() + ")");
            Context.Op.Platform.Fans.SetMode(mode);
        }

        private void ApplyFixedFanPlan(byte level) {
            level = (byte) Conv.GetConstrained(level, Config.FanLevelMin, Config.FanLevelMax);
            this.Log.Info("Program.Terminate()");
            Context.Op.Program.Terminate();
            this.Log.Info("SetMax(false)");
            Context.Op.Platform.Fans.SetMax(false);
            this.Log.Info("SetOff(false)");
            Context.Op.Platform.Fans.SetOff(false);
            this.Log.Info("SetLevels(" + level.ToString() + "," + level.ToString() + ")");
            Context.Op.Platform.Fans.SetLevels(new byte[] { level, level });
            this.Log.Info("SetFanMode(GetMode())");
            Context.Op.Platform.Fans.SetMode(Context.Op.Platform.Fans.GetMode());
        }

        private string GetFanReadback() {
            BiosData.FanMode mode = Context.Op.Platform.Fans.GetMode();
            bool fanMax = Context.Op.Platform.Fans.GetMax();
            bool fanOff = Context.Op.Platform.Fans.GetOff();
            byte[] levels = Context.Op.Platform.Fans.GetLevels();
            return "HPCM=0x" + ((byte) mode).ToString("X2")
                + ", FanMax=" + fanMax.ToString()
                + ", FanOff=" + fanOff.ToString()
                + ", FanLevel=" + levels[0].ToString() + "/" + levels[1].ToString();
        }
#endregion

#region Helpers
        private void SetLastOperation(bool success, string summary, string details) {
            this.LastOperationStatus =
                "最近操作: " + (success ? "成功" : "失败")
                + " @ " + DateTime.Now.ToString(Config.TimestampFormat)
                + "  " + summary
                + "  读回: " + details;
        }

        private string GetTemperatureSummary() {
            int cpu = GetTemperatureByName("CPUT");
            int gpu = GetTemperatureByName("GPTM");
            int tmax = Context.Op.Platform.GetMaxTemperature(false);
            return "关键温度: CPU " + FormatTemperature(cpu)
                + "  GPU " + FormatTemperature(gpu)
                + "  Tmax " + FormatTemperature(tmax);
        }

        private int GetTemperatureByName(string name) {
            for(int i = 0; i < Context.Op.Platform.Temperature.Length; i++)
                if(Context.Op.Platform.Temperature[i].GetName() == name)
                    return Context.Op.Platform.Temperature[i].GetValue();
            return 0;
        }

        private string FormatTemperature(int value) {
            return value <= 0 ? "-" : value.ToString() + "C";
        }

        private string SafeGetAdapter() {
            try {
                return Enum.GetName(typeof(BiosData.AdapterStatus), Context.Op.Platform.System.GetAdapterStatus());
            } catch(Exception ex) {
                this.Log.Warn("Adapter readback unavailable: " + ex.Message);
                return "?";
            }
        }

        private string SafeGetDefaultCpuPowerLimit4() {
            try {
                return Context.Op.Platform.System.GetDefaultCpuPowerLimit4().ToString();
            } catch {
                return "?";
            }
        }

        private string GetGpuPowerReadback(BiosData.GpuPowerData data) {
            return "CustomTgp=" + data.CustomTgp.ToString()
                + ", Ppab=" + data.Ppab.ToString()
                + ", DState=" + data.DState.ToString()
                + ", PeakTemperature=" + data.PeakTemperature.ToString();
        }

        private List<BiosData.FanMode> GetSupportedFirmwareFanModes() {

            List<BiosData.FanMode> modes = new List<BiosData.FanMode>();
            modes.Add(BiosData.FanMode.Default);

            try {
                BiosData.SystemData data = Context.Op.Platform.System.GetSystemData();
                if(data.ThermalPolicy == BiosData.ThermalPolicyVersion.V1
                    || data.SupportFlags.HasFlag(BiosData.SysSupportFlags.SwFanCtl)) {
                    modes.Add(BiosData.FanMode.Performance);
                    modes.Add(BiosData.FanMode.Cool);
                }
            } catch(Exception ex) {
                if(this.Log != null)
                    this.Log.Warn("Firmware support probe failed; using modern default modes: " + ex.Message);
                modes.Add(BiosData.FanMode.Performance);
                modes.Add(BiosData.FanMode.Cool);
            }

            return modes;

        }

        private string GetFirmwarePlanName(BiosData.FanMode mode) {
            if(mode == BiosData.FanMode.Performance)
                return "固件 性能";
            if(mode == BiosData.FanMode.Cool)
                return "固件 清凉";
            return "固件 默认";
        }

        private string GetFirmwareSupportSummary() {
            try {
                BiosData.SystemData data = Context.Op.Platform.System.GetSystemData();
                BiosData.FanMode mode = Context.Op.Platform.Fans.GetMode();
                return "ThermalPolicy=" + data.ThermalPolicy.ToString()
                    + ", SupportFlags=" + data.SupportFlags.ToString()
                    + ", HPCM=0x" + ((byte) mode).ToString("X2")
                    + ", 已展示=" + string.Join("/", GetSupportedFirmwareFanModes().ConvertAll(GetFirmwarePlanName).ToArray());
            } catch(Exception ex) {
                return "读取失败: " + ex.Message;
            }
        }

        private class GpuPlanItem {
            public string Text { get; private set; }
            public BiosData.GpuPowerLevel Level { get; private set; }
            public GpuPlanItem(string text, BiosData.GpuPowerLevel level) {
                this.Text = text;
                this.Level = level;
            }
            public override string ToString() {
                return this.Text;
            }
        }
#endregion

    }

}
