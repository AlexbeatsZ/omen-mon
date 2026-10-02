# CPU Power Input and Startup Recovery

## User contract

Keep the fork's control-panel layout. Replace the placeholder CPU profiles with
direct PL1, PL4 and CPU-with-GPU watt inputs, as requested on 2026-10-02. Blank
means leave that limit unchanged. Do not invent model-specific performance
preset values. Clear all inputs and apply to stop automatic CPU writes; this
does not reset already-written limits until firmware resets them, normally at
reboot.

## Firmware and persistence

The upstream `0x20008 / 0x29` command writes four bytes: PL1, PL2, PL4 and the
concurrent CPU limit. Follow the upstream CLI convention of PL2 = PL1.
Unspecified fields are `0xFF`; explicit decimal watts are 1-254. This represents
the protocol, not a guarantee that every watt value is supported on a model.

Save `CpuPowerDefault` as `PL1:PL4:CPUWithGpu` only after the command succeeds.
An empty default causes no startup CPU writes. Invalid inputs or rejected
commands retain the previous saved setting. Persist AutoConfig with a manual
choice, so the existing elevated startup task can replay it after login.
Power selections require an atomic, serialized configuration save; failure
propagates to the UI and restores the prior saved choice in memory.

Startup applies CPU limits after fan/GPU initialization. The existing bounded
startup/resume recovery replays them after firmware settles. Explicit GUI fan
plan changes also restore the saved CPU setting afterward. The opt-in
`PerformanceHeartbeatReapplyCpuPower` flag can replay it on the existing
heartbeat; it remains false by default. Do not add a CPU write to every fan
tick, guess factory PL1, or change global power settings.

## Evidence boundary

HP's interface supplies a command status, but no general PL1/PL4 current-value
query. Show 'sent' / 'restored' and the exact requested values, not fabricated
readback. System design data is factory metadata, not proof of current limits.
Tests use injected writers to verify the four-byte payload, blank semantics,
validation, failure persistence, fresh configuration load and startup replay.
Keep real-hardware CPU watt testing limited to values actually chosen by the
user; do not raise CPU limits merely for an acceptance check.
