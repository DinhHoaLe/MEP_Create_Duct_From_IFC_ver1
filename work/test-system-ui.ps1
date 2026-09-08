$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
[void][Reflection.Assembly]::LoadFrom("$PSScriptRoot\ifc2024-build\IFCInfo.dll")
$w = New-Object IFCInfo.IFCInfoWindow -ArgumentList @('Mechanical.ifc','D:\Mechanical.ifc','D:\Mechanical.ifc.RVT','123','Metadata','Test',$true)
$row = New-Object IFCInfo.AirTerminalRow
$row.ElementId = '451234'
$row.Name = 'Supply diffuser 600 x 600'
$row.SystemType = 'Supply Air'
$row.SystemName = 'SA-Level 01'
$w.AirTerminals.Add($row)
$row2 = New-Object IFCInfo.AirTerminalRow
$row2.ElementId = '451235'
$row2.Name = 'Return grille'
$row2.SystemType = 'Return Air'
$row2.SystemName = 'RA-Level 01'
$w.AirTerminals.Add($row2)
if (-not $w.SystemTableText().Contains("Supply Air`tSA-Level 01")) { throw 'System copy failed' }
$r = $w.Content
$scroll = $r.Children[1]
$buttons = $r.Children[2].Child.Children[1].Children
$next = $buttons[3]
$back = $buttons[0]
$firstPage = $scroll.Content
foreach ($value in @(128,0,$null)) {
    $w.AirTerminalCount = $value
    $w.AirTerminalError = 'Link unavailable'
    $next.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
    if ($next.Visibility -ne 'Collapsed' -or $back.Visibility -ne 'Visible') { throw 'Navigation failed' }
    $displayed = $scroll.Content.Children[2].Child.Children[1].Text
    if ($null -ne $value -and $displayed -ne $value.ToString('N0')) { throw 'Wrong count' }
    if ($null -eq $value -and $displayed -eq '0') { throw 'Unavailable displayed as zero' }
    $r.Measure([Windows.Size]::new(924,680))
    $r.Arrange([Windows.Rect]::new(0,0,924,680))
    $r.UpdateLayout()
    if ($value -eq 128) {
        $b = [Windows.Media.Imaging.RenderTargetBitmap]::new(924,680,96,96,[Windows.Media.PixelFormats]::Pbgra32)
        $b.Render($r)
        $enc = [Windows.Media.Imaging.PngBitmapEncoder]::new()
        $enc.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($b))
        $stream = [IO.File]::Create("$PSScriptRoot\system-preview.png")
        $enc.Save($stream)
        $stream.Dispose()
    }
    $back.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
    if ($scroll.Content -ne $firstPage -or $next.Visibility -ne 'Visible') { throw 'Back failed' }
}
$w.Close()
Write-Output 'PASS: Next/Back, count 128, zero, unavailable; layout rendered.'