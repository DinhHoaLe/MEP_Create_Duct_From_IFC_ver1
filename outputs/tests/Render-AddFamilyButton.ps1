$ErrorActionPreference='Stop'
. "$PSScriptRoot/Render-DuctWindows.ps1"
$main=New-Object IFCInfo.IFCInfoWindow
$category=New-Object IFCInfo.CategoryOption
$category.Id=[long][Autodesk.Revit.DB.BuiltInCategory]::OST_DuctFitting
$category.Name='Duct Fittings'
[IFCInfo.IFCInfoWindow].GetProperty('SelectedCategory').GetSetMethod($true).Invoke($main,[object[]]@($category.PSObject.BaseObject)) | Out-Null
$main.CanReplaceCategory=$true
$main.AirTerminalCount=2
$main.AirTerminals=$sourceRows
$page=[IFCInfo.IFCInfoWindow].GetMethod('CountPage',[Reflection.BindingFlags]'Instance,NonPublic').Invoke($main,@())
$preview=New-Object Windows.Window
$preview.Content=$page
Render $preview 'duct-fitting-add-button.png' 1100 600
$button=@(Controls $page) | Where-Object Name -eq AddAtIfcPosition | Select-Object -First 1
if ($button) { throw 'Old add fitting button must not appear above table' }
Write-Output 'Verified fitting table toolbar no longer contains the old add button.'
$preview.Close()
$main.Close()

