# Source category determines the target category

Native placement lists only family/types whose category ID matches the source element in the Revit IFC link. Examples: Duct Fittings to Duct Fittings, Air Terminals to Air Terminals, Walls to Walls. Display names can vary with the Revit language; comparison uses IDs.

The target category is fixed. If a type is missing, load a matching .rfa from the placement dialog. Loading a family in another category is rolled back. System families such as duct, pipe and wall use their corresponding native types; not every category can be loaded from an .rfa file.

Before placement, the tool verifies the target type category and each source element category. Unsupported categories or categories without suitable types are not converted into another category. Flex Ducts do not use the straight-duct workflow.

This rule uses the category assigned in the Revit IFC link. It does not infer categories from element names or reclassify IFC imported as Generic Models. Category matching does not automatically choose an exact family by shape or dimensions.

Build and UI render checks verify the code/UI. Family loading and live placement still require Revit acceptance.
