$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\NovaConnect.App\NovaConnect.App.csproj"
$publishDir = Join-Path $repoRoot "artifacts\publish\single"
$outputDir = Join-Path $repoRoot "artifacts\installer"
$sourceExe = Join-Path $publishDir "NovaConnect.exe"
$issFile = Join-Path $PSScriptRoot "NovaConnect.iss"

$isccCandidates = @(
    @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }
)

if (-not $isccCandidates) {
    throw "Inno Setup 6 compiler was not found. Install JRSoftware.InnoSetup with winget."
}

Remove-Item -LiteralPath $publishDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $outputDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $publishDir, $outputDir -Force | Out-Null

[xml]$projectXml = Get-Content -LiteralPath $project -Raw
$appVersion = [string](@(
    $projectXml.Project.PropertyGroup |
        ForEach-Object { $_.Version } |
        Where-Object { $_ }
)[0])
if ($appVersion -notmatch '^[0-9A-Za-z.-]+$') {
    throw "Project version '$appVersion' is missing or unsafe for an installer filename."
}
$versionInclude = Join-Path $outputDir "version.iss"
Set-Content -LiteralPath $versionInclude `
    -Value ('#define AppVersion "{0}"' -f $appVersion) `
    -Encoding UTF8 `
    -NoNewline

& dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publishDir `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}
if (-not (Test-Path -LiteralPath $sourceExe)) {
    throw "Published executable was not created at $sourceExe."
}

$iscc = $isccCandidates[0]
& $iscc $issFile
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
}

$installer = Join-Path $outputDir "NovaConnect-Setup-$appVersion.exe"
if (-not (Test-Path -LiteralPath $installer)) {
    throw "Installer was not created at $installer."
}

Write-Output $installer
