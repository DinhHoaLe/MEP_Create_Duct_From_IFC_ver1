using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace IFCInfo
{
    internal static class AirTerminalReplacement
    {
        private static readonly Guid TrackingId = new Guid("40333d70-0718-419c-8588-c342475082c6");

        public static void Configure(IFCInfoWindow window, Document doc)
        {
            using (var types = new FilteredElementCollector(doc))
            {
                window.ReplacementTypes = types.OfClass(typeof(FamilySymbol))
                    .OfCategory(BuiltInCategory.OST_DuctTerminal).Cast<FamilySymbol>()
                    .Select(symbol =>
                    {
                        FamilyPlacementType placement = symbol.Family.FamilyPlacementType;
                        bool supported = placement == FamilyPlacementType.OneLevelBased ||
                            placement == FamilyPlacementType.OneLevelBasedHosted || placement == FamilyPlacementType.WorkPlaneBased;
                        bool host = placement != FamilyPlacementType.OneLevelBased;
                        return new ReplacementTypeOption
                        {
                            Id = symbol.Id.Value,
                            Label = symbol.Family.Name + " : " + symbol.Name,
                            Supported = supported,
                            NeedsHost = host,
                            Placement = !supported ? "Chưa hỗ trợ: " + placement :
                                host ? "Family cần host: sẽ yêu cầu chọn một mặt host trong model chính." :
                                "Family đặt theo Level, không cần chọn host."
                        };
                    }).OrderBy(type => type.Label).ToList();
            }
            using (var levels = new FilteredElementCollector(doc))
                window.ReplacementLevels = levels.OfClass(typeof(Level)).Cast<Level>()
                    .OrderBy(level => level.ProjectElevation).Select(level => new ReplacementLevelOption
                    {
                        Id = level.Id.Value,
                        Label = level.Name
                    }).ToList();
        }

        private sealed class PlacementItem
        {
            public XYZ Point;
            public string Key;
            public AirTerminalRow Row;
        }

        public static void Execute(UIDocument uiDoc, RevitLinkInstance link, IFCInfoWindow window)
        {
            ReplacementRequest request = window.Replacement;
            if (request == null)
                return;
            Document doc = uiDoc.Document;
            Document linkedDoc = link.GetLinkDocument();
            if (linkedDoc == null)
                throw new InvalidOperationException("Link chưa được load.");
            if (doc.IsReadOnly || doc.IsFamilyDocument)
                throw new InvalidOperationException("Cần một project Revit có thể chỉnh sửa.");
            var symbol = doc.GetElement(new ElementId(request.TypeId)) as FamilySymbol;
            var level = doc.GetElement(new ElementId(request.LevelId)) as Level;
            if (symbol == null || level == null)
                throw new InvalidOperationException("Family/Type hoặc Level không còn tồn tại.");
            FamilyPlacementType placement = symbol.Family.FamilyPlacementType;
            if (placement != FamilyPlacementType.OneLevelBased && placement != FamilyPlacementType.OneLevelBasedHosted &&
                placement != FamilyPlacementType.WorkPlaneBased)
                throw new InvalidOperationException("Kiểu family chưa hỗ trợ.");
            Transform linkTransform = link.GetTotalTransform();
            var items = new List<PlacementItem>();
            Schema schema = Schema.Lookup(TrackingId);
            var alreadyCreated = new HashSet<string>();
            if (schema != null)
            {
                using (var existing = new FilteredElementCollector(doc))
                {
                    foreach (Element element in existing.OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_DuctTerminal))
                    {
                        Entity entity = element.GetEntity(schema);
                        if (entity.IsValid())
                            alreadyCreated.Add(entity.Get<string>(schema.GetField("SourceKey")));
                    }
                }
            }
            int skipped = 0;
            int boundingCenters = 0;
            foreach (string sourceId in request.SourceIds.Distinct())
            {
                long id;
                if (!long.TryParse(sourceId, NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                    throw new InvalidOperationException("Element ID nguồn không hợp lệ: " + sourceId);
                Element source = linkedDoc.GetElement(new ElementId(id));
                if (source == null || source.Category == null || source.Category.Id.Value != (long)BuiltInCategory.OST_DuctTerminal)
                    throw new InvalidOperationException("Không tìm thấy Air Terminal nguồn: " + sourceId);
                AirTerminalRow sourceRow = window.AirTerminals.First(row => row.ElementId == sourceId);
                string key = link.UniqueId + "|" + (string.IsNullOrWhiteSpace(sourceRow.IfcGuid) ? source.UniqueId : sourceRow.IfcGuid);
                if (alreadyCreated.Contains(key))
                {
                    skipped++;
                    continue;
                }
                XYZ localPoint;
                var location = source.Location as LocationPoint;
                if (location != null)
                    localPoint = location.Point;
                else
                {
                    BoundingBoxXYZ box = source.get_BoundingBox(null);
                    if (box == null)
                        throw new InvalidOperationException("Không có vị trí/hình học của phần tử " + sourceId);
                    localPoint = box.Transform.OfPoint((box.Min + box.Max) * 0.5);
                    boundingCenters++;
                }
                items.Add(new PlacementItem
                {
                    Point = linkTransform.OfPoint(localPoint),
                    Key = key,
                    Row = sourceRow
                });
            }
            if (items.Count == 0)
            {
                TaskDialog.Show("Air Terminal", "Không tạo thêm: các phần tử đã chọn đã được tool tạo trước đó (" + skipped + ").");
                return;
            }

            Reference hostReference = null;
            Face face = null;
            Element host = null;
            XYZ hostNormal = XYZ.BasisZ;
            XYZ hostX = XYZ.BasisX;
            XYZ hostY = XYZ.BasisY;
            if (placement != FamilyPlacementType.OneLevelBased)
            {
                hostReference = uiDoc.Selection.PickObject(ObjectType.Face, new HostFaceFilter(doc),
                    "Chọn một mặt host trong model chính cho các Air Terminal mới (Esc để hủy)");
                host = doc.GetElement(hostReference.ElementId);
                face = host.GetGeometryObjectFromReference(hostReference) as Face;
                if (!(face is PlanarFace))
                    throw new InvalidOperationException("Bản test hỗ trợ mặt host phẳng.");
                var hostPlane = (PlanarFace)face;
                hostNormal = hostPlane.FaceNormal;
                hostX = hostPlane.XVector;
                hostY = hostPlane.YVector;
                foreach (PlacementItem item in items)
                {
                    IntersectionResult projection = face.Project(item.Point);
                    if (projection == null || !face.IsInside(projection.UVPoint))
                        throw new InvalidOperationException("Vị trí nguồn " + item.Row.ElementId + " nằm ngoài mặt host đã chọn. Chưa tạo phần tử nào.");
                    item.Point = projection.XYZPoint;
                }
            }

            var createdIds = new List<ElementId>();
            var failures = new RollbackErrors();
            using (var transaction = new Transaction(doc, "IFC Info - Create replacement Air Terminals"))
            {
                transaction.Start();
                var options = transaction.GetFailureHandlingOptions();
                options.SetFailuresPreprocessor(failures);
                options.SetClearAfterRollback(true);
                transaction.SetFailureHandlingOptions(options);
                try
                {
                    if (!symbol.IsActive)
                    {
                        symbol.Activate();
                        doc.Regenerate();
                    }
                    if (schema == null)
                    {
                        var builder = new SchemaBuilder(TrackingId);
                        builder.SetSchemaName("IFCInfoAirTerminalSource");
                        builder.SetReadAccessLevel(AccessLevel.Public);
                        builder.SetWriteAccessLevel(AccessLevel.Public);
                        builder.AddSimpleField("SourceKey", typeof(string));
                        builder.AddSimpleField("IfcGuid", typeof(string));
                        builder.AddSimpleField("SourceSystemName", typeof(string));
                        builder.AddSimpleField("SourceSystemType", typeof(string));
                        schema = builder.Finish();
                    }
                    double angle = request.RotationDegrees * Math.PI / 180.0;
                    foreach (PlacementItem item in items)
                    {
                        FamilyInstance instance;
                        if (placement == FamilyPlacementType.WorkPlaneBased)
                        {
                            XYZ direction = Math.Cos(angle) * hostX + Math.Sin(angle) * hostY;
                            instance = doc.Create.NewFamilyInstance(hostReference, item.Point, direction, symbol);
                        }
                        else if (placement == FamilyPlacementType.OneLevelBasedHosted)
                        {
                            instance = doc.Create.NewFamilyInstance(item.Point, symbol, host, level, StructuralType.NonStructural);
                        }
                        else
                            instance = doc.Create.NewFamilyInstance(item.Point, symbol, level, StructuralType.NonStructural);
                        if (instance == null)
                            throw new InvalidOperationException("Revit không tạo được family cho " + item.Row.ElementId);
                        doc.Regenerate();
                        if (placement == FamilyPlacementType.OneLevelBased)
                        {
                            var actual = instance.Location as LocationPoint;
                            if (actual == null)
                                throw new InvalidOperationException("Family mới không có LocationPoint.");
                            XYZ delta = item.Point - actual.Point;
                            if (delta.GetLength() > 1e-7)
                                ElementTransformUtils.MoveElement(doc, instance.Id, delta);
                        }
                        if (placement != FamilyPlacementType.WorkPlaneBased && Math.Abs(angle) > 1e-10)
                        {
                            XYZ axis = hostNormal;
                            ElementTransformUtils.RotateElement(doc, instance.Id, Line.CreateUnbound(item.Point, axis), angle);
                        }
                        var tracking = new Entity(schema);
                        tracking.Set(schema.GetField("SourceKey"), item.Key);
                        tracking.Set(schema.GetField("IfcGuid"), item.Row.IfcGuid ?? "");
                        tracking.Set(schema.GetField("SourceSystemName"), item.Row.SystemName ?? "");
                        tracking.Set(schema.GetField("SourceSystemType"), item.Row.SystemType ?? "");
                        instance.SetEntity(tracking);
                        Parameter comments = instance.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                        if (comments != null && !comments.IsReadOnly && comments.StorageType == StorageType.String)
                        {
                            string sourceNote = "IFC source " + item.Row.IfcGuid + " | System Name: " + item.Row.SystemName +
                                " | System Type (IFC): " + item.Row.SystemType;
                            string previous = comments.AsString();
                            comments.Set(string.IsNullOrWhiteSpace(previous) ? sourceNote : previous + "\n" + sourceNote);
                        }
                        createdIds.Add(instance.Id);
                    }
                    if (transaction.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("Revit đã hủy lượt tạo. " + failures.Message);
                }
                catch
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                        transaction.RollBack();
                    throw;
                }
            }
            uiDoc.Selection.SetElementIds(createdIds);
            TaskDialog.Show("Air Terminal", "Đã tạo " + createdIds.Count + " Air Terminals trong model chính.\n" +
                "Bỏ qua " + skipped + " nguồn đã tạo trước đó.\n" +
                "Dùng tâm khung bao cho " + boundingCenters + " nguồn không có điểm đặt.\n\n" +
                "IFC vẫn được giữ. Family mới chưa nối mạng ống gió. Có thể Undo lượt tạo này.");
        }

        private sealed class HostFaceFilter : ISelectionFilter
        {
            private readonly Document doc;
            public HostFaceFilter(Document doc)
            {
                this.doc = doc;
            }
            public bool AllowElement(Element element)
            {
                return !(element is RevitLinkInstance);
            }
            public bool AllowReference(Reference reference, XYZ point)
            {
                return doc.GetElement(reference.ElementId)?.GetGeometryObjectFromReference(reference) is PlanarFace;
            }
        }
        private sealed class RollbackErrors : IFailuresPreprocessor
        {
            public string Message
            {
                get; private set;
            }
            public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
            {
                var errors = accessor.GetFailureMessages().Where(m => m.GetSeverity() == FailureSeverity.Error).ToList();
                if (errors.Count == 0)
                    return FailureProcessingResult.Continue;
                Message = string.Join("\n", errors.Select(m => m.GetDescriptionText()));
                return FailureProcessingResult.ProceedWithRollBack;
            }
        }
    }
}
