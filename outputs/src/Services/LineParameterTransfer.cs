using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace IFCInfo
{
    internal static class LineParameterTransfer
    {
        internal static Dictionary<string,List<Element>> TrackedLines(Document doc)
        {
            var result=DuctCreation.TrackedDucts(doc).ToDictionary(p=>p.Key,p=>p.Value.Cast<Element>().ToList());
            var schema=Schema.Lookup(NativePlacement.Tracking);
            if(schema==null)return result;
            foreach(var element in new FilteredElementCollector(doc).WhereElementIsNotElementType())
            {
                if(element.Category==null || !SupportedCategories.IsLine(element.Category.Id.Number()))continue;
                var data=element.GetEntity(schema);if(!data.IsValid())continue;
                string key=data.Get<string>(schema.GetField("SourceKey"));if(string.IsNullOrWhiteSpace(key))continue;
                if(!result.TryGetValue(key,out var values))result[key]=values=new List<Element>();
                if(!values.Any(e=>e.Id==element.Id))values.Add(element);
            }
            return result;
        }

        internal static string Transfer(Document doc,RevitLinkInstance link,List<AirTerminalRow> rows,List<IfcParameterMapping> mappings)
        {
            if(rows.Count==0 || mappings.Count==0)throw new InvalidOperationException("Select sources and map at least one parameter.");
            var tracked=TrackedLines(doc);var pairs=new List<Tuple<Element,AirTerminalRow>>();
            foreach(var row in rows)
            {
                if(!long.TryParse(row.ElementId,out var id))throw new InvalidOperationException("Invalid source ID.");
                var source=link.GetLinkDocument()?.GetElement(ElementIds.Create(id));
                if(source?.Category==null || !SupportedCategories.IsLine(source.Category.Id.Number()))throw new InvalidOperationException("Source is no longer a supported line element.");
                string key=link.UniqueId+"|"+(string.IsNullOrWhiteSpace(row.IfcGuid)?source.UniqueId:row.IfcGuid);
                if(!tracked.TryGetValue(key,out var targets) || targets.Count!=1)throw new InvalidOperationException("Source "+row.ElementId+": expected one tracked target. Create it first or resolve duplicate tracking.");
                if(targets[0].Category?.Id!=source.Category.Id)throw new InvalidOperationException("Source and tracked target categories differ.");
                pairs.Add(Tuple.Create(targets[0],row));
            }
            if(pairs.Select(p=>p.Item1.Category.Id.Number()).Distinct().Count()!=1)throw new InvalidOperationException("Transfer one category per batch.");
            if(pairs.Select(p=>p.Item1.Id.Number()).Distinct().Count()!=pairs.Count)throw new InvalidOperationException("Multiple sources refer to the same target element.");
            int written=0,missing=0;
            DuctCreation.RunTransaction(doc,"IFC - Update line parameters",()=> {
                DuctParameterMapping.Ensure(doc,mappings,(BuiltInCategory)pairs[0].Item1.Category.Id.Number());doc.Regenerate();
                foreach(var map in mappings.Where(m=>DuctParameterMapping.IsTypeTarget(doc,m.Target)))
                foreach(var group in pairs.Where(p=>map.Applies(p.Item2)).GroupBy(p=>p.Item1.GetTypeId().Number()))
                    if(group.Select(p=>IfcParameterMapping.Read(p.Item2,map.Source)).Where(v=>v!=null).Distinct().Count()>1)
                        throw new InvalidOperationException("Conflicting IFC values for shared Type parameter '"+map.Target+"'. Use Instance or separate target types.");
                foreach(var pair in pairs)
                {
                    foreach(var map in mappings.Where(m=>m.Applies(pair.Item2)))if(IfcParameterMapping.Read(pair.Item2,map.Source)==null)missing++;else written++;
                    DuctParameterMapping.Apply(pair.Item1,pair.Item2,mappings);
                }
            });
            return "Updated "+written+" value(s) on "+pairs.Count+" tracked element(s); "+missing+" missing source value(s) left unchanged. Geometry and system assignments were unchanged.";
        }
    }
}
