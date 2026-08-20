# Reverse-engineering notes

## Source inspected

The supplied `SteelSeriesGG117.0.0Setup.exe` was inspected and extracted statically; no installer or extracted SteelSeries executable was intentionally launched.

- Version: SteelSeries GG 117.0.0
- SHA-256: `bef58c2c427139f4aa918b2496638f81fc69b7bed5bb40ccfe8a850a55844187`
- Authenticode status on 2026-08-20: valid
- Signer subject: GN Hearing A/S
- Signing chain issuer: DigiCert Trusted G4 Code Signing RSA4096 SHA384 2021 CA1

The relevant managed Sonar assemblies were decompiled with ILSpy 9.1.0.7988. The relevant native Engine executable is a stripped Go binary, but it retains package/function strings and is accompanied by device-specification resources.

## GG/Sonar event path

There are two distinct sources of state in GG:

1. **Wireless headset power state from SteelSeries Engine**
   - `SoundStage.Services.Engine.EngineCommunicationService` subscribes to the Engine client's `WirelessDeviceConnectionStatusChanged` event.
   - Its handler updates the per-device wireless state and raises Sonar's `HeadsetWirelessStatusChanged` event.
   - `SoundStage.Services.Fallback.FallbackService.OnHeadsetWirelessStatusChanged` reevaluates Sonar's output and microphone redirections.
   - `SoundStage.Services.Fallback.AudioDeviceHelper.IsDeviceConnected` treats an associated wireless endpoint as connected only when the Engine-reported wireless status is connected. For ordinary wired endpoints, it uses the Windows MMDevice `Active` state.

2. **Windows audio endpoint availability**
   - `SoundStage.Interop.Services` implements `IMMNotificationClient` and registers it with `IMMDeviceEnumerator.RegisterEndpointNotificationCallback`.
   - `OnDeviceStateChanged`, `OnDeviceAdded`, and `OnDeviceRemoved` feed Sonar's fallback service when Windows endpoints appear, disappear, or change state.

`FallbackListManager` stores an ordered user fallback list. Sonar selects the first connected candidate and changes its internal Engine redirection. It does **not** need to change the operating-system default because Sonar's virtual devices remain the Windows defaults.

NovaConnect cannot reuse Sonar's internal virtual-device redirection without running Sonar. It therefore reproduces the user-visible result by changing Windows default endpoints directly for all three roles (Console, Multimedia, Communications).

## Nova 5 HID protocol

The attached transmitter enumerates as:

- USB vendor ID: `0x1038`
- USB product ID: `0x2232`
- control HID usage page: `0xFFC0`
- control HID usage: `0x0001`
- input/output report payload size: 64 bytes (HIDSharp reports a 65-byte API buffer including the report-ID byte)

A padded output report with report ID byte `0x00` followed by command `0xB0` requests status. The response begins:

| Byte | Meaning |
|---:|---|
| `0` | response opcode `0xB0` |
| `1` | wireless state: `0x02` off/disconnected, `0x03` on/connected |
| `2` | reserved/unknown |
| `3` | battery percentage, 0–100 |
| `4` | charging state: `0x01` charging, `0x02` plugged/not charging, `0x03` on battery |

This layout was corroborated against:

- HeadsetControl Nova 5 implementation at commit `8a0cc8735e68a7f07476a94b169eb83cb74b2776`: <https://github.com/Sapd/HeadsetControl/blob/8a0cc8735e68a7f07476a94b169eb83cb74b2776/lib/devices/steelseries_arctis_nova_5.hpp>
- Arctis Sound Manager Nova 5 descriptor at commit `46c754548991b6c3c157f2d316a5bd78610f1032`: <https://github.com/loteran/Arctis-Sound-Manager/blob/46c754548991b6c3c157f2d316a5bd78610f1032/src/arctis_sound_manager/devices/nova_5.yaml>

The local dongle returned normalized bytes `B0 02 02 0E 03 00 00 30` while the headset was powered off and `B0 03 02 0E 03 00 00 30` while it was powered on. A final live tray-app cycle on 2026-08-20 observed Connected → Disconnected → Connected and successfully applied the configured playback and capture endpoint IDs on every transition. NovaConnect changes state only for `0x02` or `0x03`; malformed replies and timeouts are errors, not disconnect events.

## Why Windows endpoint notifications alone are insufficient

The USB transmitter and its Windows audio endpoints stay present while the wireless headset is off. Therefore `IMMNotificationClient.OnDeviceStateChanged` cannot by itself distinguish “dongle attached, headset off” from “headset on.” The `0xB0` HID query is the load-bearing part of the feature. Windows MMDevice enumeration is used only for endpoint selection and default-role changes.

## Implementation provenance

NovaConnect's status parser and switching policy were written from the observed behavior and tests. HID transport uses HIDSharp 2.6.4. Core Audio COM declarations and the `IPolicyConfig` usage pattern were adapted from VibeSwitcher commit `d51feabc3f5873fc01b633e28b4307635b135a38` under MIT: <https://github.com/raphymany/vibeswitcher/tree/d51feabc3f5873fc01b633e28b4307635b135a38>.

No SteelSeries binary or decompiled SteelSeries source is included in NovaConnect.
