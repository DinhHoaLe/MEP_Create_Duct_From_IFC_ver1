using System;
using System.Collections.Generic;
using System.Linq;
namespace IFCInfo
{
    public sealed class IfcParameterMapping : System.ComponentModel.INotifyPropertyChanged
    {
        private string source,target;
        private bool mapped;
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        public string Source { get=>source; set {if(source==value)return;source=value;IsMapped=false;Changed(nameof(Source));} }
        public string Target { get=>target; set {if(target==value)return;target=value;CreateMissing=false;IsMapped=false;Changed(nameof(Target));} }
        public bool IsMapped {get=>mapped;set {mapped=value;Changed(nameof(IsMapped));Changed(nameof(MappingLabel));}}
        public string MappingLabel => IsMapped ? "Mapped ✓ (remove)" : "Map →";
        private void Changed(string name)=>PropertyChanged?.Invoke(this,new System.ComponentModel.PropertyChangedEventArgs(name));
        public bool CreateMissing { get; set; }
        public static string Key(IfcPropertyValue p) => (p.Scope??"")+" | "+(p.SetName??"")+" | "+p.Name;
        public static string Read(AirTerminalRow row,string source)
        {
            var values=(row.IfcProperties??new List<IfcPropertyValue>()).Where(p=>Key(p)==source || p.Name==source).ToList();
            if(values.Count==0)return null;
            if(values.Count>1)throw new InvalidOperationException("Ambiguous IFC parameter: "+source+". Select its qualified name.");
            var p=values[0]; return p.Value+(string.IsNullOrWhiteSpace(p.Unit)?"":" "+p.Unit);
        }
    }
}
