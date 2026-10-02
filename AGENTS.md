# AI Maintenance Notes

## Goal

Maintain this OmenMon fork as a persistent GUI fan-control utility for HP Omen
laptops. Preserve the built-in FanCount performance heartbeat, quiet unified
fan curves, saved GUI fan plans, and last-applied keyboard lighting.

The heartbeat is functionally required on the target laptop: community and
local testing found that without Platform.Fans.GetCount() fan control can
revert to firmware defaults after roughly two minutes. Do not remove it while
changing tray behavior, autoconfiguration, or firmware scheduling.

## Current State

- Branch: `codex/ui-refactor-control-panel`; firmware-serialization baseline
  `1ad711d`. Build outputs `Bin/`, `Obj/`, and `Dist/` are ignored.
- Local portable install: `C:\Portable Programs\OmenMon`.
- Main GUI: system status/log on the left, CPU/GPU vertical percentage bars in
  the middle, fan/CPU/GPU plans on the right. Lighting has its own tab.
- Fan plans: Curve, Firmware, Fixed and Max. Modern firmware modes: Default,
  Performance and Cool. Legacy modes and Fan Off stay out of the normal UI.
  Infer support from ThermalPolicy, SupportFlags and current HPCM; never probe
  support by writing all possible policies.
- FanPlanDefault preserves the exact GUI plan; successful application saves it
  and enables AutoConfig. Startup supports every plan kind.
- Fan percentages convert configured hardware levels. XML retains hardware
  values. Silent/Balanced remain quiet below about 70-80 C, ramp gradually at
  80-90 C and retain protective high-temperature steps.
- Every BIOS/WMI and EC operation uses one shared reentrant gate in Hw. The
  30-second heartbeat remains. Bounded write-through diagnostics are stored as
  OmenMon-firmware.log plus one previous 4 MiB log.
- 2026-10-01: lighting changes save an independent RGB snapshot and optional
  backlight choice after successful readback. Startup/resume use bounded
  recovery. Hardware reads and preview redraws never write lighting.
- 2026-10-01: low-temperature fan lookup handles curves starting above the
  current temperature. Empty curves/null names are rejected safely.
- 2026-10-01 validation: Release build, 31 isolated regression checks, and controlled
  OEM-to-user lighting restoration with matching real firmware readback.
  Existing portable presets, fan plans and other XML content were preserved.
- 2026-10-02: custom UI now exposes CPU PL1/PL4/concurrent watt inputs. CPU
  commands persist and replay; status is acknowledged send, not PL1/PL4 readback.
  GPU choices persist independently of curves, require matching fresh readback,
  and have an explicit curve-follow option. Existing configs get no CPU writes.
- 2026-10-02: configuration saves are serialized and atomically replace XML.
  Power save failures propagate and preserve prior saved choices. Release build
  and 75 isolated checks passed; real three-preset GPU restart/readback confirmed
  Off/Off, On/Off and On/On without curve override or firmware errors.

## Active Work

- Completed: GUI plan/percentage refactor, exact startup plan persistence,
  quieter curves, shared firmware gate, bounded diagnostics, keyboard startup
  persistence and curve boundary corrections. Builds copied to the portable
  install with the user's existing configuration retained.
- Hardware acceptance still needed: lighting after a normal OS reboot and
  sleep/resume. Process-restart recovery was tested; neither OS action was
  forced during the 2026-10-01 work.
- Hardware acceptance still needed: firmware Default/Performance/Cool after
  application and reboot; long-duration freeze/0x101 causality remains a
  separate investigation. Short samples do not establish a freeze fix.
- Follow-up candidates: UI thread ownership/general startup error handling,
  CIM resource lifetime and exact fan-plan recovery on power
  transitions. See the command/reliability review before broadening changes.
- CPU watt acceptance requires user-selected values. No PL1/PL4 current-value
  query is supplied by this BIOS interface. OS reboot/sleep and workload GPU
  wattage remain unforced; saved restoration is after login, not in BIOS NVRAM.
- Temporary local `Bin/power-review-original.xml` is retained because automatic
  approval review rejected its cleanup (`blocked by policy`). It is ignored by
  Git and is not read by the resident application.

## Build / Run / Test

This environment can lack the .NET Framework 4.8 targeting pack. Use the existing
Visual Studio toolchain and FrameworkPathOverride. Do not install global SDKs
merely to build this project.

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe' OmenMon.csproj /p:Configuration=Release /p:FrameworkPathOverride=C:\Windows\Microsoft.NET\Framework64\v4.0.30319
.\Tests\run-keyboard-tests.ps1
```

The runner executes KeyboardLightingTests and PowerControlTests with fake
hardware and cleans up under %LOCALAPPDATA%\Temp\.agents. It covers lighting,
fan boundaries, CPU/GPU persistence, error handling and actual GUI apply paths;
it is not a real reboot/sleep test. Optional preview:
`Tests\run-keyboard-tests.ps1 -PreviewPath Bin\power-panel-preview.png`.

### Portable deployment - every runnable build

Every new runnable build must also overwrite the local portable install:

```text
Bin\OmenMon.exe -> C:\Portable Programs\OmenMon\OmenMon.exe
Bin\OmenMon.xml -> C:\Portable Programs\OmenMon\OmenMon.xml
```

Do not replace user presets/plans with example XML. The build copies repository
XML to Bin; preserve and merge the current installed configuration into the
output before deployment when they differ. Copy both exe and XML. Wait for
actual process exit before copying a locked exe:

```powershell
$targets = @(Get-Process OmenMon -ErrorAction SilentlyContinue)
foreach($target in $targets) {
    Stop-Process -Id $target.Id -Force
    $target.WaitForExit()
}
Copy-Item -LiteralPath Bin\OmenMon.exe -Destination 'C:\Portable Programs\OmenMon\OmenMon.exe' -Force
Copy-Item -LiteralPath Bin\OmenMon.xml -Destination 'C:\Portable Programs\OmenMon\OmenMon.xml' -Force
```

The executable requires administrator privileges. The existing OmenMon task
runs OmenMon.exe -Run Gui with highest privileges; use it to restart the
resident app without creating a new task or prompting for UAC:

```powershell
Start-ScheduledTask -TaskName OmenMon
```

Restore a user's previously running resident app after deployment. Do not leave
extra test-launched tray processes running. Check startup/readback and firmware
errors; short smoke samples do not prove long-duration stability.

### Packaging and releases

Create portable folders/zips under ignored Dist/. Contents: OmenMon.exe,
OmenMon.xml, OmenMon.exe.config and README.md. Release assets should include the
exe, XML and portable zip. Use a new tag; do not replace old assets unless asked.
Binaries do not belong in git by default.

Previous tag: ui-refactor-control-panel-20260522
https://github.com/AlexbeatsZ/omen-mon/releases/tag/ui-refactor-control-panel-20260522

Commit meaningful source changes and push when connected to GitHub. Remote:
https://github.com/AlexbeatsZ/omen-mon.git. Verify current authentication.

## Design Documents

Read the relevant document before modifying its module:

- [Firmware scheduling and freeze investigation](docs/design/firmware-control-scheduling.md):
  heartbeat, fan timing, BIOS/WMI, EC, monitoring and diagnostics.
- [Keyboard lighting persistence](docs/design/keyboard-lighting.md): snapshots,
  readback, rendering/write separation, startup/resume and deployment.
- [GPU power](docs/design/gpu-power.md): independent selection, curve priority,
  fresh verification, recovery and the shared firmware gate.
- [CPU power](docs/design/cpu-power.md): direct watt inputs, upstream payload,
  acknowledged sends, persistence and bounded recovery.
- [Upstream and power review](docs/review/2026-10-02-upstream-and-power.md):
  inherited features, confirmed defects and live GPU acceptance boundaries.
- [Command and reliability review](docs/review/2026-10-01-command-and-reliability.md):
  HP public documentation, upstream/Linux comparisons and remaining opportunities.

### Unified fan curve contract

```text
Tmax = Platform.GetMaxTemperature(true)
temperature step = GetTemperatureLevel(Tmax)
fan level = unified level
SetLevels(fan level, fan level)
```

Old XML CPU/GPU differences merge with max(cpu,gpu). Save identical values for
both fans. Do not introduce independent temperature semantics in the GUI
without also changing the lower-level runtime and design documentation.

## Durable Lessons

- Writing manual levels before a firmware policy can practically override it.
  Do not prepend SetLevels(255,255) when selecting a firmware policy in the GUI.
- A 30-second heartbeat and 15-second fan update starting together align on
  every heartbeat. A heartbeat-only busy flag cannot protect the fan loop/EC;
  separate BIOS and EC locks also miss BIOS calls that touch the EC. Preserve
  one shared firmware gate.
- FanProgram.UpdateFanMode()/UpdateGpuPower() need braces around conditional
  writes; missing braces previously forced writes even when checks said skip.
- In GuiFormFanCurve, clear rows/columns, add columns, then add the two rows.
  Reversing this order crashes DataGridView row creation.
- A stop request can return before Windows releases the executable. Wait for
  process exit before deployment to avoid transient sharing violations.
- GPU writes must invalidate cached power data even if the second upstream
  write fails. A stale cache can hide resets after fan-mode changes; verify the
  two requested switches using a fresh read before claiming an applied preset.
