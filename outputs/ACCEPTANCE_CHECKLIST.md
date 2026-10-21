# Seven-step implementation and Revit acceptance

Historical baseline: 10 September 2026. Current changes are in CHANGELOG.md and usage in USER_GUIDE.txt. Code exists for all seven areas. The historical baseline reported 64 automatic checks and two offscreen WPF windows; live model acceptance was not performed.

| Area | Implemented behavior | Remaining Revit acceptance |
|---|---|---|
| 1. Duplicate detection | Centerline coverage, dimensions, mapped system type and profile orientation; recheck overlap before create/update. | Native ducts, actual systems and transformed links. |
| 2. Model navigation | Zoom source, select corresponding ducts, separate 3D review view with section box. | Linked-element selection, zoom and correct view bounds. |
| 3. Saved mappings | Profile duct types; profile/system groups; source levels; worksets; Project Information storage. | Save/reopen RVT, deleted types/levels and workshared models. |
| 4. Reports | Source to target IDs, statuses/reasons, UTF-8 CSV, rollback/fitting failures, current/original run selection. | IDs match committed elements; CSV matches the table. |
| 5. Error handling | Per-operation transactions within a TransactionGroup; full rollback by default, optional keep-successful; report unprocessed rows. | Creation/fitting failures, both policies and Undo. |
| 6. IFC updates | Source/actual snapshots, before/after geometry/system preview, selected-row updates, manual-edit checks. | Reloaded IFC updates preserve IDs where possible; manual edits are protected. |
| 7. Fittings/connectors | Elbow/transition/tee and compatible direct connections; optional host terminals; Routing Preferences; unambiguous gaps up to the selected limit. | Real routing/families, failed and ambiguous connections. |

## Current repeated-run workflow

Load the Revit 2023 DLL in Add-in Manager and select link/category. Check sources, open settings, review mappings/options and create. Export the report when needed, then click Back to tool. The same link/category is restored and comparisons are recalculated from the current model. Check another source or use Back to change the source/category. Close the main window to end the session.

## Acceptance limits

- Exact match means centerline coverage and matching dimensions/orientation/target system type, not a closed network. Revit-generated system names do not replace IFC System Name. Several segments can cover one source.
- Partial overlaps are rejected; overlap is not trimmed automatically.
- Geometric corresponding-duct selection can omit moved ducts; updates use tracked source identity separately.
- Host isolation cannot isolate just one element inside a link. The review view isolates the link and crops around the source; nearby linked elements may remain visible.
- Automatic levels are below the lower endpoint. Missing source levels form a separate group. Workset options apply only to workshared models. Deleted type/level IDs require a current choice.
- Updates use link UniqueId plus IFC GUID. Require snapshots, no manual edits, no pinning and no network connections. Changed/deleted GUIDs and replacement link instances require manual review.
- Manual-edit tracking covers centerline/profile, duct type, system type, level, workset, Comments, pinning and connections; it does not track every custom parameter or insulation.
- Fittings are off by default. Tees require three meeting ends within 1 mm and do not split a duct. Elbow/transition gaps may trim/extend endpoints; IFC fitting geometry is not reconstructed.
- Only successful ducts in the current run and optionally matching host-model terminals are connected. Linked connectors and old networks are not repaired automatically. Source System Names and target system types must match.
- Missing routing/families, ambiguous nodes and Revit rejections are reported. Fitting failures follow the selected rollback policy.
- Failed final snapshot saving in keep-successful mode is reported and may cause a later manual-change warning rather than overwriting the duct.

## Live Revit checklist - pending

- [ ] Correct source, changed width/system/profile rotation and partial centerline coverage.
- [ ] Rotated/translated link; source zoom; multiple covering ducts; correct 3D section box.
- [ ] Save/close/reopen RVT; retained mappings; worksets and deleted levels.
- [ ] Report IDs/CSV/current and original run selection.
- [ ] Mid-run failure, both failure policies, fitting errors and Undo.
- [ ] Changed IFC reloaded into the same link; before/after preview; update target ID behavior.
- [ ] Manual duct edits are detected and preserved.
- [ ] Elbow/transition/tee routing; missing family; excessive gap; ambiguous node; terminal connection.
- [ ] Create one batch, return to the tool, create another batch; created sources are no longer selectable.
- [ ] Back changes the link/category; cancelling settings does not create anything; closing the main window ends the session.

Historical tests: Test-IfcReader (20 including optional CADMEP), Test-DuctCoverage (19), Test-DuctEnhancements (25), offscreen Render-DuctWindows and successful Release build. Current counts/results are in CHANGELOG.md. No live transaction acceptance or installation is implied.
