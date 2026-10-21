# MEP_Create_Duct_From_IFC_ver1

C# add-in for Revit 2023/2024, .NET Framework 4.8, x64. Reads linked IFC models and creates or updates native MEP elements in the host model.

## Test with Add-in Manager in Revit 2023

Load `release/IFCInfo-Revit2023/IFCInfo_Revit2023.dll` and run `IFCInfo.IFCInfoCommand`. No installer or .addin manifest is required. If Revit has retained a previous DLL, restart Revit before loading the new build.

Select a loaded IFC link and a source category, then check the elements to create. The tool finds the original IFC beside the link when possible. After a create/update run, click **Back to tool** in the report to return to the refreshed source list. Use **Back** to select another link or category. Close the main window to end the session.

```powershell
dotnet build outputs/IFCInfo.csproj -c Release -p:RevitVersion=2023 -p:AssemblyName=IFCInfo_Revit2023
```

Copy the DLL/PDB from `outputs/bin/Release/net48/` into `release/IFCInfo-Revit2023/`. The separate assembly name preserves the Revit 2024 DLL in `outputs/IFCInfo.dll`.

## Revit 2024 package

`pwsh -NoProfile -File tools/Package-RevitAddin.ps1` builds and creates `release/IFCInfo-Revit2024.zip`. Add-in Manager can load the DLL directly. The optional installer in this package installs for the current Windows account without Administrator privileges or pyRevit; see `README.txt` in the package.

## Repository layout

- `outputs/IFCInfo.csproj` and `outputs/src/`: add-in project and source code.
- `outputs/assets/`: project artwork embedded in the DLL; no external image file is needed at runtime.
- `outputs/IFCInfo.dll`: published Revit 2024 DLL.
- `outputs/tests/`: IFC reader, paths, properties, duplicate detection, snapshots, connections and UI checks.
- `tools/UiPreview/`: WPF preview and regression runner, without model transactions.
- `tools/revit-installer/`: optional Revit 2024 installer source.
- `Revit/`: local IFC/RVT fixtures, excluded from Git.
- `.cleanup-archive/`: reversible cleanup archive, excluded from Git.

See [user guide](outputs/USER_GUIDE.txt), [IFC compatibility](outputs/IFC_COMPATIBILITY.md), [duct comparison](outputs/DUCT_COMPARISON.md), [acceptance checklist](outputs/ACCEPTANCE_CHECKLIST.md) and [changelog](outputs/CHANGELOG.md).

## Validation

Run from the repository root with PowerShell 7:

```powershell
pwsh -NoProfile -File outputs/tests/Test-IfcReader.ps1
pwsh -NoProfile -File outputs/tests/Test-IfcSourcePath.ps1
pwsh -NoProfile -File outputs/tests/Test-IfcReaderEdgeCases.ps1
pwsh -NoProfile -File outputs/tests/Test-IfcSample.ps1
pwsh -NoProfile -File outputs/tests/Test-IfcProperties.ps1
pwsh -NoProfile -File outputs/tests/Test-IfcSelectionCsv.ps1
pwsh -NoProfile -File outputs/tests/Test-DuctCoverage.ps1
pwsh -NoProfile -File outputs/tests/Test-DuctEnhancements.ps1
pwsh -NoProfile -File outputs/tests/Test-DuctUi.ps1
```

Build requires the matching Revit API and .NET Framework 4.8 Developer Pack. The default project/preview target is Revit 2023. Override `RevitVersion` and `RevitApiDir` for another installation. DLL reflection checks require x64 PowerShell. Creation, updates and connector operations modify the model; offscreen tests do not replace acceptance tests in Revit.

## Versioned builds and IFC classification (21 October 2026)

- Run tools/Build-RevitVersion.ps1 -RevitVersion 2023, 2024, 2025, 2026 or 2027. Supply -RevitApiDir for an external matching API installation.
- 2025 was built against API 25.4.60.9 with net8.0-windows. The installed 2026 API is 26.5.0.55 and references .NET 10; its package targets net10.0-windows and is not for earlier .NET 8 installations. Use a matching older API and -TargetFramework net8.0-windows to build for those installations.
- 2027 targets net10.0-windows but no API 2027 is available locally. No Revit 2027 DLL has been built or verified.
- 136 WPF checks passed on .NET Framework 4.8, .NET 8 and .NET 10 during this change. Live Revit transactions remain unverified. Modern API builds retain two dependency-resolution warnings.
- Duct Settings now places the source table above Level & General Options in the right sidebar with a horizontal divider. Tabs share the beige theme.
- New system classification follows explicit IFC classification properties when recognized. Name-based suggestions (including ABLUFT -> ExhaustAir) are labeled as suggestions and can be overridden. Unknown or conflicting classifications require manual input. Existing project-only mapping remains a separate option.
