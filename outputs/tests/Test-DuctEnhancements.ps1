$ErrorActionPreference = 'Stop'
Add-Type -Path "$PSScriptRoot/../src/Services/Ducts/DuctSnapshotComparison.cs", "$PSScriptRoot/../src/Models/DuctRunRow.cs", "$PSScriptRoot/../src/Services/Ducts/DuctCoverage.cs"
Add-Type -Path "$PSScriptRoot/../src/Services/Ducts/DuctConnectionGeometry.cs"
$script:checks = 0
function Assert($ok, $message) { if (!$ok) { throw $message }; $script:checks++ }
$stamp = '0;0;0;10;0;0;0;1;0;2;1;0|Supply|SA'
Assert ([IFCInfo.DuctSnapshotComparison]::Equal($stamp,$stamp)) 'Unchanged snapshot'
Assert (![IFCInfo.DuctSnapshotComparison]::Equal($null,$stamp)) 'Missing baseline'
Assert (![IFCInfo.DuctSnapshotComparison]::Equal('invalid','invalid')) 'Malformed snapshot'
Assert (![IFCInfo.DuctSnapshotComparison]::Equal($stamp,$stamp.Replace('10;','11;'))) 'Moved endpoint'
Assert (![IFCInfo.DuctSnapshotComparison]::Equal($stamp,$stamp.Replace(';2;1;0|',';3;1;0|'))) 'Changed dimensions'
Assert (![IFCInfo.DuctSnapshotComparison]::Equal($stamp,$stamp.Replace('Supply','Return'))) 'Changed system'
Assert (![IFCInfo.DuctSnapshotComparison]::Equal($stamp,$stamp.Replace('|SA','|SB'))) 'Changed system name'
Assert ([IFCInfo.DuctSnapshotComparison]::Equal($stamp,$stamp.Replace('10;','10.0001;'))) 'Numerical noise below tolerance'
Assert (![IFCInfo.DuctSnapshotComparison]::Equal($stamp,$stamp.Replace('10;','10.001;'))) 'Change above tolerance'
Assert (![IFCInfo.DuctSnapshotComparison]::Equal($stamp,$stamp.Replace('10;','NaN;'))) 'Invalid numeric baseline'
Assert (![IFCInfo.DuctSnapshotComparison]::Equal(($stamp+'|type7|ws1'),($stamp+'|type7|ws2'))) 'Manual workset edit'
Assert (![IFCInfo.DuctSnapshotComparison]::Equal(($stamp+'|port1'),($stamp+'|port2'))) 'Manual connection edit'
Assert ([IFCInfo.DuctSnapshotComparison]::Describe($stamp,$stamp.Replace('Supply','Return')).Contains('Supply')) 'Before/after preview'
$d=[IFCInfo.DuctCoverage+Segment]::new()
$d.Id=1; $d.Start=@(0,0,0); $d.End=@(10,0,0); $d.Width=2; $d.Height=1; $d.SystemTypeId=7; $d.WidthAxis=@(0,0,1)
$result=[IFCInfo.DuctCoverage]::CheckDetails(@(0,0,0),@(10,0,0),[IFCInfo.DuctCoverage+Segment[]]@($d),0.003,2,1,0,7,@(0,1,0))
Assert ($result.Status -eq 'Sai góc tiết diện') 'Wrong section rotation'
$d.WidthAxis=@(0,-1,0)
$result=[IFCInfo.DuctCoverage]::CheckDetails(@(0,0,0),@(10,0,0),[IFCInfo.DuctCoverage+Segment[]]@($d),0.003,2,1,0,7,@(0,1,0))
Assert ($result.Status -eq 'Khớp hoàn toàn') 'Reversed section axis is equivalent'
$row=[IFCInfo.DuctRunRow]::new()
$row.RunId='run'; $row.SourceId='123'; $row.IfcGuid='guid'; $row.TargetId='456'; $row.Status='Đã tạo'; $row.Reason="A, B `"quoted`"`nnext line"
$path=Join-Path ([IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString()+'.csv')
try {
    [IFCInfo.DuctRunRow]::Export($path,[IFCInfo.DuctRunRow[]]@($row))
    $loaded=Import-Csv -LiteralPath $path
    Assert ($loaded.Reason -eq $row.Reason) 'CSV quotes, commas and multiline roundtrip'
    Assert ($loaded.Status -eq $row.Status -and $loaded.'Target Element ID' -eq '456') 'CSV Unicode and IDs'
    $row.Reason='  =1+1'; [IFCInfo.DuctRunRow]::Export($path,[IFCInfo.DuctRunRow[]]@($row))
    Assert ((Import-Csv -LiteralPath $path).Reason.StartsWith("'")) 'CSV formula escaping'
} finally { Remove-Item -LiteralPath $path }
Write-Output "$script:checks enhancement checks passed."
Assert ([IFCInfo.DuctConnectionGeometry]::CanBridge(@(0,0,0),@(1,0,0),@(1,1,0),@(0,-1,0),2,0.003)) 'Elbow rays intersect ahead'
Assert (![IFCInfo.DuctConnectionGeometry]::CanBridge(@(0,0,0),@(-1,0,0),@(1,1,0),@(0,-1,0),2,0.003)) 'No backwards extension'
Assert (![IFCInfo.DuctConnectionGeometry]::CanBridge(@(0,0,0),@(1,0,0),@(1,1,1),@(0,-1,0),2,0.003)) 'Reject noncoplanar rays'
Assert (![IFCInfo.DuctConnectionGeometry]::CanBridge(@(0,0,0),@(1,0,0),@(1,1,0),@(0,-1,0),0.5,0.003)) 'Respect extension limit'
Assert ([IFCInfo.DuctConnectionGeometry]::CanBridge(@(0,0,0),@(1,0,0),@(1,0,0),@(-1,0,0),2,0.003)) 'Opposite collinear connector rays'
Assert (![IFCInfo.DuctConnectionGeometry]::CanBridge(@(0,0,0),@(1,0,0),@(1,0,0),@(1,0,0),2,0.003)) 'Same-direction ends rejected'
Assert (![IFCInfo.DuctConnectionGeometry]::CanBridge(@(0,0,0),@(1,0,0),@(1,0.1,0),@(-1,0,0),2,0.003)) 'Offset parallel ends rejected'
Write-Output "Total: $script:checks enhancement checks passed."
