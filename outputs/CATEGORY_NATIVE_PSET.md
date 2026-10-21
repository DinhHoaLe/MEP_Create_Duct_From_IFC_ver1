# Native categories and IFC Pset data

Historical implementation notes: 11 September 2026. Current category enforcement is described in CATEGORY_PLACEMENT.md.

## Native placement

- Source rows can use loaded matching-category family/types. Search by family/type name. Unsupported builders show a reason and disable creation; no Generic Model substitute is created.
- Supports level-based, hosted/work-plane, two-level, line-based and adaptive families.
- Duct, pipe, conduit and cable tray placement uses readable straight centerlines/profiles. Length 0 preserves the source; a positive length in mm keeps the first endpoint fixed. Duct/pipe need an appropriate system type. General native placement does not automatically connect fittings.
- Basic walls require a uniform vertical rectangular solid and thickness matching the wall type.
- Floors support horizontal boundaries, including internal openings.
- Roofs require one horizontal boundary without openings and a loaded roof type.

## IFC properties

- Reads instance and type Pset/Qto properties: single/boolean values, lists, enumerations, bounded values, complex properties and common quantities.
- Keeps Instance/Type scopes separate when names match. Displays set/property/value and explicit units when available. Exports an IFC Psets column in CSV.
- Stores source properties using Extensible Storage. If the original IFC is unavailable, tries linked parameters with Pset/Qto prefixes. Missing data is explicitly reported.
- Does not automatically create Revit shared parameters or map every IFC dimension/property into a selected family.
- Properties reflect the IFC on disk; reload the matching link version. Values without explicit units are retained without guessing conversions.

## Placement and geometry limits

- Families normally use dimensions of the selected family/type. LocationPoint is preferred; otherwise the bounding-box center and user rotation are used.
- Hosted families use a planar face in the host model; source positions are projected onto it. Adaptive control points must be picked in order. Line-based families use a source path or request two points. Esc cancels the run.
- Curves require readable straight circular/rectangular extrusions (direct or mapped), or native LocationCurve/connectors. Flex, BRep/mesh, U/I/hollow profiles and curved paths are unsupported by the curve inference. Centerlines are not guessed from bounding boxes.
- Pipe/conduit diameters come from the source profile; catalog OD/ID/nominal conversions are not automatic.
- Walls do not support curved/sloped/opening/variable-profile geometry. Floors/roofs do not support sloped, stepped or shape-edited geometry; roofs do not support openings.
- Wall/floor/roof solids are verified after creation. Geometry/type thickness mismatches cause rollback. Volume difference threshold is 0.1%, with a small numerical floor.
- View-based/detail families and categories requiring specialized builders are not approximated as point families. Earlier broad category support described here does not override the current SupportedCategories list.

## Transactions and tracking

Default behavior rolls back the whole run after an execution failure. Keeping successful elements is optional. Source tracking prevents repeated placement. Curves are checked for centerline overlap against the same category before creation. Ducts created through general placement also receive tracking/snapshots for the specialized duct update workflow.

## Test procedure

Load the matching DLL in Add-in Manager. Choose an IFC link, source category and checked rows, then a matching family/type. Prepare compatible system-family types where relevant. Pick required host faces/points after confirmation. Review source Pset/Qto and export results. After the report, return to the source list to create more elements.

Historical verification: Release build; 20 reader, 19 coverage, 25 enhancement and 22 property checks; four category/type UI checks and three offscreen windows. The original report recorded no live native-placement acceptance. Live Revit creation remains an acceptance task; do not treat build/UI success as proof that every category can be recreated.
