using System;
using OmenMon.Hardware.Bios;
using OmenMon.Hardware.Platform;
using OmenMon.Library;

namespace OmenMon.AppGui {

    // Owns user-selected lighting independently of the optional main form.
    // Reading hardware or repainting a preview must never select a new default.
    public sealed class KeyboardLighting {

        private readonly ISettings System;
        private readonly object Sync = new object();

        public KeyboardLighting(ISettings system) {
            this.System = system;
        }

        public void SetBacklight(bool enabled) {
            lock(Sync) {
                this.System.SetKbdBacklight(enabled);
                if((this.System.GetKbdBacklight() == BiosData.Backlight.On) != enabled)
                    throw new InvalidOperationException("Keyboard backlight readback did not match the requested state.");
                Config.KeyboardBacklightDefault = enabled;
                Config.AutoConfig = true;
                Config.Save();
            }
        }

        public void SetColors(BiosData.ColorTable colors) {
            // Validate and snapshot all four zones before any hardware write.
            string value = colors.ToString();
            lock(Sync) {
                this.System.SetKbdColor(new BiosData.ColorTable(value));
                if(this.System.GetKbdColor().ToString() != value)
                    throw new InvalidOperationException("Keyboard color readback did not match the requested colors.");
                Config.KeyboardColorDefault = value;
                Config.AutoConfig = true;
                Config.Save();
            }
        }

        public bool Restore() {
            lock(Sync) {
                bool hasColors = !String.IsNullOrEmpty(Config.KeyboardColorDefault);
                if(!hasColors && !Config.KeyboardBacklightDefault.HasValue)
                    return false;

                // Reject malformed saved data before changing either setting.
                BiosData.ColorTable colors = hasColors
                    ? new BiosData.ColorTable(Config.KeyboardColorDefault)
                    : default(BiosData.ColorTable);

                if(!this.System.GetKbdBacklightSupport())
                    return false;

                bool restored = false;
                string readback = "";
                if(hasColors && this.System.GetKbdColorSupport()) {
                    readback = this.System.GetKbdColor().ToString();
                    if(readback != colors.ToString()) {
                        this.System.SetKbdColor(colors);
                        readback = this.System.GetKbdColor().ToString();
                        if(readback != colors.ToString())
                            throw new InvalidOperationException("Keyboard color restore readback did not match the saved colors.");
                    }
                    restored = true;
                }

                // Apply colors first, then the user's explicit backlight choice.
                // A color-only choice preserves the firmware's on/off state.
                if(Config.KeyboardBacklightDefault.HasValue) {
                    if((this.System.GetKbdBacklight() == BiosData.Backlight.On)
                        != Config.KeyboardBacklightDefault.Value)
                        this.System.SetKbdBacklight(Config.KeyboardBacklightDefault.Value);
                    if((this.System.GetKbdBacklight() == BiosData.Backlight.On)
                        != Config.KeyboardBacklightDefault.Value)
                        throw new InvalidOperationException("Keyboard backlight restore readback did not match the saved state.");
                    restored = true;
                }

                if(restored)
                    FirmwareTrace.Status("GUI", "KeyboardLighting", "Restored colors=" + readback
                        + "; backlight=" + (Config.KeyboardBacklightDefault.HasValue
                            ? Config.KeyboardBacklightDefault.Value.ToString() : "preserved"));
                return restored;
            }
        }

    }
}
