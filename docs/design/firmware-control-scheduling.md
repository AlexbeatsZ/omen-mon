# Firmware Control Scheduling and Freeze Investigation

Last verified: 2026-09-21

## Goal

Keep the `FanCount` performance heartbeat required by newer HP firmware while
preventing OmenMon from issuing overlapping BIOS/WMI and embedded-controller
operations. The heartbeat must not be removed: on the target laptop, firmware
fan control is reset after roughly two minutes without it.

This document records the evidence and the intended repair. It does not claim
that OmenMon is the only possible cause of the machine freezes.

## Upstream and Fork Differences

The fork currently shares upstream commit `d89340e` as its base. The hardware
control defaults below are unchanged from upstream:

- fan levels use BIOS/WMI rather than direct EC writes;
- the fan program runs every 15 seconds;
- `FanProgramModeCheckFirst=false`, reducing EC reads but forcing a BIOS/WMI
  fan-mode write on every fan-program update;
- the dynamic tray icon updates every 3 seconds;
- eight EC temperature sensors are enabled, while the BIOS temperature sensor
  is disabled.

Fork commit `2f08052` added the behavior that matters to this investigation:

- `PerformanceHeartbeat()` calls `Platform.Fans.GetCount()`;
- the heartbeat runs every 30 seconds;
- `PerformanceHeartbeatRun()` creates a separate background thread;
- `PerformanceHeartbeatBusy` prevents heartbeat-to-heartbeat overlap only;
- both heartbeat and fan-program tick counters start at zero.

Because 30 seconds is an exact multiple of the 15-second fan-program interval,
every scheduled heartbeat is launched on the same GUI timer tick as
`FanProgram.Update()`. The heartbeat thread can therefore use the shared BIOS
CIM session while the GUI thread is setting fan level, fan mode, or GPU power.
There is no shared gate covering those BIOS/WMI operations.

The fork also changes `FanCountdownExtendAlways` from false to true. This path
is an `else if` and only runs when no fan program is active, so it is not the
extra operation during an active curve such as `OmenPerformance`.

Later GUI refactor commits changed fan-plan presentation, persistence and curve
semantics. They did not remove the independent heartbeat thread or serialize
it with the fan-program update.

## Community Evidence

The reports are relevant but do not all describe the same failure:

- Upstream issue 68 establishes why the heartbeat exists. On affected newer
  hardware, `OmenMon.exe -Bios FanCount` restores performance control for about
  two minutes. The HP service reportedly calls the same method every 30 seconds.
- Upstream issue 35 reports freezes and a BSOD together with repeated Windows
  messages that the EC did not respond before timeout. The maintainer treated
  EC workload as a plausible cause and recommended fewer sensors, disabling the
  dynamic icon, keeping the window hidden, and increasing the program interval.
  No stop code was captured, so this is not a confirmed `0x101` match.
- Upstream issue 43 and issues 58/79 describe false low-battery states and
  forced sleep/hibernate under EC load. Those reports support EC interference,
  but they are a different failure mode from an abrupt freeze or
  `CLOCK_WATCHDOG_TIMEOUT`.
- Upstream issue 112 reports repeated memory-related BSODs but supplies no text
  stop code or technical link to the heartbeat. It is weak evidence for this
  incident.
- OmenMon-Reborn issue 88 records ACPI/EC timeout warnings and firmware-initiated
  shutdowns. Its PR 91 mitigates direct EC-port polling, batching and lifecycle
  races. That code is not the fork's `FanCount` implementation, but it confirms
  that OmenMon timing and EC contention can produce system-level failures on
  some HP models.

## Evidence on the Target Laptop

The local hang monitor has now captured two severe incidents close to the
30-second heartbeat signature:

- 2026-09-19 21:12, observed `CLOCK_WATCHDOG_TIMEOUT (0x101)`: OmenMon gained
  five handles at 21:12:46.157 and all collectors stopped about four seconds
  later.
- 2026-09-21 04:26: OmenMon gained six handles at 04:26:48.051 and all
  collectors stopped 4.711 seconds later.

Healthy runtime confirms that the recurring five-handle increase is phase
locked to the configured 30-second heartbeat. Many such calls complete without
a freeze, so the timing is correlation rather than proof that `GetFanCount()`
alone is faulty.

Windows also records ACPI event 15 on this laptop: the EC returned data when no
data was requested, and the event says BIOS may be accessing the EC without OS
synchronization. In the boot after the 04:26 freeze:

- OmenMon started at 04:28:42;
- ACPI event 15 appeared at 04:28:58;
- four more event 15 warnings appeared at 04:29:16 during the next burst of
  OmenMon firmware activity.

The previous short boot recorded the same warning at 04:22:48. These events are
direct evidence of EC/BIOS synchronization trouble on the target machine. They
still do not identify which individual OmenMon, HP, Intel XTU, Windows ACPI, or
firmware operation initiated the bad transaction.

## Current Diagnosis

The leading OmenMon-specific defect is the scheduling model, not the existence
of the heartbeat:

1. the heartbeat launches on a second thread;
2. it is deliberately phase aligned with the 15-second fan update;
3. both paths reuse the same singleton BIOS/CIM objects;
4. the program also performs frequent EC reads for temperature and tray status;
5. no single in-process gate serializes all firmware-facing operations.

A single `FanCount` call remains a possible trigger because severe stops have
followed its observable process signature. Intel XTU and firmware defects also
remain possible. The current evidence does not justify removing those
alternatives.

## Required Repair

The first code experiment should change only concurrency while preserving fan
behavior and the 30-second keepalive interval:

1. Add one reentrant in-process firmware-operation gate shared by BIOS/WMI and
   EC execution helpers.
2. Route heartbeat, fan-program, dynamic-icon and user-initiated hardware calls
   through that gate. A BIOS call may itself reach firmware/EC, so independent
   BIOS and EC locks are insufficient.
3. Keep the heartbeat enabled and keep `Platform.Fans.GetCount()` as its action.
4. Record operation name, start, finish, duration and success to a bounded local
   diagnostic log. Do not log payload data unrelated to hardware control.
5. Do not silently discard heartbeat exceptions without leaving a diagnostic
   record.

This experiment preserves the known working fan-control rule while testing the
new fork-specific risk: overlapping firmware calls.

If another freeze occurs after serialized access, change one variable at a
time in this order:

1. Keep heartbeat and fan control, but use only `CPUT` and `GPTM` EC sensors and
   disable the dynamic tray icon, matching the upstream maintainer's mitigation.
2. Increase the heartbeat interval while staying below the observed two-minute
   firmware reset window. Issue 68 includes a community workaround using 100
   seconds, but the exact safe margin must be validated on this laptop.
3. Isolate Intel XTU and other firmware-control services while keeping OmenMon
   instrumentation unchanged.

Do not combine these changes in the first deployment, because doing so would
make a stable result unable to distinguish call serialization from lower EC
load or a lower heartbeat rate.

## Sources

- https://github.com/OmenMon/OmenMon/issues/68
- https://github.com/OmenMon/OmenMon/issues/35
- https://github.com/OmenMon/OmenMon/issues/43
- https://github.com/OmenMon/OmenMon/issues/58
- https://github.com/OmenMon/OmenMon/issues/79
- https://github.com/OmenMon/OmenMon/issues/112
- https://github.com/seakyy/OmenMon-Reborn/issues/88
- https://github.com/seakyy/OmenMon-Reborn/pull/91
