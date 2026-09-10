using System;
using System.Linq;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace IFCInfo
{
    internal static class DuctNavigation
    {
        internal static void Execute(UIDocument ui, RevitLinkInstance link, AirTerminalRow row, string action)
        {
            var source = link.GetLinkDocument()?.GetElement(new ElementId(long.Parse(row.ElementId)));
            if (source == null) throw new InvalidOperationException("Không tìm thấy nguồn IFC.");
            var ids = row.CorrespondingDuctIds.Select(id => new ElementId(id)).Where(id => ui.Document.GetElement(id) != null).ToList();
            if (action == "Chọn duct tương ứng")
            {
                if (ids.Count == 0) throw new InvalidOperationException("Chưa tìm thấy duct tương ứng.");
                ui.Selection.SetElementIds(ids); ui.ShowElements(ids); return;
            }
            if (action == "Cô lập trong 3D")
            {
                View3D view;
                using (var t = new Transaction(ui.Document, "IFC - Inspection 3D"))
                {
                    t.Start();
                    var family = new FilteredElementCollector(ui.Document).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                        .First(v => v.ViewFamily == ViewFamily.ThreeDimensional);
                    view = View3D.CreateIsometric(ui.Document, family.Id);
                    view.Name = "IFC kiểm tra " + DateTime.Now.ToString("yyyyMMdd HHmmss") + " " + view.Id.Value;
                    var bb = source.get_BoundingBox(null);
                    if (bb == null) throw new InvalidOperationException("Nguồn không có bounding box.");
                    var transform = link.GetTotalTransform().Multiply(bb.Transform);
                    var points = new List<XYZ>();
                    foreach (var x in new[] { bb.Min.X, bb.Max.X })
                    foreach (var y in new[] { bb.Min.Y, bb.Max.Y })
                    foreach (var z in new[] { bb.Min.Z, bb.Max.Z }) points.Add(transform.OfPoint(new XYZ(x,y,z)));
                    var pad = new XYZ(1,1,1);
                    view.SetSectionBox(new BoundingBoxXYZ { Min = new XYZ(points.Min(p=>p.X),points.Min(p=>p.Y),points.Min(p=>p.Z))-pad,
                        Max = new XYZ(points.Max(p=>p.X),points.Max(p=>p.Y),points.Max(p=>p.Z))+pad });
                    view.IsSectionBoxActive = true;
                    ids.Add(link.Id); view.IsolateElementsTemporary(ids);
                    if (t.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Không tạo được view kiểm tra.");
                }
                ui.ActiveView = view;
            }
            ui.Selection.SetReferences(new List<Reference> { new Reference(source).CreateLinkReference(link) });
            var box = source.get_BoundingBox(null);
            if (box != null)
            {
                var tr = link.GetTotalTransform().Multiply(box.Transform);
                var points = new List<XYZ>();
                foreach (var x in new[] { box.Min.X,box.Max.X })
                foreach (var y in new[] { box.Min.Y,box.Max.Y })
                foreach (var z in new[] { box.Min.Z,box.Max.Z }) points.Add(tr.OfPoint(new XYZ(x,y,z)));
                ui.GetOpenUIViews().FirstOrDefault(v=>v.ViewId == ui.ActiveView.Id)?.ZoomAndCenterRectangle(
                    new XYZ(points.Min(p=>p.X),points.Min(p=>p.Y),points.Min(p=>p.Z))-new XYZ(1,1,1),
                    new XYZ(points.Max(p=>p.X),points.Max(p=>p.Y),points.Max(p=>p.Z))+new XYZ(1,1,1));
            }
        }
    }
}
