param([string]$DestinationRoot, [string]$RevitInstallDirectory)
$ErrorActionPreference = 'Stop'
try {
    $config = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'package.json') -Raw | ConvertFrom-Json
    if ($config.RevitVersion -notin @(2025)) { throw 'Unsupported package version.' }
    if (!$DestinationRoot) {
        if (Get-Process Revit -ErrorAction SilentlyContinue) { throw 'Close Revit before installing.' }
        if (!$env:APPDATA) { throw 'APPDATA is unavailable.' }
        $DestinationRoot = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$($config.RevitVersion)"
    }
    if (!$RevitInstallDirectory) { $RevitInstallDirectory = Join-Path $env:ProgramFiles 'Autodesk\Revit 2025' }
    if (!(Test-Path -LiteralPath (Join-Path $RevitInstallDirectory 'RevitAPI.dll'))) {
        $RevitInstallDirectory = (Read-Host 'Enter the Revit 2025 installation folder').Trim('"')
    }
    $api = Join-Path $RevitInstallDirectory 'RevitAPI.dll'
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($api)
    if ($version.FileMajorPart -ne 25) { throw 'This installer is only for Revit 2025.' }
    $runtimeFile = Join-Path $RevitInstallDirectory 'RevitAPI.runtimeconfig.json'
    if (Test-Path -LiteralPath $runtimeFile) {
        $runtime = Get-Content -LiteralPath $runtimeFile -Raw | ConvertFrom-Json
        $match = [regex]::Match($runtime.runtimeOptions.tfm, '^net(8|10)\.0')
        if (!$match.Success) { throw 'This Revit runtime is not supported by the package.' }
        $runtimeMajor = [int]$match.Groups[1].Value
    } elseif ($version.FileMinorPart -lt 5) { $runtimeMajor = 8 }
    else { throw 'Cannot identify the Revit runtime. Contact the sender before installing.' }
    $variant = @($config.Variants | Where-Object { $_.RuntimeMajor -eq $runtimeMajor })
    if ($variant.Count -ne 1) { throw 'Matching runtime package is missing.' }
    $source = Join-Path $PSScriptRoot ($variant[0].Directory + '\IFCInfo.dll')
    $expectedHash = $variant[0].SHA256
    if ((Get-FileHash -LiteralPath $source).Hash -ne $expectedHash) { throw 'Package verification failed. Extract the ZIP again.' }
    $folder = Join-Path $DestinationRoot 'IFCInfo'
    $manifest = Join-Path $DestinationRoot 'IFCInfo.addin'
    if (Test-Path -LiteralPath $manifest) {
        [xml]$previous = Get-Content -LiteralPath $manifest -Raw
        if ($previous.RevitAddIns.AddIn.AddInId -ne '8875233F-C015-4400-9B76-8602141587CA') { throw 'Another add-in owns IFCInfo.addin. Contact your administrator.' }
    }
    New-Item -ItemType Directory -Force -Path $folder | Out-Null
    $target = Join-Path $folder 'IFCInfo.dll'
    Copy-Item -LiteralPath $source -Destination $target -Force
    Unblock-File -LiteralPath $target -ErrorAction SilentlyContinue
    $assembly = [Security.SecurityElement]::Escape([IO.Path]::GetFullPath($target))
    $xml = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Command">
    <Name>Create Duct from IFC</Name>
    <Assembly>$assembly</Assembly>
    <AddInId>8875233F-C015-4400-9B76-8602141587CA</AddInId>
    <FullClassName>IFCInfo.IFCInfoCommand</FullClassName>
    <Text>Create Duct from IFC</Text>
    <VendorId>IFCI</VendorId>
    <VendorDescription>Create native MEP elements from linked IFC models.</VendorDescription>
  </AddIn>
</RevitAddIns>
"@
    [IO.File]::WriteAllText($manifest, $xml, (New-Object Text.UTF8Encoding($false)))
    if ((Get-FileHash -LiteralPath $target).Hash -ne $expectedHash) { throw 'Installed DLL verification failed.' }
    Write-Host "Installed for Revit $($config.RevitVersion), API $($version.FileVersion), .NET $runtimeMajor."
    Write-Host 'Open Revit > Add-Ins > External Tools > Create Duct from IFC.'
} catch {
    Write-Host ('Installation failed: ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
