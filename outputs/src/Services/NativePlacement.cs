using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
namespace IFCInfo
{
    internal static class NativePlacement
    {
        // Reuse the previous family source schema to preserve duplicate protection.
        private static readonly Guid Tracking=new Guid("40333d70-0718-419c-8588-c342475082c6");
        internal static void Execute(UIDocument ui,RevitLinkInstance link,IFCInfoWindow window)
        {
            var request=window.Replacement; if (request==null) return;
            var doc=ui.Document; var linked=link.GetLinkDocument();
            if (linked==null || doc.IsReadOnly || doc.IsFamilyDocument) throw new InvalidOperationException("Cần project có thể sửa và link đã load.");
            var type=doc.GetElement(new ElementId(request.TypeId)) as ElementType;
            var level=doc.GetElement(new ElementId(request.LevelId)) as Level;
            if (type==null || level==null) throw new InvalidOperationException("Type/Level không còn tồn tại.");
            var symbol=type as FamilySymbol;
            Reference hostReference=null; PlanarFace hostFace=null; Element host=null;
            if (symbol!=null && (symbol.Family.FamilyPlacementType==FamilyPlacementType.OneLevelBasedHosted || symbol.Family.FamilyPlacementType==FamilyPlacementType.WorkPlaneBased))
            {
                hostReference=ui.Selection.PickObject(ObjectType.Face,new HostFilter(doc),"Chọn mặt host phẳng trong model chính cho lượt đặt này");
                host=doc.GetElement(hostReference.ElementId); hostFace=host.GetGeometryObjectFromReference(hostReference) as PlanarFace;
                if (hostFace==null) throw new InvalidOperationException("Host phải là mặt phẳng.");
            }
            var schema=Schema.Lookup(Tracking); var existing=new HashSet<string>(DuctCreation.ExistingKeys(doc));
            if (schema!=null)
                foreach (var e in new FilteredElementCollector(doc).WhereElementIsNotElementType())
                {
                    var data=e.GetEntity(schema); if (data.IsValid()) existing.Add(data.Get<string>(schema.GetField("SourceKey")));
                }
            var reports=new List<DuctRunRow>(); var created=new List<ElementId>();
            string run=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+" | "+Guid.NewGuid(); bool rolledBack=false;
            using (var group=new TransactionGroup(doc,"IFC - Place native elements"))
            {
                group.Start();
                try
                {
                    foreach (string id in request.SourceIds.Distinct())
                    {
                        var row=window.AirTerminals.First(r=>r.ElementId==id);
                        var report=new DuctRunRow { RunId=run,SourceId=id,IfcGuid=row.IfcGuid,IfcPsets=IfcPropertyStorage.Text(row) }; reports.Add(report);
                        try
                        {
                            var source=linked.GetElement(new ElementId(long.Parse(id)));
                            if (source==null) throw new InvalidOperationException("Không tìm thấy nguồn.");
                            string key=link.UniqueId+"|"+(string.IsNullOrWhiteSpace(row.IfcGuid)?source.UniqueId:row.IfcGuid);
                            if (existing.Contains(key)) { report.Status="Bỏ qua"; report.Reason="Nguồn đã được tool tạo trước đó."; continue; }
                            var adaptivePoints=new List<XYZ>();
                            if (symbol!=null && symbol.Family.FamilyPlacementType==FamilyPlacementType.Adaptive)
                            {
                                int count=AdaptiveComponentFamilyUtils.GetNumberOfPlacementPoints(symbol.Family);
                                if (count<1) throw new InvalidOperationException("Adaptive family không có điểm đặt.");
                                for (int i=0;i<count;i++) adaptivePoints.Add(ui.Selection.PickPoint("Nguồn "+id+": chọn điểm adaptive "+(i+1)+"/"+count));
                            }
                            else if (symbol!=null && (symbol.Family.FamilyPlacementType==FamilyPlacementType.CurveBased || symbol.Family.FamilyPlacementType==FamilyPlacementType.CurveDrivenStructural) && !(source.Location is LocationCurve))
                            {
                                try { Path(source,link.GetTotalTransform(),row); }
                                catch (Exception)
                                {
                                    TaskDialog.Show("Đường đặt family","Nguồn "+id+" không có đường tim đọc được. Chọn 2 điểm trong model để đặt family theo đường; Esc hủy lượt.");
                                    adaptivePoints.Add(ui.Selection.PickPoint("Nguồn "+id+": điểm đầu family"));
                                    adaptivePoints.Add(ui.Selection.PickPoint("Nguồn "+id+": điểm cuối family"));
                                }
                            }
                            Element placed=null;
                            DuctCreation.RunTransaction(doc,"IFC - Place "+id,()=>
                            {
                                if (symbol!=null)
                                    placed=CreateFamily(doc,source,link.GetTotalTransform(),row,symbol,level,request,hostReference,hostFace,host,adaptivePoints);
                                else if (request.Kind=="Wall" || request.Kind=="Floor" || request.Kind=="Roof")
                                    placed=NativeBuildingPlacement.Create(doc,source,link.GetTotalTransform(),type,level,request.Kind);
                                else placed=CreateCurve(doc,source,link.GetTotalTransform(),row,type,level,request);
                                if (placed==null) throw new InvalidOperationException("Revit không trả về phần tử mới.");
                                if (placed.Category?.Id!=type.Category?.Id) throw new InvalidOperationException("Category kết quả không khớp type đã chọn.");
                                SaveTracking(placed,key,row); IfcPropertyStorage.Save(placed,row);
                                var comments=placed.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                                if (comments!=null && !comments.IsReadOnly) comments.Set("IFC GUID: "+row.IfcGuid+" | System Name: "+row.SystemName+" | System Type: "+row.SystemType);
                                if (placed is Duct duct)
                                {
                                    DuctCreation.RecordSource(duct,key,row,request.SystemTypeId);
                                    var snapshot=Path(source,link.GetTotalTransform(),row); snapshot.Source=row; snapshot.Key=key;
                                    DuctRevision.Save(duct,snapshot,run);
                                }
                            });
                            existing.Add(key); created.Add(placed.Id); report.TargetId=placed.Id.Value.ToString(); report.Success=true; report.Status="Đã đặt";
                            report.Reason=request.Kind+" · "+type.Name+(symbol!=null ? " · Family dùng kích thước Type đã chọn; Pset là dữ liệu nguồn." : " · Native theo hình học nguồn.");
                            if (adaptivePoints.Count>0) report.Reason+=" Điểm điều khiển do người dùng chọn trong Revit.";
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException) { report.Status="Đã hủy"; throw; }
                        catch (Exception ex) { report.Status="Lỗi"; report.Reason=ex.Message; if (!request.KeepSuccessful) throw; }
                    }
                    if (group.Assimilate()!=TransactionStatus.Committed) throw new InvalidOperationException("Không hoàn tất lượt đặt.");
                }
                catch (Exception ex)
                {
                    if (group.GetStatus()==TransactionStatus.Started) group.RollBack(); rolledBack=true; created.Clear();
                    foreach (var row in reports.Where(r=>r.Success)) { row.Success=false; row.TargetId=""; row.Status="Đã hoàn tác"; row.Reason=ex.Message; }
                    reports.Add(new DuctRunRow { RunId=run,Status="Lượt đã hoàn tác",Reason=ex.Message });
                }
            }
            foreach (string id in request.SourceIds.Where(id=>!reports.Any(r=>r.SourceId==id)))
            {
                var row=window.AirTerminals.First(r=>r.ElementId==id);
                reports.Add(new DuctRunRow { RunId=run,SourceId=id,IfcGuid=row.IfcGuid,Status="Chưa thực hiện",Reason="Lượt đã dừng.",IfcPsets=IfcPropertyStorage.Text(row) });
            }
            ui.Selection.SetElementIds(created);
            var result=new DuctCreationResultWindow(reports,rolledBack,()=>ui.Selection.SetElementIds(created),"Kết quả đặt phần tử native");
            new System.Windows.Interop.WindowInteropHelper(result).Owner=ui.Application.MainWindowHandle; result.ShowDialog();
        }
        private static DuctPlanItem Path(Element source,Transform transform,AirTerminalRow row)
        {
            var item=DuctGeometryReader.Read(source,row.DuctSource);
            item.Start=transform.OfPoint(item.Start); item.End=transform.OfPoint(item.End); item.WidthAxis=transform.OfVector(item.WidthAxis).Normalize();
            return item;
        }
        private static Element CreateCurve(Document doc,Element source,Transform transform,AirTerminalRow row,ElementType type,Level level,ReplacementRequest request)
        {
            var path=Path(source,transform,row);
            if (request.LengthMm>0) path.End=path.Start+(path.End-path.Start).Normalize()*(request.LengthMm/304.8);
            if (path.Start.DistanceTo(path.End)<Math.Max(doc.Application.ShortCurveTolerance,1.0/120)) throw new InvalidOperationException("Tuyến quá ngắn.");
            var existing=new FilteredElementCollector(doc).OfClass(typeof(MEPCurve)).Cast<MEPCurve>()
                .Where(c=>c.Category?.Id==type.Category.Id).Select(c=>new { Curve=c,Line=(c.Location as LocationCurve)?.Curve as Line })
                .Where(c=>c.Line!=null).Select(c=>new DuctCoverage.Segment { Id=c.Curve.Id.Value,
                    Start=new[] { c.Line.GetEndPoint(0).X,c.Line.GetEndPoint(0).Y,c.Line.GetEndPoint(0).Z },End=new[] { c.Line.GetEndPoint(1).X,c.Line.GetEndPoint(1).Y,c.Line.GetEndPoint(1).Z } });
            var overlap=DuctCoverage.Check(new[] { path.Start.X,path.Start.Y,path.Start.Z },new[] { path.End.X,path.End.Y,path.End.Z },existing,1.0/304.8);
            if (overlap.Ids.Count>0) throw new InvalidOperationException("Đường tim chồng phần tử cùng category: "+string.Join(",",overlap.Ids));
            MEPCurve curve;
            switch (request.Kind)
            {
                case "Duct":
                    var ductType=(DuctType)type;
                    if (ductType.Shape!=(path.Round?ConnectorProfileType.Round:ConnectorProfileType.Rectangular)) throw new InvalidOperationException("Tiết diện Duct Type không khớp nguồn.");
                    curve=Duct.Create(doc,new ElementId(request.SystemTypeId),type.Id,level.Id,path.Start,path.End); break;
                case "Pipe":
                    if (!path.Round) throw new InvalidOperationException("Pipe cần tiết diện tròn.");
                    curve=Pipe.Create(doc,new ElementId(request.SystemTypeId),type.Id,level.Id,path.Start,path.End); break;
                case "Conduit":
                    if (!path.Round) throw new InvalidOperationException("Conduit cần tiết diện tròn.");
                    curve=Conduit.Create(doc,type.Id,path.Start,path.End,level.Id); break;
                case "CableTray":
                    if (path.Round) throw new InvalidOperationException("Cable Tray cần tiết diện chữ nhật.");
                    curve=CableTray.Create(doc,type.Id,path.Start,path.End,level.Id); break;
                default: throw new InvalidOperationException("Category chưa có bộ dựng native.");
            }
            if (path.Round) SetDouble(curve,request.Kind=="Duct"?BuiltInParameter.RBS_CURVE_DIAMETER_PARAM:request.Kind=="Pipe"?BuiltInParameter.RBS_PIPE_DIAMETER_PARAM:BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM,path.Diameter);
            else
            {
                SetDouble(curve,request.Kind=="CableTray"?BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM:BuiltInParameter.RBS_CURVE_WIDTH_PARAM,path.Width);
                SetDouble(curve,request.Kind=="CableTray"?BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM:BuiltInParameter.RBS_CURVE_HEIGHT_PARAM,path.Height);
                doc.Regenerate(); var c=curve.ConnectorManager.Connectors.Cast<Connector>().First(x=>x.ConnectorType==ConnectorType.End);
                var axis=(path.End-path.Start).Normalize(); var xAxis=c.CoordinateSystem.BasisX;
                double angle=Math.Atan2(axis.DotProduct(xAxis.CrossProduct(path.WidthAxis)),xAxis.DotProduct(path.WidthAxis));
                ElementTransformUtils.RotateElement(doc,curve.Id,Line.CreateUnbound(path.Start,axis),angle);
            }
            doc.Regenerate();
            var actual=(curve.Location as LocationCurve)?.Curve as Line;
            if (actual==null || !((actual.GetEndPoint(0).DistanceTo(path.Start)<0.002 && actual.GetEndPoint(1).DistanceTo(path.End)<0.002) ||
                (actual.GetEndPoint(1).DistanceTo(path.Start)<0.002 && actual.GetEndPoint(0).DistanceTo(path.End)<0.002))) throw new InvalidOperationException("Đường tim native không khớp.");
            var connector=curve.ConnectorManager.Connectors.Cast<Connector>().First(c=>c.ConnectorType==ConnectorType.End);
            if (path.Round ? connector.Shape!=ConnectorProfileType.Round || Math.Abs(connector.Radius*2-path.Diameter)>0.002 :
                connector.Shape!=ConnectorProfileType.Rectangular || Math.Abs(connector.Width-path.Width)>0.002 || Math.Abs(connector.Height-path.Height)>0.002 ||
                Math.Abs(connector.CoordinateSystem.BasisX.DotProduct(path.WidthAxis))<1-1e-6)
                throw new InvalidOperationException("Kích thước/hướng tiết diện native không khớp sau khi tạo.");
            if (curve.GetTypeId()!=type.Id) throw new InvalidOperationException("Type native không khớp lựa chọn.");
            return curve;
        }
        private static FamilyInstance CreateFamily(Document doc,Element source,Transform transform,AirTerminalRow row,FamilySymbol symbol,Level level,ReplacementRequest request,
            Reference hostRef,PlanarFace face,Element host,List<XYZ> adaptivePoints)
        {
            if (!symbol.IsActive) { symbol.Activate(); doc.Regenerate(); }
            var placement=symbol.Family.FamilyPlacementType;
            if (placement==FamilyPlacementType.Adaptive)
            {
                var adaptive=AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance(doc,symbol);
                var points=AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(adaptive);
                if (points.Count!=adaptivePoints.Count) throw new InvalidOperationException("Số điểm adaptive không khớp.");
                for (int i=0;i<points.Count;i++) ((ReferencePoint)doc.GetElement(points[i])).Position=adaptivePoints[i];
                return adaptive;
            }
            var local=(source.Location as LocationPoint)?.Point;
            if (local==null)
            {
                var box=source.get_BoundingBox(null); if (box==null) throw new InvalidOperationException("Nguồn không có điểm đặt hoặc hình học.");
                local=box.Transform.OfPoint((box.Min+box.Max)*0.5);
            }
            var point=transform.OfPoint(local);
            double angle=request.RotationDegrees*Math.PI/180;
            FamilyInstance instance;
            var structure=symbol.Category.Id.Value==(long)BuiltInCategory.OST_StructuralFraming ? StructuralType.Beam :
                symbol.Category.Id.Value==(long)BuiltInCategory.OST_StructuralColumns ? StructuralType.Column : StructuralType.NonStructural;
            if (placement==FamilyPlacementType.CurveBased || placement==FamilyPlacementType.CurveDrivenStructural)
            {
                if (adaptivePoints.Count==2) return doc.Create.NewFamilyInstance(Line.CreateBound(adaptivePoints[0],adaptivePoints[1]),symbol,level,structure);
                var line=(source.Location as LocationCurve)?.Curve;
                if (line!=null) line=line.CreateTransformed(transform);
                else { var path=Path(source,transform,row); line=Line.CreateBound(path.Start,path.End); }
                return doc.Create.NewFamilyInstance(line,symbol,level,structure);
            }
            if (face!=null)
            {
                var projection=face.Project(point);
                if (projection==null || !face.IsInside(projection.UVPoint)) throw new InvalidOperationException("Vị trí nguồn nằm ngoài mặt host đã chọn.");
                point=projection.XYZPoint;
            }
            if (placement==FamilyPlacementType.WorkPlaneBased) return doc.Create.NewFamilyInstance(hostRef,point,Math.Cos(angle)*face.XVector+Math.Sin(angle)*face.YVector,symbol);
            if (placement==FamilyPlacementType.OneLevelBasedHosted) instance=doc.Create.NewFamilyInstance(point,symbol,host,level,structure);
            else if (placement==FamilyPlacementType.OneLevelBased || placement==FamilyPlacementType.TwoLevelsBased)
                instance=doc.Create.NewFamilyInstance(point,symbol,level,structure);
            else throw new InvalidOperationException("Kiểu đặt family chưa hỗ trợ: "+placement);
            doc.Regenerate();
            if (placement==FamilyPlacementType.TwoLevelsBased)
            {
                var top=doc.GetElement(new ElementId(request.TopLevelId)) as Level;
                if (top==null || top.ProjectElevation<=point.Z) throw new InvalidOperationException("Level trên phải cao hơn điểm đặt.");
                SetId(instance,BuiltInParameter.FAMILY_BASE_LEVEL_PARAM,level.Id); SetId(instance,BuiltInParameter.FAMILY_TOP_LEVEL_PARAM,top.Id);
                SetDouble(instance,BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM,point.Z-level.ProjectElevation);
                SetDouble(instance,BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM,0);
            }
            else if (placement==FamilyPlacementType.OneLevelBased && instance.Location is LocationPoint actual)
                ElementTransformUtils.MoveElement(doc,instance.Id,point-actual.Point);
            if (Math.Abs(angle)>1e-10) ElementTransformUtils.RotateElement(doc,instance.Id,Line.CreateUnbound(point,face?.FaceNormal??XYZ.BasisZ),angle);
            return instance;
        }
        private static void SetDouble(Element element,BuiltInParameter name,double value)
        {
            var p=element.get_Parameter(name); if (p==null || p.IsReadOnly || !p.Set(value) || Math.Abs(p.AsDouble()-value)>0.002) throw new InvalidOperationException("Không đặt được tham số "+name+" theo nguồn/type.");
        }
        private static void SetId(Element element,BuiltInParameter name,ElementId id)
        {
            var p=element.get_Parameter(name); if (p==null || p.IsReadOnly || !p.Set(id)) throw new InvalidOperationException("Không đặt được Level family.");
        }
        private static void SaveTracking(Element element,string key,AirTerminalRow row)
        {
            var schema=Schema.Lookup(Tracking);
            if (schema==null)
            {
                var builder=new SchemaBuilder(Tracking); builder.SetSchemaName("IFCInfoAirTerminalSource");
                foreach (var name in new[] { "SourceKey","IfcGuid","SourceSystemName","SourceSystemType" }) builder.AddSimpleField(name,typeof(string)); schema=builder.Finish();
            }
            var data=new Entity(schema); data.Set(schema.GetField("SourceKey"),key); data.Set(schema.GetField("IfcGuid"),row.IfcGuid??"");
            data.Set(schema.GetField("SourceSystemName"),row.SystemName??""); data.Set(schema.GetField("SourceSystemType"),row.SystemType??""); element.SetEntity(data);
        }
        private sealed class HostFilter : ISelectionFilter
        {
            private readonly Document doc; public HostFilter(Document doc) { this.doc=doc; }
            public bool AllowElement(Element e) => !(e is RevitLinkInstance);
            public bool AllowReference(Reference r,XYZ p) => doc.GetElement(r.ElementId)?.GetGeometryObjectFromReference(r) is PlanarFace;
        }
    }
}
