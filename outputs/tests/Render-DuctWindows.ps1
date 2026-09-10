# Offscreen WPF layout smoke check; no Revit model operations.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml
[void][Reflection.Assembly]::LoadFrom('C:/Program Files/Autodesk/Revit 2024/RevitAPI.dll')
$assembly=[Reflection.Assembly]::LoadFrom((Resolve-Path "$PSScriptRoot/../bin/Release/net48/IFCInfo.dll"))
$items=New-Object 'System.Collections.Generic.List[IFCInfo.DuctPlanItem]'
$item=New-Object IFCInfo.DuctPlanItem
$item.Source=New-Object IFCInfo.AirTerminalRow
$item.Source.ElementId='123'; $item.Source.SystemType='Supply Air'; $item.Source.IfcGuid='guid'
$item.Width=1; $item.Height=0.5; $item.LevelKey='Level 1'
$items.Add($item)
$issues=New-Object 'System.Collections.Generic.List[string]'
$choices=New-Object 'System.Collections.Generic.List[IFCInfo.DuctChoice]'
$choice=New-Object IFCInfo.DuctChoice; $choice.Id=1; $choice.Name='Supply Air'; $choices.Add($choice)
$saved=New-Object IFCInfo.DuctSettingsData
$window=New-Object IFCInfo.DuctCreationWindow -ArgumentList $items,$issues,$choices,$choices,$choices,$choices,$choices,$saved,$false
$qa=Join-Path $PSScriptRoot '../qa'
[void][IO.Directory]::CreateDirectory($qa)
function Render($window,$name,$w,$h) {
    $root=$window.Content
    $root.Measure([Windows.Size]::new($w,$h))
    $root.Arrange([Windows.Rect]::new(0,0,$w,$h)); $root.UpdateLayout()
    $bmp=[Windows.Media.Imaging.RenderTargetBitmap]::new($w,$h,96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bmp.Render($root)
    $encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new(); $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bmp))
    $stream=[IO.File]::Create((Join-Path $qa $name)); try { $encoder.Save($stream) } finally { $stream.Dispose() }
}
Render $window 'duct-settings.png' 900 690
$scroll=$window.Content.Children | Where-Object { $_ -is [Windows.Controls.ScrollViewer] } | Select-Object -First 1
$scroll.ScrollToEnd()
Render $window 'duct-settings-options.png' 900 690
$window.Close()
$rows=New-Object 'System.Collections.Generic.List[IFCInfo.DuctRunRow]'
$row=New-Object IFCInfo.DuctRunRow; $row.SourceId='123'; $row.TargetId='456'; $row.Status='Created'; $row.Success=$true; $rows.Add($row)
$type=$assembly.GetType('IFCInfo.DuctCreationResultWindow')
$constructor=$type.GetConstructors([Reflection.BindingFlags]'Instance,NonPublic')[0]
$result=$constructor.Invoke([object[]]@($rows.PSObject.BaseObject,$false,[Action]{}))
Render $result 'duct-results.png' 1050 600
$result.Close()
Write-Output 'Two WPF windows constructed and rendered offscreen.'
