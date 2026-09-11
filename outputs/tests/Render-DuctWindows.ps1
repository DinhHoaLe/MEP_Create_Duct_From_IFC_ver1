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
$row=New-Object IFCInfo.DuctRunRow; $row.SourceId='123'; $row.TargetId='456'; $row.Status='Created'; $row.Success=$true
$row.IfcPsets="Instance | Pset_WallCommon.FireRating = 60 min`nType | Pset_Type.Manufacturer = Test Company"; $rows.Add($row)
$type=$assembly.GetType('IFCInfo.DuctCreationResultWindow')
$constructor=$type.GetConstructors([Reflection.BindingFlags]'Instance,NonPublic')[0]
$result=$constructor.Invoke([object[]]@($rows.PSObject.BaseObject,$false,[Action]{},'IFC results'))
Render $result 'duct-results.png' 1050 600
$result.Close()
$sourceRows=New-Object 'System.Collections.Generic.List[IFCInfo.AirTerminalRow]'; $sourceRows.Add($item.Source)
$nativeTypes=New-Object 'System.Collections.Generic.List[IFCInfo.ReplacementTypeOption]'
foreach ($kind in @('Family','Pipe','Wall','Floor','Roof','Unsupported')) {
    $option=New-Object IFCInfo.ReplacementTypeOption
    $option.Id=10+$nativeTypes.Count; $option.CategoryId=$option.Id; $option.CategoryName=$kind; $option.Kind=$kind; $option.Label=$kind+' test type'
    $option.Supported=$kind -ne 'Unsupported'; $option.Placement='Native placement test'
    $nativeTypes.Add($option)
}
$nativeLevels=New-Object 'System.Collections.Generic.List[IFCInfo.ReplacementLevelOption]'
$level=New-Object IFCInfo.ReplacementLevelOption; $level.Id=1; $level.Label='Level 1'; $nativeLevels.Add($level)
$nativeSystems=New-Object 'System.Collections.Generic.List[IFCInfo.ReplacementTypeOption]'
$system=New-Object IFCInfo.ReplacementTypeOption; $system.Id=20; $system.Kind='Pipe'; $system.Label='Water'; $nativeSystems.Add($system)
$native=New-Object IFCInfo.NativePlacementWindow -ArgumentList $sourceRows,$nativeTypes,$nativeLevels,$nativeSystems,12
Render $native 'native-placement.png' 780 700
function Controls($root) {
    if ($root -is [Windows.FrameworkElement] -and $root.Name) { $root }
    for ($i=0; $i -lt [Windows.Media.VisualTreeHelper]::GetChildrenCount($root); $i++) { Controls ([Windows.Media.VisualTreeHelper]::GetChild($root,$i)) }
}
$controls=@(Controls $native.Content)
$category=$controls | Where-Object Name -eq TargetCategory | Select-Object -First 1
$typeBox=$controls | Where-Object Name -eq TargetType | Select-Object -First 1
$systemBox=$controls | Where-Object Name -eq TargetSystem | Select-Object -First 1
$create=$controls | Where-Object Name -eq CreateNative | Select-Object -First 1
if ($category.SelectedItem.Name -ne 'Wall') { throw 'Source category default failed' }
$typeBox.SelectedIndex=0
if (!$create.IsEnabled -or $systemBox.IsEnabled) { throw 'Wall placement validation failed' }
$category.SelectedItem=@($category.Items | Where-Object Name -eq Pipe)[0]; $typeBox.SelectedIndex=0
if (!$systemBox.IsEnabled -or !$create.IsEnabled) { throw 'Pipe system selection failed' }
$category.SelectedItem=@($category.Items | Where-Object Name -eq Unsupported)[0]; $typeBox.SelectedIndex=0
if ($create.IsEnabled) { throw 'Unsupported type must be disabled' }
$category.SelectedItem=@($category.Items | Where-Object Name -eq Wall)[0]; $typeBox.SelectedIndex=0
Render $native 'native-placement.png' 780 700
$scroll=$native.Content.Children | Where-Object { $_ -is [Windows.Controls.ScrollViewer] } | Select-Object -First 1
$scroll.ScrollToEnd(); Render $native 'native-placement-options.png' 780 700
$native.Close()
Write-Output 'Three WPF windows rendered; 4 category/type validation checks passed.'
