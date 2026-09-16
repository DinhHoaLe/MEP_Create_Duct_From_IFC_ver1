$ErrorActionPreference='Stop'
[void][Reflection.Assembly]::LoadFrom('C:/Program Files/Autodesk/Revit 2023/RevitAPI.dll')
$published=Resolve-Path "$PSScriptRoot/../IFCInfo.dll"
$built=Resolve-Path "$PSScriptRoot/../bin/Release/net48/IFCInfo.dll"
if ((Get-FileHash $published).Hash -ne (Get-FileHash $built).Hash) { throw 'Published DLL does not match current build' }
$a=[Reflection.Assembly]::LoadFrom($published)
$api=$a.GetReferencedAssemblies() | Where-Object Name -eq 'RevitAPI'
if ($api.Version.Major -ne 23) { throw 'DLL must target Revit 2023' }
$helper=$a.GetType('IFCInfo.ElementIds')
$create=$helper.GetMethod('Create',[Reflection.BindingFlags]'Static,NonPublic')
$number=$helper.GetMethod('Number',[Reflection.BindingFlags]'Static,NonPublic')
foreach ($value in @([long]123,[long]-2008000)) {
    $id=$create.Invoke($null,[object[]]@($value))
    if ($number.Invoke($null,[object[]]@($id)) -ne $value) { throw 'ElementId round-trip failed' }
}
$m=$a.GetType('IFCInfo.SupportedCategories').GetMethod('Contains',[Reflection.BindingFlags]'Static,NonPublic')
$allowed=@([Enum]::GetValues([Autodesk.Revit.DB.BuiltInCategory]) | Where-Object { $m.Invoke($null,[object[]]@([long]$_)) })
$expected=@('OST_DuctCurves','OST_DuctFitting','OST_PipeCurves','OST_Conduit','OST_CableTray')
if ($allowed.Count -ne 5 -or @($expected | Where-Object { $_ -notin @($allowed | ForEach-Object ToString) }).Count) { throw 'Unexpected allowed categories' }
$allowed
$a.FullName
