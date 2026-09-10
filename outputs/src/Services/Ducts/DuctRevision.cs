using System;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace IFCInfo
{
    internal static class DuctRevision
    {
        private static readonly Guid Id = new Guid("9a2604f5-971f-451b-962e-127612f72b65");
        private static string Geometry(XYZ a, XYZ b, XYZ x, double w, double h, double d) =>
            string.Join(";", new[] { a.X,a.Y,a.Z,b.X,b.Y,b.Z,x.X,x.Y,x.Z,w,h,d }
                .Select(v => v.ToString("R", CultureInfo.InvariantCulture)));
        internal static string Source(DuctPlanItem i) => Geometry(i.Start,i.End,i.WidthAxis,i.Width,i.Height,i.Diameter)
            + "|" + i.Source.SystemType + "|" + i.Source.SystemName;
        internal static string Actual(Duct d)
        {
            var line = (d.Location as LocationCurve)?.Curve as Line;
            if (line == null) return "unsupported";
            var c = d.ConnectorManager.Connectors.Cast<Connector>().First(x => x.ConnectorType == ConnectorType.End);
            bool round = c.Shape == ConnectorProfileType.Round;
            return Geometry(line.GetEndPoint(0),line.GetEndPoint(1),c.CoordinateSystem.BasisX,
                round ? 0 : c.Width,round ? 0 : c.Height,round ? c.Radius*2 : 0) + "|" + d.GetTypeId().Value
                + "|" + d.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM)?.AsElementId().Value
                + "|" + d.ReferenceLevel?.Id.Value + "|" + d.WorksetId.IntegerValue
                + "|" + d.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString()
                + "|" + d.Pinned + "|" + string.Join(",",d.ConnectorManager.Connectors.Cast<Connector>()
                    .Where(p=>p.ConnectorType==ConnectorType.End).SelectMany(p=>p.AllRefs.Cast<Connector>())
                    .Where(p=>p.Owner.Id!=d.Id).Select(p=>p.Owner.UniqueId+":"+p.Id).OrderBy(v=>v,StringComparer.Ordinal));
        }
        internal static bool Equal(string a, string b)
        {
            return DuctSnapshotComparison.Equal(a,b);
        }
        internal static string Get(Duct d, string field)
        {
            var schema = Schema.Lookup(Id); if (schema == null) return null;
            var entity = d.GetEntity(schema); return entity.IsValid() ? entity.Get<string>(schema.GetField(field)) : null;
        }
        internal static void Save(Duct d, DuctPlanItem item, string run)
        {
            var schema = Schema.Lookup(Id);
            if (schema == null)
            {
                var b = new SchemaBuilder(Id); b.SetSchemaName("IFCInfoDuctRevision");
                foreach (var f in new[] { "Source", "Actual", "Run" }) b.AddSimpleField(f, typeof(string));
                schema = b.Finish();
            }
            var entity = new Entity(schema);
            entity.Set(schema.GetField("Source"), Source(item)); entity.Set(schema.GetField("Actual"), Actual(d));
            entity.Set(schema.GetField("Run"), Get(d,"Run") ?? run); d.SetEntity(entity);
        }
        internal static bool Connected(Duct d) => d.ConnectorManager.Connectors.Cast<Connector>()
            .Any(c => c.ConnectorType == ConnectorType.End && c.IsConnected);
    }
}
