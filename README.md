# OmenMon UI Refactor

This repository is a fork of the original [OmenMon](https://github.com/OmenMon/OmenMon) project, focused on making fan control usable as a daily GUI tool on HP Omen laptops.

The original OmenMon already provides the important low-level BIOS and Embedded Controller access. This fork keeps that foundation, but changes the default GUI and fan-control workflow around one practical issue discovered by the community:

> Running `OmenMon.exe -Bios FanCount` can act like a heartbeat. It refreshes the firmware performance-control context, so manual fan control works for a short period. Without that heartbeat, the BIOS may reset fan control back to its default policy after roughly two minutes.

This fork builds that heartbeat behavior into the GUI's resident loop. You should not need an external PowerShell or VBS script just to keep fan settings alive.

## Main Differences From Original OmenMon

- Built-in GUI performance heartbeat based on the same BIOS call as `-Bios FanCount`.
- Fan settings are intended to stay effective while the GUI/tray app is running, instead of being reset shortly after applying.
- New three-column control panel:
  - system status and operation log
  - CPU/GPU vertical fan level bars
  - fan, CPU, and GPU plan controls
- Unified fan plan selector instead of separate auto/program/max/fixed/off radio buttons.
- "Fan off" is removed from the normal main screen.
- Legacy fan modes are hidden from the normal main screen.
- Firmware fan modes are shown as ordinary plans when supported by the firmware readback.
- Fan curve editing now follows the actual control model: `Tmax -> unified fan level`.
- Old CPU/GPU split fan levels are merged to the higher value and saved back as `{level, level}`.
- Quieter default curves. The Silent and Balanced plans avoid aggressive fan speeds below high temperature ranges, while keeping protective high-temperature steps.
- GPU power plans are simplified to three presets:
  - Base power
  - Enhanced power
  - Enhanced power + Boost
- GUI operation log records high-level actions, BIOS/EC calls, results, and readback summaries.

## Why The Heartbeat Matters

Some Omen firmware appears to treat manual fan settings as temporary unless a performance-control context is periodically refreshed. The community workaround was to run something equivalent to:

```powershell
OmenMon.exe -Bios FanCount
```

That works because the BIOS fan-count query refreshes the context without directly changing the fan curve.

This fork moves that behavior into the GUI resident loop through the existing `PerformanceHeartbeat` path. The practical result is that applying a fan plan from the GUI should keep working continuously while OmenMon is running, instead of falling back after about two minutes.

Relevant configuration options in `OmenMon.xml`:

- `PerformanceHeartbeatEnabled`
- `PerformanceHeartbeatInterval`
- `PerformanceHeartbeatOnlyWhenFanControlActive`
- `PerformanceHeartbeatForceFanMax`
- `PerformanceHeartbeatReapplyFanMax`
- `PerformanceHeartbeatReapplyGpuPower`

## Running

Use the release package or copy these files into the same folder:

- `OmenMon.exe`
- `OmenMon.xml`

Run `OmenMon.exe`. Use administrator privileges if BIOS or EC operations fail.

The current preferred portable folder for this fork is:

```text
C:\Portable Programs\OmenMon
```

## Fan Curves

The GUI treats fan programs as a single curve:

```text
highest temperature Tmax -> unified fan level
```

The original XML schema is preserved for compatibility, but saved fan levels are written as identical CPU/GPU values:

```xml
<Level Temperature="85"><Cpu>40</Cpu><Gpu>40</Gpu></Level>
```

This matches the runtime behavior: current control uses the maximum selected temperature sensor, not separate CPU-temperature-to-CPU-fan and GPU-temperature-to-GPU-fan loops.

## Firmware Modes

The normal GUI shows modern firmware modes only:

- Firmware Default
- Firmware Performance
- Firmware Cool

The app reads firmware/system data such as thermal policy, support flags, and current `HPCM` state. It does not probe support by writing every possible fan mode, because that would change the machine state.

## Build

This is a .NET Framework WinForms project.

Example build command used for this fork:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe' OmenMon.csproj /p:Configuration=Release /p:FrameworkPathOverride=C:\Windows\Microsoft.NET\Framework64\v4.0.30319
```

The normal build output is in `Bin\`.

## Upstream

Original project:

- [OmenMon/OmenMon](https://github.com/OmenMon/OmenMon)
- Project documentation: [omenmon.github.io](https://omenmon.github.io/)

This fork is not affiliated with or endorsed by HP. Any brand names are used for informational purposes only.

## License

OmenMon is free software under the GNU General Public License Version 3. See [LICENSE.md](LICENSE.md).
