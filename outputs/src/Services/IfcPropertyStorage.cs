using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
namespace IFCInfo
{
    internal static class IfcPropertyStorage
    {
        private static readonly Guid Id=new Guid("7c8e9c76-6b96-4662-bb48-80b0d48c6ee2");
        internal static string Text(AirTerminalRow row) => row.IfcProperties.Count==0 ? "Không có IFC Pset trong nguồn đã đọc." :
            string.Join(Environment.NewLine,row.IfcProperties.Select(p=>p.ToString()));
        internal static void Save(Element target,AirTerminalRow row)
        {
            var schema=Schema.Lookup(Id);
            if (schema==null)
            {
                var builder=new SchemaBuilder(Id); builder.SetSchemaName("IFCInfoSourceProperties");
                builder.AddSimpleField("IfcGuid",typeof(string)); builder.AddSimpleField("Psets",typeof(string)); schema=builder.Finish();
            }
            var entity=new Entity(schema); entity.Set(schema.GetField("IfcGuid"),row.IfcGuid??"");
            entity.Set(schema.GetField("Psets"),Text(row)); target.SetEntity(entity);
        }
    }
}
