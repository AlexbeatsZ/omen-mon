using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Runtime.Serialization;
using System.Windows.Forms;
using OmenMon.AppGui;
using OmenMon.Hardware.Bios;
using OmenMon.Hardware.Platform;
using OmenMon.Library;

sealed class FakePowerHardware : RealProxy {
    public BiosData.GpuPowerData Actual = new BiosData.GpuPowerData(BiosData.GpuPowerLevel.Minimum);
    public BiosData.GpuPowerData Cached;
    public int Writes, Reads, Refreshes;
    public bool IgnoreWrite, FailWrite;
    public FakePowerHardware() : base(typeof(ISettings)) { Cached = Actual; }
    public ISettings Interface { get { return (ISettings) GetTransparentProxy(); } }
    public override IMessage Invoke(IMessage message) {
        IMethodCallMessage call = (IMethodCallMessage) message;
        try {
            object result = null;
            switch(call.MethodName) {
                case "GetGpuPower":
                    Reads++;
                    if((bool) call.Args[0]) Cached = Actual;
                    result = Cached;
                    break;
                case "SetGpuPower":
                    Writes++;
                    if(FailWrite) throw new InvalidOperationException("GPU write rejected");
                    if(!IgnoreWrite) Actual = (BiosData.GpuPowerData) call.Args[0];
                    break;
                default: throw new Exception("Unexpected hardware call: " + call.MethodName);
            }
            return new ReturnMessage(result, null, 0, call.LogicalCallContext, call);
        } catch(Exception error) { return new ReturnMessage(error, call); }
    }
}

sealed class FakePowerBios : RealProxy {
    public BiosData.GpuPowerData Actual = new BiosData.GpuPowerData(BiosData.GpuPowerLevel.Minimum);
    public int Writes, Reads;
    public bool FailSecond;
    public FakePowerBios() : base(typeof(IBiosCtl)) { }
    public IBiosCtl Interface { get { return (IBiosCtl) GetTransparentProxy(); } }
    public override IMessage Invoke(IMessage message) {
        IMethodCallMessage call = (IMethodCallMessage) message;
        try {
            object result = null;
            if(call.MethodName == "GetGpuPower") { Reads++; result = Actual; }
            else if(call.MethodName == "SetGpuPower") {
                Writes++;
                if(FailSecond && Writes % 2 == 0) throw new InvalidOperationException("Second write failed");
                Actual = (BiosData.GpuPowerData) call.Args[0];
            } else throw new Exception("Unexpected BIOS call: " + call.MethodName);
            return new ReturnMessage(result, null, 0, call.LogicalCallContext, call);
        } catch(Exception error) { return new ReturnMessage(error, call); }
    }
}

sealed class GuiReadProxy : RealProxy {
    private readonly Func<string, object> Read;
    public GuiReadProxy(Type type, Func<string, object> read) : base(type) { this.Read = read; }
    public object Interface { get { return GetTransparentProxy(); } }
    public override IMessage Invoke(IMessage message) {
        IMethodCallMessage call = (IMethodCallMessage) message;
        try {
            if(call.MethodName.StartsWith("Set")) throw new Exception("Unexpected GUI preview hardware write");
            object result = this.Read(call.MethodName);
            if(result == null) throw new Exception("Unexpected GUI read: " + call.MethodName);
            return new ReturnMessage(result, null, 0, call.LogicalCallContext, call);
        } catch(Exception error) { return new ReturnMessage(error, call); }
    }
}

static class PowerControlTests {
    static int Checks;
    static void Check(bool passed, string message) {
        if(!passed) throw new Exception("FAIL: " + message);
        Checks++;
        Console.WriteLine("PASS: " + message);
    }
    static void Reject(Action action, string name) {
        bool rejected = false;
        try { action(); } catch(ArgumentException) { rejected = true; }
        catch(InvalidOperationException) { rejected = true; }
        Check(rejected, name);
    }
    static void Field(object target, string name, object value) {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
    static BiosData.GpuPowerData Gpu(BiosData.GpuPowerLevel level) { return new BiosData.GpuPowerData(level); }

    [STAThread]
    static int Main(string[] args) {
        try {
            Config.Initialize();
            Config.FilePath = args[0];
            Config.Load();
            foreach(BiosData.GpuPowerLevel level in Enum.GetValues(typeof(BiosData.GpuPowerLevel))) {
                byte[] bytes = Conv.GetByteArray(Gpu(level));
                Check(bytes.Length == 4 && bytes[0] == (level == BiosData.GpuPowerLevel.Minimum ? 0 : 1)
                    && bytes[1] == (level == BiosData.GpuPowerLevel.Maximum ? 1 : 0) && bytes[2] == 1 && bytes[3] == 0,
                    "upstream GPU payload: " + level);
            }
            Reject(() => new BiosData.GpuPowerData(new byte[3]), "truncated GPU readback rejected");
            Reject(() => GpuPowerControl.Parse("unknown"), "unknown GPU selection rejected");
            Reject(() => GpuPowerControl.Parse("3"), "undefined GPU enum rejected");

            FakePowerHardware hw = new FakePowerHardware();
            GpuPowerControl gpu = new GpuPowerControl(hw.Interface, () => { hw.Refreshes++; });
            Check(String.IsNullOrEmpty(Config.GpuPowerOverride), "old configuration follows its curve by default");
            gpu.Select(BiosData.GpuPowerLevel.Maximum, Gpu(BiosData.GpuPowerLevel.Minimum));
            Check(Config.GpuPowerOverride == "Maximum" && Config.AutoConfig && hw.Writes == 1 && hw.Refreshes == 1
                && GpuPowerControl.Matches(hw.Actual, Gpu(BiosData.GpuPowerLevel.Maximum)),
                "manual GPU choice verifies hardware and enables persistent startup recovery");
            gpu.Ensure(Gpu(BiosData.GpuPowerLevel.Minimum));
            Check(hw.Writes == 1, "curve cannot overwrite manual GPU selection or trigger redundant writes");
            Config.GpuPowerOverride = "";
            Config.Load();
            hw.Actual = Gpu(BiosData.GpuPowerLevel.Minimum);
            GpuPowerControl restarted = new GpuPowerControl(hw.Interface, () => { hw.Refreshes++; });
            restarted.Ensure(Gpu(BiosData.GpuPowerLevel.Medium));
            Check(Config.GpuPowerOverride == "Maximum" && hw.Writes == 2
                && GpuPowerControl.Matches(hw.Actual, Gpu(BiosData.GpuPowerLevel.Maximum)),
                "fresh configuration load restores saved GPU choice after firmware reset");

            Platform platform = (Platform) FormatterServices.GetUninitializedObject(typeof(Platform));
            typeof(Platform).GetProperty("Gpu").SetValue(platform, gpu, null);
            FanProgram program = new FanProgram(platform, (severity, message) => { });
            Field(program, "GpuPowerData", Gpu(BiosData.GpuPowerLevel.Minimum));
            MethodInfo update = typeof(FanProgram).GetMethod("UpdateGpuPower", BindingFlags.Instance | BindingFlags.NonPublic);
            hw.Actual = Gpu(BiosData.GpuPowerLevel.Minimum); // cached readback still Maximum
            update.Invoke(program, new object[] { false, null });
            Check(hw.Writes == 3 && GpuPowerControl.Matches(hw.Actual, Gpu(BiosData.GpuPowerLevel.Maximum)),
                "actual fan update repairs reset using fresh readback and manual priority");
            update.Invoke(program, new object[] { true, (BiosData.GpuPowerData?) Gpu(BiosData.GpuPowerLevel.Minimum) });
            Check(GpuPowerControl.Matches(hw.Actual, Gpu(BiosData.GpuPowerLevel.Maximum)),
                "fan termination snapshot cannot undo explicit GPU choice");

            string saved = File.ReadAllText(Config.FilePath);
            hw.IgnoreWrite = true;
            Reject(() => gpu.Select(BiosData.GpuPowerLevel.Minimum, Gpu(BiosData.GpuPowerLevel.Minimum)),
                "acknowledged but ineffective GPU write reported as failure");
            Check(Config.GpuPowerOverride == "Maximum" && File.ReadAllText(Config.FilePath) == saved,
                "ineffective GPU write cannot replace saved choice");
            hw.IgnoreWrite = false;
            hw.FailWrite = true;
            Reject(() => gpu.Select(BiosData.GpuPowerLevel.Medium, Gpu(BiosData.GpuPowerLevel.Minimum)),
                "rejected GPU write reported as failure");
            Check(Config.GpuPowerOverride == "Maximum", "rejected GPU write preserves selection");
            update.Invoke(program, new object[] { true, null });
            Check(true, "background GPU write failure does not abort cooling");
            hw.FailWrite = false;
            gpu.Select(null, Gpu(BiosData.GpuPowerLevel.Medium));
            Check(Config.GpuPowerOverride == "" && GpuPowerControl.Matches(hw.Actual, Gpu(BiosData.GpuPowerLevel.Medium)),
                "follow choice clears override and applies curve GPU target");
            gpu.Ensure(Gpu(BiosData.GpuPowerLevel.Minimum));
            Check(GpuPowerControl.Matches(hw.Actual, Gpu(BiosData.GpuPowerLevel.Minimum)),
                "curve regains GPU control after follow selected");

            FakePowerBios bios = new FakePowerBios();
            IBiosCtl previous = Hw.Bios;
            int delay = Config.GpuPowerSetInterval;
            try {
                Hw.Bios = bios.Interface;
                Config.GpuPowerSetInterval = 0;
                Settings settings = new Settings();
                settings.GetGpuPower();
                settings.SetGpuPower(Gpu(BiosData.GpuPowerLevel.Maximum));
                Check(bios.Writes == 2 && settings.GpuPower == null
                    && GpuPowerControl.Matches(settings.GetGpuPower(), Gpu(BiosData.GpuPowerLevel.Maximum)) && bios.Reads == 2,
                    "settings preserves upstream double write and invalidates GPU cache");
                bios.FailSecond = true;
                Reject(() => settings.SetGpuPower(Gpu(BiosData.GpuPowerLevel.Minimum)), "second BIOS write failure propagated");
                Check(settings.GpuPower == null
                    && GpuPowerControl.Matches(settings.GetGpuPower(), Gpu(BiosData.GpuPowerLevel.Minimum)),
                    "partially accepted GPU write invalidates cache on failure too");
            } finally { Hw.Bios = previous; Config.GpuPowerSetInterval = delay; }

            BiosData.CpuPowerData cpuData = CpuPowerControl.Parse("45:120:35");
            byte[] payload = Conv.GetByteArray(cpuData);
            Check(payload.Length == 4 && payload[0] == 45 && payload[1] == 45 && payload[2] == 120 && payload[3] == 35,
                "CPU four-byte wire format matches upstream with PL2 following PL1");
            cpuData = CpuPowerControl.Parse(" : 100 : ");
            Check(cpuData.Limit1 == 255 && cpuData.Limit2 == 255 && cpuData.Limit4 == 100 && cpuData.LimitWithGpu == 255
                && CpuPowerControl.Format(cpuData) == ":100:", "blank CPU fields leave existing limits unchanged");
            Reject(() => CpuPowerControl.Parse("0::"), "zero CPU watt limit rejected");
            Reject(() => CpuPowerControl.Parse("255::"), "255 is reserved for no-change rather than explicit watts");
            Reject(() => CpuPowerControl.Parse("-1::"), "negative CPU watts rejected");
            Reject(() => CpuPowerControl.Parse("12.5::"), "fractional CPU watts rejected");
            Reject(() => CpuPowerControl.Parse("45:100"), "malformed saved CPU parameters rejected");
            List<BiosData.CpuPowerData> sent = new List<BiosData.CpuPowerData>();
            bool failCpu = false;
            CpuPowerControl cpu = new CpuPowerControl(data => {
                if(failCpu) throw new InvalidOperationException("CPU command rejected");
                sent.Add(data);
            }, () => { });
            Check(!cpu.Restore() && sent.Count == 0, "old configuration sends no CPU power command");
            cpu.Select("45:120:35");
            Check(Config.CpuPowerDefault == "45:120:35" && cpu.LastSent == Config.CpuPowerDefault && sent.Count == 1,
                "accepted CPU selection saves exact command and enables startup recovery");
            Config.CpuPowerDefault = "";
            Config.Load();
            CpuPowerControl freshCpu = new CpuPowerControl(data => { sent.Add(data); }, () => { });
            Check(freshCpu.Restore() && sent.Count == 2 && sent[1].Limit1 == 45 && sent[1].Limit4 == 120 && sent[1].LimitWithGpu == 35,
                "fresh process restores every saved CPU field after restart");
            saved = File.ReadAllText(Config.FilePath);
            failCpu = true;
            Reject(() => cpu.Select("20::"), "rejected CPU command cannot appear successful");
            Check(Config.CpuPowerDefault == "45:120:35" && File.ReadAllText(Config.FilePath) == saved,
                "failed CPU selection preserves prior startup configuration");
            failCpu = false;
            cpu.Select("::");
            Check(Config.CpuPowerDefault == "" && sent.Count == 2 && !cpu.Restore(),
                "clearing CPU fields disables recovery without guessing factory limits");
            Check(File.ReadAllText(Config.FilePath).Contains("ReviewFixture")
                && File.ReadAllText(Config.FilePath).Contains("Fixture User Preset"),
                "power saves preserve unrelated configuration and lighting presets");
            string configPath = Config.FilePath;
            string existing = File.ReadAllText(configPath);
            File.WriteAllText(configPath, "<broken");
            try {
                bool gpuSaveFailed = false;
                try { gpu.Select(BiosData.GpuPowerLevel.Maximum, Gpu(BiosData.GpuPowerLevel.Minimum)); }
                catch(IOException) { gpuSaveFailed = true; }
                Check(gpuSaveFailed && Config.GpuPowerOverride == "" && File.ReadAllText(configPath) == "<broken",
                    "GPU save failure is reported and cannot overwrite a malformed user configuration");
                bool cpuSaveFailed = false;
                try { cpu.Select("30::"); } catch(IOException) { cpuSaveFailed = true; }
                Check(cpuSaveFailed && Config.CpuPowerDefault == "" && File.ReadAllText(configPath) == "<broken",
                    "CPU save failure cannot replace the previously persisted choice");
            } finally { File.WriteAllText(configPath, existing); }
            Check(Directory.GetFiles(Path.GetDirectoryName(configPath), "*.writing-*").Length == 0,
                "atomic configuration saves leave no staging files");

            // Exercise the actual control panel against fake hardware, including
            // both apply buttons. No tray, EC driver or real WMI is initialized.
            Gui.Initialize();
            Config.GuiCloseWindowExit = false;
            BiosData.SystemData systemData = default(BiosData.SystemData);
            systemData.ThermalPolicy = BiosData.ThermalPolicyVersion.V1;
            ISettings guiSystem = (ISettings) new GuiReadProxy(typeof(ISettings), name => {
                switch(name) {
                    case "GetKbdBacklightSupport": return false;
                    case "GetManufacturer": return "Fixture HP";
                    case "GetProduct": return "8BB3";
                    case "GetVersion": return "GUI fixture";
                    case "GetBornDate": return "20261002";
                    case "IsFullPower": return true;
                    case "GetAdapterStatus": return BiosData.AdapterStatus.MeetsRequirement;
                    case "GetDefaultCpuPowerLimit4": return (byte) 0;
                    case "GetSystemData": return systemData;
                    default: return null;
                }
            }).Interface;
            IFan fan = (IFan) new GuiReadProxy(typeof(IFan), name => {
                if(name == "GetLevel") return 30;
                if(name == "GetSpeed") return 3000;
                return null;
            }).Interface;
            IFanArray fans = (IFanArray) new GuiReadProxy(typeof(IFanArray), name => {
                switch(name) {
                    case "get_Fan": return new IFan[] { fan, fan };
                    case "GetMode": return BiosData.FanMode.Performance;
                    case "GetMax": case "GetOff": return false;
                    default: return null;
                }
            }).Interface;
            typeof(Platform).GetProperty("System").SetValue(platform, guiSystem, null);
            typeof(Platform).GetProperty("Fans").SetValue(platform, fans, null);
            typeof(Platform).GetProperty("Temperature").SetValue(platform, new IPlatformReadComponent[0], null);
            typeof(Platform).GetProperty("TemperatureUse").SetValue(platform, new bool[0], null);
            typeof(Platform).GetProperty("Cpu").SetValue(platform, cpu, null);
            GuiTray tray = (GuiTray) FormatterServices.GetUninitializedObject(typeof(GuiTray));
            GuiOp op = (GuiOp) FormatterServices.GetUninitializedObject(typeof(GuiOp));
            Field(op, "Platform", platform);
            Field(op, "Program", program);
            Field(tray, "Op", op);
            typeof(GuiTray).GetProperty("Context", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, tray, null);
            using(GuiFormMain form = new GuiFormMain()) {
                Func<string, Control> control = name => (Control) typeof(GuiFormMain)
                    .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                Control pl1 = control("TxtCpuPl1"), pl4 = control("TxtCpuPl4"), concurrent = control("TxtCpuWithGpu");
                Check(pl1.Parent.ClientRectangle.Contains(pl1.Bounds) && pl4.Parent.ClientRectangle.Contains(pl4.Bounds)
                    && concurrent.Parent.ClientRectangle.Contains(concurrent.Bounds)
                    && !pl1.Bounds.IntersectsWith(pl4.Bounds) && !pl4.Bounds.IntersectsWith(concurrent.Bounds),
                    "three CPU watt inputs fit the existing control panel without overlap");
                Check(control("LblGpuPlanState").Parent.ClientRectangle.Contains(control("LblGpuPlanState").Bounds),
                    "GPU controls fit below CPU inputs inside the existing panel");
                pl1.Text = "35"; pl4.Text = ""; concurrent.Text = "25";
                typeof(GuiFormMain).GetMethod("EventActionCpuApply", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(form, new object[] { null, EventArgs.Empty });
                Check(Config.CpuPowerDefault == "35::25" && sent[sent.Count - 1].Limit4 == 255,
                    "actual CPU apply button writes and persists entered limits with blank fields unchanged");
                ((ComboBox) control("CmbGpuPlan")).SelectedIndex = 3;
                typeof(GuiFormMain).GetMethod("EventActionGpuApply", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(form, new object[] { null, EventArgs.Empty });
                Check(Config.GpuPowerOverride == "Maximum" && control("LblGpuPlanState").Text.Contains("Boost"),
                    "actual GPU apply button persists selection and shows verified firmware state");
                Config.CpuPowerDefault = "";
                Config.GpuPowerOverride = "";
                Config.Load();
                hw.Actual = Gpu(BiosData.GpuPowerLevel.Minimum);
                int previousCpuWrites = sent.Count;
                op.RestoreGpuPower();
                op.RestoreCpuPower();
                Check(Config.GpuPowerOverride == "Maximum" && Config.CpuPowerDefault == "35::25"
                    && GpuPowerControl.Matches(hw.Actual, Gpu(BiosData.GpuPowerLevel.Maximum))
                    && sent.Count == previousCpuWrites + 1 && sent[sent.Count - 1].LimitWithGpu == 25,
                    "GUI startup/resume recovery restores both saved power selections after firmware reset");
                if(args.Length > 1) {
                    // WinForms only renders tab children after their handles and
                    // visibility exist. Keep this fixture outside the desktop.
                    form.ShowInTaskbar = false;
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new Point(-32000, -32000);
                    form.Show();
                    Application.DoEvents();
                    using(Bitmap preview = new Bitmap(form.Width, form.Height)) {
                        form.DrawToBitmap(preview, new Rectangle(0, 0, preview.Width, preview.Height));
                        preview.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
                    }
                    form.Hide();
                }
            }
            Console.WriteLine("All " + Checks + " power checks passed.");
            return 0;
        } catch(Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
