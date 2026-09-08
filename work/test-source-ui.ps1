$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
[void][Reflection.Assembly]::LoadFrom("$PSScriptRoot\ifc2024-build\IFCInfo.dll")
$data = [IFCInfo.IfcSourceReader]::Read('F:\Hoa Le\3. References\Test Revit API\0908\Snowdon Towers Sample HVAC.ifc')
$w = New-Object IFCInfo.IFCInfoWindow -ArgumentList @('Snowdon Towers Sample HVAC.ifc','F:\Sample.ifc','F:\Sample.ifc.RVT','Preview','Metadata','Preview',$true)
$w.AirTerminalCount = $data.Terminals.Count
$w.IfcSourceStatus = 'IFC source: Snowdon Towers Sample HVAC.ifc | 509 Air Terminals. Preview data from source file.'
foreach ($source in $data.Terminals.Values) {
    $row = New-Object IFCInfo.AirTerminalRow
    $row.ElementId = 'Preview'
    $row.Name = $source.Name
    $row.SystemType = $source.SystemType
    $row.SystemName = $source.SystemName
    $row.IfcGuid = $source.Guid
    $row.Elevation = $source.ElevationMm.ToString('0.0',[Globalization.CultureInfo]::InvariantCulture)
    $row.DataSource = 'IFC source'
    $w.AirTerminals.Add($row)
}
$r = $w.Content
$scroll = $r.Children[1]
$buttons = $r.Children[2].Child.Children[1].Children
$buttons[3].RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
$table = $scroll.Content.Children | Where-Object { $_ -is [Windows.Controls.DataGrid] }
if ($table.Items.Count -ne 509 -or $table.Columns.Count -ne 7) { throw 'Wrong table data' }
if (-not $w.SystemTableText().Contains('Mechanical Supply Air 1')) { throw 'Missing system in copied data' }
if (-not $w.SystemTableText().Contains('242798.6')) { throw 'Missing elevation in copied data' }
$r.Measure([Windows.Size]::new(1164,780))
$r.Arrange([Windows.Rect]::new(0,0,1164,780))
$r.UpdateLayout()
$b = [Windows.Media.Imaging.RenderTargetBitmap]::new(1164,780,96,96,[Windows.Media.PixelFormats]::Pbgra32)
$b.Render($r)
$enc = [Windows.Media.Imaging.PngBitmapEncoder]::new()
$enc.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($b))
$stream = [IO.File]::Create("$PSScriptRoot\ifc-source-preview.png")
$enc.Save($stream)
$stream.Dispose()
$buttons[0].RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
if ($buttons[3].Visibility -ne 'Visible') { throw 'Back navigation failed' }
$w.Close()
Write-Output 'PASS: 509 table rows, 7 columns, copy text with systems/elevation, Next/Back, WPF layout.'
