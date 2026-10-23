param([string]$LegacyApiDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (!$LegacyApiDirectory) { $LegacyApiDirectory = Join-Path $root 'outputs/obj/package-api-2025/reference' }
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$package = Join-Path $root "release/Shareable-$stamp/Create-Duct-from-IFC-Revit2025"
New-Item -ItemType Directory -Force -Path $package | Out-Null
$variants = @()
foreach ($runtimeMajor in @(8,10)) {
    $api = if ($runtimeMajor -eq 8) { $LegacyApiDirectory } else { 'C:\Program Files\Autodesk\Revit 2025' }
    $log = Join-Path $root "outputs/obj/package-net$runtimeMajor-build.log"
    & (Join-Path $PSScriptRoot 'Build-RevitVersion.ps1') -RevitVersion 2025 -RevitApiDir $api -TargetFramework "net$runtimeMajor.0-windows" *> $log
    if ($LASTEXITCODE -ne 0) { throw "Build failed. See $log" }
    $destination = Join-Path $package "runtime-$runtimeMajor"
    New-Item -ItemType Directory -Path $destination | Out-Null
    $source = Join-Path $root 'release/IFCInfo-Revit2025/IFCInfo_Revit2025.dll'
    Copy-Item -LiteralPath $source -Destination (Join-Path $destination 'IFCInfo.dll')
    $variants += @{ RuntimeMajor=$runtimeMajor; Directory="runtime-$runtimeMajor"; SHA256=(Get-FileHash $source).Hash }
    Write-Output "Built and staged .NET $runtimeMajor variant."
}
foreach ($file in @('Install.cmd','Install.ps1','HUONG-DAN.txt')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "shareable-installer/$file") -Destination $package
}
@{ Product='Create Duct from IFC'; RevitVersion=2025; PackageVersion=$stamp; Variants=$variants } |
    ConvertTo-Json -Depth 4 | Set-Content (Join-Path $package 'package.json') -Encoding UTF8
$zip = Join-Path $root "release/Create-Duct-from-IFC-Revit2025-$stamp.zip"
Compress-Archive -LiteralPath $package -DestinationPath $zip
Write-Output "PACKAGE_FOLDER=$package"
Write-Output "PACKAGE_ZIP=$zip"
