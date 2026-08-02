# AI Maintenance Notes

This file records project-specific context and operational habits for future AI-assisted work on this fork.

## Project Context

This repository is a fork/refactor of OmenMon focused on persistent GUI fan control for HP Omen laptops.

The key practical difference from upstream is the built-in GUI heartbeat:

- Community testing found that `OmenMon.exe -Bios FanCount` can refresh the firmware performance-control context.
- Without this refresh, manual fan control may work briefly and then be reset by BIOS default policy after about two minutes.
- This fork should keep the equivalent BIOS fan-count call inside the GUI resident loop instead of relying on external PowerShell/VBS scripts.
- The relevant implementation path is `GuiOp.PerformanceHeartbeat()`, which calls `Platform.Fans.GetCount()`.
- Preserve this behavior when changing GUI fan control, tray behavior, or startup/autoconfig logic.

## Local Portable Install Rule

Every time a new runnable build is produced, also overwrite the local portable install:

```text
C:\Portable Programs\OmenMon
```

Minimum files to copy:

```text
Bin\OmenMon.exe -> C:\Portable Programs\OmenMon\OmenMon.exe
Bin\OmenMon.xml -> C:\Portable Programs\OmenMon\OmenMon.xml
```

`OmenMon.xml` must be copied with the exe because fan plans and important runtime configuration live there. Do not update only the exe when XML defaults or fan curves changed.

Before copying, stop any running OmenMon process if the target exe is locked:

```powershell
Get-Process OmenMon -ErrorAction SilentlyContinue | Stop-Process -Force
Copy-Item -LiteralPath Bin\OmenMon.exe -Destination 'C:\Portable Programs\OmenMon\OmenMon.exe' -Force
Copy-Item -LiteralPath Bin\OmenMon.xml -Destination 'C:\Portable Programs\OmenMon\OmenMon.xml' -Force
```

After copying, a short startup smoke test is useful:

```powershell
$p = Start-Process -FilePath 'C:\Portable Programs\OmenMon\OmenMon.exe' -WorkingDirectory 'C:\Portable Programs\OmenMon' -PassThru -WindowStyle Minimized
Start-Sleep -Seconds 3
if(Get-Process -Id $p.Id -ErrorAction SilentlyContinue){ Stop-Process -Id $p.Id -Force }
```

Do not leave a test-launched tray process running unless the user explicitly wants that.

## Build Command

This environment may lack the normal .NET Framework 4.8 targeting pack. The build that worked here used `FrameworkPathOverride`:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe' OmenMon.csproj /p:Configuration=Release /p:FrameworkPathOverride=C:\Windows\Microsoft.NET\Framework64\v4.0.30319
```

Expected output:

- `Bin\OmenMon.exe`
- `Bin\OmenMon.xml`

If `msbuild` is not in `PATH`, use the full Visual Studio MSBuild path above.

## Packaging / Release

Release packaging should create a portable folder and zip under `Dist\`, but `Dist\` is intentionally ignored by git.

Suggested package contents:

```text
OmenMon.exe
OmenMon.xml
OmenMon.exe.config
README.md
```

Example packaging flow:

```powershell
$distRoot = Join-Path (Resolve-Path .) 'Dist'
$pkg = Join-Path $distRoot 'OmenMon-ui-refactor-control-panel'
$zip = Join-Path $distRoot 'OmenMon-ui-refactor-control-panel.zip'
New-Item -ItemType Directory -Force -Path $pkg | Out-Null
Copy-Item -LiteralPath Bin\OmenMon.exe -Destination (Join-Path $pkg 'OmenMon.exe') -Force
Copy-Item -LiteralPath Bin\OmenMon.xml -Destination (Join-Path $pkg 'OmenMon.xml') -Force
Copy-Item -LiteralPath OmenMon.exe.config -Destination (Join-Path $pkg 'OmenMon.exe.config') -Force
Copy-Item -LiteralPath README.md -Destination (Join-Path $pkg 'README.md') -Force
if(Test-Path $zip){ Remove-Item -LiteralPath $zip -Force }
Compress-Archive -LiteralPath $pkg -DestinationPath $zip
```

GitHub CLI was available and authenticated as `AlexbeatsZ` during the previous release.

Previous release tag:

```text
ui-refactor-control-panel-20260522
```

Previous release URL:

```text
https://github.com/AlexbeatsZ/omen-mon/releases/tag/ui-refactor-control-panel-20260522
```

When creating a new release, upload at least:

- `OmenMon.exe`
- `OmenMon.xml`
- the portable zip

Use a new tag for each release; do not overwrite old release assets unless the user explicitly asks.

## GUI / Fan Control Design Notes

The main GUI was refactored around:

- left column: system status and operation log
- middle column: two vertical CPU/GPU fan level bars
- right column: fan, CPU, and GPU plan controls

Normal main UI should not show:

- Fan Off
- legacy fan modes
- large RPM/percent readouts
- old red/blue percent bars
- all raw temperature sensor tiles by default

Fan plans use `FanPlanKind`:

- `Curve`
- `Firmware`
- `Fixed`
- `Max`

Firmware modes shown in the normal UI should be modern modes only:

- Default
- Performance
- Cool

The firmware support listing should be based on readback hints such as `ThermalPolicy`, `SupportFlags`, and current `HPCM`. Do not probe support by writing every possible fan mode, because that changes machine state.

## Fan Curve Semantics

The actual runtime control model is:

```text
Tmax = Platform.GetMaxTemperature(true)
temperature level = GetTemperatureLevel(Tmax)
fan level = unified level
SetLevels(fan level, fan level)
```

Do not reintroduce separate CPU-temperature-to-CPU-fan and GPU-temperature-to-GPU-fan semantics in the GUI unless the lower-level runtime is also changed.

The XML schema still stores two values for compatibility:

```xml
<Level Temperature="85"><Cpu>40</Cpu><Gpu>40</Gpu></Level>
```

When loading old curves with different CPU/GPU levels, merge them with `max(cpu, gpu)`. When saving, write identical values.

## Quiet Curve Preference

The user prefers quieter curves:

- below roughly 70-80 C, avoid aggressive fan speeds
- 80-90 C can ramp gradually
- 90 C and above should still retain protective high fan levels

The intent is to avoid unnecessary noise when the laptop is not uncomfortable to touch and is not throttling.

Do not revert Silent/Balanced curves to the original aggressive levels unless explicitly asked.

## Lessons Learned

- Firmware fan plans should not write manual fan levels before setting `FanMode`. Writing `SetLevels(255,255)` while selecting a firmware policy can leave the firmware mode looking selected but practically overridden by the manual-control path.
- The GUI should present fan output as 0-100% and convert to the hardware level scale only at the BIOS/EC write boundary. The XML schema remains hardware-level based for compatibility.
- Persist the exact GUI fan plan as `FanPlanDefault`, not just `FanProgramDefault`, because startup restoration must support curve, firmware, fixed-percent, and max-fan plans.
- `FanProgram.UpdateFanMode()` and `UpdateGpuPower()` need braces around the conditional bodies; otherwise the final write still runs every update even when the pre-check says to skip it.

## Task Board

- Done: remove manual `SetLevels(255,255)` from GUI firmware fan mode application.
- Done: add `FanPlanDefault` loading/saving and startup restoration for curve, firmware, fixed-percent, and max-fan plans.
- Done: save the last applied GUI fan plan and enable `AutoConfig` after a successful GUI fan-plan apply.
- Done: convert main fan bars, fixed fan control, curve manager, chart, and readbacks to percentage display/input.
- Done: adjust default `CoolBoost` and `OmenBalanced` curves to smoother, stronger percent-based ramps.
- Done: build Release with MSBuild and sync `Bin` output to `C:\Portable Programs\OmenMon`.
- Needs hardware validation: confirm firmware Default/Performance/Cool now actually takes effect on the target Omen after applying and after reboot.

## Known Bug Fixed

The fan curve manager previously crashed with:

```text
Cannot add rows to a DataGridView that has no columns. Columns must be added first.
```

Cause:

- `Rows.Add(2)` was called before columns were added.

Fix:

- Clear rows/columns on program load.
- Add columns for the selected curve first.
- Then add the two horizontal rows: temperature and fan level.

Keep this order when editing `GuiFormFanCurve`.

## Source Control Notes

Current working branch used for this refactor:

```text
codex/ui-refactor-control-panel
```

Build outputs are ignored:

```text
Bin/
Obj/
Dist/
```

Do not commit generated exe/zip artifacts unless the user explicitly asks for binary artifacts in git. Prefer GitHub Releases for binaries.
