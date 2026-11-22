param(
    [string]$PublishedDll = "$PSScriptRoot/../IFCInfo.dll",
    [string]$BuiltDll = "$PSScriptRoot/../bin/Release/net48/IFCInfo.dll"
)
$ErrorActionPreference='Stop'
if (![Environment]::Is64BitProcess) { throw 'Run this Revit API check with x64 PowerShell.' }
[void][Reflection.Assembly]::LoadFrom('C:/Program Files/Autodesk/Revit 2023/RevitAPI.dll')
[void][Reflection.Assembly]::LoadFrom('C:/Program Files/Autodesk/Revit 2023/RevitAPIUI.dll')
$published=Resolve-Path $PublishedDll
$built=Resolve-Path $BuiltDll
if ((Get-FileHash $published).Hash -ne (Get-FileHash $built).Hash) { throw 'Published DLL does not match current build' }
$a=[Reflection.Assembly]::LoadFrom($published)
$api=$a.GetReferencedAssemblies() | Where-Object Name -eq 'RevitAPI'
if ($api.Version.Major -ne 23) { throw 'DLL must target Revit 2023' }
if (!$a.GetType('IFCInfo.IFCInfoCommand').IsPublic) { throw 'Add-in Manager command must be public' }
if ('IFCInfo.CreateDuctIcon.png' -notin $a.GetManifestResourceNames()) { throw 'Project icon must be embedded in the DLL' }
$helper=$a.GetType('IFCInfo.ElementIds')
$create=$helper.GetMethod('Create',[Reflection.BindingFlags]'Static,NonPublic')
$number=$helper.GetMethod('Number',[Reflection.BindingFlags]'Static,NonPublic')
foreach ($value in @([long]123,[long]-2008000)) {
    $id=$create.Invoke($null,[object[]]@($value))
    if ($number.Invoke($null,[object[]]@($id)) -ne $value) { throw 'ElementId round-trip failed' }
}
$m=$a.GetType('IFCInfo.SupportedCategories').GetMethod('Contains',[Reflection.BindingFlags]'Static,NonPublic')
$allowed=@([Enum]::GetValues([Autodesk.Revit.DB.BuiltInCategory]) | Where-Object { $m.Invoke($null,[object[]]@([long]$_)) })
$expected=@('OST_DuctCurves','OST_PipeCurves','OST_Conduit','OST_CableTray',
 'OST_DuctFitting','OST_DuctAccessory','OST_DuctTerminal','OST_MechanicalEquipment',
 'OST_PipeFitting','OST_PipeAccessory','OST_PlumbingFixtures','OST_Sprinklers',
 'OST_CableTrayFitting','OST_ConduitFitting','OST_ElectricalEquipment','OST_ElectricalFixtures',
 'OST_LightingFixtures','OST_LightingDevices','OST_DataDevices','OST_CommunicationDevices',
 'OST_FireAlarmDevices','OST_SecurityDevices','OST_TelephoneDevices','OST_NurseCallDevices','OST_GenericModel')
if ($allowed.Count -ne $expected.Count -or @($expected | Where-Object { $_ -notin @($allowed | ForEach-Object ToString) }).Count) { throw 'Unexpected allowed categories' }
$lineMethod=$a.GetType('IFCInfo.SupportedCategories').GetMethod('IsLine',[Reflection.BindingFlags]'Static,NonPublic')
$lineAllowed=@($allowed | Where-Object { $lineMethod.Invoke($null,[object[]]@([long]$_)) })
if ($lineAllowed.Count -ne 4) { throw 'Only the four native curve categories may use line builders' }
$allowed
$a.FullName
