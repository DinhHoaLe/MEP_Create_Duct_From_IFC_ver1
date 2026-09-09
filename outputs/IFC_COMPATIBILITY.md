# IFC reader compatibility

Supports STEP files declaring IFC2X3 or IFC4. This is a focused duct reader,
not a general implementation of every geometry representation in either schema.

- Reads direct duct occurrences and ducts classified through IfcDuctSegmentType.
- Reads rectangular/circular IfcExtrudedAreaSolid, directly or through nested
  IfcMappedItem / IfcRepresentationMap.
- Applies mapping origin rotation, solid/profile rotation, uniform/nonuniform
  mapping scale when calculating dimensions. Translation does not affect sizes;
  endpoints and orientation still come from the Revit link geometry.
- Reads IfcSystem and IFC4 IfcDistributionSystem/IfcDistributionCircuit membership.
- Rejects skew sections, circular sections scaled into ellipses, cyclic mappings,
  multiple body solids, unsupported profiles and unsupported geometry such as
  BRep/tessellation/Boolean results. Fittings and flex ducts are not reconstructed.
- IFC4X1/IFC4X3 are not included in this change.

Validation: 20 reader assertions passed, including all six straight ducts in
CADMEP_HVAC.ifc from AutoCAD MEP 2023 (75 x 75 mm; lengths 9083, 18299, 30156,
39297, 58667, 97983 mm). IFC4 tests use synthetic reader fixtures, not a real
IFC4 export or a Revit import test. The five fittings are excluded.

Run in PowerShell 7 from the workspace:

```powershell
./outputs/tests/Test-IfcReader.ps1 -CadMepFile 'path/to/CADMEP_HVAC.ifc'
```

Revit validation remains necessary: replace the installed DLL while Revit is
closed, restart Revit 2024, reload the IFC link as needed, and run the duct tool.
Check ready/skipped counts, dimensions, endpoints and section orientation.
No changes to the original IFC are needed.
