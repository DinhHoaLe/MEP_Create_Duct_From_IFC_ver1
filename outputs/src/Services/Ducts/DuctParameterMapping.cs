using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
namespace IFCInfo
{
    internal static class DuctParameterMapping
    {
        internal static string SpecLabel(ForgeTypeId id){try{return LabelUtils.GetLabelForSpec(id);}catch(ArgumentException){return id.TypeId;}}
        internal static ForgeTypeId TypeId(string type)=>type.StartsWith("autodesk.")?new ForgeTypeId(type):type=="Integer"?SpecTypeId.Int.Integer:type=="Number"?SpecTypeId.Number:type=="Yes/No"?SpecTypeId.Boolean.YesNo:SpecTypeId.String.Text;
        internal static string Kind(ForgeTypeId type)=>type==SpecTypeId.String.Text?"text":type==SpecTypeId.Int.Integer?"integer":type==SpecTypeId.Number?"number":type==SpecTypeId.Boolean.YesNo?"boolean":type==SpecTypeId.Length?"length":"";
        static bool Supported(ForgeTypeId type)=>new[]{SpecTypeId.String.Text,SpecTypeId.Int.Integer,SpecTypeId.Number,SpecTypeId.Boolean.YesNo,SpecTypeId.Length}.Contains(type);
        internal static IEnumerable<ParameterOption> BuiltInComments()
        {
            foreach(var id in new[]{BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS,BuiltInParameter.ALL_MODEL_TYPE_COMMENTS})
                yield return new ParameterOption {Name=LabelUtils.GetLabelFor(id),Id=SpecTypeId.String.Text.TypeId,DataKind="text",Discipline=SpecLabel(SpecTypeId.String.Text),Binding=id==BuiltInParameter.ALL_MODEL_TYPE_COMMENTS?"Type":"Instance",Categories="Ducts (built-in)"};
        }
        static BuiltInParameter? CommentId(string name)
        {
            foreach(var id in new[]{BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS,BuiltInParameter.ALL_MODEL_TYPE_COMMENTS})
                if(name==LabelUtils.GetLabelFor(id))return id;
            return null;
        }
        internal static List<string> Targets(Document doc)
        {
            var names=new List<string>();var it=doc.ParameterBindings.ForwardIterator();
            while(it.MoveNext())
            {
                names.Add(it.Key.Name);
            }
            return names.Concat(BuiltInComments().Select(p=>p.Name)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n=>n).ToList();
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
            if(CommentId(name).HasValue)return "Ready";
            var found=new List<Definition>(); var it=doc.ParameterBindings.ForwardIterator();
            while(it.MoveNext()) if(string.Equals(it.Key.Name,name,StringComparison.OrdinalIgnoreCase))
            {
                if(it.Key.Name!=name)return "Use the exact target name: "+it.Key.Name;
                var binding=it.Current as ElementBinding;
                if(binding==null || !binding.Categories.Contains(doc.Settings.Categories.get_Item(BuiltInCategory.OST_DuctCurves)))return "Parameter is not bound to Ducts";
                found.Add(it.Key);
            }
            if(found.Count>1)return "Ambiguous target name";
            if(found.Count==1)return Supported(found[0].GetDataType()) ? "Ready" : "This data type is not yet supported for IFC transfer";
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
                    var definition=file.Groups.Create("IFC mapped parameters").Definitions.Create(new ExternalDefinitionCreationOptions(map.Target,TypeId(map.ParameterType)));
                    var categories=app.Create.NewCategorySet(); categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_DuctCurves));
                    if(!doc.ParameterBindings.Insert(definition,map.ParameterBinding=="Type"?(Binding)app.Create.NewTypeBinding(categories):app.Create.NewInstanceBinding(categories),map.ParameterGroup.StartsWith("autodesk.")?new ForgeTypeId(map.ParameterGroup):map.ParameterGroup=="Identity Data"?GroupTypeId.IdentityData:GroupTypeId.Data))throw new InvalidOperationException("Cannot bind parameter: "+map.Target);
                }
                finally {app.SharedParametersFilename=old; if(File.Exists(temp))File.Delete(temp);}
            }
        }
        internal static void ValidateTypeValues(Document doc,DuctRequest request)
        {
            var typeTargets=new HashSet<string>();var it=doc.ParameterBindings.ForwardIterator();
            while(it.MoveNext())if(it.Current is TypeBinding)typeTargets.Add(it.Key.Name);
            typeTargets.Add(LabelUtils.GetLabelFor(BuiltInParameter.ALL_MODEL_TYPE_COMMENTS));
            foreach(var map in request.ParameterMappings.Where(m=>typeTargets.Contains(m.Target)))
            {
                var groups=request.Items.Where(i=>map.Applies(i.Source)).GroupBy(i=>request.DuctTypes[i.GroupKey]);
                foreach(var group in groups)
                {
                    var values=group.Select(i=>IfcParameterMapping.Read(i.Source,map.Source)).Where(v=>v!=null).Distinct().ToList();
                    if(values.Count>1)throw new InvalidOperationException("Type parameter '"+map.Target+"' has different IFC values for Duct Type "+group.Key+". Use an Instance parameter or separate Duct Types.");
                }
            }
        }
        internal static void Apply(Element duct,AirTerminalRow source,List<IfcParameterMapping> mappings)
        {
            foreach(var map in mappings)
            {
                if(!map.Applies(source))continue;
                string value=IfcParameterMapping.Read(source,map.Source);
                if(value==null)continue;
                var builtIn=CommentId(map.Target);
                Element owner=builtIn==BuiltInParameter.ALL_MODEL_TYPE_COMMENTS?duct.Document.GetElement(duct.GetTypeId()):duct;
                IList<Parameter> matches=builtIn.HasValue?new List<Parameter>{owner.get_Parameter(builtIn.Value)}:duct.GetParameters(map.Target);
                if(matches.Count==0){var ductType=duct.Document.GetElement(duct.GetTypeId());if(ductType!=null)matches=ductType.GetParameters(map.Target);}
                if(matches.Count!=1)throw new InvalidOperationException("Missing or ambiguous target parameter: "+map.Target);
                var p=matches[0];
                if(p.IsReadOnly)throw new InvalidOperationException("Read-only parameter: "+map.Target);
                var type=p.Definition.GetDataType();bool success;
                if(type==SpecTypeId.String.Text)success=p.Set(value);
                else if(type==SpecTypeId.Length) {
                    var property=source.IfcProperties.SingleOrDefault(x=>IfcParameterMapping.Key(x)==map.Source || x.Name==map.Source);
                    if(property?.LengthMm==null)throw new InvalidOperationException("IFC length has no resolved unit: "+map.Source);
                    success=p.Set(UnitUtils.ConvertToInternalUnits(property.LengthMm.Value,UnitTypeId.Millimeters));
                }
                else if(type==SpecTypeId.Int.Integer)success=p.Set(int.Parse(value,System.Globalization.CultureInfo.InvariantCulture));
                else if(type==SpecTypeId.Boolean.YesNo) {
                    string v=value.Trim().ToUpperInvariant();if(!new[]{"TRUE","FALSE",".T.",".F.","1","0"}.Contains(v))throw new InvalidOperationException("Invalid Yes/No value: "+value);
                    success=p.Set(v=="TRUE"||v==".T."||v=="1"?1:0);
                } else success=p.Set(double.Parse(value,System.Globalization.CultureInfo.InvariantCulture));
                if(!success)throw new InvalidOperationException("Cannot write parameter: "+map.Target);
            }
        }
    }
}

