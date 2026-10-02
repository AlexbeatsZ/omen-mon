# Upstream Features and CPU/GPU Recovery Review

Reviewed 2026-10-02; fork baseline `2164fba`, branch
`codex/ui-refactor-control-panel`; HP board 8BB3, RTX 4060 Laptop GPU.

## Upstream comparison

Fetched the public OmenMon/OmenMon master into `refs/remotes/upstream/master`.
Its head is `d89340e6d4dc9802b609390729b0bfaec05563e2`, dated 2024-05-06,
also the merge base of this fork. `git log HEAD..upstream/master` is empty.
There are no missing master commits to merge. Latest upstream release is
0.61.1, which fixes the standby callback delegate lifetime; it is already in
this ancestry.

| Upstream capability | Existing fork availability | This change |
| --- | --- | --- |
| CPU PL1/PL2, PL4, concurrent CPU power | BIOS/CLI implementation; GUI profiles were placeholders | Direct inputs in the existing right panel, real writes, save and startup/resume replay |
| Custom TGP and PPAB GPU presets | GUI/tray/CLI writes existed | Verify actual result, independent saved selection, curve-follow option, recovery |
| Fan control and programmable curves | Already present and extended | Preserve unified Tmax, quiet curves and 30-second heartbeat |
| Four-zone lighting and keyboard toggle | Already present, persistence repaired previously | Preserve existing snapshots and presets |
| GPU MUX, refresh rate and display/color actions | Existing tray/CLI paths | No speculative model-dependent changes |
| XMP, idle, firmware capabilities and EC monitor | Existing BIOS/CLI paths | Retain existing implementation; no new automatic writes |

The requested outcome is to expose useful upstream hardware capabilities
through the user's custom UI, not to replace the fork with upstream's GUI.

## Confirmed defects

1. CPU Apply did not write hardware at all. It logged a TODO and selected a
   label without persistence.
2. GPU Apply always reported success after reading firmware, even when the
   returned CustomTgp/PPAB did not match the request. It did not save the choice.
3. Fan programs owned a separate GPU target, so their updates could undo the
   user's independent choice.
4. Settings.SetGpuPower left its cached readback intact. Fan-program comparisons
   could use stale data and miss firmware resets.
5. Configuration saving swallowed errors and directly truncated the destination.
   Power controls could not reliably distinguish saved state from a failed save.

## Implemented behavior

The new controllers reuse upstream command IDs and payloads: GPU get/set
`0x21/0x22`, CPU `0x29`, all under `0x20008`. Preserve the upstream GPU double
write and configured delay. CPU PL2 follows PL1, with `0xFF` for unspecified
fields. Force command-status checking for CPU/GPU so ignored BIOS errors cannot
look like success.

GPU manual choices override curves; empty override retains prior curve behavior.
Fresh comparisons after fan-mode writes repair firmware resets only when needed.
Startup and the existing bounded startup/resume checks recover both controls.
Standalone GPU choices use the existing heartbeat. CPU heartbeat replay remains
opt-in; saved CPU values are not blindly written on every fan update.

Save CPU parameters / verified GPU choice only after a successful command.
Configuration writes are serialized and atomically replace the destination.
An unreadable existing file is preserved; power save errors propagate to the UI.
One shared BIOS/EC gate still covers every operation and the GPU transaction.

## Validation

- Release build succeeded with the existing Visual Studio toolchain.
- 75 isolated checks passed: 31 lighting/fan checks and 44 power/GUI checks.
  Includes actual GUI Apply handlers, saved reload/recovery, ignored/rejected
  writes, real Settings double-write/cache paths, blank CPU inputs and failed
  saves preserving user configuration. A simulated GUI screenshot confirmed
  CPU inputs and GPU section fit the existing three-column layout.
- Controlled resident-process restarts on this laptop verified all three GPU
  states, each at startup and delayed recovery, with two fan-mode updates and
  zero firmware errors per sample: Minimum Off/Off, Medium On/Off, Maximum
  On/On. This proves BIOS switch control and priority over an active Maximum
  fan curve, not a sustained workload wattage.
- Returned the portable installation to its original Curve:CoolBoost plan,
  curve-follow GPU choice, original keyboard colors and no invented CPU limits.
  Copied both binary and retained XML; the existing elevated logon task and
  one resident instance are used.
- No OS reboot, real sleep, GPU load benchmark or new CPU watt selection was
  forced. PL1/PL4 readback is unavailable; fake-hardware replay validates the
  CPU startup code path, not actual sustained CPU limits on this model.

NVIDIA-SMI reported default 80 W, maximum 130 W and current power limit N/A
on this driver. Those are capability/default metadata, not evidence of a current
130 W policy or expected idle consumption.

## Sources

- [Upstream head](https://github.com/OmenMon/OmenMon/commit/d89340e6d4dc9802b609390729b0bfaec05563e2)
- [Release 0.61.1](https://github.com/OmenMon/OmenMon/releases/tag/0.61.1)
- [Upstream BIOS implementation](https://github.com/OmenMon/OmenMon/blob/d89340e6d4dc9802b609390729b0bfaec05563e2/Hardware/BiosCtl.cs)
- [OmenMon CLI CPU/GPU documentation](https://omenmon.github.io/cli)
- [OmenMon GPU write delay](https://omenmon.github.io/config#gpupowersetinterval)
- [NVIDIA-SMI power reporting](https://docs.nvidia.com/deploy/nvidia-smi/index.html)
