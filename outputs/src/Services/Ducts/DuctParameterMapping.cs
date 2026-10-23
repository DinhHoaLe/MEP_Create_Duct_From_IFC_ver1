using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
namespace IFCInfo
{
    internal static class DuctParameterMapping
    {
        internal static List<string> Targets(Document doc)
        {
            var names=new List<string>();var it=doc.ParameterBindings.ForwardIterator();
            while(it.MoveNext())
            {
                var binding=it.Current as InstanceBinding;
                if(binding!=null && binding.Categories.Contains(doc.Settings.Categories.get_Item(BuiltInCategory.OST_DuctCurves)) &&
                    it.Key.GetDataType()==SpecTypeId.String.Text)names.Add(it.Key.Name);
            }
            return names.GroupBy(n=>n,StringComparer.OrdinalIgnoreCase).Where(g=>g.Count()==1).Select(g=>g.First()).OrderBy(n=>n).ToList();
        }
        internal static string Transfer(Document doc,RevitLinkInstance link,List<AirTerminalRow> rows,List<IfcParameterMapping> mappings)
        {
            if(rows.Count==0 || mappings.Count==0)throw new InvalidOperationException("Select IFC sources and map at least one parameter.");
            var tracked=DuctCreation.TrackedDucts(doc);
            var pairs=new List<Tuple<Autodesk.Revit.DB.Mechanical.Duct,AirTerminalRow>>();
            foreach(var row in rows)
            {
                long id; if(!long.TryParse(row.ElementId,out id))throw new InvalidOperationException("Invalid IFC element ID.");
                var source=link.GetLinkDocument()?.GetElement(ElementIds.Create(id));
                if(source==null)throw new InvalidOperationException("Source no longer exists: "+row.ElementId);
                var key=link.UniqueId+"|"+(string.IsNullOrWhiteSpace(row.IfcGuid)?source.UniqueId:row.IfcGuid);
                List<Autodesk.Revit.DB.Mechanical.Duct> ducts;
                if(!tracked.TryGetValue(key,out ducts)||ducts.Count!=1)throw new InvalidOperationException("Source "+row.ElementId+": expected one tracked Revit duct. Create it first or resolve duplicate tracking.");
                pairs.Add(Tuple.Create(ducts[0],row));
            }
            if(pairs.Select(p=>p.Item1.Id.Number()).Distinct().Count()!=pairs.Count)throw new InvalidOperationException("Multiple sources refer to the same target duct.");
            int written=0,missing=0;
            DuctCreation.RunTransaction(doc,"IFC - Transfer mapped parameters",()=>
            {
                Ensure(doc,mappings);doc.Regenerate();
                foreach(var pair in pairs)
                {
                    foreach(var map in mappings)if(IfcParameterMapping.Read(pair.Item2,map.Source)==null)missing++;else written++;
                    Apply(pair.Item1,pair.Item2,mappings);
                }
            });
            return "Transferred "+written+" value(s) to "+pairs.Count+" existing duct(s). "+missing+" source value(s) missing; those targets were unchanged. Geometry was unchanged.";
        }
        internal static string Check(Document doc,string name)
        {
            var found=new List<Definition>(); var it=doc.ParameterBindings.ForwardIterator();
            while(it.MoveNext()) if(string.Equals(it.Key.Name,name,StringComparison.OrdinalIgnoreCase))
            {
                if(it.Key.Name!=name)return "Use the exact target name: "+it.Key.Name;
                var binding=it.Current as InstanceBinding;
                if(binding==null || !binding.Categories.Contains(doc.Settings.Categories.get_Item(BuiltInCategory.OST_DuctCurves)))return "Not an instance parameter bound to Ducts";
                found.Add(it.Key);
            }
            if(found.Count>1)return "Ambiguous target name";
            if(found.Count==1)return found[0].GetDataType()==SpecTypeId.String.Text ? "Ready" : "Target must be Text (preserves IFC values and units)";
            // Built-in names must not be shadowed by a new project parameter.
            foreach(var duct in new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.Mechanical.Duct)))
                if(duct.GetParameters(name).Count>0)return "Existing built-in parameter; choose a project Text parameter";
            return "Missing";
        }
        internal static void Ensure(Document doc,List<IfcParameterMapping> mappings)
        {
            foreach(var map in mappings)
            {
                string status=Check(doc,map.Target);
                if(status=="Ready")continue;
                if(status!="Missing" || !map.CreateMissing)throw new InvalidOperationException(map.Target+": "+status);
                var app=doc.Application; string old=app.SharedParametersFilename;
                string temp=Path.Combine(Path.GetTempPath(),"IFC-Parameters-"+Guid.NewGuid()+".txt");
                try
                {
                    File.WriteAllText(temp,""); app.SharedParametersFilename=temp;
                    var file=app.OpenSharedParameterFile();
                    var definition=file.Groups.Create("IFC mapped parameters").Definitions.Create(new ExternalDefinitionCreationOptions(map.Target,SpecTypeId.String.Text));
                    var categories=app.Create.NewCategorySet(); categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_DuctCurves));
                    if(!doc.ParameterBindings.Insert(definition,app.Create.NewInstanceBinding(categories),GroupTypeId.Data))throw new InvalidOperationException("Cannot bind parameter: "+map.Target);
                }
                finally {app.SharedParametersFilename=old; if(File.Exists(temp))File.Delete(temp);}
            }
        }
        internal static void Apply(Element duct,AirTerminalRow source,List<IfcParameterMapping> mappings)
        {
            foreach(var map in mappings)
            {
                string value=IfcParameterMapping.Read(source,map.Source);
                if(value==null)continue;
                var matches=duct.GetParameters(map.Target);
                if(matches.Count!=1)throw new InvalidOperationException("Missing or ambiguous target parameter: "+map.Target);
                var p=matches[0];
                if(p.IsReadOnly || p.StorageType!=StorageType.String || !p.Set(value))throw new InvalidOperationException("Cannot write parameter: "+map.Target);
            }
        }
    }
}

