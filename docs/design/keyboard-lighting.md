# Keyboard Lighting Persistence

Last verified: 2026-10-01

## Goal and scope

Restore the last successfully applied GUI lighting after OmenMon starts and
after sleep, without continuously fighting keyboard hotkeys or other lighting
software. Support the existing four-zone BIOS interface and backlight-only
devices. Per-key RGB remains outside this interface's supported scope.

The reported symptom was custom colors returning to the OEM defaults on reboot.
Previously `ColorPresets` saved a library of colors, but neither the selected
colors nor the backlight choice were saved or applied by startup configuration.

## State and configuration

- `KeyboardColorDefault` stores four complete six-digit RGB values in the
  existing `Right:Middle:Left:WASD` order. Empty means leave firmware colors alone.
- `KeyboardBacklightDefault` is optional. It is saved only after an explicit
  GUI backlight toggle. An absent setting leaves the firmware on/off state alone.
- `ColorPresets` remains a separate named library. Deleting a preset does not
  erase the independent color snapshot selected for startup.
- Successful GUI changes enable `AutoConfig`, matching fan-plan persistence.
  If the user later disables startup configuration, lighting is not restored.
- CLI color changes remain one-shot and do not rewrite GUI configuration.

`KeyboardLighting` is owned by `GuiOp`, not by `GuiFormMain`. Both the tray menu
and the main form therefore save selections even if the main form has never
opened. A write must succeed and its immediate readback must match before the
new value can replace the persisted choice. Keyboard setters always check BIOS
return codes, including when general `BiosErrorReporting` is disabled.

## Reading, editing and rendering

`GuiKbd.GetHw()` reads hardware into the preview only. `GuiKbd.Update()` redraws
only. Neither operation writes firmware or changes the startup defaults.

Explicit setters call `KeyboardLighting` before updating the preview. Backlight
toggles do not rewrite cached colors. UI color arrays are copied and normalized
without an alpha channel. `UpdatingKbd` suppresses the color textbox handler
while controls are populated programmatically.

Color input must contain exactly four complete RGB values. Previously even one
valid segment was accepted, with unspecified zones initialized to black. The
live textbox could therefore write a partial selection while the user typed.
Malformed firmware color tables are also rejected before indexing zone data.

## Restore scheduling and firmware access

Startup autoconfiguration attempts lighting before GPU and fan configuration.
A lighting failure is contained and recorded, allowing fan configuration to
continue. The GUI timer performs one additional check after the startup grace
period (six ticks including dispatch, about six seconds with the default timer).
Resume queues one check after three default timer ticks. Power callbacks do not
write lighting directly.

Restore compares actual colors/backlight first and writes only if different.
There is no ongoing RGB polling or repeated lighting heartbeat. Every underlying
BIOS read and write still uses `Hw`'s shared firmware gate. The performance
`FanCount` heartbeat and fan/temperature settings are unaffected.

The bounded firmware trace records `KeyboardLighting NOTE` entries with the
verified colors, or the reason a restore failed. Restoring colors is an
application action after login/startup; this does not program BIOS NVRAM or
change the colors shown before OmenMon runs.

## Validation and deployment

Run `Tests/run-keyboard-tests.ps1` after building. Its isolated fixture covers
configuration reload, firmware reset, rejected and ineffective writes, exact
RGB wire encoding, unsupported/monochrome devices, and hardware-free rendering.
The same fixture also protects the reviewed fan-curve boundaries.

The 2026-10-01 deployment retained the existing portable XML rather than
replacing it with the repository's example defaults. The sole additional user
preset in that installation was migrated to `KeyboardColorDefault`. All
pre-existing XML content was compared after excluding that new field and was
unchanged. Source defaults remain empty and contain no user-specific colors.

A controlled real-hardware test first applied the existing OEM preset, then
restarted with the saved user colors. Firmware readback confirmed both values,
including the delayed startup check. Real OS reboot and resume acceptance still
require a normal future reboot/sleep; neither was forced during this task.
