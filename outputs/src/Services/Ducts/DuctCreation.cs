using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;
namespace IFCInfo
{
    internal static class DuctCreation
    {
        private static readonly Guid TrackingId = new Guid("e01d5292-927b-4692-915f-0ca747bc2b83");
        private const double Tolerance = 0.002;
        internal static Dictionary<string,List<Duct>> TrackedDucts(Document doc)
        {
            var result=new Dictionary<string,List<Duct>>(); var schema=Schema.Lookup(TrackingId);
            if (schema==null) return result;
            foreach (Duct d in new FilteredElementCollector(doc).OfClass(typeof(Duct)))
            {
                var e=d.GetEntity(schema); if (!e.IsValid()) continue;
                string key=e.Get<string>(schema.GetField("SourceKey"));
                if (!result.ContainsKey(key)) result[key]=new List<Duct>(); result[key].Add(d);
            }
            return result;
        }
        internal static HashSet<string> ExistingKeys(Document doc) => new HashSet<string>(TrackedDucts(doc).Keys);
        internal static string SourceSystemName(Element duct)
        {
            var schema=Schema.Lookup(TrackingId); if (schema==null) return "";
            var entity=duct.GetEntity(schema); return entity.IsValid() ? entity.Get<string>(schema.GetField("SystemName")) : "";
        }
        public static void Execute(UIDocument ui, IFCInfoWindow window)
        {
            var request=window.DuctCreationRequest; if (request==null) return;
            var doc=ui.Document; string run=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+" | "+Guid.NewGuid().ToString();
            var rows=new List<DuctRunRow>(request.Skipped); var successful=new Dictionary<long,DuctPlanItem>();
            var existing=ExistingKeys(doc); bool rolledBack=false;
            using (var group=new TransactionGroup(doc,"IFC - Duct run"))
            {
                group.Start();
                try
                {
                    foreach (var item in request.Items)
                    {
                        var row=new DuctRunRow { RunId=run,SourceId=item.Source.ElementId,IfcGuid=item.Source.IfcGuid,IfcPsets=IfcPropertyStorage.Text(item.Source) }; rows.Add(row);
                        try
                        {
                            if (item.ExistingId==0 && existing.Contains(item.Key)) throw new InvalidOperationException("Nguồn đã được tạo trước đó.");
                            long id=0;
                            RunTransaction(doc,"IFC - "+item.Source.ElementId,()=>
                            {
                                var duct=CreateOrUpdate(doc,item,request); DuctRevision.Save(duct,item,run); id=duct.Id.Value;
                            });
                            existing.Add(item.Key); successful[id]=item; row.TargetId=id.ToString(); row.Success=true;
                            row.Status=item.ExistingId>0 ? "Đã cập nhật" : "Đã tạo";
                        }
                        catch (Exception ex) { row.Status="Lỗi"; row.Reason=ex.Message; if (!request.KeepSuccessful) throw; }
                    }
                    if (request.CreateFittings || request.ConnectTerminals) DuctConnections.Execute(doc,successful.Keys.ToList(),request,rows,run);
                    if (successful.Count>0)
                    {
                        try
                        {
                            RunTransaction(doc,"IFC - Save revision",()=>
                            {
                                foreach (var pair in successful) DuctRevision.Save((Duct)doc.GetElement(new ElementId(pair.Key)),pair.Value,run);
                            });
                        }
                        catch (Exception ex)
                        {
                            rows.Add(new DuctRunRow { Status="Lỗi lưu trạng thái cuối",Reason=ex.Message+" Các duct đổi sau khi nối sẽ bị cảnh báo khi cập nhật IFC lần sau." });
                            if (!request.KeepSuccessful) throw;
                        }
                    }
                    if (request.SaveSettings)
                    {
                        try { RunTransaction(doc,"IFC - Save mappings",()=>DuctProjectSettings.Save(doc,request)); }
                        catch (Exception ex) { rows.Add(new DuctRunRow { Status="Lỗi lưu cấu hình",Reason=ex.Message }); if (!request.KeepSuccessful) throw; }
                    }
                    if (group.Assimilate()!=TransactionStatus.Committed) throw new InvalidOperationException("Không hoàn tất transaction group.");
                }
                catch (Exception ex)
                {
                    if (group.GetStatus()==TransactionStatus.Started) group.RollBack(); rolledBack=true;
                    foreach (var row in rows.Where(r=>r.Success)) { row.Success=false; row.TargetId=""; row.Status="Đã hoàn tác"; row.Reason="Toàn bộ lượt đã hoàn tác: "+ex.Message; }
                    successful.Clear(); rows.Add(new DuctRunRow { Status="Lượt đã hoàn tác",Reason=ex.Message });
                }
            }
            foreach (var item in request.Items.Where(i=>!rows.Any(r=>r.SourceId==i.Source.ElementId)))
                rows.Add(new DuctRunRow { SourceId=item.Source.ElementId,IfcGuid=item.Source.IfcGuid,Status="Chưa thực hiện",Reason="Lượt đã dừng và hoàn tác." });
            foreach (var row in rows) row.RunId=run;
            var ids=successful.Keys.Select(id=>new ElementId(id)).ToList(); ui.Selection.SetElementIds(ids);
            var result=new DuctCreationResultWindow(rows,rolledBack,()=>ui.Selection.SetElementIds(ids));
            new System.Windows.Interop.WindowInteropHelper(result).Owner=ui.Application.MainWindowHandle; result.ShowDialog();
        }
        internal static void RunTransaction(Document doc,string name,Action action)
        {
            using (var t=new Transaction(doc,name))
            {
                t.Start(); var failures=new RollbackErrors();
                t.SetFailureHandlingOptions(t.GetFailureHandlingOptions().SetFailuresPreprocessor(failures).SetClearAfterRollback(true));
                try { action(); if (t.Commit()!=TransactionStatus.Committed) throw new InvalidOperationException("Revit từ chối thay đổi. "+failures.Message); }
                catch { if (t.GetStatus()==TransactionStatus.Started) t.RollBack(); throw; }
            }
        }
        private static Duct CreateOrUpdate(Document doc,DuctPlanItem item,DuctRequest request)
        {
            var others=new FilteredElementCollector(doc).OfClass(typeof(Duct)).Cast<Duct>().Where(d=>d.Id.Value!=item.ExistingId)
                .Select(d=>new { Duct=d,Line=(d.Location as LocationCurve)?.Curve as Line }).Where(d=>d.Line!=null)
                .Select(d=>new DuctCoverage.Segment { Id=d.Duct.Id.Value,
                    Start=new[] { d.Line.GetEndPoint(0).X,d.Line.GetEndPoint(0).Y,d.Line.GetEndPoint(0).Z },
                    End=new[] { d.Line.GetEndPoint(1).X,d.Line.GetEndPoint(1).Y,d.Line.GetEndPoint(1).Z } });
            var overlap=DuctCoverage.Check(new[] { item.Start.X,item.Start.Y,item.Start.Z },new[] { item.End.X,item.End.Y,item.End.Z },others,1.0/304.8);
            if (overlap.Ids.Count>0) throw new InvalidOperationException("Đường tim chồng duct ID "+string.Join(",",overlap.Ids)+"; không tạo/cập nhật chồng ống.");
            string key=DuctRequest.SystemKey(item.Round,item.Source.SystemType);
            var system=new ElementId(request.SystemTypes[key]); var type=new ElementId(item.Round?request.RoundTypeId:request.RectangularTypeId);
            var levels=new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l=>l.ProjectElevation).ToList();
            long mapped;
            var level=request.Levels.TryGetValue(item.LevelKey??"Không có Level nguồn",out mapped) && mapped>0 ? doc.GetElement(new ElementId(mapped)) as Level
                : levels.LastOrDefault(l=>l.ProjectElevation<=Math.Min(item.Start.Z,item.End.Z)+Tolerance)??levels.FirstOrDefault();
            if (level==null) throw new InvalidOperationException("Level đích không tồn tại.");
            Duct duct;
            if (item.ExistingId>0)
            {
                duct=doc.GetElement(new ElementId(item.ExistingId)) as Duct;
                if (duct==null) throw new InvalidOperationException("Duct cần cập nhật đã bị xóa.");
                if (!DuctRevision.Equal(DuctRevision.Get(duct,"Actual"),DuctRevision.Actual(duct))) throw new InvalidOperationException("Duct đã sửa trong Revit; không ghi đè.");
                if (duct.Pinned || DuctRevision.Connected(duct)) throw new InvalidOperationException("Duct đang ghim hoặc nối mạng; không cập nhật tự động.");
                duct.ChangeTypeId(type); SetId(duct,BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM,system); SetId(duct,BuiltInParameter.RBS_START_LEVEL_PARAM,level.Id);
                ((LocationCurve)duct.Location).Curve=Line.CreateBound(item.Start,item.End);
            }
            else duct=Duct.Create(doc,system,type,level.Id,item.Start,item.End);
            long workset;
            if (doc.IsWorkshared && request.Worksets.TryGetValue(key,out workset) && workset>0)
            {
                var p=duct.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                if (p==null || p.IsReadOnly || !p.Set((int)workset)) throw new InvalidOperationException("Không đặt được Workset.");
            }
            if (item.Round) SetSize(duct,BuiltInParameter.RBS_CURVE_DIAMETER_PARAM,item.Diameter);
            else
            {
                SetSize(duct,BuiltInParameter.RBS_CURVE_WIDTH_PARAM,item.Width); SetSize(duct,BuiltInParameter.RBS_CURVE_HEIGHT_PARAM,item.Height); doc.Regenerate();
                var c=duct.ConnectorManager.Connectors.Cast<Connector>().First(x=>x.ConnectorType==ConnectorType.End);
                var axis=(item.End-item.Start).Normalize(); var current=c.CoordinateSystem.BasisX;
                double angle=Math.Atan2(axis.DotProduct(current.CrossProduct(item.WidthAxis)),current.DotProduct(item.WidthAxis));
                ElementTransformUtils.RotateElement(doc,duct.Id,Line.CreateUnbound(item.Start,axis),angle);
            }
            doc.Regenerate(); var line=(duct.Location as LocationCurve)?.Curve as Line;
            if (line==null || !((line.GetEndPoint(0).DistanceTo(item.Start)<Tolerance && line.GetEndPoint(1).DistanceTo(item.End)<Tolerance) ||
                (line.GetEndPoint(1).DistanceTo(item.Start)<Tolerance && line.GetEndPoint(0).DistanceTo(item.End)<Tolerance))) throw new InvalidOperationException("Đường tim không khớp nguồn IFC.");
            if (!item.Round && Math.Abs(duct.ConnectorManager.Connectors.Cast<Connector>().First(c=>c.ConnectorType==ConnectorType.End).CoordinateSystem.BasisX.DotProduct(item.WidthAxis))<1-1e-6)
                throw new InvalidOperationException("Góc tiết diện không khớp IFC.");
            if (duct.GetTypeId()!=type || duct.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM)?.AsElementId()!=system) throw new InvalidOperationException("Duct Type hoặc System Type không khớp ánh xạ.");
            if (duct.ReferenceLevel?.Id!=level.Id) throw new InvalidOperationException("Level không khớp ánh xạ.");
            if (doc.IsWorkshared && request.Worksets.TryGetValue(key,out workset) && workset>0 && duct.WorksetId.IntegerValue!=workset)
                throw new InvalidOperationException("Workset không khớp ánh xạ.");
            if (item.Round ? !DuctGeometryReader.Near(duct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM).AsDouble(),item.Diameter) :
                !DuctGeometryReader.Near(duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM).AsDouble(),item.Width) ||
                !DuctGeometryReader.Near(duct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM).AsDouble(),item.Height))
                throw new InvalidOperationException("Kích thước sau regenerate không khớp IFC.");
            RecordSource(duct,item.Key,item.Source,system.Value);
            IfcPropertyStorage.Save(duct,item.Source);
            var comments=duct.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (comments!=null && !comments.IsReadOnly) comments.Set("IFC GUID: "+item.Source.IfcGuid+" | System Name: "+item.Source.SystemName+" | System Type: "+item.Source.SystemType);
            return duct;
        }
        internal static void RecordSource(Duct duct,string key,AirTerminalRow row,long system)
        {
            var schema=Schema.Lookup(TrackingId);
            if (schema==null)
            {
                var b=new SchemaBuilder(TrackingId); b.SetSchemaName("IFCInfoDuctSource");
                foreach (var f in new[] { "SourceKey","SystemName","SystemType" }) b.AddSimpleField(f,typeof(string)); schema=b.Finish();
            }
            var e=new Entity(schema); e.Set(schema.GetField("SourceKey"),key); e.Set(schema.GetField("SystemName"),row.SystemName??""); e.Set(schema.GetField("SystemType"),row.SystemType??""); duct.SetEntity(e);
            DuctExistenceChecker.RecordMapping(duct,key,row.SystemType,system);
        }
        private static void SetId(Duct d,BuiltInParameter name,ElementId value)
        {
            var p=d.get_Parameter(name); if (p?.AsElementId()==value) return;
            if (p==null || p.IsReadOnly || !p.Set(value)) throw new InvalidOperationException("Không đặt được "+name);
        }
        private static void SetSize(Duct d,BuiltInParameter name,double value)
        {
            var p=d.get_Parameter(name); if (p==null || p.IsReadOnly || !p.Set(value) || !DuctGeometryReader.Near(p.AsDouble(),value)) throw new InvalidOperationException("Không đặt được kích thước theo IFC.");
        }
        private sealed class RollbackErrors : IFailuresPreprocessor
        {
            public string Message;
            public FailureProcessingResult PreprocessFailures(FailuresAccessor a)
            {
                var errors=a.GetFailureMessages().Where(m=>m.GetSeverity()==FailureSeverity.Error).ToList(); Message=string.Join("; ",errors.Select(m=>m.GetDescriptionText()));
                return errors.Count>0 ? FailureProcessingResult.ProceedWithRollBack : FailureProcessingResult.Continue;
            }
        }
    }
}
