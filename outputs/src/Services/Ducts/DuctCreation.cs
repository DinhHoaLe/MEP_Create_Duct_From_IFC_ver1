using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;


namespace IFCInfo
{
    /// <summary>Tạo Duct trong transaction, kiểm tra kết quả và lưu dấu nguồn chống trùng.</summary>
    internal static class DuctCreation
    {
        private static readonly Guid TrackingId = new Guid("e01d5292-927b-4692-915f-0ca747bc2b83");
        private const double Tolerance = 0.002; // feet, approximately 0.6 mm
        internal static HashSet<string> ExistingKeys(Document doc)
        {
            var result = new HashSet<string>();
            Schema schema = Schema.Lookup(TrackingId);
            if (schema == null)
                return result;
            foreach (Element e in new FilteredElementCollector(doc).OfClass(typeof(Duct)))
            {
                var entity = e.GetEntity(schema);
                if (entity.IsValid())
                    result.Add(entity.Get<string>(schema.GetField("SourceKey")));
            }
            return result;
        }
        public static void Execute(UIDocument uiDoc, IFCInfoWindow window)
        {
            var request = window.DuctCreationRequest;
            if (request == null)
                return;
            Document doc = uiDoc.Document;
            var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.ProjectElevation).ToList();
            if (levels.Count == 0)
                throw new InvalidOperationException("Model chính chưa có Level.");
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
                        if (!existing.Add(item.Key))
                            continue;
                        var typeId = new ElementId(item.Round ? request.RoundTypeId : request.RectangularTypeId);
                        var systemId = new ElementId(request.SystemTypes[DuctRequest.SystemKey(item.Round, item.Source.SystemType)]);
                        double z = Math.Min(item.Start.Z, item.End.Z);
                        Level level = levels.LastOrDefault(l => l.ProjectElevation <= z + Tolerance) ?? levels[0];
                        Duct duct = Duct.Create(doc, systemId, typeId, level.Id, item.Start, item.End);
                        if (item.Round)
                            SetSize(duct, BuiltInParameter.RBS_CURVE_DIAMETER_PARAM, item.Diameter);
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
                        DuctExistenceChecker.RecordMapping(duct, item.Key, item.Source.SystemType, systemId.Value);
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
            var result = new DuctCreationResultWindow(created.Count);
            new System.Windows.Interop.WindowInteropHelper(result).Owner = uiDoc.Application.MainWindowHandle;
            result.ShowDialog();
        }
        private static void SetSize(Duct duct, BuiltInParameter parameter, double value)
        {
            Parameter p = duct.get_Parameter(parameter);
            if (p == null || p.IsReadOnly || !p.Set(value) || !DuctGeometryReader.Near(p.AsDouble(), value))
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

