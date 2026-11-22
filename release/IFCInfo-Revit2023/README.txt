CREATE DUCT FROM IFC - ADD-IN MANAGER, REVIT 2023

Expanded category support: 25 categories (4 line + 21 family), including MEP equipment, air terminals, fittings/accessories, electrical/lighting and communication/safety devices, plumbing fixtures, sprinklers and Generic Models. Categories appear only when present in the IFC link. Native family placement requires a loaded matching-category family/type and its placement requirements. Duct parameter mapping remains duct-only. Step 2 now includes a searchable/sortable Category column.

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
