using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;


namespace IFCInfo
{
    /// <summary>Đối chiếu ống nguồn với Duct trong model chính và cập nhật trạng thái từng dòng.</summary>
    internal static class DuctExistenceChecker
    {
        private static double[] Coordinates(XYZ p) => new[] { p.X, p.Y, p.Z };
        public static void Update(IFCInfoWindow window, Document doc, RevitLinkInstance link)
        {
            var ducts = new List<DuctCoverage.Segment>();
            bool incomplete = false;
            foreach (Duct duct in new FilteredElementCollector(doc).OfClass(typeof(Duct)))
            {
                var line = (duct.Location as LocationCurve)?.Curve as Line;
                if (line == null)
                {
                    incomplete = true;
                    continue;
                }
                ducts.Add(new DuctCoverage.Segment
                {
                    Id = duct.Id.Value,
                    Start = Coordinates(line.GetEndPoint(0)),
                    End = Coordinates(line.GetEndPoint(1))
                });
            }
            Transform transform = link.GetTotalTransform();
            foreach (var row in window.AirTerminals)
            {
                try
                {
                    Element source = link.GetLinkDocument()?.GetElement(new ElementId(long.Parse(row.ElementId)));
                    if (source == null)
                        throw new InvalidOperationException("Không tìm thấy ống nguồn.");
                    var geometry = DuctGeometryReader.Read(source, row.DuctSource);
                    var result = DuctCoverage.Check(Coordinates(transform.OfPoint(geometry.Start)),
                        Coordinates(transform.OfPoint(geometry.End)), ducts, 1.0 / 304.8);
                    row.DuctExistence = incomplete && result.Status != "Đã tồn tại" ? "Chưa xác định" : result.Status;
                    row.DuctExistenceDetail = "Đối chiếu đường tim trong model chính, sai số 1 mm. Không kiểm tra kích thước hoặc hệ thống." +
                        (result.Ids.Count == 0 ? "" : " Duct ID: " + string.Join(", ", result.Ids)) +
                        (incomplete ? " Có ống trong model chính chưa đọc được đường tim." : "");
                }
                catch (Exception ex)
                {
                    row.DuctExistence = "Chưa xác định";
                    row.DuctExistenceDetail = ex.Message;
                }
            }
        }


    }
}
