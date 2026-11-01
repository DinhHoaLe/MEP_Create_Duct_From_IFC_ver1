using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;


namespace IFCInfo
{
    /// <summary>Connect the UI to validation and duct creation planning.</summary>
    internal static class DuctWorkflow
    {
        public static void Configure(IFCInfoWindow window, Document doc, RevitLinkInstance link)
        {
            window.ProjectParameters=()=> {
                var result=new List<ParameterOption>();var it=doc.ParameterBindings.ForwardIterator();
                while(it.MoveNext()) {
                    var binding=it.Current as ElementBinding;
                    result.Add(new ParameterOption {Name=it.Key.Name,Id=it.Key.GetDataType().TypeId,DataKind=DuctParameterMapping.Kind(it.Key.GetDataType()),
                        Discipline=DuctParameterMapping.SpecLabel(it.Key.GetDataType()),Binding=it.Current is InstanceBinding?"Instance":"Type",
                        Categories=binding==null?"":string.Join(", ",binding.Categories.Cast<Category>().Select(c=>c.Name).OrderBy(x=>x))});
                }
                return result.Concat(DuctParameterMapping.BuiltInComments()).OrderBy(x=>x.Name).ToList();
            };
            window.ParameterTypes=()=>SpecUtils.GetAllSpecs().Select(id=>new ParameterOption {Id=id.TypeId,Name=LabelUtils.GetLabelForSpec(id),Discipline=UnitUtils.IsMeasurableSpec(id)?LabelUtils.GetLabelForDiscipline(UnitUtils.GetDiscipline(id)):"Common"}).OrderBy(x=>x.Name).ToList();
            window.ParameterGroups=()=>ParameterUtils.GetAllBuiltInGroups().Select(id=>new ParameterOption{Id=id.TypeId,Name=LabelUtils.GetLabelForGroup(id)}).OrderBy(x=>x.Name).ToList();
            window.CreateParameter=(name,type,group,binding)=> {
                if(string.IsNullOrWhiteSpace(name) || name.IndexOfAny(new[]{(char)92,'/',':','{','}','[',']','|',';','<','>','?'})>=0)throw new InvalidOperationException("Enter a valid parameter name.");
                if(DuctParameterMapping.Check(doc,name)!="Missing")throw new InvalidOperationException("This parameter name already exists or is not available.");
                DuctCreation.RunTransaction(doc,"IFC - Create Text parameter",()=>DuctParameterMapping.Ensure(doc,new List<IfcParameterMapping>{new IfcParameterMapping{Target=name,CreateMissing=true,ParameterType=type,ParameterGroup=group,ParameterBinding=binding}}));
            };
            window.CheckParameterTarget=name=>DuctParameterMapping.Check(doc,name);
            window.LoadParameterTargets=()=>DuctParameterMapping.Targets(doc);
            window.TransferParameters=(rows,maps)=>DuctParameterMapping.Transfer(doc,link,rows,maps);
            var loadCategory = window.LoadCategory;
            window.LoadCategory = category =>
            {
                loadCategory?.Invoke(category);
                if (window.CanCreateDucts)
                    DuctExistenceChecker.Update(window, doc, link);
            };
            Func<List<AirTerminalRow>, bool, DuctRequest> prepare = (rows, updating) =>
            {
                DuctCreationWindow dialog;
                using (LoadingWindow.ShowWhile(window, "Preparing duct groups", "Checking centerlines, profiles and Revit system types…"))
                {
                var items = new List<DuctPlanItem>();
                var issues = new List<string>();
                var existing = DuctCreation.ExistingKeys(doc);
                var tracked = DuctCreation.TrackedDucts(doc);
                foreach (var row in rows.Where(row => updating || row.CanSelect))
                {
                    try
                    {
                        Element source = link.GetLinkDocument()?.GetElement(ElementIds.Create(long.Parse(row.ElementId)));
                        if (source == null)
                            throw new InvalidOperationException("Source element not found.");
                        if (source.Category?.Id.Number() != (long)BuiltInCategory.OST_DuctCurves)
                            throw new InvalidOperationException("Ducts can only be created from the IFC Ducts category.");
                        string key = link.UniqueId + "|" + (string.IsNullOrWhiteSpace(row.IfcGuid) ? source.UniqueId : row.IfcGuid);
                        if (!updating && existing.Contains(key))
                            throw new InvalidOperationException("A duct has already been created from this source.");
                        if (!updating && row.DuctExistence == "Partial overlap")
                            throw new InvalidOperationException("Partial overlap: resolve the overlap before creation to avoid duplicate ducts.");
                        if (updating && !tracked.ContainsKey(key)) continue;
                        var item = DuctGeometryReader.Read(source, row.DuctSource);
                        Transform transform = link.GetTotalTransform();
                        item.Start = transform.OfPoint(item.Start);
                        item.End = transform.OfPoint(item.End);
                        item.WidthAxis = transform.OfVector(item.WidthAxis).Normalize();
                        item.Key = key;
                        item.Source = row;
                        item.LevelKey = (source.Document.GetElement(source.LevelId) as Level)?.Name ?? "No source level";
                        if (updating)
                        {
                            var targets = tracked[key];
                            if (targets.Count != 1) throw new InvalidOperationException("Multiple ducts share the source GUID; automatic update is disabled.");
                            var target = targets[0];
                            if (DuctRevision.Get(target, "Actual") == null)
                                throw new InvalidOperationException("The existing duct has no baseline for detecting manual edits; update skipped.");
                            if (!DuctRevision.Equal(DuctRevision.Get(target, "Actual"), DuctRevision.Actual(target)))
                                throw new InvalidOperationException("Duct ID " + target.Id.Number() + " was edited in Revit; preserved for manual review.");
                            if (DuctRevision.Equal(DuctRevision.Get(target, "Source"), DuctRevision.Source(item))) continue;
                            if (DuctRevision.Connected(target))
                                throw new InvalidOperationException("Duct ID " + target.Id.Number() + " is connected; resolve connections before updating.");
                            item.ExistingId = target.Id.Number();
                            item.ChangeSummary = DuctSnapshotComparison.Describe(DuctRevision.Get(target,"Source"),DuctRevision.Source(item));
                        }
                        if (item.Start.DistanceTo(item.End) < Math.Max(doc.Application.ShortCurveTolerance, 1.0 / 120))
                            throw new InvalidOperationException("The segment is too short to create a duct.");
                        items.Add(item);
                    }
                    catch (Exception ex) { issues.Add(row.ElementId + ": " + ex.Message); }
                }
                var types = new FilteredElementCollector(doc).OfClass(typeof(DuctType)).Cast<DuctType>().ToList();
                var systems = new FilteredElementCollector(doc).OfClass(typeof(MechanicalSystemType)).Cast<MechanicalSystemType>()
                    .OrderBy(t => t.Name).Select(t => new DuctChoice { Id = t.Id.Number(), Name = t.Name, Classification=t.SystemClassification.ToString() }).ToList();
                dialog = new DuctCreationWindow(items, issues,
                    types.Where(t => t.Shape == ConnectorProfileType.Round).Select(t => new DuctChoice { Id = t.Id.Number(), Name = t.Name }).ToList(),
                    types.Where(t => t.Shape == ConnectorProfileType.Rectangular).Select(t => new DuctChoice { Id = t.Id.Number(), Name = t.Name }).ToList(), systems,
                    new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l=>l.ProjectElevation)
                        .Select(l=>new DuctChoice { Id=l.Id.Number(), Name=l.Name }).ToList(),
                    doc.IsWorkshared ? new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset)
                        .Select(w=>new DuctChoice { Id=w.Id.IntegerValue, Name=w.Name }).ToList() : new List<DuctChoice>(),
                    DuctProjectSettings.Load(doc), updating,
                    types.Where(t => t.Shape == ConnectorProfileType.Oval).Select(t => new DuctChoice { Id = t.Id.Number(), Name = t.Name }).ToList());
                }
                dialog.IfcSources=window.AirTerminals;
                dialog.CreateSystemType=(name,templateId,classificationName,abbreviation)=> {
                    if(string.IsNullOrWhiteSpace(name))throw new InvalidOperationException("Enter a system type name.");
                    if(new FilteredElementCollector(doc).OfClass(typeof(MechanicalSystemType)).Cast<MechanicalSystemType>().Any(t=>string.Equals(t.Name,name,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("A system type with this name already exists.");
                    MechanicalSystemType created=null;
                    DuctCreation.RunTransaction(doc,"IFC - Create System Type",()=> {
                        if(templateId>0)created=((MechanicalSystemType)doc.GetElement(ElementIds.Create(templateId))).Duplicate(name) as MechanicalSystemType;
                        else {
                            MEPSystemClassification classification;
                            if(!Enum.TryParse(classificationName,out classification) || !new[]{MEPSystemClassification.SupplyAir,MEPSystemClassification.ReturnAir,MEPSystemClassification.ExhaustAir,MEPSystemClassification.OtherAir}.Contains(classification))throw new InvalidOperationException("Select a valid HVAC classification.");
                            created=MechanicalSystemType.Create(doc,classification,name);
                        }
                    if(!string.IsNullOrWhiteSpace(abbreviation)){var parameter=created.get_Parameter(BuiltInParameter.RBS_SYSTEM_ABBREVIATION_PARAM);if(parameter==null || parameter.IsReadOnly || !parameter.Set(abbreviation))throw new InvalidOperationException("Cannot write system abbreviation.");}
                    });
                    return new DuctChoice{Id=created.Id.Number(),Name=created.Name,Classification=created.SystemClassification.ToString()};
                };
                dialog.CreateDuctType=(name,templateId)=> {
                    if(string.IsNullOrWhiteSpace(name))throw new InvalidOperationException("Enter a duct type name.");
                    if(new FilteredElementCollector(doc).OfClass(typeof(DuctType)).Cast<DuctType>().Any(t=>string.Equals(t.Name,name,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("A duct type with this name already exists.");
                    long id=0;DuctCreation.RunTransaction(doc,"IFC - Create Duct Type",()=>id=((DuctType)doc.GetElement(ElementIds.Create(templateId))).Duplicate(name).Id.Number());return id;
                };

                dialog.BrowseSystems=owner=>ProjectSystemBrowser.Show(owner,ProjectSystemBrowser.Read(doc));
                window.OpenDuctSettings(dialog,request=>
                {
                    foreach(var skipped in request.Skipped)
                        if(string.IsNullOrEmpty(skipped.IfcGuid)) skipped.IfcGuid=rows.FirstOrDefault(r=>r.ElementId==skipped.SourceId)?.IfcGuid;
                });
                return null;
            };
            window.PrepareDucts = rows => prepare(rows, false);
            window.PrepareUpdates = rows => prepare(rows, true);
        }


    }
}


