using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;

namespace IFCInfo
{
    public sealed class DuctPlanItem
    {
        public AirTerminalRow Source;
        public XYZ Start, End, WidthAxis;
        public double Width, Height, Diameter;
        public string Key;
        public bool Round => Diameter > 0;
    }
    public sealed class DuctChoice
    {
        public long Id;
        public string Name;
        public override string ToString() => Name;
    }
    public sealed class DuctRequest
    {
        public List<DuctPlanItem> Items;
        public long RoundTypeId, RectangularTypeId;
        public Dictionary<string, long> SystemTypes = new Dictionary<string, long>();
    }

    internal static class DuctCreation
    {
        private static readonly Guid TrackingId = new Guid("e01d5292-927b-4692-915f-0ca747bc2b83");
        private const double Tolerance = 0.002; // feet, approximately 0.6 mm
        public static void Configure(IFCInfoWindow window, Document doc, RevitLinkInstance link)
        {
            window.PrepareDucts = rows =>
            {
                var items = new List<DuctPlanItem>();
                var issues = new List<string>();
                var existing = ExistingKeys(doc);
                foreach (var row in rows)
                {
                    try
                    {
                        Element source = link.GetLinkDocument()?.GetElement(new ElementId(long.Parse(row.ElementId)));
                        if (source == null) throw new InvalidOperationException("Không tìm thấy phần tử nguồn.");
                        string key = link.UniqueId + "|" + (string.IsNullOrWhiteSpace(row.IfcGuid) ? source.UniqueId : row.IfcGuid);
                        if (existing.Contains(key)) throw new InvalidOperationException("Đã tạo Duct từ nguồn này trước đó.");
                        var item = ReadGeometry(source, row.DuctSource);
                        Transform transform = link.GetTotalTransform();
                        item.Start = transform.OfPoint(item.Start);
                        item.End = transform.OfPoint(item.End);
                        item.WidthAxis = transform.OfVector(item.WidthAxis).Normalize();
                        item.Key = key; item.Source = row;
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

        private static DuctPlanItem ReadGeometry(Element source, IfcTerminalSource expected)
        {
            var native = source as Duct;
            var line = (source.Location as LocationCurve)?.Curve as Line;
            if (native != null && line != null)
            {
                var connector = native.ConnectorManager.Connectors.Cast<Connector>().First(c => c.ConnectorType == ConnectorType.End);
                if (connector.Shape != ConnectorProfileType.Round && connector.Shape != ConnectorProfileType.Rectangular)
                    throw new NotSupportedException("Chưa hỗ trợ Duct oval.");
                return new DuctPlanItem { Start = line.GetEndPoint(0), End = line.GetEndPoint(1),
                    WidthAxis = connector.CoordinateSystem.BasisX,
                    Diameter = connector.Shape == ConnectorProfileType.Round ? connector.Radius * 2 : 0,
                    Width = connector.Shape == ConnectorProfileType.Rectangular ? connector.Width : 0,
                    Height = connector.Shape == ConnectorProfileType.Rectangular ? connector.Height : 0 };
            }
            if (expected == null) throw new NotSupportedException("Cần IFC gốc khớp GUID để xác định kích thước ống.");
            if (!string.IsNullOrEmpty(expected.GeometryError)) throw new NotSupportedException(expected.GeometryError);
            if (expected.LengthMm <= 0) throw new NotSupportedException("Thiếu chiều dài ống IFC.");
            var solids = Solids(source.get_Geometry(new Options { DetailLevel = ViewDetailLevel.Fine })).ToList();
            if (solids.Count != 1) throw new NotSupportedException("Cần một khối ống thẳng; hình học hiện có " + solids.Count + " khối.");
            Solid solid = solids[0];
            var ends = solid.Faces.Cast<Face>().OfType<PlanarFace>().Select(f => EndProfile(f)).Where(p => p != null).ToList();
            var matches = new List<DuctPlanItem>();
            double length = expected.LengthMm / 304.8;
            for (int i = 0; i < ends.Count; i++) for (int j = i + 1; j < ends.Count; j++)
            {
                var a = ends[i]; var b = ends[j];
                XYZ delta = b.Center - a.Center;
                if (delta.GetLength() < Tolerance || Math.Abs(delta.GetLength() - length) > Tolerance) continue;
                if (Math.Abs(a.Normal.DotProduct(b.Normal)) < 1 - 1e-7 ||
                    delta.Normalize().CrossProduct(a.Normal).GetLength() > 1e-6) continue;
                bool round = expected.DiameterMm > 0;
                double width = expected.WidthMm / 304.8, height = expected.HeightMm / 304.8, diameter = expected.DiameterMm / 304.8;
                if (round != a.Round || round != b.Round) continue;
                XYZ axis = a.Axis;
                if (round)
                {
                    if (!Near(a.Width, diameter) || !Near(b.Width, diameter)) continue;
                }
                else
                {
                    if (!SizeMatches(a, width, height) || !SizeMatches(b, width, height)) continue;
                    if (!Near(a.Width, width)) axis = a.Normal.CrossProduct(axis).Normalize();
                }
                double volume = (round ? Math.PI * diameter * diameter / 4 : width * height) * length;
                if (Math.Abs(solid.Volume - volume) > Math.Max(volume * 0.001, 1e-8)) continue;
                matches.Add(new DuctPlanItem { Start = a.Center, End = b.Center, WidthAxis = axis,
                    Width = width, Height = height, Diameter = diameter });
            }
            if (matches.Count != 1) throw new NotSupportedException("Không xác định duy nhất đường tim khớp kích thước IFC (" + matches.Count + " kết quả). Hãy kiểm tra IFC và reload link.");
            return matches[0];
        }
        private static bool Near(double a, double b) => Math.Abs(a - b) <= Tolerance;
        private static bool SizeMatches(Profile p, double w, double h) =>
            (Near(p.Width, w) && Near(p.Height, h)) || (Near(p.Width, h) && Near(p.Height, w));
        private sealed class Profile { public XYZ Center, Normal, Axis; public double Width, Height; public bool Round; }
        private static Profile EndProfile(PlanarFace face)
        {
            if (face.EdgeLoops.Size != 1) return null;
            var edges = face.EdgeLoops.get_Item(0).Cast<Edge>().Select(e => e.AsCurve()).ToList();
            var arcs = edges.OfType<Arc>().ToList();
            if (arcs.Count == edges.Count && arcs.Count > 0)
            {
                var arc = arcs[0];
                if (arcs.Any(a => !Near(a.Radius, arc.Radius) || a.Center.DistanceTo(arc.Center) > Tolerance) ||
                    Math.Abs(arcs.Sum(a => a.Length) - 2 * Math.PI * arc.Radius) > Tolerance) return null;
                return new Profile { Round = true, Center = arc.Center, Normal = face.FaceNormal,
                    Axis = face.XVector, Width = arc.Radius * 2 };
            }
            var lines = edges.OfType<Line>().ToList();
            if (lines.Count != 4 || edges.Count != 4) return null;
            XYZ axis = lines[0].Direction;
            var parallel = lines.Where(l => Math.Abs(l.Direction.DotProduct(axis)) > 1 - 1e-7).ToList();
            var perpendicular = lines.Where(l => Math.Abs(l.Direction.DotProduct(axis)) < 1e-7).ToList();
            if (parallel.Count != 2 || perpendicular.Count != 2 || !Near(parallel[0].Length, parallel[1].Length) ||
                !Near(perpendicular[0].Length, perpendicular[1].Length)) return null;
            XYZ center = XYZ.Zero;
            foreach (var edge in lines) center += edge.GetEndPoint(0) + edge.GetEndPoint(1);
            double width = parallel[0].Length, height = perpendicular[0].Length;
            if (Math.Abs(face.Area - width * height) > Math.Max(1e-8, width * height * 0.001)) return null;
            return new Profile { Center = center / 8, Normal = face.FaceNormal, Axis = axis, Width = width, Height = height };
        }
        private static IEnumerable<Solid> Solids(GeometryElement geometry)
        {
            if (geometry == null) yield break;
            foreach (GeometryObject obj in geometry)
            {
                var solid = obj as Solid;
                if (solid != null && solid.Volume > 1e-9) yield return solid;
                var instance = obj as GeometryInstance;
                if (instance != null) foreach (var nested in Solids(instance.GetInstanceGeometry())) yield return nested;
            }
        }
        private static HashSet<string> ExistingKeys(Document doc)
        {
            var result = new HashSet<string>();
            Schema schema = Schema.Lookup(TrackingId);
            if (schema == null) return result;
            foreach (Element e in new FilteredElementCollector(doc).OfClass(typeof(Duct)))
            {
                var entity = e.GetEntity(schema);
                if (entity.IsValid()) result.Add(entity.Get<string>(schema.GetField("SourceKey")));
            }
            return result;
        }
        public static void Execute(UIDocument uiDoc, IFCInfoWindow window)
        {
            var request = window.DuctCreationRequest;
            if (request == null) return;
            Document doc = uiDoc.Document;
            var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.ProjectElevation).ToList();
            if (levels.Count == 0) throw new InvalidOperationException("Model chính chưa có Level.");
            var existing = ExistingKeys(doc);
            var created = new List<ElementId>();
            using (var transaction = new Transaction(doc, "IFC - Create Ducts"))
            {
                transaction.Start();
                transaction.SetFailureHandlingOptions(transaction.GetFailureHandlingOptions()
                    .SetFailuresPreprocessor(new RollbackErrors()).SetClearAfterRollback(true));
                try
                {
                    Schema schema = Schema.Lookup(TrackingId);
                    if (schema == null)
                    {
                        var builder = new SchemaBuilder(TrackingId);
                        builder.SetSchemaName("IFCInfoDuctSource");
                        builder.AddSimpleField("SourceKey", typeof(string));
                        builder.AddSimpleField("SystemName", typeof(string));
                        builder.AddSimpleField("SystemType", typeof(string));
                        schema = builder.Finish();
                    }
                    foreach (var item in request.Items)
                    {
                        if (!existing.Add(item.Key)) continue;
                        var typeId = new ElementId(item.Round ? request.RoundTypeId : request.RectangularTypeId);
                        var systemId = new ElementId(request.SystemTypes[item.Source.SystemType ?? ""]);
                        double z = Math.Min(item.Start.Z, item.End.Z);
                        Level level = levels.LastOrDefault(l => l.ProjectElevation <= z + Tolerance) ?? levels[0];
                        Duct duct = Duct.Create(doc, systemId, typeId, level.Id, item.Start, item.End);
                        if (item.Round) SetSize(duct, BuiltInParameter.RBS_CURVE_DIAMETER_PARAM, item.Diameter);
                        else
                        {
                            SetSize(duct, BuiltInParameter.RBS_CURVE_WIDTH_PARAM, item.Width);
                            SetSize(duct, BuiltInParameter.RBS_CURVE_HEIGHT_PARAM, item.Height);
                            doc.Regenerate();
                            var connector = duct.ConnectorManager.Connectors.Cast<Connector>().First(c => c.ConnectorType == ConnectorType.End);
                            XYZ axis = (item.End - item.Start).Normalize();
                            XYZ current = connector.CoordinateSystem.BasisX;
                            double angle = Math.Atan2(axis.DotProduct(current.CrossProduct(item.WidthAxis)), current.DotProduct(item.WidthAxis));
                            ElementTransformUtils.RotateElement(doc, duct.Id, Line.CreateUnbound(item.Start, axis), angle);
                        }
                        doc.Regenerate();
                        var actualLine = (duct.Location as LocationCurve)?.Curve as Line;
                        if (actualLine == null || !((actualLine.GetEndPoint(0).DistanceTo(item.Start) < Tolerance &&
                            actualLine.GetEndPoint(1).DistanceTo(item.End) < Tolerance) ||
                            (actualLine.GetEndPoint(1).DistanceTo(item.Start) < Tolerance && actualLine.GetEndPoint(0).DistanceTo(item.End) < Tolerance)))
                            throw new InvalidOperationException("Đường tim Duct mới không khớp nguồn " + item.Source.ElementId);
                        if (!item.Round)
                        {
                            var actualConnector = duct.ConnectorManager.Connectors.Cast<Connector>().First(c => c.ConnectorType == ConnectorType.End);
                            if (Math.Abs(actualConnector.CoordinateSystem.BasisX.DotProduct(item.WidthAxis)) < 1 - 1e-6)
                                throw new InvalidOperationException("Góc tiết diện Duct không khớp nguồn " + item.Source.ElementId);
                        }
                        var tracking = new Entity(schema);
                        tracking.Set(schema.GetField("SourceKey"), item.Key);
                        tracking.Set(schema.GetField("SystemName"), item.Source.SystemName ?? "");
                        tracking.Set(schema.GetField("SystemType"), item.Source.SystemType ?? "");
                        duct.SetEntity(tracking);
                        Parameter comments = duct.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                        if (comments != null && !comments.IsReadOnly)
                            comments.Set("IFC GUID: " + item.Source.IfcGuid + " | System Name: " + item.Source.SystemName +
                                " | System Type: " + item.Source.SystemType);
                        created.Add(duct.Id);
                    }
                    if (transaction.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("Revit đã hủy lượt tạo Duct vì lỗi model.");
                }
                catch { if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack(); throw; }
            }
            uiDoc.Selection.SetElementIds(created);
            TaskDialog.Show("Tạo Duct", "Đã tạo " + created.Count + " đoạn ống trong model chính.\n" +
                "Giữ nguyên IFC link. Có thể Undo lượt tạo.\nChưa tạo fitting hoặc nối mạng. System Name nguồn được lưu trong Comments; Revit quản lý tên hệ thống thực tế.");
        }
        private static void SetSize(Duct duct, BuiltInParameter parameter, double value)
        {
            Parameter p = duct.get_Parameter(parameter);
            if (p == null || p.IsReadOnly || !p.Set(value) || !Near(p.AsDouble(), value))
                throw new InvalidOperationException("Không đặt được kích thước Duct theo IFC.");
        }
        private sealed class RollbackErrors : IFailuresPreprocessor
        {
            public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor) =>
                accessor.GetFailureMessages().Any(m => m.GetSeverity() == FailureSeverity.Error)
                ? FailureProcessingResult.ProceedWithRollBack : FailureProcessingResult.Continue;
        }
    }
}
