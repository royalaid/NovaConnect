# Windows installer

NovaConnect uses Inno Setup 6 to create a self-contained, per-user Windows installer.
The installer:

- installs to `%LOCALAPPDATA%\Programs\NovaConnect` without administrator rights;
- creates **NovaConnect** and **Uninstall NovaConnect** shortcuts in the Start Menu;
- registers an uninstaller in Windows Installed Apps;
- preserves an existing **Start with Windows** preference while migrating the executable path;
- keeps `%LOCALAPPDATA%\NovaConnect\config.json` and logs when uninstalling.

## Build

Install the compiler once:

```powershell
winget install --id JRSoftware.InnoSetup --exact --scope user
```

Build the application and installer:

```powershell
pwsh -File installer/build-installer.ps1
```

The output is `artifacts/installer/NovaConnect-Setup-0.1.0.exe`.

## Verify

The verification requires NovaConnect to be uninstalled and stopped first. It installs into a
temporary per-user directory, checks both Start Menu shortcuts and the Windows Installed Apps
entry, launches the installed executable, runs the uninstaller, confirms cleanup, and restores
the pre-test startup registry value.

```powershell
pwsh -File installer/verify-installer.ps1 `
  -InstallerPath artifacts/installer/NovaConnect-Setup-0.1.0.exe
```
