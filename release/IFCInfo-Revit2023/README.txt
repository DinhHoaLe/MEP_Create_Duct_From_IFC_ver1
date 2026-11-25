CREATE DUCT FROM IFC - ADD-IN MANAGER, REVIT 2023

Latest main-tab/service update: All four line categories use Source > Elements > Parameters > Preview & Create > Review & Create as five MAIN tabs. Final review is not a nested tab; Create is available only there. Cable Tray Auto has an explicit Follow IFC / Use shared service selector and resolved service text in each right-hand group. Missing source service leaves the native default unchanged. Manual choices are retained across mode changes. Build: zero warnings/errors; 371 WPF checks pass outside Revit. Live Revit acceptance remains pending.

Latest confirmation changes: In Pipe/CT/Conduit preview, finish Settings, then open Review & Create. The final tab contains source and target type/system/service/level information plus the parameter mapping count. Create is available only there; unresolved choices block it. Auto fields explicitly show Follow IFC -> matched result, or a no-match message. Follow IFC does not silently select an unrelated sole type/system. Choose a shared project type/system or use Manual if the source name cannot be matched. Build: zero warnings/errors; 365 WPF checks passed outside Revit. Live model acceptance remains pending.

Latest UI/service changes: Pipe and CT use a Duct-style narrow sidebar (Manual/Auto, shared type and system/service, level, error policy) with per-source groups on the right. Manual choices are restored after Auto. Duct source-level mappings are now on the left. Cable Tray has Service Type fields, written to its native RBS_CTC_SERVICE_TYPE parameter; it has no native System Type. Blank service leaves the native default unchanged. Missing Pipe/CT types and piping systems must already be loaded/created in the host project; Auto only matches existing types. Build: zero warnings/errors; 355 WPF checks pass outside Revit. Live grouped placement and service writes remain pending.

Latest 24 November 2026: Create and Update/Transfer parameter values for tracked Ducts, Pipes, Cable Trays and Conduits. For existing targets, highlight source rows in Elements, configure Parameters, then Update parameter values (or Values > Update existing elements). Non-duct Update changes parameters only; duct geometry updates retain the existing protections. Targets must have been created/tracked by this tool; duplicate/missing targets are reported. Parameter updates preserve geometry and system assignments.

Creation follows Source > Elements > Parameters > Preview & Create. Pipe, CT and Conduit preview is now embedded in that tab, with Back returning to Parameters. Pipe requires a piping System Type; CT and Conduit do not. Source Refresh is a normal button at the top right. Auto and Manual show only their respective level controls. Successful parameter creation removes unused blank placeholder rows. Build: zero warnings/errors; 337 WPF checks passed outside Revit. Live model writes, binding changes and rollback remain unverified in Revit.

Expanded category support: 25 categories (4 line + 21 family), including MEP equipment, air terminals, fittings/accessories, electrical/lighting and communication/safety devices, plumbing fixtures, sprinklers and Generic Models. Categories appear only when present in the IFC link. Native family placement requires a loaded matching-category family/type and its placement requirements. Parameter mapping supports the four line categories. Step 2 includes a searchable/sortable Category column.

22 November 2026: select a link to browse Line categories and Family categories. Tick one or more categories, then Continue. Step 2 provides a Review category picker and retains element checks and parameter mapping drafts separately for each category. Each creation operation acts on the active category. The short-window Source layout keeps link selection and Continue outside the independently scrolling category list. Parameters tools are grouped under Parameters, CSV, Presets and Values, plus a Refresh icon, with hover descriptions. Parameter mapping remains optional for duct creation. Live Revit acceptance is still required.

1. Open a project in Revit 2023.
2. In Add-in Manager, load IFCInfo_Revit2023.dll from this folder.
3. Select IFCInfo.IFCInfoCommand and run it. The new dashboard opens first.
4. Select a task, MEP, and Line or Family Instance, then Open workspace. Architecture and Structure are marked Planned. Update and Transfer currently support ducts only; use Line for these tasks. Select a loaded IFC link and category. The original IFC is located beside the link when possible.
5. Check source elements, configure duct settings and create.
6. In the results report, click Back to tool to create another batch. Use Back to change the link/category or Dashboard to switch tasks/kinds. For Update/Transfer, highlight source rows in Elements (Ctrl/Shift). Review disables model-changing screens. Close the main window to end the session.

Follow project system types (Step 3): enable this option to use existing HVAC system types for all groups. IFC type names are matched without case sensitivity; existing explicit mappings are retained when no name matches. Select an existing type for unmatched groups. Disable the option to restore the previous per-group creation settings. This option maps System Type; IFC System Name remains source metadata in Comments.

No Install.cmd or .addin manifest is required. The PDB is optional debugging information, not a DLL to load.
If Revit has retained an old DLL, close/reopen Revit before loading the new version.
Built for net48/x64 with RevitAPI/RevitAPIUI 2023. Live transaction acceptance remains pending.

Dashboard build: 22 November 2026. Release build: zero warnings/errors. Fourteen dashboard WPF/dispatch checks passed outside Revit. Release reflection/hash checks passed. These checks do not execute transactions inside Revit.
