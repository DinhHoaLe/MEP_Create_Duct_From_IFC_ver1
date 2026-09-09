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
Check @() 'Chưa tồn tại' $false
Check @((Segment 1 0 10)) 'Khớp hoàn toàn' $true
Check @((Segment 1 10 0)) 'Khớp hoàn toàn' $true
Check @((Segment 1 0 4), (Segment 2 4 10)) 'Khớp hoàn toàn' $true
Check @((Segment 1 0 4), (Segment 2 5 10)) 'Trùng một phần' $false
Check @((Segment 1 0 5)) 'Trùng một phần' $false
Check @((Segment 1 0 10 3)) 'Sai kích thước' $true
Check @((Segment 1 0 10 2 1 0 8)) 'Sai hệ thống' $true
Check @((Segment 1 0 10 3 1 0 8)) 'Sai kích thước và hệ thống' $true
Check @((Segment 1 0 10)) 'Chưa xác định hệ thống' $true $null
Check @((Segment 1 0 10 3)) 'Sai kích thước' $true $null
Check @((Segment 1 0 5), (Segment 2 5 10 3)) 'Sai kích thước' $true
Check @((Segment 1 0 10 2.002)) 'Khớp hoàn toàn' $true
Check @((Segment 1 0 10 2.004)) 'Sai kích thước' $true
Check @((Segment 1 0 10 0 0 2)) 'Khớp hoàn toàn' $true 7 2
Check @((Segment 1 0 10 0 0 3)) 'Sai kích thước' $true 7 2
Check @((Segment 1 0 10)) 'Sai kích thước' $true 7 2
$oval = Segment 1 0 10
$oval.UnsupportedShape = $true
Check @($oval) 'Sai kích thước' $true
$offset = Segment 1 0 10
$offset.Start = @(0,1,0); $offset.End = @(10,1,0)
Check @($offset) 'Chưa tồn tại' $false
Write-Output "$script:checks checks passed."
