param([string]$RevitApiDir = 'C:\Program Files\Autodesk\Revit 2023')
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '../../tools/UiPreview/UiPreview.csproj'
& dotnet build $project -c Release --nologo -v minimal "-p:RevitApiDir=$RevitApiDir"
if ($LASTEXITCODE -ne 0) { throw 'UiPreview build failed.' }
$runner = Join-Path $PSScriptRoot '../../tools/UiPreview/bin/Release/net48/UiPreview.exe'
& $runner --test
if ($LASTEXITCODE -ne 0) { throw 'WPF regression checks failed.' }
