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
            var loadCategory = window.LoadCategory;
            window.LoadCategory = category =>
            {
                loadCategory?.Invoke(category);
                if (window.CanCreateDucts)
                    DuctExistenceChecker.Update(window, doc, link);
            };
            window.PrepareDucts = rows =>
            {
                var items = new List<DuctPlanItem>();
                var issues = new List<string>();
                var existing = DuctCreation.ExistingKeys(doc);
                foreach (var row in rows)
                {
                    try
                    {
                        Element source = link.GetLinkDocument()?.GetElement(new ElementId(long.Parse(row.ElementId)));
                        if (source == null)
                            throw new InvalidOperationException("Không tìm thấy phần tử nguồn.");
                        string key = link.UniqueId + "|" + (string.IsNullOrWhiteSpace(row.IfcGuid) ? source.UniqueId : row.IfcGuid);
                        if (existing.Contains(key))
                            throw new InvalidOperationException("Đã tạo Duct từ nguồn này trước đó.");
                        var item = DuctGeometryReader.Read(source, row.DuctSource);
                        Transform transform = link.GetTotalTransform();
                        item.Start = transform.OfPoint(item.Start);
                        item.End = transform.OfPoint(item.End);
                        item.WidthAxis = transform.OfVector(item.WidthAxis).Normalize();
                        item.Key = key;
                        item.Source = row;
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
                    types.Where(t => t.Shape == ConnectorProfileType.Rectangular).Select(t => new DuctChoice { Id = t.Id.Value, Name = t.Name }).ToList(), systems);
                dialog.Owner = window;
                return dialog.ShowDialog() == true ? dialog.Request : null;
            };
        }


    }
}
