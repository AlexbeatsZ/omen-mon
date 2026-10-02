using System;
using System.Globalization;
using OmenMon.Hardware.Bios;
using OmenMon.Library;

namespace OmenMon.Hardware.Platform {
    // The HP interface acknowledges writes but provides no general PL1/PL4 readback.
    public sealed class CpuPowerControl {
        private readonly Action<BiosData.CpuPowerData> Write;
        private readonly Action RefreshContext;
        private readonly object Sync = new object();
        private string Sent = "";

        public CpuPowerControl(Action<BiosData.CpuPowerData> write, Action refreshContext) {
            this.Write = write;
            this.RefreshContext = refreshContext;
        }

        public string LastSent { get { lock(this.Sync) return this.Sent; } }

        // Empty values are 0xFF (no change). PL2 follows PL1 as in upstream CLI.
        public static BiosData.CpuPowerData Parse(string value) {
            BiosData.CpuPowerData data = new BiosData.CpuPowerData();
            if(String.IsNullOrEmpty(value)) return data;
            string[] parts = value.Split(':');
            if(parts.Length != 3)
                throw new ArgumentException("CPU power needs PL1:PL4:CPUWithGpu.");
            data.Limit1 = ParseLimit(parts[0]);
            data.Limit2 = data.Limit1;
            data.Limit4 = ParseLimit(parts[1]);
            data.LimitWithGpu = ParseLimit(parts[2]);
            return data;
        }

        private static byte ParseLimit(string value) {
            if(String.IsNullOrWhiteSpace(value)) return Byte.MaxValue;
            byte watts;
            if(!Byte.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out watts)
                || watts == 0 || watts == Byte.MaxValue)
                throw new ArgumentException("CPU limit must be 1-254 watts or empty.");
            return watts;
        }

        public static string Format(BiosData.CpuPowerData data) {
            return FormatLimit(data.Limit1) + ":" + FormatLimit(data.Limit4)
                + ":" + FormatLimit(data.LimitWithGpu);
        }

        private static string FormatLimit(byte value) {
            return value == Byte.MaxValue ? "" : value.ToString(CultureInfo.InvariantCulture);
        }

        public static bool HasLimits(BiosData.CpuPowerData data) {
            return data.Limit1 != Byte.MaxValue || data.Limit4 != Byte.MaxValue
                || data.LimitWithGpu != Byte.MaxValue;
        }

        public void Select(string value) {
            lock(this.Sync) {
                BiosData.CpuPowerData data = Parse(value);
                if(HasLimits(data)) Apply(data);
                string previous = Config.CpuPowerDefault;
                bool autoConfig = Config.AutoConfig;
                try {
                    Config.CpuPowerDefault = HasLimits(data) ? Format(data) : "";
                    Config.AutoConfig = true;
                    Config.Save(true);
                } catch {
                    Config.CpuPowerDefault = previous;
                    Config.AutoConfig = autoConfig;
                    throw;
                }
            }
        }

        public bool Restore() {
            lock(this.Sync) {
                BiosData.CpuPowerData data = Parse(Config.CpuPowerDefault);
                if(!HasLimits(data)) return false;
                Apply(data);
                return true;
            }
        }

        private void Apply(BiosData.CpuPowerData data) {
            Hw.WithFirmwareGate(() => {
                this.RefreshContext();
                this.Write(data);
                return true;
            });
            this.Sent = Format(data);
            FirmwareTrace.Status("CPU", "Power", "Accepted PL1:PL4:CPUWithGpu="
                + this.Sent + "; PL2 follows PL1; no PL1/PL4 readback available");
        }
    }
}
