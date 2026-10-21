# Changelog

## 11 October 2026 - English UI, project icon and repeated runs

- Embedded a text-free IFC-to-duct project icon in the DLL and applied it to window headers/icons. The reference was the project's tile in the supplied MEP icon grid.
- Localized project UI, dialogs, error/status text, exports, comments, tests and documentation to English. Source model/IFC property values and persisted tracking identifiers are retained as data.
- The command keeps the workflow active after create/update reports and cancelled model selections. It restores link/category selection, recreates rows from current model data, and does not reuse an old request. Full-coverage/created sources remain protected against duplication. The report provides Back to tool; the main-window close button ends the session.
- Revit 2023 remains the Add-in Manager test target, with a separate IFCInfo_Revit2023 assembly. Revit 2024 has a separate DLL/package; no installer is required for 2023 tests.
- Validation: 107 logic/path assertions plus IFC edge-case checks and 119 WPF checks passed. The x64 release check confirmed Revit 2023 API references, public Add-in Manager command, embedded icon, ElementId compatibility and matching staged/build DLL hash. Revit 2023 built with zero errors/warnings; Revit 2024 built with zero errors and two existing ElementId obsolete warnings. Published/package DLLs were synchronized and source diff checks passed.
- The optional Snowdon sample was not present for this final run, so its test reports SKIP unless an explicit -SampleFile is supplied. Live transaction acceptance, including repeated creation in an actual model, remains pending in ACCEPTANCE_CHECKLIST.md.

## 11 October 2026 - IFC source paths

- Resolve filename-only or stale IFC metadata beside the link, including relative paths and .ifc.RVT caches, linked-document paths and host-model folders.
- Open the file picker only if no source exists; initialize it to a source folder and suggested filename instead of the process working directory.
- Eleven path checks passed. Debug Revit 2023 and Release Revit 2024 built successfully; 2024 retained two pre-existing ElementId obsolete warnings. Published DLL and 2024 package were synchronized; no live Revit acceptance was run.

## 16 September 2026 - Selection and UI repairs

- Validate duct/system types only for checked preview rows. Requests and saved settings contain mappings only for included groups.
- Disable Delete in the preview grid; Include checkboxes preserve unchecked rows in reports. Counts and button state follow selection.
- Remember manually selected IFC paths per LinkOption during the dialog session.
- UiPreview references this repository's IFCInfo project and supports offscreen create/update regression checks.
- Updated guides, comparison notes and historical acceptance records.
- The then-present read_dxf utility accepted explicit input/output paths and verified paired coordinates; it was later archived as unrelated CAD tooling.

Historical verification: IFCInfo/UiPreview/CadInspection Release builds with no errors/warnings; reader 13, coverage 19, enhancements 25 and UI 26 (83 total). The external CADMEP file was unavailable, so seven optional checks were not rerun. Six synthetic DXF CLI checks passed. Settings/results were rendered and visually inspected at normal/compact sizes.

Live model creation/updates, fitting routing, hosts, worksharing, RVT save, rollback and Undo remained pending. Large-model performance was not measured. Building did not install a DLL into Revit.
