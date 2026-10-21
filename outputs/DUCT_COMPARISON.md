# Duct comparison

Compares centerlines in host-model coordinates, dimensions with 1 mm tolerance, profile orientation and the target system type when known.

| Status | Meaning |
|---|---|
| Exact match | Full centerline coverage with matching dimensions, orientation and target system type. |
| Size mismatch | Full coverage with at least one different size/profile. |
| System mismatch | Full coverage with a different target system type. |
| Size and system mismatch | Both mismatches occur. |
| Profile rotation mismatch | Size/system checks pass but rectangular profile orientation differs. |
| System unknown | Full coverage and matching dimensions/orientation, but no target system type is known. |
| Partial overlap | Some overlap without complete coverage; no full-source size/system conclusion. |
| Not created | No overlapping centerline segment within the algorithm's scope. |
| Unknown | Source geometry or host-model centerline data is insufficient. |

Size/system mismatches take priority over rotation when several criteria differ. Tooltips list corresponding IDs, dimensions and system types.

All fully covered sources are locked against new creation. Partial overlap is rejected during preparation; the tool does not trim overlap or create the missing portion automatically. Overlap is checked again immediately before creation/update. After a run, the source list is refreshed before selecting more elements.

Target system type priority: saved Project Information mapping, then tracked source mapping on the duct, then a unique Revit type-name match to the trimmed IFC name (case insensitive). Creation-dialog mappings are persisted only when the corresponding option is enabled.

Exact match does not verify a closed network or compare IFC System Name to Revit-generated network names. Multiple segments can cover one source; an overlapping wrong-size segment still causes a mismatch. Width/height are not swapped automatically; oval is not rectangular. Opposite profile-axis signs are equivalent.

IFC updates are a separate tracked-source workflow. Only ducts with snapshots and no manual changes/connections are updated automatically; the comparison table does not automatically repair every displayed mismatch.

Logic tests: Test-DuctCoverage.ps1 and Test-DuctEnhancements.ps1. Revit acceptance must cover translated/rotated links, joined segments, size/system/rotation mismatches, partial overlap and unreadable sources.
