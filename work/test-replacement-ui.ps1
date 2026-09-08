$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
[void][Reflection.Assembly]::LoadFrom("$PSScriptRoot\ifc2024-build\IFCInfo.dll")
$rows = New-Object 'System.Collections.Generic.List[IFCInfo.AirTerminalRow]'
$row = New-Object IFCInfo.AirTerminalRow
$row.ElementId = '123'
$row.Name = 'Supply diffuser'
$rows.Add($row)
$types = New-Object 'System.Collections.Generic.List[IFCInfo.ReplacementTypeOption]'
$type = New-Object IFCInfo.ReplacementTypeOption
$type.Id = 100
$type.Label = 'Supply Diffuser : 600 x 600'
$type.Supported = $true
$type.Placement = 'Level based - no host'
$types.Add($type)
$unsupported = New-Object IFCInfo.ReplacementTypeOption
$unsupported.Id = 101
$unsupported.Label = 'Unsupported family'
$unsupported.Supported = $false
$types.Add($unsupported)
$levels = New-Object 'System.Collections.Generic.List[IFCInfo.ReplacementLevelOption]'
$level = New-Object IFCInfo.ReplacementLevelOption
$level.Id = 200
$level.Label = 'L2'
$levels.Add($level)
$window = New-Object IFCInfo.AirTerminalReplacementWindow -ArgumentList @($rows,$types,$levels)
$root = $window.Content
$body = $root.Children[2].Content
$combo = @($body.Children | Where-Object { $_ -is [Windows.Controls.ComboBox] })
$create = $root.Children[1].Child.Children[1].Children[1]
if ($create.IsEnabled) { throw 'Must require explicit type selection' }
$combo[0].SelectedIndex = 1
if ($create.IsEnabled) { throw 'Unsupported type enabled' }
$combo[0].SelectedIndex = 0
if (-not $create.IsEnabled) { throw 'Supported type disabled' }
$combo[1].SelectedIndex = -1
if ($create.IsEnabled) { throw 'Missing level enabled' }
$combo[1].SelectedIndex = 0
$angle = $body.Children | Where-Object { $_ -is [Windows.Controls.TextBox] }
$angle.Text = 'invalid'
$create.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
if ($window.Request) { throw 'Invalid angle accepted' }
$angle.Text = '0'
$root.Measure([Windows.Size]::new(764,660))
$root.Arrange([Windows.Rect]::new(0,0,764,660))
$root.UpdateLayout()
$bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(764,660,96,96,[Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($root)
$encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$stream = [IO.File]::Create("$PSScriptRoot\replacement-preview.png")
$encoder.Save($stream)
$stream.Dispose()
$window.Close()
$main = New-Object IFCInfo.IFCInfoWindow -ArgumentList @('Model.ifc','D:\Model.ifc','D:\Model.ifc.RVT','Link','IFC','Preview',$true)
$main.AirTerminals.Add($row)
$buttons = $main.Content.Children[2].Child.Children[1].Children
$buttons[3].RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
$page = $main.Content.Children[1].Content
$selection = $page.Children | Where-Object { $_ -is [Windows.Controls.WrapPanel] }
$selection.Children[0].RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
if (-not $row.IsSelected) { throw 'Select all failed' }
$selection.Children[1].RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
if ($row.IsSelected) { throw 'Clear selection failed' }
$buttons[5].RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
if ($main.Replacement) { throw 'Empty selection accepted' }
$main.Close()
Write-Output 'PASS: explicit type selection, unsupported type, missing level, invalid angle, select all/none, empty selection; preview rendered.'
