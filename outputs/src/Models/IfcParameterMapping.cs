using System;
using System.Collections.Generic;
using System.Linq;
namespace IFCInfo
{
    public sealed class IfcParameterMapping : System.ComponentModel.INotifyPropertyChanged
    {
        private string source,target;
        private string targetType;public string TargetType {get=>targetType;set {targetType=value;Changed(nameof(TargetType));}}
        private bool mapped; private string sourceType; public string SourceType {get=>sourceType;set {sourceType=value;Changed(nameof(SourceType));}}
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        public string Source { get=>source; set {if(source==value)return;source=value;IsMapped=false;Changed(nameof(Source));} }
        public string Target { get=>target; set {if(target==value)return;target=value;CreateMissing=false;IsMapped=false;Changed(nameof(Target));} }
        public bool IsMapped {get=>mapped;set {mapped=value;Changed(nameof(IsMapped));Changed(nameof(MappingLabel));}}
        public string MappingLabel => IsMapped ? "Mapped ✓ (remove)" : "Map →";
        private void Changed(string name)=>PropertyChanged?.Invoke(this,new System.ComponentModel.PropertyChangedEventArgs(name));
        public List<string> ElementIds {get;set;}
        public string IdSummary => ElementIds==null?"All selected":string.Join(", ",ElementIds);
        public string System {get;set;}
        private bool selected=true;
        public bool Selected {get=>selected;set {selected=value;Changed(nameof(Selected));}}
        public bool Applies(AirTerminalRow row)=>ElementIds==null || ElementIds.Contains(row.ElementId);
        public string ParameterBinding {get;set;}="Instance";
        public string ParameterType {get;set;}="Text"; public string ParameterGroup {get;set;}="Data";
        public bool CreateMissing { get; set; }
        public static string Key(IfcPropertyValue p) => (p.Scope??"")+" | "+(p.SetName??"")+" | "+p.Name;
        public static string Read(AirTerminalRow row,string source)
        {
            if(source=="IFC GUID")return string.IsNullOrWhiteSpace(row.IfcGuid)?null:row.IfcGuid;
            if(source=="IFC Element ID")return row.ElementId;
            var values=(row.IfcProperties??new List<IfcPropertyValue>()).Where(p=>Key(p)==source || p.Name==source).ToList();
            if(values.Count==0)return null;
            if(values.Count>1)throw new InvalidOperationException("Ambiguous IFC parameter: "+source+". Select its qualified name.");
            var p=values[0]; return p.Value+(string.IsNullOrWhiteSpace(p.Unit)?"":" "+p.Unit);
        }
    }
}
