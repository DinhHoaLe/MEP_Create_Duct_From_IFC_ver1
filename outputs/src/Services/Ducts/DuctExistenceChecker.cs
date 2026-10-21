using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;


namespace IFCInfo
{
    /// <summary>Compare source ducts with host-model ducts and update each row status.</summary>
    internal static class DuctExistenceChecker
    {
        private static double[] Coordinates(XYZ p) => new[] { p.X, p.Y, p.Z };
        private static readonly Guid MappingId = new Guid("d9d3c216-43bd-47ad-b2fa-f279bdc87240");
        internal static void RecordMapping(Duct duct, string sourceKey, string sourceType, long systemTypeId)
        {
            var schema = Schema.Lookup(MappingId);
            if (schema == null)
            {
                var builder = new SchemaBuilder(MappingId);
                builder.SetSchemaName("IFCInfoDuctExpectedSystem");
                builder.AddSimpleField("SourceKey", typeof(string));
                builder.AddSimpleField("SourceType", typeof(string));
                builder.AddSimpleField("SystemTypeId", typeof(string));
                schema = builder.Finish();
            }
            var entity = new Entity(schema);
            entity.Set(schema.GetField("SourceKey"), sourceKey);
            entity.Set(schema.GetField("SourceType"), sourceType ?? "");
            entity.Set(schema.GetField("SystemTypeId"), systemTypeId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            duct.SetEntity(entity);
        }
        private static double Size(Duct duct, BuiltInParameter parameter) => duct.get_Parameter(parameter)?.AsDouble() ?? 0;
        public static void Update(IFCInfoWindow window, Document doc, RevitLinkInstance link)
        {
            var ducts = new List<DuctCoverage.Segment>();
            var savedMappings = new Dictionary<string, HashSet<long>>();
            var mappingSchema = Schema.Lookup(MappingId);
            var systemTypes = new FilteredElementCollector(doc).OfClass(typeof(MechanicalSystemType))
                .Cast<MechanicalSystemType>().ToList();
            var settings = DuctProjectSettings.Load(doc);
            bool incomplete = false;
            foreach (Duct duct in new FilteredElementCollector(doc).OfClass(typeof(Duct)))
            {
                if (mappingSchema != null)
                {
                    var entity = duct.GetEntity(mappingSchema);
                    if (entity.IsValid())
                    {
                        string key = entity.Get<string>(mappingSchema.GetField("SourceKey")) + "|" + entity.Get<string>(mappingSchema.GetField("SourceType"));
                        if (!savedMappings.TryGetValue(key, out var ids)) savedMappings[key] = ids = new HashSet<long>();
                        if (long.TryParse(entity.Get<string>(mappingSchema.GetField("SystemTypeId")), out long savedId)) ids.Add(savedId);
                    }
                }
                var line = (duct.Location as LocationCurve)?.Curve as Line;
                if (line == null)
                {
                    incomplete = true;
                    continue;
                }
                ducts.Add(new DuctCoverage.Segment
                {
                    Id = duct.Id.Number(),
                    Start = Coordinates(line.GetEndPoint(0)),
                    End = Coordinates(line.GetEndPoint(1)),
                    Width = Size(duct, BuiltInParameter.RBS_CURVE_WIDTH_PARAM),
                    Height = Size(duct, BuiltInParameter.RBS_CURVE_HEIGHT_PARAM),
                    Diameter = Size(duct, BuiltInParameter.RBS_CURVE_DIAMETER_PARAM),
                    WidthAxis = Coordinates(duct.ConnectorManager.Connectors.Cast<Connector>().First(c=>c.ConnectorType==ConnectorType.End).CoordinateSystem.BasisX),
                    Oval = duct.DuctType.Shape == ConnectorProfileType.Oval,
                    UnsupportedShape = duct.DuctType.Shape != ConnectorProfileType.Round && duct.DuctType.Shape != ConnectorProfileType.Rectangular && duct.DuctType.Shape != ConnectorProfileType.Oval,
                    SystemTypeId = duct.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM)?.AsElementId().Number() ?? -1
                });
            }
            Transform transform = link.GetTotalTransform();
            foreach (var row in window.AirTerminals)
            {
                try
                {
                    Element source = link.GetLinkDocument()?.GetElement(ElementIds.Create(long.Parse(row.ElementId)));
                    if (source == null)
                        throw new InvalidOperationException("Source duct not found.");
                    var geometry = DuctGeometryReader.Read(source, row.DuctSource);
                    string key = link.UniqueId + "|" + (string.IsNullOrWhiteSpace(row.IfcGuid) ? source.UniqueId : row.IfcGuid) + "|" + (row.SystemType ?? "");
                    long? expectedSystem = null;
                    var configured = settings.Systems.FirstOrDefault(m => m.Key == DuctRequest.SystemKey(geometry.ShapeKey, row.SystemType))
                        ?? (geometry.Oval ? null : settings.Systems.FirstOrDefault(m => m.Key == DuctRequest.SystemKey(geometry.Round, row.SystemType)));
                    if (configured != null && systemTypes.Any(t => t.Id.Number() == configured.Id)) expectedSystem = configured.Id;
                    else if (savedMappings.TryGetValue(key, out var mappedIds))
                    {
                        if (mappedIds.Count == 1) expectedSystem = mappedIds.Single();
                    }
                    else
                    {
                        var namedTypes = systemTypes.Where(t => !string.IsNullOrWhiteSpace(row.SystemType) &&
                            string.Equals(t.Name.Trim(), row.SystemType.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
                        if (namedTypes.Count == 1) expectedSystem = namedTypes[0].Id.Number();
                    }
                    var result = DuctCoverage.CheckDetails(Coordinates(transform.OfPoint(geometry.Start)),
                        Coordinates(transform.OfPoint(geometry.End)), ducts, 1.0 / 304.8,
                        geometry.Width, geometry.Height, geometry.Diameter, expectedSystem,Coordinates(transform.OfVector(geometry.WidthAxis).Normalize()),geometry.Oval);
                    row.CorrespondingDuctIds = result.Ids;
                    row.DuctExistence = incomplete && !result.FullyCovered ? "Unknown" : result.Status;
                    row.DuctExistenceDetail = "Comparing centerlines, dimensions (1 mm tolerance) and profile orientation. Systems are compared by target system type. IFC System Name is stored separately from Revit-generated system names." +
                        (expectedSystem.HasValue ? " Target system type ID: " + expectedSystem.Value + "." : " Target system type is unknown: saved mapping is missing or the IFC name does not uniquely match a Revit type.") +
                        (result.FullyCovered ? " The centerline is fully covered; creation is disabled to avoid duplicate ducts." : "") +
                        (result.Ids.Count == 0 ? "" : " Duct ID: " + string.Join(", ", result.Ids)) +
                        (result.Ids.Count == 0 ? "" : " | Source (mm): " + (geometry.Round
                            ? "D=" + (geometry.Diameter * 304.8).ToString("0.##")
                            : (geometry.Width * 304.8).ToString("0.##") + " x " + (geometry.Height * 304.8).ToString("0.##"))) +
                        string.Join("", ducts.Where(d => result.Ids.Contains(d.Id)).Select(d => " | ID " + d.Id + ": " +
                            (d.UnsupportedShape ? "Other profile" : d.Diameter > 0 ? "D=" + (d.Diameter * 304.8).ToString("0.##") :
                                (d.Width * 304.8).ToString("0.##") + " x " + (d.Height * 304.8).ToString("0.##")) + " mm, System Type ID=" + d.SystemTypeId)) +
                        (incomplete ? " Some host-model ducts have unreadable centerlines." : "");
                }
                catch (Exception ex)
                {
                    row.CorrespondingDuctIds.Clear();
                    row.DuctExistence = "Unknown";
                    row.DuctExistenceDetail = ex.Message;
                }
            }
        }


    }
}
