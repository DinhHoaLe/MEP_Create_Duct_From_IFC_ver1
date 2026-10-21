CREATE DUCT FROM IFC - ADD-IN MANAGER, REVIT 2023

1. Open a project in Revit 2023.
2. In Add-in Manager, load IFCInfo_Revit2023.dll from this folder.
3. Select IFCInfo.IFCInfoCommand and run it.
4. Select a loaded IFC link and category. The original IFC is located beside the link when possible.
5. Check source elements, configure duct settings and create.
6. In the results report, click Back to tool to create another batch. Use Back to change the link/category. Close the main window to end the session.

Follow project system types (Step 3): enable this option to use existing HVAC system types for all groups. IFC type names are matched without case sensitivity; existing explicit mappings are retained when no name matches. Select an existing type for unmatched groups. Disable the option to restore the previous per-group creation settings. This option maps System Type; IFC System Name remains source metadata in Comments.

No Install.cmd or .addin manifest is required. The PDB is optional debugging information, not a DLL to load.
If Revit has retained an old DLL, close/reopen Revit before loading the new version.
Built for net48/x64 with RevitAPI/RevitAPIUI 2023. Live transaction acceptance remains pending.
