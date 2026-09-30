using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Runtime.Serialization;
using OmenMon.AppGui;
using OmenMon.Hardware.Bios;
using OmenMon.Hardware.Platform;
using OmenMon.Library;

// Hardware-free regression checks. Unexpected firmware calls fail the fixture.
sealed class FakeSettings : RealProxy {
    public string Colors = "000000:000000:000000:000000";
    public bool Backlight;
    public bool Supported = true;
    public bool ColorSupported = true;
    public bool FailWrite;
    public bool IgnoreWrite;
    public readonly List<string> Writes = new List<string>();

    public FakeSettings() : base(typeof(ISettings)) { }
    public ISettings Interface { get { return (ISettings) GetTransparentProxy(); } }

    public override IMessage Invoke(IMessage message) {
        IMethodCallMessage call = (IMethodCallMessage) message;
        try {
            object result = null;
            switch(call.MethodName) {
                case "GetKbdBacklightSupport": result = Supported; break;
                case "GetKbdColorSupport": result = ColorSupported; break;
                case "GetKbdBacklight": result = Backlight ? BiosData.Backlight.On : BiosData.Backlight.Off; break;
                case "GetKbdColor": result = new BiosData.ColorTable(Colors); break;
                case "SetKbdColor":
                    if(FailWrite) throw new InvalidOperationException("Firmware rejected colors");
                    if(!IgnoreWrite) Colors = ((BiosData.ColorTable) call.Args[0]).ToString();
                    Writes.Add("Color:" + Colors);
                    break;
                case "SetKbdBacklight":
                    if(FailWrite) throw new InvalidOperationException("Firmware rejected backlight");
                    if(!IgnoreWrite) Backlight = (bool) call.Args[0];
                    Writes.Add("Backlight:" + Backlight);
                    break;
                default: throw new InvalidOperationException("Unexpected hardware call: " + call.MethodName);
            }
            return new ReturnMessage(result, null, 0, call.LogicalCallContext, call);
        } catch(Exception error) {
            return new ReturnMessage(error, call);
        }
    }
}

static class KeyboardLightingTests {
    const string Selected = "7900FF:FF17D0:F97000:FFFFFF";
    static int Checks;

    static void Check(bool condition, string name) {
        if(!condition) throw new Exception("FAIL: " + name);
        Checks++;
        Console.WriteLine("PASS: " + name);
    }

    static void Reject(Action action, string name) {
        bool rejected = false;
        try { action(); } catch(ArgumentException) { rejected = true; }
        Check(rejected, name);
    }

    static void Field(object target, string name, object value) {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    [STAThread]
    static int Main(string[] args) {
        try {
            Config.Initialize();
            Config.FilePath = args[0];
            Config.Load();
            Config.KeyboardColorDefault = "";
            Config.KeyboardBacklightDefault = null;
            Config.AutoConfig = false;

            BiosData.ColorTable table = new BiosData.ColorTable(Selected.ToLowerInvariant());
            Check(table.ToString() == Selected, "RGB channel order and canonical serialization");
            byte[] wire = Conv.GetByteArray(table);
            Check(wire.Length == 128 && wire[0] == 3 && wire[25] == 0x79
                && wire[26] == 0 && wire[27] == 0xFF, "128-byte wire format and zone offsets");
            Check(new BiosData.ColorTable(wire).ToString() == Selected, "BIOS color table round trip");
            Reject(() => new BiosData.ColorTable("FF0000"), "partial input cannot turn other zones black");
            Reject(() => new BiosData.ColorTable("FF0000:00FF00:0000FF:FFFFF"), "incomplete final zone rejected");
            Reject(() => new BiosData.ColorTable("FF0000:00FF00:0000FF:1000000"), "out of range RGB rejected");
            Reject(() => new BiosData.ColorTable("FF0000:00FF00:0000FF:GGGGGG"), "invalid RGB rejected");
            Reject(() => new BiosData.ColorTable(new int[3]), "short GUI array rejected");
            Reject(() => new BiosData.ColorTable(new byte[] { 255 }), "invalid firmware data rejected");

            FakeSettings hardware = new FakeSettings();
            KeyboardLighting lighting = new KeyboardLighting(hardware.Interface);
            Check(!lighting.Restore() && hardware.Writes.Count == 0, "old config leaves lighting untouched");
            lighting.SetColors(table);
            Check(Config.AutoConfig && Config.KeyboardColorDefault == Selected
                && Config.KeyboardBacklightDefault == null, "color choice persisted without forcing backlight");
            lighting.SetBacklight(false);
            Check(hardware.Colors == Selected && hardware.Writes[hardware.Writes.Count - 1] == "Backlight:False",
                "backlight toggle preserves selected colors");

            Config.KeyboardColorDefault = "";
            Config.KeyboardBacklightDefault = null;
            Config.Load();
            Check(Config.KeyboardColorDefault == Selected && Config.KeyboardBacklightDefault == false,
                "fresh process loads last applied lighting");
            hardware.Colors = "000000:000000:000000:000000";
            hardware.Backlight = true;
            hardware.Writes.Clear();
            Check(lighting.Restore() && hardware.Colors == Selected && !hardware.Backlight,
                "startup repairs firmware color and backlight reset");
            Check(hardware.Writes.Count == 2 && hardware.Writes[0].StartsWith("Color:"),
                "restore applies colors before explicit backlight state");
            hardware.Writes.Clear();
            lighting.Restore();
            Check(hardware.Writes.Count == 0, "bounded followup avoids redundant firmware writes");

            hardware.Supported = false;
            Check(!lighting.Restore() && hardware.Writes.Count == 0, "unsupported keyboard receives no writes");
            hardware.Supported = true;
            hardware.ColorSupported = false;
            hardware.Backlight = true;
            lighting.Restore();
            Check(hardware.Writes.Count == 1 && hardware.Writes[0] == "Backlight:False",
                "monochrome keyboard can restore backlight independently");
            hardware.ColorSupported = true;
            hardware.Writes.Clear();

            hardware.FailWrite = true;
            string saved = File.ReadAllText(Config.FilePath);
            try { lighting.SetColors(new BiosData.ColorTable("000000:000000:000000:000000")); }
            catch(InvalidOperationException) { }
            Check(Config.KeyboardColorDefault == Selected && saved == File.ReadAllText(Config.FilePath),
                "failed write cannot replace persisted choice");
            hardware.FailWrite = false;
            hardware.IgnoreWrite = true;
            try { lighting.SetColors(new BiosData.ColorTable("000000:000000:000000:000000")); }
            catch(InvalidOperationException) { }
            Check(Config.KeyboardColorDefault == Selected && saved == File.ReadAllText(Config.FilePath),
                "acknowledged but ineffective write cannot replace persisted choice");
            hardware.IgnoreWrite = false;
            hardware.Writes.Clear();
            Config.KeyboardColorDefault = "FF0000";
            Reject(() => lighting.Restore(), "malformed saved color rejected before firmware writes");
            Check(hardware.Writes.Count == 0, "malformed restoration has no partial side effects");
            Config.KeyboardColorDefault = Selected;

            // Construct only the rendering seam, without starting the tray, EC or WMI.
            GuiTray tray = (GuiTray) FormatterServices.GetUninitializedObject(typeof(GuiTray));
            GuiOp op = (GuiOp) FormatterServices.GetUninitializedObject(typeof(GuiOp));
            Platform platform = (Platform) FormatterServices.GetUninitializedObject(typeof(Platform));
            typeof(Platform).GetProperty("System").SetValue(platform, hardware.Interface, null);
            Field(op, "Platform", platform);
            Field(op, "Keyboard", lighting);
            Field(tray, "Op", op);
            GuiKbd preview = new GuiKbd(tray);
            Check(hardware.Writes.Count == 0 && preview.GetImage() != null,
                "initial hardware read renders even black/off lighting without writing");
            hardware.Colors = "112233:445566:778899:AABBCC";
            preview.GetHw();
            preview.Update();
            Check(hardware.Writes.Count == 0 && Config.KeyboardColorDefault == Selected,
                "hardware reads and redraws cannot replace saved colors");
            int[] copy = preview.GetColors();
            copy[0] = 0;
            Check(preview.GetColor(BiosData.KbdZone.Right) == 0x112233, "GUI color snapshot cannot mutate cached state");
            preview.SetColors(table);
            Check(hardware.Writes.Count == 1 && preview.GetParam().ToUpperInvariant() == Selected,
                "GUI color selection performs one color write only");

            // Keep an unknown extension node and user presets through normal saves.
            Check(File.ReadAllText(Config.FilePath).Contains("ReviewFixture")
                && File.ReadAllText(Config.FilePath).Contains("Fixture User Preset"),
                "lighting saves preserve existing configuration and user presets");

            FanProgram program = (FanProgram) FormatterServices.GetUninitializedObject(typeof(FanProgram));
            Field(program, "Levels", new List<byte> { 70, 90 });
            MethodInfo getLevel = typeof(FanProgram).GetMethod("GetTemperatureLevel", BindingFlags.Instance | BindingFlags.NonPublic);
            Check((byte) getLevel.Invoke(program, new object[] { (byte) 30 }) == 70,
                "curve beginning above zero safely handles a cold start");
            Check((byte) getLevel.Invoke(program, new object[] { (byte) 90 }) == 90
                && (byte) getLevel.Invoke(program, new object[] { (byte) 100 }) == 90,
                "fan curve preserves exact and highest temperature steps");
            Check(!program.Run(null), "missing alternate fan plan cannot crash power change");
            Config.FanProgram["Empty Fixture"] = new FanProgramData("Empty Fixture", BiosData.FanMode.Default,
                BiosData.GpuPowerLevel.Minimum, new SortedDictionary<byte, byte[]>());
            Check(!program.Run("Empty Fixture"), "empty fan curve rejected before enabling control");
            Console.WriteLine("All " + Checks + " checks passed.");
            return 0;
        } catch(Exception error) {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
