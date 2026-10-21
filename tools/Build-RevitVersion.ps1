param(
    [ValidateSet(2023,2024,2025,2026,2027)][int]$RevitVersion=2023,
    [string]$RevitApiDir,
    [string]$TargetFramework
)
$ErrorActionPreference='Stop'
if (!$RevitApiDir) { $RevitApiDir="C:\Program Files\Autodesk\Revit $RevitVersion" }
$api=Join-Path $RevitApiDir 'RevitAPI.dll'
if (!(Test-Path -LiteralPath $api)) {throw "Missing Revit $RevitVersion API: $api. Install the matching Revit/API or pass -RevitApiDir. No DLL was packaged."}
$version=[Diagnostics.FileVersionInfo]::GetVersionInfo($api)
if ($version.FileMajorPart -ne ($RevitVersion-2000)) {throw "API version $($version.FileVersion) does not match Revit $RevitVersion."}
if (!$TargetFramework) {
    $TargetFramework=if($RevitVersion -le 2024){'net48'}elseif($RevitVersion -eq 2027 -or ($RevitVersion -eq 2026 -and $version.FileMinorPart -ge 5)){'net10.0-windows'}else{'net8.0-windows'}
}
$root=Split-Path -Parent $PSScriptRoot
$name="IFCInfo_Revit$RevitVersion"
& dotnet build (Join-Path $root 'outputs/IFCInfo.csproj') -c Release "-p:RevitVersion=$RevitVersion" "-p:RevitApiDir=$RevitApiDir" "-p:RevitTargetFramework=$TargetFramework" "-p:AssemblyName=$name" -v:q
if($LASTEXITCODE -ne 0){throw 'Build failed; no DLL packaged.'}
$destination=Join-Path $root "release/IFCInfo-Revit$RevitVersion"
New-Item -ItemType Directory -Force -Path $destination | Out-Null
foreach($extension in @('dll','pdb')){
    Copy-Item -LiteralPath (Join-Path $root "outputs/bin/Release/$TargetFramework/$name.$extension") -Destination $destination
}
@"
Revit $RevitVersion build
API file version: $($version.FileVersion)
Target framework: $TargetFramework
Load $name.dll using a compatible Add-in Manager.
Command: IFCInfo.IFCInfoCommand
Build success does not certify live model transactions.
Use this package only with a compatible Revit API/runtime; the 26.5 .NET 10 build is not for earlier .NET 8 Revit 2026 installations.
"@ | Set-Content -LiteralPath (Join-Path $destination 'BUILD_INFO.txt') -Encoding UTF8
Write-Output "Packaged $destination ($TargetFramework; API $($version.FileVersion))"