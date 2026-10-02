using System;
using OmenMon.Hardware.Bios;
using OmenMon.Library;

namespace OmenMon.Hardware.Platform {

    // Owns the user's GPU selection independently of fan-curve settings.
    public sealed class GpuPowerControl {
        private readonly ISettings System;
        private readonly Action RefreshContext;
        private readonly object Sync = new object();
        private BiosData.GpuPowerData? Readback;

        public GpuPowerControl(ISettings system, Action refreshContext) {
            this.System = system;
            this.RefreshContext = refreshContext;
        }

        public BiosData.GpuPowerData? LastReadback {
            get { lock(this.Sync) return this.Readback; }
        }

        public static BiosData.GpuPowerLevel Parse(string value) {
            BiosData.GpuPowerLevel level;
            if(!Enum.TryParse(value, true, out level)
                || !Enum.IsDefined(typeof(BiosData.GpuPowerLevel), level))
                throw new ArgumentException("Invalid GPU power selection: " + value);
            return level;
        }

        public static BiosData.GpuPowerData Resolve(BiosData.GpuPowerData fallback) {
            return String.IsNullOrEmpty(Config.GpuPowerOverride) ? fallback
                : new BiosData.GpuPowerData(Parse(Config.GpuPowerOverride));
        }

        public static bool Matches(BiosData.GpuPowerData actual, BiosData.GpuPowerData target) {
            return actual.CustomTgp == target.CustomTgp && actual.Ppab == target.Ppab;
        }

        public static string Describe(BiosData.GpuPowerData data) {
            return "CustomTgp=" + data.CustomTgp + ", Ppab=" + data.Ppab
                + ", DState=" + data.DState + ", PeakTemperature=" + data.PeakTemperature;
        }

        // A failed selection must not replace the last verified saved choice.
        public BiosData.GpuPowerData Select(BiosData.GpuPowerLevel? level, BiosData.GpuPowerData fallback) {
            lock(this.Sync) {
                if(level.HasValue && !Enum.IsDefined(typeof(BiosData.GpuPowerLevel), level.Value))
                    throw new ArgumentException("Invalid GPU power level.");
                BiosData.GpuPowerData result = Apply(level.HasValue
                    ? new BiosData.GpuPowerData(level.Value) : fallback, false);
                string previous = Config.GpuPowerOverride;
                bool autoConfig = Config.AutoConfig;
                try {
                    Config.GpuPowerOverride = level.HasValue ? level.Value.ToString() : "";
                    Config.AutoConfig = true;
                    Config.Save(true);
                } catch {
                    Config.GpuPowerOverride = previous;
                    Config.AutoConfig = autoConfig;
                    throw;
                }
                FirmwareTrace.Status("GPU", "Selection", "Saved="
                    + (level.HasValue ? level.Value.ToString() : "Follow") + "; " + Describe(result));
                return result;
            }
        }

        public BiosData.GpuPowerData Ensure(BiosData.GpuPowerData fallback, bool force = false) {
            lock(this.Sync)
                return Apply(Resolve(fallback), force);
        }

        private BiosData.GpuPowerData Apply(BiosData.GpuPowerData target, bool force) {
            return Hw.WithFirmwareGate(() => {
                this.Readback = this.System.GetGpuPower(true);
                if(force || !Matches(this.Readback.Value, target)) {
                    this.RefreshContext();
                    this.System.SetGpuPower(target);
                    this.Readback = this.System.GetGpuPower(true);
                    FirmwareTrace.Status("GPU", "Power", "Requested=" + Describe(target)
                        + "; Actual=" + Describe(this.Readback.Value));
                }
                if(!Matches(this.Readback.Value, target))
                    throw new InvalidOperationException("GPU firmware did not apply the requested power: "
                        + Describe(this.Readback.Value));
                return this.Readback.Value;
            });
        }
    }
}
