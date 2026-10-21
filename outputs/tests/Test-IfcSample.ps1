param([string]$SampleFile = (Join-Path $PSScriptRoot '../../Revit/Snowdon Towers Sample HVAC.ifc'))
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $SampleFile)) {
    if ($PSBoundParameters.ContainsKey('SampleFile')) { throw "Sample file not found: $SampleFile" }
    Write-Output 'SKIP: optional Snowdon IFC sample is not present. Supply -SampleFile to run this test.'
    exit 0
}
Add-Type -Path @("$PSScriptRoot/../src/Ifc/IfcSourceReader.cs", "$PSScriptRoot/../src/Models/IfcTerminalSource.cs")
$timer = [Diagnostics.Stopwatch]::StartNew()
$reader = [IFCInfo.IfcSourceReader]::Read($SampleFile)
if ($reader.Terminals.Count -ne 509) { throw 'Expected 509 Air Terminals' }
if ($reader.SystemCount -ne 148) { throw 'Expected 148 systems' }
$withSystems = @($reader.Terminals.Values | Where-Object { $_.SystemName }).Count
$withElevation = @($reader.Terminals.Values | Where-Object { $null -ne $_.ElevationMm }).Count
if ($withSystems -ne 509) { throw 'System membership missing' }
if ($withElevation -ne 509) { throw 'Elevation missing' }
$sample = $reader.Terminals['0B82sWVf587AtmxOGOUsde']
if ($sample.SystemName -ne 'Mechanical Supply Air 1') { throw 'Wrong system name' }
if ($sample.SystemType -ne 'Supply Air') { throw 'Wrong system type' }
$sample | Format-List
Write-Output "PASS: 509 terminals, 148 systems, $withSystems memberships, $withElevation elevations. Seconds: $($timer.Elapsed.TotalSeconds)"

