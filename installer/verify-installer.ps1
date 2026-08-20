param(
    [Parameter(Mandatory = $true)]
    [string]$InstallerPath
)

$ErrorActionPreference = "Stop"
$installer = (Resolve-Path -LiteralPath $InstallerPath).Path
$testRoot = Join-Path $env:LOCALAPPDATA "Temp\NovaConnect-InstallerTest"
$installedExe = Join-Path $testRoot "NovaConnect.exe"
$uninstaller = Join-Path $testRoot "unins000.exe"
$startMenuDirectory = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\NovaConnect"
$appShortcut = Join-Path $startMenuDirectory "NovaConnect.lnk"
$uninstallShortcut = Join-Path $startMenuDirectory "Uninstall NovaConnect.lnk"
$uninstallRegistryPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{A19467AF-B45A-40B1-A4B5-44A9760BF066}_is1"
$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$runValueName = "NovaConnect"
$hadStartupValue = $false
$startupValue = $null
$legacyStartupCommand = '"C:\Legacy NovaConnect\NovaConnect.exe" --background'

if (Test-Path -LiteralPath $uninstallRegistryPath) {
    throw "Installer verification requires NovaConnect to be uninstalled first."
}

try {
    try {
        $startupValue = Get-ItemPropertyValue -LiteralPath $runKey -Name $runValueName -ErrorAction Stop
        $hadStartupValue = $true
    }
    catch [System.Management.Automation.ItemNotFoundException] {
    }
    catch [System.Management.Automation.PSArgumentException] {
    }

    if (Get-Process NovaConnect -ErrorAction SilentlyContinue) {
        throw "Installer verification requires all NovaConnect processes to be stopped first."
    }
    New-Item -Path $runKey -Force | Out-Null
    Set-ItemProperty -LiteralPath $runKey -Name $runValueName -Value $legacyStartupCommand
    Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $startMenuDirectory -Recurse -Force -ErrorAction SilentlyContinue

    $install = Start-Process -FilePath $installer -ArgumentList @(
        "/VERYSILENT",
        "/SUPPRESSMSGBOXES",
        "/NORESTART",
        "/CURRENTUSER",
        "/DIR=`"$testRoot`""
    ) -Wait -PassThru
    if ($install.ExitCode -ne 0) {
        throw "Installer exited with code $($install.ExitCode)."
    }

    foreach ($requiredPath in @($installedExe, $uninstaller, $appShortcut, $uninstallShortcut)) {
        if (-not (Test-Path -LiteralPath $requiredPath)) {
            throw "Expected installed path is missing: $requiredPath"
        }
    }
    if (-not (Test-Path -LiteralPath $uninstallRegistryPath)) {
        throw "Windows Installed Apps registration is missing: $uninstallRegistryPath"
    }
    $expectedStartupCommand = '"' + $installedExe + '" --background'
    $actualStartupCommand = Get-ItemPropertyValue -LiteralPath $runKey -Name $runValueName
    if (-not [string]::Equals(
        $actualStartupCommand,
        $expectedStartupCommand,
        [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Startup migration wrote '$actualStartupCommand' instead of '$expectedStartupCommand'."
    }

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($appShortcut)
    if (-not [string]::Equals($shortcut.TargetPath, $installedExe, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Start Menu shortcut target '$($shortcut.TargetPath)' does not match '$installedExe'."
    }

    $launched = Start-Process -FilePath $installedExe -PassThru
    Start-Sleep -Seconds 4
    $running = Get-Process NovaConnect -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and [string]::Equals($_.Path, $installedExe, [System.StringComparison]::OrdinalIgnoreCase) }
    if (-not $running) {
        throw "The installed Start Menu target did not launch NovaConnect."
    }
    $running | Stop-Process -Force
    $launched.WaitForExit(5000) | Out-Null

    $uninstall = Start-Process -FilePath $uninstaller -ArgumentList @(
        "/VERYSILENT",
        "/SUPPRESSMSGBOXES",
        "/NORESTART"
    ) -Wait -PassThru
    if ($uninstall.ExitCode -ne 0) {
        throw "Uninstaller exited with code $($uninstall.ExitCode)."
    }

    if ((Test-Path -LiteralPath $installedExe) -or
        (Test-Path -LiteralPath $appShortcut) -or
        (Test-Path -LiteralPath $uninstallRegistryPath)) {
        throw "Uninstall left the executable, Start Menu shortcut, or Installed Apps entry behind."
    }
    try {
        $remainingStartupCommand = Get-ItemPropertyValue -LiteralPath $runKey -Name $runValueName -ErrorAction Stop
        throw "Uninstall left the installer-owned startup command behind: $remainingStartupCommand"
    }
    catch [System.Management.Automation.ItemNotFoundException] {
    }
    catch [System.Management.Automation.PSArgumentException] {
    }

    Write-Output "Installer verification passed."
}
finally {
    Get-Process NovaConnect -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and [string]::Equals($_.Path, $installedExe, [System.StringComparison]::OrdinalIgnoreCase) } |
        Stop-Process -Force
    if (Test-Path -LiteralPath $uninstaller) {
        Start-Process -FilePath $uninstaller -ArgumentList @(
            "/VERYSILENT",
            "/SUPPRESSMSGBOXES",
            "/NORESTART"
        ) -Wait | Out-Null
    }
    Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $startMenuDirectory -Recurse -Force -ErrorAction SilentlyContinue

    if ($hadStartupValue) {
        New-Item -Path $runKey -Force | Out-Null
        Set-ItemProperty -LiteralPath $runKey -Name $runValueName -Value $startupValue
    }
    else {
        Remove-ItemProperty -LiteralPath $runKey -Name $runValueName -ErrorAction SilentlyContinue
    }
}
