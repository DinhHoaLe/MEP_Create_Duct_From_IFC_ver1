$ErrorActionPreference = 'Stop'
Add-Type -Path "$PSScriptRoot/../src/Services/Ducts/DuctCoverage.cs", "$PSScriptRoot/../src/Models/AirTerminalRow.cs", "$PSScriptRoot/../src/Models/IfcTerminalSource.cs"
$script:checks = 0
function Segment($id, $start, $end, $width = 2, $height = 1, $diameter = 0, $system = 7) {
    $s = [IFCInfo.DuctCoverage+Segment]::new()
    $s.Id = $id; $s.Start = @($start,0,0); $s.End = @($end,0,0)
    $s.Width = $width; $s.Height = $height; $s.Diameter = $diameter; $s.SystemTypeId = $system
    return $s
}
function Check($segments, $status, $full, $expected = 7, $diameter = 0) {
    $result = [IFCInfo.DuctCoverage]::CheckDetails(@(0,0,0), @(10,0,0), [IFCInfo.DuctCoverage+Segment[]]$segments, (1/304.8), 2, 1, $diameter, $expected)
    if ($result.Status -ne $status -or $result.FullyCovered -ne $full) { throw "Expected $status/$full; got $($result.Status)/$($result.FullyCovered)" }
    $row = [IFCInfo.AirTerminalRow]::new()
    $row.IsSelected = $true
    $row.DuctExistence = $result.Status
    if ($full -and ($row.CanSelect -or $row.IsSelected)) { throw 'Fully covered duct must not be selectable' }
    $script:checks++
}
Check @() 'Not created' $false
Check @((Segment 1 0 10)) 'Exact match' $true
Check @((Segment 1 10 0)) 'Exact match' $true
Check @((Segment 1 0 4), (Segment 2 4 10)) 'Exact match' $true
Check @((Segment 1 0 4), (Segment 2 5 10)) 'Partial overlap' $false
Check @((Segment 1 0 5)) 'Partial overlap' $false
Check @((Segment 1 0 10 3)) 'Size mismatch' $true
Check @((Segment 1 0 10 2 1 0 8)) 'System mismatch' $true
Check @((Segment 1 0 10 3 1 0 8)) 'Size and system mismatch' $true
Check @((Segment 1 0 10)) 'System unknown' $true $null
Check @((Segment 1 0 10 3)) 'Size mismatch' $true $null
Check @((Segment 1 0 5), (Segment 2 5 10 3)) 'Size mismatch' $true
Check @((Segment 1 0 10 2.002)) 'Exact match' $true
Check @((Segment 1 0 10 2.004)) 'Size mismatch' $true
Check @((Segment 1 0 10 0 0 2)) 'Exact match' $true 7 2
Check @((Segment 1 0 10 0 0 3)) 'Size mismatch' $true 7 2
Check @((Segment 1 0 10)) 'Size mismatch' $true 7 2
$oval = Segment 1 0 10
$oval.UnsupportedShape = $true
Check @($oval) 'Size mismatch' $true
$offset = Segment 1 0 10
$offset.Start = @(0,1,0); $offset.End = @(10,1,0)
Check @($offset) 'Not created' $false
$flat = Segment 1 0 10
$flat.Oval = $true
$result = [IFCInfo.DuctCoverage]::CheckDetails(@(0,0,0),@(10,0,0),[IFCInfo.DuctCoverage+Segment[]]@($flat),(1/304.8),2,1,0,7,$null,$true)
if ($result.Status -ne 'Exact match') { throw 'Matching oval should be recognized' }
$script:checks++
Check @($flat) 'Size mismatch' $true
$result = [IFCInfo.DuctCoverage]::CheckDetails(@(0,0,0),@(10,0,0),[IFCInfo.DuctCoverage+Segment[]]@((Segment 1 0 10)),(1/304.8),2,1,0,7,$null,$true)
if ($result.Status -ne 'Size mismatch') { throw 'Rectangle must not match oval with the same dimensions' }
$script:checks++
Write-Output "$script:checks checks passed."
