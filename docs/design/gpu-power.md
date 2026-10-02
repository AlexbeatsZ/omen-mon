# GPU Power Selection and Verification

## Goal

Expose the existing upstream Custom TGP / PPAB controls as a persistent,
readback-verified GPU selection. These switches permit firmware-defined extra
power; they do not specify watts or force GPU consumption at idle.

## State and ownership

- `GpuPowerOverride`: empty means follow the active fan curve's `GpuPower`, or
  `GpuPowerDefault` when no curve is active. A named Minimum / Medium / Maximum
  value is an explicit user choice that takes precedence over curves.
- Keep the existing curve definitions, `GpuPowerDefault`, and double-write
  delay. Do not change CPU limits, sensors, fan policy or power adapters to
  manufacture a successful GPU result.
- One `GpuPowerControl` on Platform serializes selection and recovery. Save a
  manual choice and enable AutoConfig only after fresh firmware readback agrees
  with CustomTgp and Ppab. DState and PeakTemperature are reported state, not
  preset success criteria.
- Readback failures/rejected writes preserve the previous saved choice. The UI
  shows actual firmware state separately from the requested selection.
- Configuration saves are serialized and atomically replace XML. Save failures
  propagate and restore the previous saved selection in memory.

## Recovery and scheduling

- Refresh the existing FanCount context before a necessary GPU write. Keep
  reads, the upstream two writes, and verification within the shared BIOS/EC
  gate; no separate hardware lock or new polling thread.
- Fan updates compare a fresh GPU readback after their fan-mode write. Cached
  pre-write GPU data must not hide a firmware reset or undo a manual choice.
- Startup, the existing bounded startup/resume recovery, and fan transitions
  resolve the same effective selection. An explicit choice without a curve is
  checked by the existing 30-second heartbeat. The optional force-reapply flag
  also uses this selection instead of overriding it with the curve setting.
- Background GPU errors are logged without aborting cooling. Manual actions
  report failure; no unlimited immediate retry loop.

## Validation boundaries

Test presets/payloads, ignored and rejected writes, persistence, curve versus
override precedence, firmware resets, cache invalidation, startup recovery and
fan-program restoration with fake hardware. Deploy both binary and retained
portable XML. Compare real firmware flags across Minimum / Medium / Maximum
and return the installation to its original choice. A matching BIOS switch is
not proof of a particular sustained GPU wattage; workload, thermal and shared
CPU/GPU power constraints still apply.
