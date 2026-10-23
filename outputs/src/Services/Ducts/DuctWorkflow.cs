using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;


namespace IFCInfo
{
    /// <summary>Connect the UI to validation and duct creation planning.</summary>
    internal static class DuctWorkflow
    {
        public static void Configure(IFCInfoWindow window, Document doc, RevitLinkInstance link)
        {
            window.CheckParameterTarget=name=>DuctParameterMapping.Check(doc,name);
            window.LoadParameterTargets=()=>DuctParameterMapping.Targets(doc);
            window.TransferParameters=(rows,maps)=>DuctParameterMapping.Transfer(doc,link,rows,maps);
            var loadCategory = window.LoadCategory;
            window.LoadCategory = category =>
            {
                loadCategory?.Invoke(category);
                if (window.CanCreateDucts)
                    DuctExistenceChecker.Update(window, doc, link);
            };
            Func<List<AirTerminalRow>, bool, DuctRequest> prepare = (rows, updating) =>
            {
                DuctCreationWindow dialog;
                using (LoadingWindow.ShowWhile(window, "Preparing duct groups", "Checking centerlines, profiles and Revit system types…"))
                {
                var items = new List<DuctPlanItem>();
                var issues = new List<string>();
                var existing = DuctCreation.ExistingKeys(doc);
                var tracked = DuctCreation.TrackedDucts(doc);
                foreach (var row in rows.Where(row => updating || row.CanSelect))
                {
                    try
                    {
                        Element source = link.GetLinkDocument()?.GetElement(ElementIds.Create(long.Parse(row.ElementId)));
                        if (source == null)
                            throw new InvalidOperationException("Source element not found.");
                        if (source.Category?.Id.Number() != (long)BuiltInCategory.OST_DuctCurves)
                            throw new InvalidOperationException("Ducts can only be created from the IFC Ducts category.");
                        string key = link.UniqueId + "|" + (string.IsNullOrWhiteSpace(row.IfcGuid) ? source.UniqueId : row.IfcGuid);
                        if (!updating && existing.Contains(key))
                            throw new InvalidOperationException("A duct has already been created from this source.");
                        if (!updating && row.DuctExistence == "Partial overlap")
                            throw new InvalidOperationException("Partial overlap: resolve the overlap before creation to avoid duplicate ducts.");
                        if (updating && !tracked.ContainsKey(key)) continue;
                        var item = DuctGeometryReader.Read(source, row.DuctSource);
                        Transform transform = link.GetTotalTransform();
                        item.Start = transform.OfPoint(item.Start);
                        item.End = transform.OfPoint(item.End);
                        item.WidthAxis = transform.OfVector(item.WidthAxis).Normalize();
                        item.Key = key;
                        item.Source = row;
                        item.LevelKey = (source.Document.GetElement(source.LevelId) as Level)?.Name ?? "No source level";
                        if (updating)
                        {
                            var targets = tracked[key];
                            if (targets.Count != 1) throw new InvalidOperationException("Multiple ducts share the source GUID; automatic update is disabled.");
                            var target = targets[0];
                            if (DuctRevision.Get(target, "Actual") == null)
                                throw new InvalidOperationException("The existing duct has no baseline for detecting manual edits; update skipped.");
                            if (!DuctRevision.Equal(DuctRevision.Get(target, "Actual"), DuctRevision.Actual(target)))
                                throw new InvalidOperationException("Duct ID " + target.Id.Number() + " was edited in Revit; preserved for manual review.");
                            if (DuctRevision.Equal(DuctRevision.Get(target, "Source"), DuctRevision.Source(item))) continue;
                            if (DuctRevision.Connected(target))
                                throw new InvalidOperationException("Duct ID " + target.Id.Number() + " is connected; resolve connections before updating.");
                            item.ExistingId = target.Id.Number();
                            item.ChangeSummary = DuctSnapshotComparison.Describe(DuctRevision.Get(target,"Source"),DuctRevision.Source(item));
                        }
                        if (item.Start.DistanceTo(item.End) < Math.Max(doc.Application.ShortCurveTolerance, 1.0 / 120))
                            throw new InvalidOperationException("The segment is too short to create a duct.");
                        items.Add(item);
                    }
                    catch (Exception ex) { issues.Add(row.ElementId + ": " + ex.Message); }
                }
                var types = new FilteredElementCollector(doc).OfClass(typeof(DuctType)).Cast<DuctType>().ToList();
                var systems = new FilteredElementCollector(doc).OfClass(typeof(MechanicalSystemType)).Cast<MechanicalSystemType>()
                    .OrderBy(t => t.Name).Select(t => new DuctChoice { Id = t.Id.Number(), Name = t.Name, Classification=t.SystemClassification.ToString() }).ToList();
                dialog = new DuctCreationWindow(items, issues,
                    types.Where(t => t.Shape == ConnectorProfileType.Round).Select(t => new DuctChoice { Id = t.Id.Number(), Name = t.Name }).ToList(),
                    types.Where(t => t.Shape == ConnectorProfileType.Rectangular).Select(t => new DuctChoice { Id = t.Id.Number(), Name = t.Name }).ToList(), systems,
                    new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l=>l.ProjectElevation)
                        .Select(l=>new DuctChoice { Id=l.Id.Number(), Name=l.Name }).ToList(),
                    doc.IsWorkshared ? new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset)
                        .Select(w=>new DuctChoice { Id=w.Id.IntegerValue, Name=w.Name }).ToList() : new List<DuctChoice>(),
                    DuctProjectSettings.Load(doc), updating,
                    types.Where(t => t.Shape == ConnectorProfileType.Oval).Select(t => new DuctChoice { Id = t.Id.Number(), Name = t.Name }).ToList());
                }
                var systemSnapshot=ProjectSystemBrowser.Read(doc);
                dialog.BrowseSystems=owner=>ProjectSystemBrowser.Show(owner,systemSnapshot);
                window.OpenDuctSettings(dialog,request=>
                {
                    foreach(var skipped in request.Skipped)
                        if(string.IsNullOrEmpty(skipped.IfcGuid)) skipped.IfcGuid=rows.FirstOrDefault(r=>r.ElementId==skipped.SourceId)?.IfcGuid;
                });
                return null;
            };
            window.PrepareDucts = rows => prepare(rows, false);
            window.PrepareUpdates = rows => prepare(rows, true);
        }


    }
}


