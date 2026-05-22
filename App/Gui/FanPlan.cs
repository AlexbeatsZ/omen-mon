  //\\   OmenMon: Hardware Monitoring & Control Utility
 //  \\  Copyright © 2023-2024 Piotr Szczepański * License: GPL3
     //  https://omenmon.github.io/

using OmenMon.Hardware.Bios;

namespace OmenMon.AppGui {

    public enum FanPlanKind {
        Curve,
        Firmware,
        Fixed,
        Max
    }

    public class FanPlan {

        public string Name { get; private set; }
        public FanPlanKind Kind { get; private set; }
        public string Description { get; private set; }
        public BiosData.FanMode? FirmwareMode { get; private set; }
        public byte? FixedLevel { get; set; }

        public FanPlan(
            string name,
            FanPlanKind kind,
            string description,
            BiosData.FanMode? firmwareMode = null,
            byte? fixedLevel = null) {

            this.Name = name;
            this.Kind = kind;
            this.Description = description;
            this.FirmwareMode = firmwareMode;
            this.FixedLevel = fixedLevel;

        }

        public string Value {
            get { return this.Kind.ToString() + ":" + this.Name; }
        }

        public override string ToString() {
            return this.Name + " [" + this.Description + "]";
        }

    }

}
