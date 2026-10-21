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
                var element=doc.GetElement(ElementIds.Create(Owner));
                var manager=(element as Duct)?.ConnectorManager ?? (element as FamilyInstance)?.MEPModel?.ConnectorManager;
                return manager?.Connectors.Cast<Connector>().FirstOrDefault(c=>c.Id==Port);
            }
        }
        private static List<End> Ends(Element e)
        {
            var manager=(e as Duct)?.ConnectorManager ?? (e as FamilyInstance)?.MEPModel?.ConnectorManager;
            return manager==null ? new List<End>() : manager.Connectors.Cast<Connector>()
                .Where(c=>c.Domain==Domain.DomainHvac && c.ConnectorType==ConnectorType.End && !c.IsConnected)
                .Select(c=>new End { Owner=e.Id.Number(),Port=c.Id,Point=c.Origin }).ToList();
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
            var ends=ids.SelectMany(id=>Ends(doc.GetElement(ElementIds.Create(id)))).ToList();
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
                            throw new InvalidOperationException("The connection node is ambiguous; skipped for manual review.");
                        var ports=cluster.Select(e=>e.Get(doc)).ToList();
                        if (ports.Any(c=>c==null || c.IsConnected)) throw new InvalidOperationException("The connector has changed or is already connected.");
                        if (cluster.Select(e=>doc.GetElement(ElementIds.Create(e.Owner)).get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM)?.AsElementId().Number()).Distinct().Count()!=1)
                            throw new InvalidOperationException("Ducts have different system types; automatic connection is disabled.");
                        if (cluster.Select(e=>DuctCreation.SourceSystemName(doc.GetElement(ElementIds.Create(e.Owner)))).Distinct().Count()!=1)
                            throw new InvalidOperationException("Ducts have different IFC System Names; separate source networks will not be connected.");
                        FamilyInstance fitting=null;
                        if (ports.Count==3)
                        {
                            int a=-1,b=-1;
                            for (int i=0;i<3;i++) for (int j=i+1;j<3;j++)
                                if (ports[i].CoordinateSystem.BasisZ.DotProduct(ports[j].CoordinateSystem.BasisZ)<-0.9999) { a=i; b=j; }
                            if (a<0) throw new InvalidOperationException("A tee requires two aligned duct ends and one branch.");
                            fitting=doc.Create.NewTeeFitting(ports[a],ports[b],ports[3-a-b]);
                        }
                        else
                        {
                            double dot=ports[0].CoordinateSystem.BasisZ.DotProduct(ports[1].CoordinateSystem.BasisZ);
                            if (dot>0.9999) throw new InvalidOperationException("Both connectors point in the same direction; connection skipped.");
                            if (dot < -0.9999 && SameSize(ports[0],ports[1])) ports[0].ConnectTo(ports[1]);
                            else if (dot < -0.9999) fitting=doc.Create.NewTransitionFitting(ports[0],ports[1]);
                            else fitting=doc.Create.NewElbowFitting(ports[0],ports[1]);
                        }
                        doc.Regenerate();
                        if (cluster.Any(e=>e.Get(doc)==null || !e.Get(doc).IsConnected)) throw new InvalidOperationException("The fitting has unconnected connectors.");
                        return fitting?.Id.Number().ToString() ?? string.Join(",",cluster.Select(e=>e.Owner));
                    });
                }
                if (request.FittingGapMm>1)
                {
                    var remaining=ids.SelectMany(id=>Ends(doc.GetElement(ElementIds.Create(id)))).ToList();
                    var used=new HashSet<End>();
                    foreach (var end in remaining)
                    {
                        if (!used.Add(end)) continue;
                        var candidates=remaining.Where(e=>e!=end && CanBridge(doc,end,e,request.FittingGapMm/304.8)).ToList();
                        if (candidates.Count==0) continue;
                        Attempt(doc,new List<End> { end },rows,run,request,()=>
                        {
                            if (candidates.Count!=1 || remaining.Count(e=>e!=candidates[0] && CanBridge(doc,candidates[0],e,request.FittingGapMm/304.8))!=1)
                                throw new InvalidOperationException("Multiple connections across the gap are possible; automatic selection is disabled.");
                            var other=candidates[0]; used.Add(other);
                            var a=end.Get(doc); var b=other.Get(doc);
                            if (DuctCreation.SourceSystemName(doc.GetElement(ElementIds.Create(end.Owner)))!=DuctCreation.SourceSystemName(doc.GetElement(ElementIds.Create(other.Owner))))
                                throw new InvalidOperationException("Ducts have different IFC System Names.");
                            if (doc.GetElement(ElementIds.Create(end.Owner)).get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM)?.AsElementId()!=
                                doc.GetElement(ElementIds.Create(other.Owner)).get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM)?.AsElementId())
                                throw new InvalidOperationException("Ducts have different system types.");
                            double dot=a.CoordinateSystem.BasisZ.DotProduct(b.CoordinateSystem.BasisZ);
                            if (dot < -0.9999 && SameSize(a,b)) throw new InvalidOperationException("A straight gap with the same profile requires a duct segment rather than a fitting.");
                            var fitting=dot < -0.9999 ? doc.Create.NewTransitionFitting(a,b) : doc.Create.NewElbowFitting(a,b);
                            doc.Regenerate();
                            if (!end.Get(doc).IsConnected || !other.Get(doc).IsConnected) throw new InvalidOperationException("The fitting does not connect all duct ends.");
                            return fitting.Id.Number().ToString();
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
                        if (matches.Count!=1) throw new InvalidOperationException("Multiple air terminal connectors were found at the duct end.");
                        var terminal=matches[0].Get(doc); var current=end.Get(doc);
                        if (terminal==null || terminal.IsConnected || current==null || current.IsConnected || !SameSize(current,terminal) ||
                            current.DuctSystemType!=terminal.DuctSystemType || current.CoordinateSystem.BasisZ.DotProduct(terminal.CoordinateSystem.BasisZ)>-0.9999)
                            throw new InvalidOperationException("Air terminal profile, direction or system classification does not match, or it is already connected.");
                        current.ConnectTo(terminal); doc.Regenerate();
                        if (!current.IsConnectedTo(terminal)) throw new InvalidOperationException("Could not verify the air terminal connection.");
                        return matches[0].Owner.ToString();
                    });
                }
            }
            foreach (long id in ids)
            {
                int open=Ends(doc.GetElement(ElementIds.Create(id))).Count;
                if (open>0) rows.Add(new DuctRunRow { RunId=run,TargetId=id.ToString(),Status="Open duct end",
                    Reason=open+" unconnected connectors. Gaps are not bridged, ducts are not split and IFC fittings are not modified automatically." });
            }
        }
        private static void Attempt(Document doc,List<End> ends,List<DuctRunRow> rows,string run,DuctRequest request,Func<string> action)
        {
            var row=new DuctRunRow { RunId=run,SourceId="Duct: "+string.Join(",",ends.Select(e=>e.Owner)),Status="Connect connectors" }; rows.Add(row);
            try
            {
                string id=null; DuctCreation.RunTransaction(doc,"IFC - Connect",()=>id=action());
                row.TargetId=id; row.Success=true; row.Status="Connected";
            }
            catch (Exception ex)
            {
                row.Status="Connection error"; row.Reason=ex.Message+" Check Routing Preferences and fitting families.";
                if (!request.KeepSuccessful) throw;
            }
        }
    }
}
