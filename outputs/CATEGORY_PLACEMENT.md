# Source category determines the target category

Native placement lists only family/types whose category ID matches the source element in the Revit IFC link. Examples: Duct Fittings to Duct Fittings, Air Terminals to Air Terminals, Mechanical Equipment to Mechanical Equipment. Display names can vary with the Revit language; comparison uses IDs.

## Supported categories - 22 November 2026

- Line (4): Ducts, Pipes, Cable Trays, Conduits.
- HVAC families: Duct Fittings, Duct Accessories, Air Terminals, Mechanical Equipment.
- Plumbing families: Pipe Fittings, Pipe Accessories, Plumbing Fixtures, Sprinklers.
- Electrical routing families: Cable Tray Fittings, Conduit Fittings.
- Electrical/lighting families: Electrical Equipment, Electrical Fixtures, Lighting Fixtures, Lighting Devices.
- Device families: Data Devices, Communication Devices, Fire Alarm Devices, Security Devices, Telephone Devices, Nurse Call Devices.
- Generic Models: only a loaded matching-category family; no reclassification into a different category.

There are 25 supported categories (4 line + 21 family). The Source list displays only categories that actually contain elements in the selected IFC link. Annotation/view-specific categories, walls/floors/roofs and flex curves remain outside this enabled category list. Available target family placement modes are checked separately; unsupported modes cannot be submitted.

The target category is fixed. If a type is missing, load a matching .rfa from the placement dialog. Loading a family in another category is rolled back. System families such as duct and pipe use their corresponding native types; not every category can be loaded from an .rfa file.

Before placement, the tool verifies the target type category and each source element category. Unsupported categories or categories without suitable types are not converted into another category. Flex Ducts do not use the straight-duct workflow.

This rule uses the category assigned in the Revit IFC link. It does not infer categories from element names or reclassify IFC imported as Generic Models. Category matching does not automatically choose an exact family by shape or dimensions.

Build and UI render checks verify the code/UI. Family loading and live placement still require Revit acceptance.
