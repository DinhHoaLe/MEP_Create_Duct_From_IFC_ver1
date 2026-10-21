CREATE DUCT FROM IFC - REVIT 2024

For Add-in Manager, load IFCInfo.dll from this folder and run IFCInfo.IFCInfoCommand.
The 2023 Add-in Manager version is in the separate IFCInfo-Revit2023 folder; do not mix Revit API versions.

Optional installation:
1. Close Revit 2024 and extract the entire ZIP into one folder.
2. Run Install.cmd. Reopen Revit 2024 and a project.
3. Choose Add-Ins > External Tools > IFC Info.

The installer targets the current Windows account. No Administrator rights, Visual Studio or pyRevit are required.
For updates, close Revit and rerun the installer from the new package.
If the command is missing, check the Revit version and:
%APPDATA%\Autodesk\Revit\Addins\2024\IFCInfo.addin

The tool reads loaded IFC links and creates/updates native elements. Test model changes in a project copy.
After a run, click Back to tool in the report to create more elements. Close the main window to finish.
