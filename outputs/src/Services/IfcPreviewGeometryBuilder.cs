using System.Collections.Generic;
using System.Windows.Media.Media3D;
using Autodesk.Revit.DB;

namespace IFCInfo
{
    internal static class IfcPreviewGeometryBuilder
    {
        internal static IfcPreviewMesh Build(Element element)
        {
            if (element==null) return null;
            var result=new IfcPreviewMesh { ElementId=element.Id.Number(),Name=element.Name };
            AppendGeometry(element.get_Geometry(new Options { DetailLevel=ViewDetailLevel.Fine }),
                result.Points,result.Normals,result.Indices,200000);
            if (result.Indices.Count==0)
                AppendBox(element.get_BoundingBox(null),result.Points,result.Normals,result.Indices);
            return result;
        }
        private static void AppendGeometry(GeometryElement geometry,List<Point3D> points,List<Vector3D> normals,List<int> indices,int limit)
        {
            if (geometry==null || indices.Count>=limit) return;
            foreach (GeometryObject item in geometry)
            {
                if (indices.Count>=limit) break;
                if (item is Solid solid && solid.Volume>1e-9)
                    foreach (Face face in solid.Faces) AppendMesh(face.Triangulate(),points,normals,indices,limit);
                else if (item is Autodesk.Revit.DB.Mesh mesh) AppendMesh(mesh,points,normals,indices,limit);
                else if (item is GeometryInstance instance) AppendGeometry(instance.GetInstanceGeometry(),points,normals,indices,limit);
            }
        }
        private static void AppendMesh(Autodesk.Revit.DB.Mesh mesh,List<Point3D> points,List<Vector3D> normals,List<int> indices,int limit)
        {
            for (int i=0;i<mesh.NumTriangles && indices.Count+3<=limit;i++)
            {
                var triangle=mesh.get_Triangle(i);
                var a=triangle.get_Vertex(0); var b=triangle.get_Vertex(1); var c=triangle.get_Vertex(2);
                var pa=new Point3D(a.X,a.Y,a.Z); var pb=new Point3D(b.X,b.Y,b.Z); var pc=new Point3D(c.X,c.Y,c.Z);
                var normal=Vector3D.CrossProduct(pb-pa,pc-pa);
                if (normal.LengthSquared<1e-18) continue;
                normal.Normalize(); int start=points.Count;
                points.Add(pa); points.Add(pb); points.Add(pc);
                normals.Add(normal); normals.Add(normal); normals.Add(normal);
                indices.Add(start); indices.Add(start+1); indices.Add(start+2);
            }
        }
        private static void AppendBox(BoundingBoxXYZ box,List<Point3D> points,List<Vector3D> normals,List<int> indices)
        {
            if (box==null) return;
            var p=new Point3D[8]; int n=0;
            foreach(double x in new[] { box.Min.X,box.Max.X })
            foreach(double y in new[] { box.Min.Y,box.Max.Y })
            foreach(double z in new[] { box.Min.Z,box.Max.Z })
            {
                var value=box.Transform.OfPoint(new XYZ(x,y,z)); p[n++]=new Point3D(value.X,value.Y,value.Z);
            }
            foreach(var face in new[] {
                new[] {0,2,3,1},new[] {4,5,7,6},new[] {0,1,5,4},
                new[] {2,6,7,3},new[] {0,4,6,2},new[] {1,3,7,5} })
            {
                AddTriangle(p[face[0]],p[face[1]],p[face[2]],points,normals,indices);
                AddTriangle(p[face[0]],p[face[2]],p[face[3]],points,normals,indices);
            }
        }
        private static void AddTriangle(Point3D a,Point3D b,Point3D c,List<Point3D> points,List<Vector3D> normals,List<int> indices)
        {
            var normal=Vector3D.CrossProduct(b-a,c-a);
            if (normal.LengthSquared<1e-18) return;
            normal.Normalize(); int start=points.Count;
            points.Add(a); points.Add(b); points.Add(c);
            normals.Add(normal); normals.Add(normal); normals.Add(normal);
            indices.Add(start); indices.Add(start+1); indices.Add(start+2);
        }
    }
}
