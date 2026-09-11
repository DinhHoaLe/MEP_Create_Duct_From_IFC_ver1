using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
namespace IFCInfo
{
    internal static class NativeBuildingPlacement
    {
        internal static IEnumerable<Solid> Solids(GeometryElement geometry)
        {
            if (geometry==null) yield break;
            foreach (GeometryObject obj in geometry)
            {
                if (obj is Solid s && s.Volume>1e-9) yield return s;
                if (obj is GeometryInstance instance) foreach (var child in Solids(instance.GetInstanceGeometry())) yield return child;
            }
        }
        internal static Solid SourceSolid(Element source,Transform transform)
        {
            var solids=Solids(source.get_Geometry(new Options { DetailLevel=ViewDetailLevel.Fine })).ToList();
            if (solids.Count!=1) throw new InvalidOperationException("Cần một solid nguồn duy nhất; tìm thấy "+solids.Count+".");
            return SolidUtils.CreateTransformed(solids[0],transform);
        }
        internal static Element Create(Document doc,Element source,Transform transform,ElementType type,Level level,string kind)
        {
            var solid=SourceSolid(source,transform);
            var faces=solid.Faces.Cast<Face>().OfType<PlanarFace>().ToList();
            var bottom=faces.Where(f=>f.FaceNormal.Z < -0.999999).OrderBy(f=>f.Origin.Z).FirstOrDefault();
            var top=faces.Where(f=>f.FaceNormal.Z > 0.999999).OrderByDescending(f=>f.Origin.Z).FirstOrDefault();
            if (bottom==null || top==null) throw new InvalidOperationException("Chưa hỗ trợ hình học không có mặt trên/dưới phẳng ngang (ví dụ mái dốc).");
            double height=top.Origin.Z-bottom.Origin.Z;
            if (height<=doc.Application.ShortCurveTolerance || Math.Abs(solid.Volume-bottom.Area*height)>solid.Volume*0.001 || Math.Abs(top.Area-bottom.Area)>bottom.Area*0.001)
                throw new InvalidOperationException("Cần khối đùn đứng đều; hình học nghiêng, bậc hoặc lỗ bên chưa hỗ trợ.");
            Element created;
            if (kind=="Wall")
            {
                var wallType=type as WallType;
                var loops=bottom.GetEdgesAsCurveLoops();
                var lines=loops.Count==1 ? loops[0].OfType<Line>().ToList() : new List<Line>();
                if (lines.Count!=4 || loops[0].Count()!=4) throw new InvalidOperationException("Wall native hiện cần mặt bằng hình chữ nhật, không có lỗ.");
                var ends=lines.Where(l=>Math.Abs(l.Length-wallType.Width)<1.0/304.8).ToList();
                if (ends.Count!=2 || Math.Abs(ends[0].Direction.DotProduct(ends[1].Direction))<0.999999)
                    throw new InvalidOperationException("Bề dày Wall Type không khớp IFC hoặc không xác định duy nhất đường tim.");
                var a=ends[0].Evaluate(0.5,true); var b=ends[1].Evaluate(0.5,true);
                created=Wall.Create(doc,Line.CreateBound(a,b),type.Id,level.Id,height,bottom.Origin.Z-level.ProjectElevation,false,false);
            }
            else
            {
                var loops=bottom.GetEdgesAsCurveLoops().Select(loop=>CurveLoop.Create(loop.Select(c=>c.CreateTransformed(
                    Transform.CreateTranslation(new XYZ(0,0,level.ProjectElevation-bottom.Origin.Z)))).ToList())).ToList();
                if (kind=="Floor")
                {
                    var floor=Floor.Create(doc,loops,type.Id,level.Id);
                    SetOffset(floor,BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM,top.Origin.Z-level.ProjectElevation); created=floor;
                }
                else
                {
                    if (loops.Count!=1) throw new InvalidOperationException("Roof native hiện chưa hỗ trợ lỗ trong biên mái.");
                    var footprint=new CurveArray(); foreach (var curve in loops[0]) footprint.Append(curve);
                    ModelCurveArray mapping;
                    var roof=doc.Create.NewFootPrintRoof(footprint,level,(RoofType)type,out mapping);
                    foreach (ModelCurve curve in mapping) roof.set_DefinesSlope(curve,false);
                    doc.Regenerate();
                    var actual=Solids(roof.get_Geometry(new Options { DetailLevel=ViewDetailLevel.Fine })).ToList();
                    double actualTop=actual.SelectMany(s=>s.Faces.Cast<Face>().OfType<PlanarFace>()).Where(f=>f.FaceNormal.Z>0.999999).Max(f=>f.Origin.Z);
                    SetOffset(roof,BuiltInParameter.ROOF_LEVEL_OFFSET_PARAM,(roof.get_Parameter(BuiltInParameter.ROOF_LEVEL_OFFSET_PARAM)?.AsDouble()??0)+top.Origin.Z-actualTop);
                    created=roof;
                }
            }
            doc.Regenerate();
            ValidateSolid(created,solid);
            return created;
        }
        private static void SetOffset(Element element,BuiltInParameter name,double value)
        {
            var p=element.get_Parameter(name); if (p==null || p.IsReadOnly || !p.Set(value)) throw new InvalidOperationException("Không đặt được cao độ native.");
        }
        private static void ValidateSolid(Element element,Solid expected)
        {
            var solids=Solids(element.get_Geometry(new Options { DetailLevel=ViewDetailLevel.Fine })).ToList();
            if (solids.Count!=1) throw new InvalidOperationException("Native tạo ra nhiều khối; không xác nhận được hình học.");
            var missing=BooleanOperationsUtils.ExecuteBooleanOperation(expected,solids[0],BooleanOperationsType.Difference);
            var excess=BooleanOperationsUtils.ExecuteBooleanOperation(solids[0],expected,BooleanOperationsType.Difference);
            if (Math.Abs(missing.Volume)+Math.Abs(excess.Volume)>Math.Max(1e-8,expected.Volume*0.001))
                throw new InvalidOperationException("Hình học native không khớp IFC. Kiểm tra bề dày Type, biên và cấu tạo lớp; phần tử này được hoàn tác.");
        }
    }
}
