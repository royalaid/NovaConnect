# SteelSeries Arctis Nova 5 wireless connection-state protocol research

## Executive summary

For the SteelSeries Arctis Nova 5 receiver (`VID 0x1038`, `PID 0x2232`), the best corroborated open-source connection-state query is:

1. Open the vendor HID collection on **USB interface 3 / `MI_03`**, Usage Page **`0xFFC0`**, Usage **`0x0001`**.
2. Send an HID **output report**, report ID `0`, whose payload begins `B0` (commonly represented by HID libraries as `00 B0`, with zero padding to the collection's output-report length).
3. Read the corresponding input report and normalize away any leading report-ID byte before parsing.
4. In the normalized payload beginning with `B0`, byte 1 is headset power/connection state: **`02` = offline**, **`03` = online**. Byte 3 is battery percentage and byte 4 is cable charging (`01` on, `03` off).

This is not a feature-report protocol in the implementations examined. HeadsetControl calls `hid_write` followed by `hid_read_timeout`; Linux Arctis Manager issues HID `SET_REPORT` with report type `0x02` (Output), then reads interrupt-IN endpoints. No exact Nova 5 implementation found uses `hid_send_feature_report`/`HidD_SetFeature` for connection state.

Two production-shaped endpoint-switching implementations were found:

- **Windows:** PROX2-AutoSwitch polls the Nova 5 HID state, debounces disconnects, and uses `IPolicyConfig::SetDefaultEndpoint` for Console, Multimedia, and Communications, verifying all three roles afterward.
- **Linux:** Linux Arctis Manager observes parsed status changes and calls `pulsectl.Pulse.default_set()` to select its Arctis media sink on connect or a configured fallback sink on disconnect.

Important caveat: PROX2-AutoSwitch's current Windows Nova parser checks raw `Data[1] == 0x02` without first locating/removing a possible report-ID byte. Its own HID code uses `ReadFile`/`FileStream`, while a sibling Windows implementation using HidSharp shows raw buffers as `00 B0 ...`, making the connection byte index 2, not 1. Its PR explicitly says real Nova 5 hardware was not validated. A new implementation should locate the `B0` marker at offset 0 or 1 and parse relative to it.

## Confidence labels

- **Confirmed, exact Nova 5:** independently encoded in HeadsetControl and Linux Arctis Manager, and/or directly enumerated from a Nova 5.
- **Implemented but hardware-unverified:** source exists for Nova 5, but the author explicitly states CI did not validate real hardware.
- **Sibling evidence:** proven on another Nova-family receiver; useful for transport framing, not automatically proof for Nova 5.
- **Inference:** a conclusion drawn by reconciling sources, clearly marked as such.

## 1. Exact Nova 5 USB identity and interfaces

HeadsetControl registers Nova 5 PID `0x2232` and Nova 5X PID `0x2253`, and assigns every supported capability to interface 3, Usage Page `0xFFC0`, Usage `0x0001`.[^hsc-nova5-identity]

A Windows `hid_enumerate` dump from a physical `1038:2232` receiver gives the complete HID interface map:[^hsc-issue389]

| Interface | Usage Page / Usage | Observed role |
|---|---:|---|
| `MI_03` / interface 3 | `FFC0:0001` | Vendor command/status collection; use this for `B0` status |
| `MI_04` / interface 4 | `000C:0001` | Consumer Control |
| `MI_05` / interface 5 | `FF00:0001` | Secondary vendor collection; Linux Arctis Manager also listens here |

On Windows, HeadsetControl selects by Usage Page/Usage first; on non-Windows it falls back to `interface_number == 3`.[^hsc-path-selection] Linux Arctis Manager specifies control endpoint 0 with `wIndex=3` and listens on interfaces 3 and 5.[^lam-nova5-config]

The mainline Linux USB-audio driver also recognizes `1038:2232`, but only to apply `QUIRK_FLAG_MIXER_GET_CUR_BROKEN`; it does **not** implement wireless connection detection.[^linux-audio-quirk]

## 2. Exact request transport and bytes

### 2.1 HeadsetControl: HIDAPI output report + timed input read

The Nova protocol helper constructs `request = { 0x00, 0xB0 }`, sends it through `writeHID`, allocates a 128-byte status buffer, and calls a timed HID read.[^hsc-nova-protocol] `writeHID` resolves to HIDAPI `hid_write`, while the response path is `hid_read_timeout`; the separate feature-report methods call `hid_send_feature_report` and are not used here.[^hsc-hid-transport]

Thus, for HeadsetControl:

```text
Host -> receiver: 00 B0
                  ^^ ^^
                  ID command

Receiver -> host: normalized B0-prefixed status payload (read into a 128-byte buffer)
```

Although HeadsetControl's Nova helper defines a 64-byte general message size, this particular function passes the two-byte span directly to `hid_write`; it does not explicitly pad the request to 64 bytes.[^hsc-nova-protocol]

### 2.2 Linux Arctis Manager: control `SET_REPORT(Output)` + interrupt reads

The Nova 5 YAML declares a 64-byte, zero-padded command, status request `B0`, control endpoint 0 with target interface (`wIndex`) 3, and listeners on interfaces 3 and 5.[^lam-nova5-config] Its USB sender uses:

```text
bmRequestType = Class | Interface | Host-to-device
bRequest      = 09             (SET_REPORT)
wValue        = 0200           (report type 02 = Output, report ID 00)
wIndex        = 0003           (interface 3)
data           = B0 00 00 ...  (64 bytes)
```

That exact construction is visible in `send_command`: `CTRL_OUT`, class/interface request, `bRequest=0x09`, `wValue=(0x02 << 8) | 0x00`, then `ctrl_transfer`.[^lam-send-command]

**Conclusion:** `B0` status is an **output-report request**, not a feature report. Different APIs expose report ID 0 differently:

- HIDAPI/Windows-friendly representation: `00 B0 ...`.
- USB control-transfer payload: `B0 00 ...`, while report ID 0 is carried in `wValue`.

## 3. Exact response bytes and connection-state mapping

Both exact Nova 5 implementations agree on the normalized `B0` payload:

| Normalized offset | Meaning | Known values |
|---:|---|---|
| `0` | Response marker/command | `B0` |
| `1` | Headset power / 2.4 GHz connection | `02` offline, `03` online |
| `2` | Bluetooth connection | `00` off, `02` disconnected, `03` connected |
| `3` | Headset battery | direct `0..100` percent |
| `4` | Cable charging | `01` on, `03` off |

Linux Arctis Manager declares these offsets and value mappings directly in `nova_5.yaml`.[^lam-nova5-status] HeadsetControl independently treats `data[1] == 0x02` as device offline, `data[3]` as percentage, and `data[4] == 0x01` as charging.[^hsc-nova5-battery]

Recommended robust parser:

```text
read one input report
if bytes[0] == B0: marker = 0
else if bytes[0] == 00 and bytes[1] == B0: marker = 1
else: ignore this report / continue reading

power = bytes[marker + 1]
02 -> Disconnected
03 -> Connected
anything else -> Unknown
```

A response should also be length-checked before indexing. HeadsetControl requires at least 16 bytes, even though the currently used fields end at offset 4.[^hsc-nova5-battery]

### Report-ID framing caveat on Windows

PROX2-AutoSwitch's native Windows module opens `FFC0:0001`, writes a buffer whose bytes 0 and 1 are `00 B0`, reads with `FileStream.BeginRead`, and then maps raw `Data[1] == 02` to disconnected.[^prox2-hid-module] That source was merged, but its integration PR states that real Nova 5/5X hardware was **not validated by CI**.[^prox2-pr14]

This raw index is questionable. The Nova 3X Windows/HidSharp implementation documents and handles a real response as:

```text
00 B0 03 02 47 03 64 64 ...
^^ ^^ ^^
ID B0 connection
```

It explicitly searches for `B0` at raw index 0 or 1 and parses connection at `marker + 1`.[^nova3x-layout][^nova3x-parser] Microsoft's HID documentation also directs applications to size buffers from the collection's report lengths and treats the first report-buffer byte as the report ID when selecting reports.[^ms-hid-reports]

**Inference:** a Windows `ReadFile` implementation should not hard-code the normalized connection offset against an unnormalized buffer. Marker-relative parsing avoids both API framings and unrelated queued input reports.

## 4. Polling versus event mechanisms

### 4.1 HeadsetControl — on-demand polling

HeadsetControl performs one request/response transaction whenever `getBattery()` is called. It has no background subscription or connection-change callback in the Nova 5 device implementation: write `00 B0`, timed read, parse offline/battery/charge.[^hsc-nova-protocol][^hsc-nova5-battery]

### 4.2 PROX2-AutoSwitch on Windows — synchronous polling and debounce

The runtime invokes `Get-SteelSeriesNova5State -TimeoutMilliseconds 800` once per worker iteration. `Unknown` causes no switch. Known results pass through a disconnect debounce, and the installer defaults to a 1,500 ms interval with two misses required before declaring off.[^prox2-runtime][^prox2-defaults]

The HID module re-enumerates and opens the matching HID collection per query, writes the output report asynchronously with a timeout, then performs one asynchronous read with a timeout.[^prox2-hid-module] This is polling, not a Windows HID arrival/event subscription.

### 4.3 Linux Arctis Manager — request-driven reads with event propagation

Linux Arctis Manager repeatedly:

1. starts asynchronous interrupt-IN readers for interfaces 3 and 5;
2. sends one `B0` status request;
3. reads each endpoint with a 200 ms timeout;
4. sleeps 100 ms before the outer loop repeats.[^lam-read-loop]

It is therefore **request-driven polling at the USB layer**, even though status changes become events inside the application. Parsed values update an `ObservableDict`, which calls observers only when a value changes.[^lam-observable] The core reacts immediately when `headset_power_status` changes, and the D-Bus service emits a deduplicated `StatusChanged` signal.[^lam-switch-trigger][^lam-dbus]

This distinction matters: consumers of Linux Arctis Manager can subscribe to D-Bus events, but the daemon itself obtains state through recurring HID requests/reads.

## 5. Default audio endpoint switching code

### 5.1 Windows: native Core Audio / `IPolicyConfig`

PROX2-AutoSwitch's switching path is complete and integrated:

- HID `Connected`/`Disconnected` results are debounced.
- On state transition, the worker selects the configured headset or fallback endpoint.
- `Set-CoreAudioDefaultRenderDevice` calls `IPolicyConfig::SetDefaultEndpoint(deviceId, role)` for role values 0, 1, and 2 (Console, Multimedia, Communications).[^prox2-policyconfig]
- The runtime waits briefly, verifies that all three default-role IDs match, retries once, then fails safely if verification still fails.[^prox2-switch-verify]

`IPolicyConfig` is an undocumented Windows COM interface, not a documented public endpoint-selection API; the project isolates it in embedded C# and notes that limitation itself.[^prox2-coreaudio-notes]

The same project still polls Core Audio endpoint state for its generic `WindowsEndpoint` mode. Its maintainers explicitly list event-driven `IMMNotificationClient` as a future improvement, not the current mechanism.[^prox2-future-event]

### 5.2 Linux: PulseAudio/PipeWire through `pulsectl`

Linux Arctis Manager wires `headset_power_status` changes to audio routing:

- online -> `redirect_to_media_sink()`;
- offline -> `redirect_audio_on_disconnect()` if configured and the current default is one of the Arctis virtual sinks.[^lam-switch-trigger]

`redirect_audio()` resolves the requested sink by `node.nick` or `node.name` and calls `self.pulse.default_set(sink)`.[^lam-pulse-default] This works with PulseAudio and PipeWire's PulseAudio compatibility API through `pulsectl`.

## 6. Closest sibling evidence (not Nova 5 proof)

### Nova 3X Wireless (`1038:226D`)

The Windows tray implementation is valuable because it exposes report-ID framing and defensive report matching on actual hardware. It uses `MI_03`, 65-byte input/output report lengths, sends `00 B0` padded to 65 bytes, ignores `MI_04` (no output reports) and `MI_05` (no `B0` response), and polls every two seconds.[^nova3x-layout][^nova3x-read-loop]

Its normalized status layout matches the Nova 5 mapping for the connection, battery, and charging fields. This strengthens the marker-relative framing recommendation, but it is still a different PID and should not replace a Nova 5 capture.

### Arctis 7+ (`1038:2212`)

Passive USBPcap research shows the same broad interface pattern: `MI_03` Usage Page `FFC0` as a proprietary command channel, `MI_04` Consumer Control, and `MI_05` Usage Page `FF00` as a secondary vendor channel.[^a7plus-map] Its documented high-frequency GG polling packet is unrelated to the Nova `B0` status query and must not be copied as a Nova 5 command.

## 7. Implementation recommendation

For NovaConnect, the lowest-risk design is:

1. Enumerate `VID 1038`, `PID 2232` (optionally known 5X variants separately).
2. Select Usage Page `FFC0`, Usage `0001`; on Linux verify/fallback to interface 3.
3. Open read/write with sharing on Windows so SteelSeries GG is less likely to block coexistence.
4. Send an **Output report** using the report length reported by HID capabilities:
   - byte 0 `00` report ID;
   - byte 1 `B0` command;
   - remaining bytes zero.
5. Read until timeout and search only offsets 0 and 1 for the `B0` marker; ignore unrelated queued reports.
6. Require `marker + 4` to exist; preferably enforce the proven 16-byte minimum.
7. Map only `02 -> Disconnected` and `03 -> Connected`; all malformed, timeout, access-denied, or unknown values -> `Unknown`.
8. Poll around 1–2 seconds and require at least two consecutive disconnected results. Do not switch on `Unknown`.
9. On Windows, set and verify all three Core Audio roles. On Linux, use the Pulse default-sink API and only switch away if the application still owns the current default.
10. Before release, capture one real Nova 5 ON/OFF/ON cycle and log raw hex. This is especially necessary to resolve the current PROX2 raw-index discrepancy.

This validation was completed locally: normalized offline bytes were `B0 02 02 0E 03 00 00 30`, online bytes were `B0 03 02 0E 03 00 00 30`, and the finished tray app logged a Connected → Disconnected → Connected cycle with the user-confirmed endpoint IDs.

## 8. What was not found

- No official SteelSeries public protocol specification for the Nova 5 connection report.
- No Nova 5 connection-state implementation in OpenRGB; its SteelSeries code is for RGB peripherals/headsets and does not contain PID `2232`.
- No Linux kernel HID driver that decodes Nova 5 wireless state; only the USB-audio mixer quirk noted above.
- No exact Nova 5 implementation using a HID feature report for this query.
- No open-source implementation of SteelSeries GG's internal `v1/wirelessDeviceConnectionStatus` / `ArctisNova5TxDeviceEventConverter` path. The open-source projects bypass that private Engine API and query the receiver directly.

## Sources

[^hsc-nova5-identity]: HeadsetControl, `steelseries_arctis_nova_5.hpp`, PID list and HID capability details: <https://github.com/Sapd/HeadsetControl/blob/8a0cc8735e68a7f07476a94b169eb83cb74b2776/lib/devices/steelseries_arctis_nova_5.hpp#L27-L99>
[^hsc-issue389]: HeadsetControl issue #389, physical Nova 5 Windows enumeration (`MI_03`, `MI_04`, `MI_05`): <https://github.com/Sapd/HeadsetControl/issues/389>
[^hsc-path-selection]: HeadsetControl, `hid_utility.cpp`, Windows Usage Page/Usage selection and non-Windows interface fallback: <https://github.com/Sapd/HeadsetControl/blob/8a0cc8735e68a7f07476a94b169eb83cb74b2776/lib/hid_utility.cpp#L28-L62>
[^lam-nova5-config]: Linux Arctis Manager, exact Nova 5 IDs, 64-byte padding, control/listen interfaces and status request: <https://github.com/elegos/Linux-Arctis-Manager/blob/9d877ef1c852fa714c489e64cc2f98e4c1c8a8bc/src/linux_arctis_manager/devices/nova_5.yaml#L1-L40>
[^linux-audio-quirk]: Linux kernel, Nova 5 USB-audio mixer quirk: <https://github.com/torvalds/linux/blob/a4ff2be345d0abc943da8dd8da98151843b750dc/sound/usb/quirks.c#L2371-L2374>
[^hsc-nova-protocol]: HeadsetControl, Nova `00 B0` write and 128-byte timed read: <https://github.com/Sapd/HeadsetControl/blob/8a0cc8735e68a7f07476a94b169eb83cb74b2776/lib/devices/protocols/steelseries_protocol.hpp#L224-L283>
[^hsc-hid-transport]: HeadsetControl, HIDAPI output/read versus separate feature-report functions: <https://github.com/Sapd/HeadsetControl/blob/8a0cc8735e68a7f07476a94b169eb83cb74b2776/lib/devices/hid_interface.hpp#L84-L168>
[^lam-send-command]: Linux Arctis Manager, output endpoint or control `SET_REPORT(Output)` construction: <https://github.com/elegos/Linux-Arctis-Manager/blob/9d877ef1c852fa714c489e64cc2f98e4c1c8a8bc/src/linux_arctis_manager/core.py#L549-L587>
[^lam-nova5-status]: Linux Arctis Manager, exact Nova 5 response offsets and value mapping: <https://github.com/elegos/Linux-Arctis-Manager/blob/9d877ef1c852fa714c489e64cc2f98e4c1c8a8bc/src/linux_arctis_manager/devices/nova_5.yaml#L37-L61> and <https://github.com/elegos/Linux-Arctis-Manager/blob/9d877ef1c852fa714c489e64cc2f98e4c1c8a8bc/src/linux_arctis_manager/devices/nova_5.yaml#L127-L151>
[^hsc-nova5-battery]: HeadsetControl, Nova 5 offline/battery/charging parser: <https://github.com/Sapd/HeadsetControl/blob/8a0cc8735e68a7f07476a94b169eb83cb74b2776/lib/devices/steelseries_arctis_nova_5.hpp#L170-L203>
[^prox2-hid-module]: PROX2-AutoSwitch, native SetupAPI/HID filter, `00 B0` write/read and raw status parser: <https://github.com/Ayerdi/PROX2-AutoSwitch/blob/85a27ab8d3c9183471be2f6614d1da296510b82b/lib/SteelSeriesNova5.psm1#L1-L35> and <https://github.com/Ayerdi/PROX2-AutoSwitch/blob/85a27ab8d3c9183471be2f6614d1da296510b82b/lib/SteelSeriesNova5.psm1#L190-L358>
[^prox2-pr14]: PROX2-AutoSwitch PR #14, integration and explicit lack of real-hardware CI validation: <https://github.com/Ayerdi/PROX2-AutoSwitch/pull/14>
[^nova3x-layout]: ArctisBatteryTray, hardware-observed Nova 3X interface, request, raw reports and normalized layout: <https://github.com/pokjump/arctisnova3xbatterytray/blob/5acd9e86a11ee1eb80660c5214966c720ac1abcb/README.md#L24-L62>
[^nova3x-parser]: ArctisBatteryTray, marker-relative Windows parser and output-report writer: <https://github.com/pokjump/arctisnova3xbatterytray/blob/5acd9e86a11ee1eb80660c5214966c720ac1abcb/src/HeadsetService.cs#L140-L243>
[^ms-hid-reports]: Microsoft, obtaining and initializing HID reports with `ReadFile`, report lengths and report-ID byte: <https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/obtaining-hid-reports> and <https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/initializing-hid-reports>
[^prox2-runtime]: PROX2-AutoSwitch runtime, Nova state polling, `Unknown` behavior, debounce and transitions: <https://github.com/Ayerdi/PROX2-AutoSwitch/blob/85a27ab8d3c9183471be2f6614d1da296510b82b/Runtime-PROX2-AutoSwitch.ps1#L1089-L1195>
[^prox2-defaults]: PROX2-AutoSwitch installer defaults (`PollMilliseconds=1500`, `OffMissThreshold=2`): <https://github.com/Ayerdi/PROX2-AutoSwitch/blob/85a27ab8d3c9183471be2f6614d1da296510b82b/Instalar-PROX2-AutoSwitch.ps1#L670-L682>
[^lam-read-loop]: Linux Arctis Manager, interrupt reader, 200 ms timeout, 100 ms delay and repeated status request: <https://github.com/elegos/Linux-Arctis-Manager/blob/9d877ef1c852fa714c489e64cc2f98e4c1c8a8bc/src/linux_arctis_manager/core.py#L104-L160>
[^lam-observable]: Linux Arctis Manager, observer callback only on value change: <https://github.com/elegos/Linux-Arctis-Manager/blob/9d877ef1c852fa714c489e64cc2f98e4c1c8a8bc/src/linux_arctis_manager/utils.py#L65-L93>
[^lam-switch-trigger]: Linux Arctis Manager, power-status observer and connect/disconnect routing: <https://github.com/elegos/Linux-Arctis-Manager/blob/9d877ef1c852fa714c489e64cc2f98e4c1c8a8bc/src/linux_arctis_manager/core.py#L459-L497>
[^lam-dbus]: Linux Arctis Manager, deduplicated D-Bus `StatusChanged`: <https://github.com/elegos/Linux-Arctis-Manager/blob/9d877ef1c852fa714c489e64cc2f98e4c1c8a8bc/src/linux_arctis_manager/dbus_service.py#L40-L83>
[^prox2-policyconfig]: PROX2-AutoSwitch, `SetDefaultEndpoint` for all three roles: <https://github.com/Ayerdi/PROX2-AutoSwitch/blob/85a27ab8d3c9183471be2f6614d1da296510b82b/lib/AutoSwitchCore.psm1#L856-L878>
[^prox2-switch-verify]: PROX2-AutoSwitch, set/verify/retry switching logic: <https://github.com/Ayerdi/PROX2-AutoSwitch/blob/85a27ab8d3c9183471be2f6614d1da296510b82b/Runtime-PROX2-AutoSwitch.ps1#L303-L338>
[^prox2-coreaudio-notes]: PROX2-AutoSwitch's Core Audio implementation notes: <https://github.com/Ayerdi/PROX2-AutoSwitch/blob/85a27ab8d3c9183471be2f6614d1da296510b82b/AGENT.md#L180-L186>
[^prox2-future-event]: PROX2-AutoSwitch lists `IMMNotificationClient` as a future replacement for 1.5 s polling: <https://github.com/Ayerdi/PROX2-AutoSwitch/blob/85a27ab8d3c9183471be2f6614d1da296510b82b/AGENT.md#L244-L248>
[^lam-pulse-default]: Linux Arctis Manager, sink lookup and `pulse.default_set`: <https://github.com/elegos/Linux-Arctis-Manager/blob/9d877ef1c852fa714c489e64cc2f98e4c1c8a8bc/src/linux_arctis_manager/pactl.py#L133-L151>
[^nova3x-read-loop]: ArctisBatteryTray, timed read loop and candidate-interface probing: <https://github.com/pokjump/arctisnova3xbatterytray/blob/5acd9e86a11ee1eb80660c5214966c720ac1abcb/src/HeadsetService.cs#L140-L173> and <https://github.com/pokjump/arctisnova3xbatterytray/blob/5acd9e86a11ee1eb80660c5214966c720ac1abcb/src/HeadsetService.cs#L245-L319>
[^a7plus-map]: Arctis 7+ USB HID reverse-engineering report, interface map and polling observations: <https://github.com/Mason23Johnson/SteelSeries-Arctis-7-USB-HID-Protocol-Reverse-Engineering/blob/5bc6bc16ea4ce704a72fd84fd06c9b6d114fee51/steelseries_arctis7plus_research.md#L16-L50>
