# NovaConnect

NovaConnect is a small Windows tray app for **SteelSeries Arctis Nova 5** headsets. It detects the headset's real wireless power state through the USB dongle and switches Windows playback and microphone defaults—without running SteelSeries GG or Sonar.

## What it does

- Polls the Nova 5 transmitter (`VID 0x1038`, `PID 0x2232`) once per second by default.
- Treats protocol status `0x03` as connected and `0x02` as disconnected.
- On connection, selects the configured Nova 5 playback endpoint and, optionally, its microphone.
- On disconnection, returns only the Windows roles that still use the configured Nova endpoint to the configured fallback. A manual choice for Console, Multimedia, or Communications is preserved independently.
- On connection, sets Console, Multimedia, and Communications default roles to the configured headset endpoint.
- Runs as a per-user tray app. No service, administrator rights, network access, telemetry, virtual audio driver, or SteelSeries process is required.

## Install and use

### Installer (recommended)

Run `NovaConnect-Setup-0.1.1.exe`. It installs per-user without administrator rights,
adds **NovaConnect** to the Start Menu, and includes an uninstaller under both the Start
Menu and Windows Installed Apps. Existing configuration under `%LOCALAPPDATA%\NovaConnect`
is preserved across upgrades and uninstall.

### Portable executable

1. Put `NovaConnect.exe` in a stable folder.
2. Run `NovaConnect.exe`.
3. On first launch, confirm the four automatically detected endpoint choices:
   - headset playback
   - fallback playback
   - headset microphone
   - fallback microphone
4. Leave **Start NovaConnect with Windows** clear until you have tested the selections. Enable it later in Settings if wanted.
5. Select **Save**. NovaConnect remains in the notification area.

Double-click the tray icon to reopen Settings. The tray menu can switch either profile immediately, disable automatic switching, open the log folder, or exit.

Configuration and logs are stored under `%LOCALAPPDATA%\NovaConnect`. The log rotates at 1 MiB and keeps one previous file. The optional startup entry is a per-user `HKCU\...\Run` value and can be removed by clearing the checkbox.

## Safety behavior

NovaConnect switches on a confirmed HID state transition. A connection applies immediately; disconnection requires two consecutive valid offline replies. Query failures do not count as disconnection. On power-off, each Windows role is returned to fallback only when that role still points to the configured headset endpoint. This avoids fighting a device that you selected manually.

The first-run detector selects the active endpoint whose name contains `Arctis Nova 5`. It uses the current non-Nova defaults as fallback choices. These are suggestions only: review and correct all four dropdowns before Save, especially the microphone fallback. Saved endpoint IDs are retained on later launches and are not replaced by automatic guesses.

## Build and test

Requirements: Windows 10/11 and .NET 8 SDK. Inno Setup 6 is also required to build the installer.

```text
dotnet test tests/NovaConnect.Core.Tests/NovaConnect.Core.Tests.csproj
dotnet test tests/NovaConnect.App.Tests/NovaConnect.App.Tests.csproj
dotnet publish src/NovaConnect.App/NovaConnect.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
pwsh -File installer/build-installer.ps1
```

The installer build produces `artifacts/installer/NovaConnect-Setup-0.1.1.exe`. See
[`installer/README.md`](installer/README.md) for the packaging and verification workflow.

The application tests include non-mutating hardware checks for the attached Nova 5 dongle and Core Audio endpoint enumeration. The default-endpoint integration check only sets the current endpoint to itself.

## Scope and compatibility

This build targets the Arctis Nova 5 USB transmitter identified above. It does not include Sonar EQ, spatial processing, ChatMix, firmware updates, game detection, or other GG features. SteelSeries and Arctis are trademarks of their respective owner; NovaConnect is an independent compatibility project.

See [SPEC.md](SPEC.md) for policy details, [docs/REVERSE_ENGINEERING.md](docs/REVERSE_ENGINEERING.md) for the GG/Sonar trace, and [docs/protocol-research.md](docs/protocol-research.md) for the source-by-source HID protocol dossier.

## License

NovaConnect is MIT licensed. It uses HIDSharp under Apache-2.0 and adapts Core Audio interop patterns from VibeSwitcher under MIT. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) and `licenses/`.
