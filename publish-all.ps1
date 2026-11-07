param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$project = Join-Path $root "src\ThreeFingerDrag.csproj"
$launcherProject = Join-Path $root "launcher\ThreeFingerDrag.Launcher.csproj"
$outRoot = Join-Path $root "publish"
$installerScript = Join-Path $root "installer\ThreeFingerDrag.iss"

$isccCommand = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
$iscc = if ($isccCommand) { $isccCommand.Source } else { $env:INNO_SETUP_COMPILER }
if (-not $iscc -or -not (Test-Path -LiteralPath $iscc)) {
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
    )
    $iscc = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $iscc) {
    throw "Inno Setup 6 compiler (ISCC.exe) is required. Install Inno Setup 6 or set INNO_SETUP_COMPILER to its ISCC.exe path."
}

$targets = @(
    @{ Rid = "win-x64";   Platform = "x64" },
    @{ Rid = "win-x86";   Platform = "x86" },
    @{ Rid = "win-arm64"; Platform = "ARM64" }
)

dotnet restore $project
if ($LASTEXITCODE -ne 0) { throw "restore failed" }
dotnet restore $launcherProject
if ($LASTEXITCODE -ne 0) { throw "launcher restore failed" }

foreach ($t in $targets) {
    $dir = Join-Path $outRoot $t.Rid
    dotnet publish $project -c $Configuration -r $t.Rid -p:Platform=$($t.Platform) `
        --no-restore --self-contained false -o $dir
    if ($LASTEXITCODE -ne 0) { throw "publish failed for $($t.Rid)" }
    dotnet publish $launcherProject -c $Configuration -r $t.Rid -p:PlatformTarget=$($t.Platform) `
        --no-restore --self-contained false -o $dir
    if ($LASTEXITCODE -ne 0) { throw "launcher publish failed for $($t.Rid)" }
}

$installerDir = Join-Path $outRoot "installers"
New-Item -ItemType Directory -Path $installerDir -Force | Out-Null
foreach ($t in $targets) {
    $sourceDir = Join-Path $outRoot $t.Rid
    & $iscc "/DSourceDir=$sourceDir" "/DOutputDir=$installerDir" "/DTargetRid=$($t.Rid)" $installerScript
    if ($LASTEXITCODE -ne 0) { throw "installer build failed for $($t.Rid)" }
}

Write-Host "Done: $outRoot"
Write-Host "Installers: $installerDir"
