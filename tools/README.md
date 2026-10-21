# Development tools

## IFC / Duct UI preview

```powershell
dotnet build tools/UiPreview/UiPreview.csproj -c Release
./tools/UiPreview/bin/Release/net48/UiPreview.exe outputs/qa
./tools/UiPreview/bin/Release/net48/UiPreview.exe --test
```

Uses this repository's IFCInfo project. Requires Windows, .NET Framework 4.8 and RevitAPI.dll 2023 by default. No model is opened or modified. `--test` runs offscreen WPF checks and closes automatically. Autodesk libraries copied into the preview's bin folder are for local testing only; do not ship them in the add-in package.

## Build for Add-in Manager 2023

```powershell
dotnet build outputs/IFCInfo.csproj -c Release -p:RevitVersion=2023 -p:AssemblyName=IFCInfo_Revit2023
```

Load the resulting IFCInfo_Revit2023.dll in Add-in Manager and run IFCInfo.IFCInfoCommand.

## Optional Revit 2024 packaging

Run `pwsh -NoProfile -File tools/Package-RevitAddin.ps1`. Installer source is in `tools/revit-installer/`; no installation is needed for Add-in Manager tests.
