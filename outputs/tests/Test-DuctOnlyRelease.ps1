$ErrorActionPreference='Stop'
[void][Reflection.Assembly]::LoadFrom('C:/Program Files/Autodesk/Revit 2024/RevitAPI.dll')
$a=[Reflection.Assembly]::LoadFrom((Resolve-Path "$PSScriptRoot/../releases/IFCInfo-DuctOnly-20260912/IFCInfo_DuctOnly_20260912.dll"))
$m=$a.GetType('IFCInfo.SupportedCategories').GetMethod('Contains',[Reflection.BindingFlags]'Static,NonPublic')
$allowed=@([Enum]::GetValues([Autodesk.Revit.DB.BuiltInCategory]) | Where-Object { $m.Invoke($null,[object[]]@([long]$_)) })
if ($allowed.Count -ne 2 -or $allowed -notcontains [Autodesk.Revit.DB.BuiltInCategory]::OST_DuctCurves -or $allowed -notcontains [Autodesk.Revit.DB.BuiltInCategory]::OST_DuctFitting) { throw 'Unexpected allowed categories' }
$allowed
$a.FullName
