# Command and Reliability Review

Reviewed: 2026-10-01 on HP OMEN Transcend 16-u0xxx, board 8BB3.
Baseline: `1ad711d` on `codex/ui-refactor-control-panel`.

## Result

The reviewed BIOS command identifiers and payloads match OmenMon's upstream
implementation. Selected performance commands are independently corroborated by
the Linux HP WMI driver. No command-number change was justified by this review.

HP's public documentation describes lighting and performance features, with
model-specific availability. It is not a complete public specification for the
private `hpqBIOSInt` binary interface. Matching upstream and Linux source is
cross-validation, not HP certification of every undocumented command or of this
laptop's firmware behavior.

## Reference comparison

| Operation | Existing command and data | Assessment |
| --- | --- | --- |
| Keyboard color | `0x20009`, get `0x02`, set `0x03`, 128-byte table | Matches upstream; RGB starts at byte 25, last zone index 3 means four zones |
| Backlight | `0x20009`, get `0x04`, set `0x05`, off `0x64`, on `0xE4` | Matches upstream/documented OmenMon representation |
| Fan count | `0x20008`, `0x10` | Matches upstream and Linux; keepalive behavior is a hardware/community observation |
| Fan policy | `0x20008`, `0x1A`, payload starts `FF,mode` | Linux also uses the `FF` prefix; modern modes `30/31/50` match |
| Fan levels | `0x20008`, get `0x2D`, set `0x2E` | Matches upstream and corresponding Linux path; implementation is model dependent |
| Max fan | `0x20008`, get `0x26`, set `0x27` | Matches upstream and Linux |
| GPU power | `0x20008`, get `0x21`, set `0x22` | Matches upstream and Linux identifiers |
| CPU power | `0x20008`, set `0x29` | Matches upstream and Linux identifier; no general PL1/PL2 readback is supplied here |
| GPU MUX | read command `1`, write command `2`, type `0x52` | Matches upstream and current Linux identifiers; support and mode masks remain model dependent |

References:

- [HP OMEN Gaming Hub](https://support.hp.com/gb-en/document/ish_3912817-3737596-16)
- [HP OMEN Light Studio](https://support.hp.com/ca-en/document/ish_9282184-5659745-16)
- [OmenMon CLI documentation](https://omenmon.github.io/cli)
- [OmenMon configuration documentation](https://omenmon.github.io/config)
- [Upstream BIOS command implementation](https://github.com/OmenMon/OmenMon/blob/master/Hardware/BiosCtl.cs)
- [Linux HP WMI driver](https://github.com/torvalds/linux/blob/master/drivers/platform/x86/hp/hp-wmi.c)

The experimental animation table has an unknown format and is already described
upstream as potentially having no effect. It should not be treated as a working
Light Studio animation API. `PerformanceHeartbeatReapplyCpuPower` is a reserved
configuration flag with no implementation, explicitly stated in `GuiOp`; it
does not promise effective periodic CPU power control.

## Implemented fixes

1. **Reboot lighting loss:** save the applied color snapshot and optional
   explicit backlight choice, restore without requiring a visible main form,
   and verify firmware readback. Add bounded startup/resume recovery.
2. **Rendering writes firmware:** separate hardware reads/rendering from
   selection writes; suppress programmatic textbox input events. Do not write
   cached colors when only toggling the backlight.
3. **Partial input darkens zones:** require four complete RGB values and reject
   malformed firmware tables. Retain the existing 128-byte RGB wire format.
4. **Low-temperature curve crash:** reproduced an out-of-range index when a
   curve begins at 70 C and the measured temperature is 30 C. Clamp lookup to
   the first step below its threshold. Reject empty curves and null program
   names before enabling control. Existing high-temperature steps are preserved.

## Further improvements worth scheduling

- **GUI thread ownership and startup errors:** `AutoConfigRun` still applies
  GPU/fan settings on a background thread, calls some form methods from that
  thread, and has no general exception boundary. `FanProgramCallback` and power
  status callbacks also contain direct UI updates. Route UI changes to the
  main thread and isolate failures by operation before widening asynchronous
  control. The new lighting refresh already marshals back to that thread.
- **BIOS resource lifetime:** `Bios.Send` manually disposes CIM results and
  parameters on the success path, before finishing reading the embedded output
  instance. Failure paths do not uniformly dispose every object. Use nested
  `using` scopes and detach output bytes before disposing results. This is a
  code-quality concern, not evidence that it caused the historical freezes.
- **Atomic configuration saving:** `Config.Save` rewrites the live XML directly.
  A same-directory temporary file plus atomic replacement would better preserve
  presets and curves across interruptions; coordinate concurrent saves as part
  of that change and test failure recovery.
- **Exact fan-plan recovery on AC transitions/resume:** `PowerChange` switches
  active curves by the legacy program fields, while fixed/firmware/max plans
  use `FanPlanDefault` for startup. Consolidate the saved-plan state machine so
  these events consistently restore the selected plan. Validate on hardware
  before changing cooling behavior.

These are follow-up opportunities, not prerequisites for the reported lighting
repair. The review did not change sensors, fan ramps, performance-heartbeat
frequency, CPU/GPU power presets, or HP services.

## Verification

- Release build completed without compiler warnings/errors using the existing
  Visual Studio MSBuild and `FrameworkPathOverride` workflow.
- 31 isolated hardware-free regression checks passed, including a regression
  that failed on the original fan-curve lookup before the boundary correction.
- Updated both executable and configuration in the portable installation.
- Controlled OEM color reset and process restart: firmware readback confirmed
  the selected colors, followed by the same result in the delayed check.
- Pre-existing portable XML content remained identical after excluding the new
  default-color field; user presets and fan configuration were preserved.
- Final running-process sample: 410 completed firmware operations, 19 completed
  FanCount calls, zero shared-gate violations and zero firmware errors. This is
  a short runtime sample, not long-duration freeze acceptance.
- OS reboot, sleep/resume, and long-duration freeze acceptance were not claimed.
