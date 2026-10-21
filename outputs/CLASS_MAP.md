# Class map - IFCInfo, Revit 2023/2024

Open IFCInfo.csproj. Source files are under src/; top-level classes normally have matching filenames. The IFCInfo namespace and command identity remain stable for Add-in Manager.

## Main workflow

1. Commands/IFCInfoCommand.cs opens the tool, executes accepted requests and resumes the refreshed source list after each run.
2. Services/IfcLinkCatalog.cs finds IFC links; IfcSourcePathResolver.cs resolves the original IFC.
3. Services/CategoryReader.cs reads categories, systems and properties and matches IFC data by GUID.
4. UI/IFCInfoWindow.cs presents source selection, element review and request submission.
5. Services/Ducts/DuctWorkflow.cs prepares selected ducts and update previews.
6. DuctExistenceChecker.cs and DuctCoverage.cs compare host ducts and detect overlap.
7. DuctGeometryReader.cs reads centerlines/profiles; DuctCreation.cs manages creation/update transactions.
8. NativePlacement.cs and NativeBuildingPlacement.cs place supported native categories.
9. DuctConnections.cs connects compatible duct ends using Routing Preferences.

## UI and supporting services

- UiDesign.cs: shared window chrome and embedded text-free project icon.
- DuctCreationWindow.cs / DuctGroupEditor.cs: profile/system group mappings and options.
- CurveCreationWindow.cs: native pipe/conduit/cable tray settings.
- NativePlacementWindow.cs: matching-category family/type selection and loading.
- DuctCreationResultWindow.cs: run report, export, selection and return to the tool.
- IfcPropertiesPanel.cs / IfcObjectViewer.cs: property review and geometry preview.
- IfcSelectionCsv.cs / IfcSelectionExport.cs: checked-source exports.
- IfcPropertyStorage.cs: IFC property storage on placed elements.
- DuctProjectSettings.cs / DuctRevision.cs / DuctSnapshotComparison.cs: saved mappings, tracking and revision checks.
- Ifc/IfcSourceReader.cs: focused STEP IFC2X3/IFC4 reader.

## Models

| Model | Purpose |
|---|---|
| LinkOption | IFC link ID, display name, source path and load state. |
| CategoryOption | Source category selection. |
| AirTerminalRow | Shared MEP source row; legacy name retained for compatibility. |
| IfcTerminalSource | IFC source data keyed by GUID. |
| DuctPlanItem | Planned centerline/profile in host coordinates. |
| DuctChoice | Duct/system type or level/workset option. |
| DuctRequest | Checked items and mappings for one create/update run. |
| DuctRunRow | Per-operation report and target IDs. |
| ReplacementTypeOption / ReplacementLevelOption | Native placement choices. |
| ReplacementRequest | Confirmed native placement request. |

## Invariants

- Creation geometry tolerance: 0.002 feet (approximately 0.6 mm).
- Comparison tolerance: 1 mm; see DUCT_COMPARISON.md.
- Configure CategoryReader before DuctWorkflow, which wraps its category-loading callback.
- Keep manually resolved IFC paths on the session's LinkOption objects.
- Rebuild rows after model changes; previous selections and creation requests are not reused.
- DuctCreationWindow validates and saves only Include=true groups. DuctPlanItem emits PropertyChanged for Include.
- Tracking GUIDs, Extensible Storage field names and model identities remain unchanged when localizing UI text.
- Compile src/**/*.cs and embed assets/create-duct-from-ifc.png. Old prototypes are in the ignored cleanup archive.

Historical samples contained 1,084 ducts (1,053 readable sizes), 509 terminals and 148 systems. These figures are sample-specific. Current verification is in CHANGELOG.md; live transaction acceptance remains pending.
