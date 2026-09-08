$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
[void][Reflection.Assembly]::LoadFrom("$PSScriptRoot\..\outputs\IFCInfo.dll")
function Controls($node, $type) {
 if($node -is $type){$node}
 foreach($child in [Windows.LogicalTreeHelper]::GetChildren($node)){if($child -is [Windows.DependencyObject]){Controls $child $type}}
}
$window = New-Object IFCInfo.IFCInfoWindow
foreach($n in 1..3){$link=New-Object IFCInfo.LinkOption; $link.Id=$n; $link.Name="HVAC $n"; $link.IsLoaded=($n -ne 3); $window.Links.Add($link)}
$window.LoadLink = [Action[IFCInfo.LinkOption]] {
 param($link)
 $cat=New-Object IFCInfo.CategoryOption; $cat.Id=$link.Id; $cat.Name="Ducts $($link.Id)"; $window.Categories.Add($cat)
 $window.LoadCategory = [Action[IFCInfo.CategoryOption]] {
  param($category)
  $row=New-Object IFCInfo.AirTerminalRow; $row.ElementId='100'; $row.Name='Duct'; $row.SystemType='Supply Air'; $row.SystemName='SA 1'; $row.Elevation='3000'; $row.IfcGuid='HIDDEN'; $row.DataSource='HIDDEN SOURCE'
  $window.AirTerminals.Add($row); $window.AirTerminalCount=1; $window.CanCreateDucts=$true
 }
}
$window.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.FrameworkElement]::LoadedEvent)))
$boxes=@(Controls $window ([Windows.Controls.ComboBox])); if($boxes.Count -ne 2){throw 'Expected link + category dropdowns'}
$next=@(Controls $window ([Windows.Controls.Button])) | Where-Object {$_.Content -eq 'Tiếp tục →'}
if($next.IsEnabled){throw 'Next enabled before selection'}
$boxes[0].SelectedIndex=0; $boxes[1].SelectedIndex=0
if(-not $next.IsEnabled){throw 'Next not enabled'}
function Render($name){$root=$window.Content; $root.Measure([Windows.Size]::new(1050,650)); $root.Arrange([Windows.Rect]::new(0,0,1050,650)); $root.UpdateLayout(); $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new(1050,650,96,96,[Windows.Media.PixelFormats]::Pbgra32); $bitmap.Render($root); $encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new(); $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap)); $stream=[IO.File]::Create("$PSScriptRoot\$name.png"); $encoder.Save($stream); $stream.Dispose()}
Render 'link-dropdown-preview'
$next.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
$table=@(Controls $window ([Windows.Controls.DataGrid]))[0]; if($table.Columns.Count -ne 6){throw 'Unexpected visible columns'}
$export=$window.SystemTableText(); if($export.Contains('HIDDEN') -or $export.Contains('GUID')){throw 'Metadata leaked to export'}
foreach($line in ($export -split "`r?`n")){if(($line -split "`t").Count -ne 5){throw 'Export column mismatch'}}
Render 'category-table-preview'
$back=@(Controls $window ([Windows.Controls.Button])) | Where-Object {$_.Content -eq '← Quay lại'}
$back.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
$boxes[0].SelectedIndex=1
if($next.IsEnabled -or $window.AirTerminals.Count -ne 0 -or $boxes[1].SelectedItem -ne $null -or $window.Categories[0].Name -ne 'Ducts 2'){throw 'Link change did not reset data'}
$boxes[0].SelectedIndex=2
if($next.IsEnabled -or $boxes[1].IsEnabled){throw 'Unloaded link allowed'}
$window.Close()
'PASS: link/category selection, navigation, switching links, unloaded links, metadata removed, export columns.'
