using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;


namespace IFCInfo
{
    /// <summary>Nối giao diện với bước kiểm tra và chuẩn bị danh sách ống cần tạo.</summary>
    internal static class DuctWorkflow
    {
        public static void Configure(IFCInfoWindow window, Document doc, RevitLinkInstance link)
        {
            window.SavedDuctRuns = new FilteredElementCollector(doc).OfClass(typeof(Duct)).Cast<Duct>()
                .Select(d=>new { Duct=d,Run=DuctRevision.Get(d,"Run") }).Where(x=>!string.IsNullOrEmpty(x.Run))
                .GroupBy(x=>x.Run).ToDictionary(g=>g.Key,g=>g.Select(x=>x.Duct.Id.Value).ToList());
            var loadCategory = window.LoadCategory;
            window.LoadCategory = category =>
            {
                loadCategory?.Invoke(category);
                if (window.CanCreateDucts)
                    DuctExistenceChecker.Update(window, doc, link);
            };
            Func<List<AirTerminalRow>, bool, DuctRequest> prepare = (rows, updating) =>
            {
                var items = new List<DuctPlanItem>();
                var issues = new List<string>();
                var existing = DuctCreation.ExistingKeys(doc);
                var tracked = DuctCreation.TrackedDucts(doc);
                foreach (var row in rows.Where(row => updating || row.CanSelect))
                {
                    try
                    {
                        Element source = link.GetLinkDocument()?.GetElement(new ElementId(long.Parse(row.ElementId)));
                        if (source == null)
                            throw new InvalidOperationException("Không tìm thấy phần tử nguồn.");
                        string key = link.UniqueId + "|" + (string.IsNullOrWhiteSpace(row.IfcGuid) ? source.UniqueId : row.IfcGuid);
                        if (!updating && existing.Contains(key))
                            throw new InvalidOperationException("Đã tạo Duct từ nguồn này trước đó.");
                        if (!updating && row.DuctExistence == "Trùng một phần")
                            throw new InvalidOperationException("Trùng một phần: cần xử lý phần chồng trước khi tạo để tránh ống trùng.");
                        if (updating && !tracked.ContainsKey(key)) continue;
                        var item = DuctGeometryReader.Read(source, row.DuctSource);
                        Transform transform = link.GetTotalTransform();
                        item.Start = transform.OfPoint(item.Start);
                        item.End = transform.OfPoint(item.End);
                        item.WidthAxis = transform.OfVector(item.WidthAxis).Normalize();
                        item.Key = key;
                        item.Source = row;
                        item.LevelKey = (source.Document.GetElement(source.LevelId) as Level)?.Name ?? "Không có Level nguồn";
                        if (updating)
                        {
                            var targets = tracked[key];
                            if (targets.Count != 1) throw new InvalidOperationException("Có nhiều duct cùng GUID nguồn; không cập nhật tự động.");
                            var target = targets[0];
                            if (DuctRevision.Get(target, "Actual") == null)
                                throw new InvalidOperationException("Duct cũ chưa có trạng thái gốc để kiểm tra sửa tay; bỏ qua cập nhật.");
                            if (!DuctRevision.Equal(DuctRevision.Get(target, "Actual"), DuctRevision.Actual(target)))
                                throw new InvalidOperationException("Duct ID " + target.Id.Value + " đã sửa trong Revit; giữ nguyên để kiểm tra thủ công.");
                            if (DuctRevision.Equal(DuctRevision.Get(target, "Source"), DuctRevision.Source(item))) continue;
                            if (DuctRevision.Connected(target))
                                throw new InvalidOperationException("Duct ID " + target.Id.Value + " đang nối mạng; cần xử lý kết nối trước khi cập nhật.");
                            item.ExistingId = target.Id.Value;
                            item.ChangeSummary = DuctSnapshotComparison.Describe(DuctRevision.Get(target,"Source"),DuctRevision.Source(item));
                        }
                        if (item.Start.DistanceTo(item.End) < Math.Max(doc.Application.ShortCurveTolerance, 1.0 / 120))
                            throw new InvalidOperationException("Đoạn quá ngắn để tạo Duct.");
                        items.Add(item);
                    }
                    catch (Exception ex) { issues.Add(row.ElementId + ": " + ex.Message); }
                }
                var types = new FilteredElementCollector(doc).OfClass(typeof(DuctType)).Cast<DuctType>().ToList();
                var systems = new FilteredElementCollector(doc).OfClass(typeof(MechanicalSystemType)).Cast<MechanicalSystemType>()
                    .OrderBy(t => t.Name).Select(t => new DuctChoice { Id = t.Id.Value, Name = t.Name }).ToList();
                var dialog = new DuctCreationWindow(items, issues,
                    types.Where(t => t.Shape == ConnectorProfileType.Round).Select(t => new DuctChoice { Id = t.Id.Value, Name = t.Name }).ToList(),
                    types.Where(t => t.Shape == ConnectorProfileType.Rectangular).Select(t => new DuctChoice { Id = t.Id.Value, Name = t.Name }).ToList(), systems,
                    new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l=>l.ProjectElevation)
                        .Select(l=>new DuctChoice { Id=l.Id.Value, Name=l.Name }).ToList(),
                    doc.IsWorkshared ? new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset)
                        .Select(w=>new DuctChoice { Id=w.Id.IntegerValue, Name=w.Name }).ToList() : new List<DuctChoice>(),
                    DuctProjectSettings.Load(doc), updating);
                dialog.Owner = window;
                if (dialog.ShowDialog()!=true) return null;
                foreach (var skipped in dialog.Request.Skipped)
                    if (string.IsNullOrEmpty(skipped.IfcGuid)) skipped.IfcGuid=rows.FirstOrDefault(r=>r.ElementId==skipped.SourceId)?.IfcGuid;
                return dialog.Request;
            };
            window.PrepareDucts = rows => prepare(rows, false);
            window.PrepareUpdates = rows => prepare(rows, true);
        }


    }
}

