# NovaConnect behavior specification

NovaConnect is a small Windows tray application for SteelSeries Arctis Nova 5 (`VID 0x1038`, `PID 0x2232`). It replaces only the GG/Sonar wireless-fallback behavior.

## Detection

- Open HID interface 3 (`usage page 0xFFC0`, usage `0x0001`) on the Nova 5 USB dongle.
- Poll with output report `00 B0`, padded to the output-report length declared by the HID descriptor.
- Ignore unrelated queued input reports and continue reading until a `B0` response or the one-second timeout. Accept an optional leading zero report-ID byte.
- Require at least five normalized response bytes.
- Interpret response byte 1 as headset power state: `02` disconnected, `03` connected.
- Response byte 3 is battery percentage. Byte 4 is charging state (`01` charging, `02` plugged but full, `03` on battery).
- A failed read is unknown, not disconnected. It must not trigger a switch.
- Emit a connect switch immediately. Require two consecutive valid disconnected samples before emitting a disconnect switch. On startup, apply the confirmed state only if enabled.

## Switching

- On connect, set the configured Nova playback endpoint as default for Console, Multimedia, and Communications roles. Optionally do the same for its microphone.
- On disconnect, evaluate Console, Multimedia, and Communications independently. Switch a role back only when that role still points to the configured Nova endpoint. This preserves manual role-specific choices made after the automatic switch.
- Apply the same role-specific guard independently to the microphone.
- If applying a role fails, retain the transition for retry on the next valid sample instead of consuming it.
- If a configured endpoint is missing or inactive, log the error and leave the current default unchanged.

## Safety and footprint

- The tray icon shows headphones for a connected headset, a speaker for a disconnected headset, and the NovaConnect logo for startup, unavailable status, or configuration errors. It follows the reported connection state even with automatic switching disabled, rather than the Windows default audio endpoint.

- No firmware, EQ, volume, or device-setting writes. The only HID write is the read-only status query `B0`.
- No service, driver, administrator rights, telemetry, network access, or SteelSeries process is required.
- Configuration and logs are per-user under `%LOCALAPPDATA%\NovaConnect`; logs rotate at 1 MiB with one previous generation.
- Missing, damaged, or invalid configuration opens Settings and does not start switching until the user saves valid selections.
- Start-with-Windows uses the per-user `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value and is opt-in.
