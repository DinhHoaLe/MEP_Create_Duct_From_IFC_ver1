using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;


namespace IFCInfo
{
    /// <summary>Đọc đường tim và tiết diện của Duct native hoặc solid IFC đã khớp kích thước.</summary>
    internal static class DuctGeometryReader
    {
        internal const double Tolerance = 0.002; // feet, khoảng 0.6 mm
        public static DuctPlanItem Read(Element source, IfcTerminalSource expected)
        {
            var native = source as MEPCurve;
            var line = (source.Location as LocationCurve)?.Curve as Line;
            if (native != null && line != null)
            {
                var connector = native.ConnectorManager.Connectors.Cast<Connector>().First(c => c.ConnectorType == ConnectorType.End);
                if (connector.Shape != ConnectorProfileType.Round && connector.Shape != ConnectorProfileType.Rectangular)
                    throw new NotSupportedException("Chưa hỗ trợ tiết diện oval.");
                return new DuctPlanItem
                {
                    Start = line.GetEndPoint(0),
                    End = line.GetEndPoint(1),
                    WidthAxis = connector.CoordinateSystem.BasisX,
                    Diameter = connector.Shape == ConnectorProfileType.Round ? connector.Radius * 2 : 0,
                    Width = connector.Shape == ConnectorProfileType.Rectangular ? connector.Width : 0,
                    Height = connector.Shape == ConnectorProfileType.Rectangular ? connector.Height : 0
                };
            }
            if (expected == null)
                throw new NotSupportedException("Cần IFC gốc khớp GUID để xác định kích thước ống.");
            if (!string.IsNullOrEmpty(expected.GeometryError))
                throw new NotSupportedException(expected.GeometryError);
            if (expected.LengthMm <= 0)
                throw new NotSupportedException("Thiếu chiều dài ống IFC.");
            var solids = Solids(source.get_Geometry(new Options { DetailLevel = ViewDetailLevel.Fine })).ToList();
            if (solids.Count != 1)
                throw new NotSupportedException("Cần một khối ống thẳng; hình học hiện có " + solids.Count + " khối.");
            Solid solid = solids[0];
            var ends = solid.Faces.Cast<Face>().OfType<PlanarFace>().Select(f => EndProfile(f)).Where(p => p != null).ToList();
            var matches = new List<DuctPlanItem>();
            double length = expected.LengthMm / 304.8;
            for (int i = 0; i < ends.Count; i++)
            for (int j = i + 1; j < ends.Count; j++)
            {
                var a = ends[i];
                var b = ends[j];
                XYZ delta = b.Center - a.Center;
                if (delta.GetLength() < Tolerance || Math.Abs(delta.GetLength() - length) > Tolerance)
                    continue;
                if (Math.Abs(a.Normal.DotProduct(b.Normal)) < 1 - 1e-7 ||
                    delta.Normalize().CrossProduct(a.Normal).GetLength() > 1e-6)
                    continue;
                bool round = expected.DiameterMm > 0;
                double width = expected.WidthMm / 304.8, height = expected.HeightMm / 304.8, diameter = expected.DiameterMm / 304.8;
                if (round != a.Round || round != b.Round)
                    continue;
                XYZ axis = a.Axis;
                if (round)
                {
                    if (!Near(a.Width, diameter) || !Near(b.Width, diameter))
                        continue;
                }
                else
                {
                    if (!SizeMatches(a, width, height) || !SizeMatches(b, width, height))
                        continue;
                    if (!Near(a.Width, width))
                        axis = a.Normal.CrossProduct(axis).Normalize();
                }
                double volume = (round ? Math.PI * diameter * diameter / 4 : width * height) * length;
                if (Math.Abs(solid.Volume - volume) > Math.Max(volume * 0.001, 1e-8))
                    continue;
                matches.Add(new DuctPlanItem
                {
                    Start = a.Center,
                    End = b.Center,
                    WidthAxis = axis,
                    Width = width,
                    Height = height,
                    Diameter = diameter
                });
            }
            if (matches.Count != 1)
                throw new NotSupportedException("Không xác định duy nhất đường tim khớp kích thước IFC (" + matches.Count + " kết quả). Hãy kiểm tra IFC và reload link.");
            return matches[0];
        }
        internal static bool Near(double a, double b) => Math.Abs(a - b) <= Tolerance;
        private static bool SizeMatches(Profile p, double w, double h) =>
            (Near(p.Width, w) && Near(p.Height, h)) || (Near(p.Width, h) && Near(p.Height, w));
        private sealed class Profile
        {
            public XYZ Center, Normal, Axis; public double Width, Height; public bool Round;
        }
        private static Profile EndProfile(PlanarFace face)
        {
            if (face.EdgeLoops.Size != 1)
                return null;
            var edges = face.EdgeLoops.get_Item(0).Cast<Edge>().Select(e => e.AsCurve()).ToList();
            var arcs = edges.OfType<Arc>().ToList();
            if (arcs.Count == edges.Count && arcs.Count > 0)
            {
                var arc = arcs[0];
                if (arcs.Any(a => !Near(a.Radius, arc.Radius) || a.Center.DistanceTo(arc.Center) > Tolerance) ||
                    Math.Abs(arcs.Sum(a => a.Length) - 2 * Math.PI * arc.Radius) > Tolerance)
                    return null;
                return new Profile
                {
                    Round = true,
                    Center = arc.Center,
                    Normal = face.FaceNormal,
                    Axis = face.XVector,
                    Width = arc.Radius * 2
                };
            }
            var lines = edges.OfType<Line>().ToList();
            if (lines.Count != 4 || edges.Count != 4)
                return null;
            XYZ axis = lines[0].Direction;
            var parallel = lines.Where(l => Math.Abs(l.Direction.DotProduct(axis)) > 1 - 1e-7).ToList();
            var perpendicular = lines.Where(l => Math.Abs(l.Direction.DotProduct(axis)) < 1e-7).ToList();
            if (parallel.Count != 2 || perpendicular.Count != 2 || !Near(parallel[0].Length, parallel[1].Length) ||
                !Near(perpendicular[0].Length, perpendicular[1].Length))
                return null;
            XYZ center = XYZ.Zero;
            foreach (var edge in lines)
                center += edge.GetEndPoint(0) + edge.GetEndPoint(1);
            double width = parallel[0].Length, height = perpendicular[0].Length;
            if (Math.Abs(face.Area - width * height) > Math.Max(1e-8, width * height * 0.001))
                return null;
            return new Profile { Center = center / 8, Normal = face.FaceNormal, Axis = axis, Width = width, Height = height };
        }
        private static IEnumerable<Solid> Solids(GeometryElement geometry)
        {
            if (geometry == null)
                yield break;
            foreach (GeometryObject obj in geometry)
            {
                var solid = obj as Solid;
                if (solid != null && solid.Volume > 1e-9)
                    yield return solid;
                var instance = obj as GeometryInstance;
                if (instance != null)
                foreach (var nested in Solids(instance.GetInstanceGeometry()))
                    yield return nested;
            }
        }

    }
}
