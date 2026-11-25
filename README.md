# MEP_Create_Duct_From_IFC_ver1

### Service Type danh sách đã fill và Pipe slope — 25/11/2026

Cable Tray Manual chọn trực tiếp Service Type đã có từ model/IFC; Enter new… hiện ô nhập phía dưới, Keep native default là lựa chọn riêng. Manual không còn mục Follow IFC/Manual value. Auto hiển thị Service Type/Type/Level đã resolve trong selector bị khóa; đổi về Manual giữ giá trị cũ. Pipe Elements có cột Slope (%) từ đường tâm: ống đứng hiện Vertical, không đọc được hiện Unavailable. Release Revit2023 build 0 lỗi/cảnh báo, 91 kiểm tra PASS và MCP chỉ đọc 10 Pipe thật. Chi tiết: outputs/LEVEL_CABLE_VERIFICATION.md. Hướng dẫn cũ bên dưới là lịch sử trước lần cập nhật này.


### UI đồng bộ Level và Cable Tray — 25/11/2026

Level nay nằm ngay trong mỗi thẻ nhóm của Duct/Pipe/Cable Tray/Conduit, sau Type, cùng kiểu icon/tiêu đề/hint/selector. Auto hiển thị Follow IFC đã resolve; Manual cho chọn Follow IFC hoặc project level. Các thẻ Duct dùng chung source level giữ mapping đồng bộ. Service Type và ô nhập Manual của Cable Tray cùng thiết kế với các field khác. Build 0 lỗi/cảnh báo, 72 kiểm tra headless PASS. Ảnh Auto/Manual xem `outputs/verification/ui-level-cable/`.


## Level / Cable Tray corrections — 25/11/2026

- Pipe, Cable Tray and Conduit: Auto offers **Follow IFC — match name / elevation** or a shared project level. Manual offers the same Follow IFC choice or an explicit target for each source-level group. Requests retain a separate level ID per source; IFC centerlines stay in their transformed host positions.
- Follow IFC uses a unique source level name, then transformed source-level elevation. If source level data is unavailable it uses the floor at/below the source geometry center in host coordinates. Missing/ambiguous matches require a manual choice; no silent first-level assignment and no automatic level creation.
- Cable Tray Service Type has an explicit Follow IFC / Manual value selector in Manual as well as Follow IFC / shared service in Auto. Missing IFC service remains visibly reported and preserves the native default.
- Curve types resolve from the actual IFC type relationship (family/type), not the instance name with its numeric ID. Missing or ambiguous project type matches remain unresolved.
- Fixed the sample cable tray's heightwise extrusion: a unique declared longitudinal length and world vertical section axis identify its 304.8 × 101.6 mm cross-section. Vertical/ambiguous or unsupported geometry does not receive a guessed section orientation. Two source Conduits imported under Cable Trays are reported and blocked from rectangular tray creation.

Load **`release/IFCInfo-Revit2023/IFCInfo_Revit2023.dll`**, command **`IFCInfo.IFCInfoCommand`**, via Add-In Manager → Manual. Restart Revit if the previous DLL is retained in that session. This is the IFC creation project, separate from MEP_Compare_Project_ver1.

Verification: Release build 0 warnings/errors; focused WPF checks run with `UiPreview.exe --test-curves` without showing windows; IFC regression fixtures and read-only MCP geometry checks passed. Native creation/export was **not executed** on the live model. See `outputs/LEVEL_CABLE_VERIFICATION.md` for exact results and the remaining user acceptance step.


C# add-in for Revit 2023/2024, .NET Framework 4.8, x64. Reads linked IFC models and creates or updates native MEP elements in the host model.

## Test with Add-in Manager in Revit 2023

The source opens a dashboard before the workspace. Choose a task, MEP and either Line or Family Instance. All four line categories follow five main tabs: Source → Elements → Parameters → Preview & Create → Review & Create. Final confirmation is a sibling main tab, not a nested tab. Only final review exposes Create; target/mapping validation remains active. Cable Tray Service Type has explicit Follow IFC / Use shared service choices in Auto mode and displays each resolved service on the right. Missing source service is reported and leaves the native default unchanged. Cable Tray and Conduit do not require System Type. Create and map parameters for all four lines, including bulk IFC/CSV definitions. Update/Transfer supports tracked line parameters; highlight source rows in Elements, configure mappings and click Update parameter values. Duct geometry updates retain existing revision checks; other line updates change parameters only. Source has a normal Refresh button at its top right. Use Dashboard to switch tasks. Architecture and Structure remain planned.

The Revit 2023 release DLL includes the dashboard. Its Release/net48/x64 build completed with zero warnings/errors; 14 dashboard WPF/dispatch checks passed outside Revit, and the release check confirmed API 2023 references and matching built/staged DLL hashes. Live Revit acceptance remains pending.

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
