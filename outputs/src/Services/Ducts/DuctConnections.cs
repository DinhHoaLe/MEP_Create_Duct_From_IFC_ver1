using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;

namespace IFCInfo
{
    internal static class DuctConnections
    {
        private const double Tolerance = 1.0 / 304.8;
        private sealed class End
        {
            public long Owner; public int Port; public XYZ Point;
            public Connector Get(Document doc)
            {
                var element=doc.GetElement(new ElementId(Owner));
                var manager=(element as Duct)?.ConnectorManager ?? (element as FamilyInstance)?.MEPModel?.ConnectorManager;
                return manager?.Connectors.Cast<Connector>().FirstOrDefault(c=>c.Id==Port);
            }
        }
        private static List<End> Ends(Element e)
        {
            var manager=(e as Duct)?.ConnectorManager ?? (e as FamilyInstance)?.MEPModel?.ConnectorManager;
            return manager==null ? new List<End>() : manager.Connectors.Cast<Connector>()
                .Where(c=>c.Domain==Domain.DomainHvac && c.ConnectorType==ConnectorType.End && !c.IsConnected)
                .Select(c=>new End { Owner=e.Id.Value,Port=c.Id,Point=c.Origin }).ToList();
        }
        private static bool SameSize(Connector a,Connector b) => a.Shape==b.Shape &&
            (a.Shape==ConnectorProfileType.Round ? Math.Abs(a.Radius-b.Radius)<Tolerance/2 :
             a.Shape==ConnectorProfileType.Rectangular && Math.Abs(a.Width-b.Width)<Tolerance && Math.Abs(a.Height-b.Height)<Tolerance);
        private static double[] Coordinates(XYZ p) => new[] { p.X,p.Y,p.Z };
        private static bool CanBridge(Document doc,End a,End b,double limit)
        {
            if (a.Owner==b.Owner) return false;
            var x=a.Get(doc); var y=b.Get(doc);
            return x!=null && y!=null && !x.IsConnected && !y.IsConnected &&
                DuctConnectionGeometry.CanBridge(Coordinates(x.Origin),Coordinates(x.CoordinateSystem.BasisZ),Coordinates(y.Origin),Coordinates(y.CoordinateSystem.BasisZ),limit,Tolerance);
        }
        internal static void Execute(Document doc,List<long> ids,DuctRequest request,List<DuctRunRow> rows,string run)
        {
            var ends=ids.SelectMany(id=>Ends(doc.GetElement(new ElementId(id)))).ToList();
            if (request.CreateFittings)
            {
                var visited=new HashSet<End>();
                foreach (var end in ends)
                {
                    if (!visited.Add(end)) continue;
                    var cluster=ends.Where(e=>e!=end && e.Point.DistanceTo(end.Point)<=Tolerance).ToList(); cluster.Insert(0,end);
                    foreach (var e in cluster) visited.Add(e);
                    if (cluster.Count==1) continue;
                    Attempt(doc,cluster,rows,run,request,()=>
                    {
                        if (cluster.Count>3 || cluster.Select(e=>e.Owner).Distinct().Count()!=cluster.Count ||
                            cluster.Any(a=>cluster.Any(b=>a.Point.DistanceTo(b.Point)>Tolerance)) ||
                            ends.Any(e=>!cluster.Contains(e) && cluster.Any(c=>c.Point.DistanceTo(e.Point)<=Tolerance)))
                            throw new InvalidOperationException("Nút nối không duy nhất; bỏ qua để kiểm tra thủ công.");
                        var ports=cluster.Select(e=>e.Get(doc)).ToList();
                        if (ports.Any(c=>c==null || c.IsConnected)) throw new InvalidOperationException("Connector đã thay đổi hoặc đã nối.");
                        if (cluster.Select(e=>doc.GetElement(new ElementId(e.Owner)).get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM)?.AsElementId().Value).Distinct().Count()!=1)
                            throw new InvalidOperationException("Các duct khác System Type; không nối tự động.");
                        if (cluster.Select(e=>DuctCreation.SourceSystemName(doc.GetElement(new ElementId(e.Owner)))).Distinct().Count()!=1)
                            throw new InvalidOperationException("Các duct khác System Name IFC; không nối hai mạng nguồn.");
                        FamilyInstance fitting=null;
                        if (ports.Count==3)
                        {
                            int a=-1,b=-1;
                            for (int i=0;i<3;i++) for (int j=i+1;j<3;j++)
                                if (ports[i].CoordinateSystem.BasisZ.DotProduct(ports[j].CoordinateSystem.BasisZ)<-0.9999) { a=i; b=j; }
                            if (a<0) throw new InvalidOperationException("Tee cần hai đầu ống thẳng hàng và một nhánh.");
                            fitting=doc.Create.NewTeeFitting(ports[a],ports[b],ports[3-a-b]);
                        }
                        else
                        {
                            double dot=ports[0].CoordinateSystem.BasisZ.DotProduct(ports[1].CoordinateSystem.BasisZ);
                            if (dot>0.9999) throw new InvalidOperationException("Hai đầu connector cùng hướng; không nối.");
                            if (dot < -0.9999 && SameSize(ports[0],ports[1])) ports[0].ConnectTo(ports[1]);
                            else if (dot < -0.9999) fitting=doc.Create.NewTransitionFitting(ports[0],ports[1]);
                            else fitting=doc.Create.NewElbowFitting(ports[0],ports[1]);
                        }
                        doc.Regenerate();
                        if (cluster.Any(e=>e.Get(doc)==null || !e.Get(doc).IsConnected)) throw new InvalidOperationException("Fitting chưa nối đủ các connector.");
                        return fitting?.Id.Value.ToString() ?? string.Join(",",cluster.Select(e=>e.Owner));
                    });
                }
                if (request.FittingGapMm>1)
                {
                    var remaining=ids.SelectMany(id=>Ends(doc.GetElement(new ElementId(id)))).ToList();
                    var used=new HashSet<End>();
                    foreach (var end in remaining)
                    {
                        if (!used.Add(end)) continue;
                        var candidates=remaining.Where(e=>e!=end && CanBridge(doc,end,e,request.FittingGapMm/304.8)).ToList();
                        if (candidates.Count==0) continue;
                        Attempt(doc,new List<End> { end },rows,run,request,()=>
                        {
                            if (candidates.Count!=1 || remaining.Count(e=>e!=candidates[0] && CanBridge(doc,candidates[0],e,request.FittingGapMm/304.8))!=1)
                                throw new InvalidOperationException("Có nhiều cách nối qua khoảng hở; không tự chọn.");
                            var other=candidates[0]; used.Add(other);
                            var a=end.Get(doc); var b=other.Get(doc);
                            if (DuctCreation.SourceSystemName(doc.GetElement(new ElementId(end.Owner)))!=DuctCreation.SourceSystemName(doc.GetElement(new ElementId(other.Owner))))
                                throw new InvalidOperationException("Các duct khác System Name IFC.");
                            if (doc.GetElement(new ElementId(end.Owner)).get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM)?.AsElementId()!=
                                doc.GetElement(new ElementId(other.Owner)).get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM)?.AsElementId())
                                throw new InvalidOperationException("Các duct khác System Type.");
                            double dot=a.CoordinateSystem.BasisZ.DotProduct(b.CoordinateSystem.BasisZ);
                            if (dot < -0.9999 && SameSize(a,b)) throw new InvalidOperationException("Khoảng hở thẳng cùng tiết diện cần thêm đoạn ống, không dùng fitting.");
                            var fitting=dot < -0.9999 ? doc.Create.NewTransitionFitting(a,b) : doc.Create.NewElbowFitting(a,b);
                            doc.Regenerate();
                            if (!end.Get(doc).IsConnected || !other.Get(doc).IsConnected) throw new InvalidOperationException("Fitting chưa nối đủ đầu ống.");
                            return fitting.Id.Value.ToString();
                        });
                    }
                }
            }
            if (request.ConnectTerminals)
            {
                var terminals=new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DuctTerminal).WhereElementIsNotElementType()
                    .SelectMany(Ends).ToList();
                foreach (var end in ends)
                {
                    var port=end.Get(doc); if (port==null || port.IsConnected) continue;
                    var matches=terminals.Where(t=>t.Point.DistanceTo(port.Origin)<=Tolerance).ToList(); if (matches.Count==0) continue;
                    Attempt(doc,new List<End> { end },rows,run,request,()=>
                    {
                        if (matches.Count!=1) throw new InvalidOperationException("Có nhiều connector miệng gió tại đầu ống.");
                        var terminal=matches[0].Get(doc); var current=end.Get(doc);
                        if (terminal==null || terminal.IsConnected || current==null || current.IsConnected || !SameSize(current,terminal) ||
                            current.DuctSystemType!=terminal.DuctSystemType || current.CoordinateSystem.BasisZ.DotProduct(terminal.CoordinateSystem.BasisZ)>-0.9999)
                            throw new InvalidOperationException("Miệng gió không khớp tiết diện, hướng, phân loại hệ thống hoặc đã nối.");
                        current.ConnectTo(terminal); doc.Regenerate();
                        if (!current.IsConnectedTo(terminal)) throw new InvalidOperationException("Không xác nhận được kết nối miệng gió.");
                        return matches[0].Owner.ToString();
                    });
                }
            }
            foreach (long id in ids)
            {
                int open=Ends(doc.GetElement(new ElementId(id))).Count;
                if (open>0) rows.Add(new DuctRunRow { RunId=run,TargetId=id.ToString(),Status="Đầu ống còn hở",
                    Reason=open+" connector chưa nối. Không tự bắc cầu qua khoảng trống, chia ống hoặc sửa IFC fitting." });
            }
        }
        private static void Attempt(Document doc,List<End> ends,List<DuctRunRow> rows,string run,DuctRequest request,Func<string> action)
        {
            var row=new DuctRunRow { RunId=run,SourceId="Duct: "+string.Join(",",ends.Select(e=>e.Owner)),Status="Nối connector" }; rows.Add(row);
            try
            {
                string id=null; DuctCreation.RunTransaction(doc,"IFC - Connect",()=>id=action());
                row.TargetId=id; row.Success=true; row.Status="Đã nối";
            }
            catch (Exception ex)
            {
                row.Status="Lỗi nối"; row.Reason=ex.Message+" Kiểm tra Routing Preferences và family fitting.";
                if (!request.KeepSuccessful) throw;
            }
        }
    }
}
