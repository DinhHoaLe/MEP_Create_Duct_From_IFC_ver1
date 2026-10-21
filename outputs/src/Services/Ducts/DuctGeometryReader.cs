using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;


namespace IFCInfo
{
    /// <summary>Read centerlines and profiles from native ducts or IFC solids with matched dimensions.</summary>
    internal static class DuctGeometryReader
    {
        internal const double Tolerance = 0.002; // feet, approximately 0.6 mm
        public static DuctPlanItem Read(Element source, IfcTerminalSource expected)
        {
            var native = source as MEPCurve;
            var line = (source.Location as LocationCurve)?.Curve as Line;
            if (native != null && line != null)
            {
                var connector = native.ConnectorManager.Connectors.Cast<Connector>().First(c => c.ConnectorType == ConnectorType.End);
                if (connector.Shape != ConnectorProfileType.Round && connector.Shape != ConnectorProfileType.Rectangular && connector.Shape != ConnectorProfileType.Oval)
                    throw new NotSupportedException("Could not determine the source duct profile.");
                return new DuctPlanItem
                {
                    Start = line.GetEndPoint(0),
                    End = line.GetEndPoint(1),
                    WidthAxis = connector.CoordinateSystem.BasisX,
                    Diameter = connector.Shape == ConnectorProfileType.Round ? connector.Radius * 2 : 0,
                    Oval = connector.Shape == ConnectorProfileType.Oval,
                    Width = connector.Shape != ConnectorProfileType.Round ? connector.Width : 0,
                    Height = connector.Shape != ConnectorProfileType.Round ? connector.Height : 0
                };
            }
            if (expected == null)
                throw new NotSupportedException("A matching GUID in the original IFC is required to determine duct dimensions.");
            if (!string.IsNullOrEmpty(expected.GeometryError))
                throw new NotSupportedException(expected.GeometryError);
            if (expected.LengthMm <= 0)
                throw new NotSupportedException("IFC duct length is missing.");
            var solids = Solids(source.get_Geometry(new Options { DetailLevel = ViewDetailLevel.Fine })).ToList();
            if (solids.Count != 1)
                throw new NotSupportedException("A single straight duct solid is required; the geometry contains " + solids.Count + " solids.");
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
                bool oval = expected.ProfileShape == "Oval";
                if (round != a.Round || round != b.Round || oval != a.Oval || oval != b.Oval)
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
                double minor = Math.Min(width,height), major = Math.Max(width,height);
                double volume = (round ? Math.PI * diameter * diameter / 4 : oval ? (major-minor)*minor+Math.PI*minor*minor/4 : width * height) * length;
                if (Math.Abs(solid.Volume - volume) > Math.Max(volume * 0.001, 1e-8))
                    continue;
                matches.Add(new DuctPlanItem
                {
                    Start = a.Center,
                    End = b.Center,
                    WidthAxis = axis,
                    Width = width,
                    Height = height,
                    Diameter = diameter,
                    Oval = oval
                });
            }
            if (matches.Count != 1)
                throw new NotSupportedException("Could not find a unique centerline matching IFC dimensions (" + matches.Count + " matches). Check the IFC and reload the link.");
            return matches[0];
        }
        internal static bool Near(double a, double b) => Math.Abs(a - b) <= Tolerance;
        private static bool SizeMatches(Profile p, double w, double h) =>
            (Near(p.Width, w) && Near(p.Height, h)) || (Near(p.Width, h) && Near(p.Height, w));
        private sealed class Profile
        {
            public XYZ Center, Normal, Axis; public double Width, Height; public bool Round, Oval;
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
            if (arcs.Count == 2 && lines.Count == 2 && edges.Count == 4)
            {
                var a=arcs[0]; var b=arcs[1];
                double separation=a.Center.DistanceTo(b.Center), minor=2*a.Radius;
                if (separation<=Tolerance || !Near(a.Radius,b.Radius) ||
                    !Near(a.Length,Math.PI*a.Radius) || !Near(b.Length,Math.PI*b.Radius)) return null;
                XYZ majorAxis=(b.Center-a.Center).Normalize();
                if (lines.Any(l=>!Near(l.Length,separation) || Math.Abs(l.Direction.DotProduct(majorAxis))<1-1e-7)) return null;
                double area=separation*minor+Math.PI*a.Radius*a.Radius;
                if (Math.Abs(face.Area-area)>Math.Max(1e-8,area*.001)) return null;
                return new Profile { Oval=true, Center=(a.Center+b.Center)/2, Normal=face.FaceNormal, Axis=majorAxis, Width=separation+minor, Height=minor };
            }
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
