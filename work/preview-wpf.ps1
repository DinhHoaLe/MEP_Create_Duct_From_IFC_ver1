Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
$assembly = [Reflection.Assembly]::LoadFrom("$PSScriptRoot\ifc2024-build\IFCInfo.dll")
$window = New-Object IFCInfo.IFCInfoWindow -ArgumentList @('STR_Tower-A_Level-01_Coordination.ifc','D:\BIM Projects\Tower A\Shared\IFC\STR_Tower-A_Level-01_Coordination.ifc','D:\BIM Projects\Tower A\Shared\IFC\STR_Tower-A_Level-01_Coordination.ifc.RVT','428691','Project Information → Original IFC File Name','Thông tin lưu khi import/link; đường dẫn có thể đã thay đổi.',$true)
$root = $window.Content
$root.Measure([Windows.Size]::new(724,680))
$root.Arrange([Windows.Rect]::new(0,0,724,680))
$root.UpdateLayout()
$bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(724,680,96,96,[Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($root)
$encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$stream = [IO.File]::Create("$PSScriptRoot\wpf-preview.png")
$encoder.Save($stream)
$stream.Dispose()
$window.Close()
Write-Output 'WPF layout rendered successfully.'
