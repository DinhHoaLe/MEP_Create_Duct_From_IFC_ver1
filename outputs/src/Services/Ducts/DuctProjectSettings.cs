using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace IFCInfo
{
    public sealed class DuctSettingsData
    {
        public long RoundTypeId, RectangularTypeId;
        public List<DuctSettingEntry> Systems = new List<DuctSettingEntry>();
        public List<DuctSettingEntry> Levels = new List<DuctSettingEntry>();
        public List<DuctSettingEntry> Worksets = new List<DuctSettingEntry>();
    }
    public sealed class DuctSettingEntry { public string Key; public long Id; }
    internal static class DuctProjectSettings
    {
        private static readonly Guid Id = new Guid("6b2d1996-e9cc-46a8-902a-6b0102c98231");
        internal static DuctSettingsData Load(Document doc)
        {
            var schema = Schema.Lookup(Id);
            if (schema == null) return new DuctSettingsData();
            var entity = doc.ProjectInformation.GetEntity(schema);
            if (!entity.IsValid()) return new DuctSettingsData();
            using (var reader = new StringReader(entity.Get<string>(schema.GetField("Xml"))))
                return (DuctSettingsData)new XmlSerializer(typeof(DuctSettingsData)).Deserialize(reader);
        }
        internal static void Save(Document doc, DuctRequest request)
        {
            var schema = Schema.Lookup(Id);
            if (schema == null)
            {
                var b = new SchemaBuilder(Id); b.SetSchemaName("IFCInfoProjectMappings");
                b.AddSimpleField("Xml", typeof(string)); schema = b.Finish();
            }
            var data = Load(doc);
            if (request.RoundTypeId > 0) data.RoundTypeId = request.RoundTypeId;
            if (request.RectangularTypeId > 0) data.RectangularTypeId = request.RectangularTypeId;
            Merge(data.Systems, request.SystemTypes); Merge(data.Levels, request.Levels); Merge(data.Worksets, request.Worksets);
            using (var writer = new StringWriter(CultureInfo.InvariantCulture))
            {
                new XmlSerializer(typeof(DuctSettingsData)).Serialize(writer, data);
                var entity = new Entity(schema); entity.Set(schema.GetField("Xml"), writer.ToString());
                doc.ProjectInformation.SetEntity(entity);
            }
        }
        private static void Merge(List<DuctSettingEntry> entries, Dictionary<string, long> values)
        {
            foreach (var value in values)
            {
                entries.RemoveAll(e => e.Key == value.Key);
                entries.Add(new DuctSettingEntry { Key = value.Key, Id = value.Value });
            }
        }
    }
}
