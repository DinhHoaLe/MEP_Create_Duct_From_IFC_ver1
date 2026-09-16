$ErrorActionPreference='Stop'
Add-Type -Path @("$PSScriptRoot/../src/Models/AirTerminalRow.cs", "$PSScriptRoot/../src/Models/IfcTerminalSource.cs", "$PSScriptRoot/../src/Models/DuctRunRow.cs", "$PSScriptRoot/../src/Services/IfcSelectionCsv.cs")
$a=[IFCInfo.AirTerminalRow]::new(); $a.ElementId='123'; $a.Name='Pipe, "A"'; $a.IsSelected=$true
$b=[IFCInfo.AirTerminalRow]::new(); $b.ElementId='456'; $b.Name='Excluded'
$rows=[IFCInfo.AirTerminalRow[]]@($a,$b)
$lines=[IFCInfo.IfcSelectionCsv]::Lines($rows,'Source.ifc','Pipes')
if ($lines.Count -ne 2) { throw 'Must export only checked rows' }
$parsed=@($lines | ConvertFrom-Csv)
if ($parsed[0].Name -ne $a.Name -or $parsed[0].'Element ID' -ne '123') { throw 'CSV quoting or source identity failed' }
$a.Name='=1+1'
$parsed=@([IFCInfo.IfcSelectionCsv]::Lines($rows,'Source.ifc','Pipes') | ConvertFrom-Csv)
if ($parsed[0].Name -ne "'=1+1") { throw 'Formula escape failed' }
$a.IsSelected=$false
if ([IFCInfo.IfcSelectionCsv]::Lines($rows,'Source.ifc','Pipes').Count -ne 1) { throw 'Empty selection must export no data rows' }
$a.IsSelected=$true; $b.IsSelected=$true
$a.Elevation='100'; $b.Elevation='200'
foreach ($row in $rows) {
    $p=[IFCInfo.IfcPropertyValue]::new(); $p.Scope='Instance'; $p.SetName='Pset'; $p.Name='Size'
    $p.Value=if ($row.ElementId -eq '123') {'150'} else {'250'}
    $row.IfcProperties.Add($p)
}
$parsed=@([IFCInfo.IfcSelectionCsv]::Lines($rows,'Source.ifc','Pipes') | ConvertFrom-Csv)
if ($parsed.Count -ne 2 -or $parsed[0].'Instance / Pset / Size' -ne '150' -or $parsed[1].'Instance / Pset / Size' -ne '250') { throw 'Each selected element must retain its own parameter values' }
if ($parsed[0].'Elevation (mm)' -ne '100' -or $parsed[1].'Elevation (mm)' -ne '200') { throw 'Different element elevations must not aggregate' }
if (([IFCInfo.IfcSelectionCsv]::Lines($rows,'Source.ifc','Pipes') -join "`n").Contains('<varies>')) { throw 'Export must not use aggregated properties' }
Write-Output '7 IFC selection CSV checks passed.'
