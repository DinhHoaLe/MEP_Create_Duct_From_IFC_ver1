$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $projectRoot 'outputs\IFCInfo.csproj'
$builtDll = Join-Path $projectRoot 'outputs\bin\Release\net48\IFCInfo.dll'
$installerRoot = Join-Path $PSScriptRoot 'revit-installer'
$packageRoot = Join-Path $projectRoot 'release\IFCInfo-Revit2024'
$zipPath = Join-Path $projectRoot 'release\IFCInfo-Revit2024.zip'

& dotnet build $project -c Release -p:RevitVersion=2024
if ($LASTEXITCODE -ne 0) { throw 'Revit 2024 build failed.' }

New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
Copy-Item -LiteralPath $builtDll -Destination (Join-Path $packageRoot 'IFCInfo.dll') -Force
foreach ($name in @('Install.cmd', 'Install.ps1', 'README.txt')) {
    Copy-Item -LiteralPath (Join-Path $installerRoot $name) -Destination (Join-Path $packageRoot $name) -Force
}
Compress-Archive -Path (Join-Path $packageRoot '*') -DestinationPath $zipPath -Force
Write-Host "Package: $zipPath"
Get-FileHash -LiteralPath $zipPath -Algorithm SHA256 | Format-List
