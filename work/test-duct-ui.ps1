$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
[void][Reflection.Assembly]::LoadFrom('C:\Program Files\Autodesk\Revit 2024\RevitAPI.dll')
[void][Reflection.Assembly]::LoadFrom("$PSScriptRoot\..\outputs\IFCInfo.dll")
$items = New-Object 'System.Collections.Generic.List[IFCInfo.DuctPlanItem]'
$item = New-Object IFCInfo.DuctPlanItem
$item.Source = New-Object IFCInfo.AirTerminalRow
$item.Source.SystemType = 'Supply Air'
$item.Diameter = 1
$items.Add($item)
$issues = New-Object 'System.Collections.Generic.List[string]'
$issues.Add('123: Unsupported curved source')
$types = New-Object 'System.Collections.Generic.List[IFCInfo.DuctChoice]'
$type = New-Object IFCInfo.DuctChoice
$type.Id = 10; $type.Name = 'Round Duct'
$types.Add($type)
$systems = New-Object 'System.Collections.Generic.List[IFCInfo.DuctChoice]'
$system = New-Object IFCInfo.DuctChoice
$system.Id = 20; $system.Name = 'Return Air'
$systems.Add($system)
$window = New-Object IFCInfo.DuctCreationWindow -ArgumentList $items,$issues,$types,$types,$systems
function Find-Controls($node, $typeName) {
    if ($node -is $typeName) { $node }
    foreach ($child in [Windows.LogicalTreeHelper]::GetChildren($node)) {
        if ($child -is [Windows.DependencyObject]) { Find-Controls $child $typeName }
    }
}
$boxes = @(Find-Controls $window ([Windows.Controls.ComboBox]))
if ($boxes.Count -ne 2) { throw 'Expected round type and system mapping' }
if ($null -ne $boxes[1].SelectedItem) { throw 'Unmatched system must require selection' }
$button = @(Find-Controls $window ([Windows.Controls.Button])) | Where-Object { $_.Content -like '*Duct' }
$button.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
if ($null -ne $window.Request) { throw 'Created request with missing system mapping' }
$window.Content.Measure((New-Object Windows.Size(760,720)))
$window.Content.Arrange((New-Object Windows.Rect(0,0,760,720)))
$window.Content.UpdateLayout()
$bitmap = New-Object Windows.Media.Imaging.RenderTargetBitmap(760,720,96,96,[Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($window.Content)
$encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$stream = [IO.File]::Create("$PSScriptRoot\duct-ui-preview.png")
$encoder.Save($stream); $stream.Dispose()
$window.Close()
'PASS: correct controls, unmatched system blocked, preview rendered.'


