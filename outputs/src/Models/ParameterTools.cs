using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
namespace IFCInfo
{
    public sealed class ParameterValuePreview
    {
        public string SourceId {get;set;}
        public string TargetId {get;set;}
        public string IFCProperty {get;set;}
        public string RevitParameter {get;set;}
        public string CurrentValue {get;set;}
        public string ProposedValue {get;set;}
        public string Status {get;set;}
        internal long TargetTypeId {get;set;}
        internal bool TypeParameter {get;set;}
    }
    public sealed class ParameterMappingPreset
    {
        public int Version {get;set;}=1;
        public List<ParameterPresetEntry> Entries {get;set;}=new List<ParameterPresetEntry>();
        public static string Write(ParameterMappingPreset preset)
        {
            using(var writer=new StringWriter()){new XmlSerializer(typeof(ParameterMappingPreset)).Serialize(writer,preset);return writer.ToString();}
        }
        public static ParameterMappingPreset Read(string xml)
        {
            using(var reader=XmlReader.Create(new StringReader(xml),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))
            {
                var result=(ParameterMappingPreset)new XmlSerializer(typeof(ParameterMappingPreset)).Deserialize(reader);
                if(result.Version!=1 || result.Entries==null)throw new InvalidOperationException("Unsupported mapping preset version.");
                if(result.Entries.Any(e=>e==null || string.IsNullOrWhiteSpace(e.Source) || string.IsNullOrWhiteSpace(e.Target)))throw new InvalidOperationException("Preset entries need IFC source and Revit target names.");
                if(result.Entries.GroupBy(e=>e.Source).Any(g=>g.Count()>1)||result.Entries.GroupBy(e=>e.Target,StringComparer.OrdinalIgnoreCase).Any(g=>g.Count()>1))throw new InvalidOperationException("Preset contains duplicate sources or targets.");
                return result;
            }
        }
    }
    public sealed class ParameterPresetEntry
    {
        public string Source {get;set;}
        public string Target {get;set;}
        public string DataType {get;set;}="Text";
        public string Group {get;set;}="Data";
        public string Binding {get;set;}="Instance";
    }
    public static class ParameterNames
    {
        public static string Unique(string name,string prefix,ISet<string> used)
        {
            string cleaned=new string((name??"").Select(c=>"\\/:{}[]|;<>?`~".Contains(c)?'_':c).ToArray()).Trim();
            string stem=new string(((prefix??"")+(cleaned.Length==0?"Parameter":cleaned)).Select(c=>"\\/:{}[]|;<>?`~".Contains(c)?'_':c).ToArray()), candidate=stem;
            int suffix=2;while(used.Contains(candidate))candidate=stem+"_"+(suffix++);
            used.Add(candidate);return candidate;
        }
    }
}
