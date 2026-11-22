# Changelog

## 22 November 2026 - Dashboard entry for the existing workflow

- Add a warm-theme WPF dashboard with task, discipline and Line / Family Instance choices. Route into the existing Source, Elements, Parameters and creation/placement screens rather than replacing them with the HTML proposal.
- Filter populated source categories by the selected kind. Restrict Update and Transfer to Ducts; use highlighted source rows and the existing update preparation / parameter transfer services. Review retains inspection, exports and duct comparisons while disabling model-changing screens.
- Add return-to-dashboard navigation and retain the current link/review session when reopening the same choice. Clear category review state when changing task or kind. Architecture and Structure are marked planned and cannot launch.
- At the user's subsequent request, built and staged IFCInfo_Revit2023.dll/PDB for Add-in Manager (Release/net48/x64): zero warnings/errors. Fourteen dashboard WPF/dispatch checks passed outside Revit, including task/kind availability, duct-only filtering, read-only tab restrictions, dashboard return visibility and a WPF layout pass. Release API/category/hash checks passed. Live Revit transaction and visual acceptance remain pending.

## 22 November 2026 - Expanded MEP families and per-element Category column

- Expand the enabled category policy from five categories to 25: retain four native line categories and add 20 family categories alongside Duct Fittings. New HVAC, plumbing, electrical, lighting, safety/communication and Generic Model categories reuse the matching-category family loader/placement builders. Source category kind classification now uses the shared line policy; only categories actually populated in the selected link are listed.
- Add source Category ID/name to element rows and show Category in Step 2. Category participates in search, sorting and copied table text. Rows without metadata use the active category as a display fallback.
- Keep exact target/source category matching and loaded-family/level/placement-mode checks. Duct parameter mapping is marked as duct-only for native family categories and is not required before opening Placement.
- Revit 2023 source/preview builds pass without warnings/errors. 284 automated WPF checks pass, including representative new-family category filtering/missing-type handling and Step 2 category display/search/sort/copy. The x64 release check verifies all 25 category IDs and the four line builders. Source/element screens were rendered for visual review. Live placement for the new family categories remains pending in Revit.

## 22 November 2026 - Short-window layout and checked category review

- Replace the fixed-height Source browser/outer ScrollViewer with an available-height grid. Compact headers at short heights keep the link picker and footer accessible; the category table scrolls independently. Category column widths follow the viewport on resize.
- Add single-click category checkboxes and a select-all-visible checkbox per group. Checks persist across filtering and Line/Family groups. Continue passes every checked category to a Step 2 Review category picker, retaining element checks and mapping drafts separately while freshly reading/comparing each active category. Each creation operation still uses the active category's supported builder.
- Retain the review queue and active category/check/mapping state across native-placement/cancelled-pick returns within the same command session. Clear state when the IFC link changes. Existing duplicate and manual-edit protections remain in the creation services.
- Revit 2023 builds pass with zero warnings/errors; 266 automated checks pass, including actual 540 px window layout bounds, cross-group checks, per-category state and recreated review sessions. Offscreen layouts were rendered at normal and short sizes. Live Revit creation, placement, resizing under the user's DPI and transaction acceptance remain pending.

## 22 November 2026 - Category-first source UI and compact parameter menus

- Correct the Source page to list categories, rather than individual elements. Line/Family category tabs contain only category names and linked-element counts, with name filtering and column sorting. Selection is single-category; Continue loads its individual elements in Step 2 without preselecting them. Remove the superseded element-inventory model and the per-element source-system enumeration.
- Replace twelve visible Parameters toolbar buttons with a Refresh icon and four dropdown groups: Parameters, CSV, Presets and Values. All actions remain available with hover descriptions; existing-duct parameter transfer is retained under Values.
- Revit 2023 source and preview builds pass with zero warnings/errors. 259 automated checks pass, including category counts/filtering/sorting/selection/routing, restored sessions and retained parameter actions. Both revised screens were rendered and visually inspected. Live Revit acceptance remains pending.

## 22 November 2026 - Source browser and parameter productivity tools

- Selecting an IFC link now immediately lists supported line elements and placed-family sources in separate tabs. Search, category filtering, numeric ID/header sorting, single-click checks and visible-row selection feed a single-category batch into the existing detailed reader/comparison workflow. Hidden checked rows are retained; mixed categories are rejected. The existing five-category creation scope is unchanged.
- Add CSV export from selected IFC property catalogs with qualified names and inferred supported types, portable XML mapping presets, a bulk prefix/collision resolver, read-only current/proposed parameter previews and an explicit Update existing ducts action for highlighted source rows.
- Existing-duct transfer remains one transaction without geometry creation. It now rejects conflicting shared Type values and refreshes the tool revision baseline only for targets that matched their saved baseline before the transfer, retaining pre-existing manual-edit warnings.
- Value preview reports missing source values, unresolved length units, missing/read-only/ambiguous targets and shared Type conflicts. New ducts have no current tracked value. Presets store names/settings, not model IDs or values; loading does not create definitions. Bulk definitions still use the chosen common group/binding.
- Validation: Revit 2023 source/preview builds passed with zero errors/warnings; 256 automated WPF/logic/CSV/preset checks passed. Offscreen source/parameter screens were rendered and inspected. Callback tests do not execute Revit model transactions; live source classification, parameter writes, rollback and Undo acceptance remain pending. The Revit 2023 Add-in Manager package was updated.

## 21 November 2026 - Bulk IFC parameters and CSV definitions

- Add Create multiple from IFC with editable target names, inferred supported data types, Include/Select all/Clear selection, shared group and Instance/Type binding, and per-row results. Compatible existing parameters are reused; conflicting names/types/bindings and unknown IFC types are reported. Each parameter has its own transaction, so successful creations remain after another row fails.
- Add Import parameter CSV and Export CSV template. UTF-8 comma-separated IFCProperty/ParameterName/DataType records create parameters and optional mappings; blank IFCProperty allows parameter-only creation with an explicit supported type. Parsing handles BOM, quoted commas and escaped quotes and rejects malformed records/headers. No family loading is included.
- Fix Follow IFC Text inference using the Revit data-kind catalog and explicitly include Text, Integer, Number, Yes/No and Length specs. This avoids relying on a textual match between the word Text and a Forge type ID.
- Revit 2023 source/preview builds: zero warnings/errors; 238 automated WPF/CSV checks passed, including simulated creation/mapping callbacks, real-format Text spec IDs, CSV reuse, duplicate/source/type rejection and optional parameter handling. The preview was rendered and visually inspected. These checks do not execute transactions inside Revit; live acceptance remains pending.

## 21 November 2026 - Optional parameter mapping and bulk property selection

- Follow-up fix: bulk-added properties with no Revit target are drafts and no longer produce repeated validation errors or block Next/Create. Only complete pairs enter the creation request. A single summary reports skipped drafts; invalid complete pairs remain validated. Revit 2023 builds pass with zero warnings/errors and 221 WPF checks pass, including mixed complete/draft pairs and actual creation callbacks with unfinished rows. Live Revit transactions remain pending.

- Allow the default blank parameter row or an empty mapping table to continue to duct settings and creation. Incomplete configured mappings still require correction or removal; optional mapping does not bypass validation of entered pairs.
- Add Select all, Clear selection and a selection count to Add IFC parameters. Previously added properties remain excluded and empty selections cannot be submitted.
- Revit 2023 source/preview builds passed with zero errors and warnings; 218 offscreen WPF checks passed, including continuation and creation callbacks without mappings, incomplete-pair rejection and bulk property selection. The Revit 2023 Add-in Manager DLL was updated. Live model transaction acceptance remains pending.

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
