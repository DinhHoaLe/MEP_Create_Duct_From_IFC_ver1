using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

public class Inspect
{
    static double[] P(Point3d p) => new[] { p.X, p.Y, p.Z };
    [CommandMethod("DUMPTRAY")]
    public void Dump()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        var db = doc.Database;
        var rows = new List<object>();
        using (var tx = db.TransactionManager.StartTransaction())
        {
            var table = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
            Walk(tx, table[BlockTableRecord.ModelSpace], Matrix3d.Identity, "", rows, 0);
        }
        var output = new { Units = db.Insunits.ToString(), Base = P(db.Insbase), Blocks = rows };
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "inspection.json"),
            new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(output));
        doc.Editor.WriteMessage("\nDUMPTRAY complete: " + rows.Count + " dynamic blocks.\n");
    }
    static void Walk(Transaction tx, ObjectId definition, Matrix3d parent, string path, List<object> rows, int depth)
    {
        if (depth > 15) return;
        var block = (BlockTableRecord)tx.GetObject(definition, OpenMode.ForRead);
        if (block.IsFromExternalReference) return;
        foreach (ObjectId id in block)
        {
            var reference = tx.GetObject(id, OpenMode.ForRead) as BlockReference;
            if (reference == null) continue;
            var world = parent * reference.BlockTransform;
            if (reference.IsDynamicBlock)
            {
                var props = new Dictionary<string, object>();
                foreach (DynamicBlockReferenceProperty prop in reference.DynamicBlockReferencePropertyCollection) props[prop.PropertyName] = prop.Value;
                if (props.ContainsKey("Distance1") || props.ContainsKey("Distance2"))
                {
                    var geometry = new List<object>();
                    var evaluated = (BlockTableRecord)tx.GetObject(reference.BlockTableRecord, OpenMode.ForRead);
                    foreach (ObjectId entityId in evaluated)
                    {
                        var entity = (Entity)tx.GetObject(entityId, OpenMode.ForRead);
                        if (entity is Line line) geometry.Add(new { Kind = "Line", A = P(line.StartPoint), B = P(line.EndPoint), Layer = entity.Layer });
                        else if (entity is Polyline poly)
                        {
                            var vertices = new List<double[]>(); var bulges = new List<double>();
                            for (int i = 0; i < poly.NumberOfVertices; i++) { vertices.Add(P(poly.GetPoint3dAt(i))); bulges.Add(poly.GetBulgeAt(i)); }
                            geometry.Add(new { Kind = "Polyline", Points = vertices, Bulges = bulges, Closed = poly.Closed, Layer = entity.Layer });
                        }
                        else geometry.Add(new { Kind = entity.GetType().Name, Layer = entity.Layer });
                    }
                    rows.Add(new { Handle = path + reference.Handle, Name = ((BlockTableRecord)tx.GetObject(reference.DynamicBlockTableRecord, OpenMode.ForRead)).Name,
                        Layer = reference.Layer, Properties = props, Origin = P(Point3d.Origin.TransformBy(world)), Matrix = world.ToArray(), DefinitionOrigin = P(evaluated.Origin), Geometry = geometry });
                }
            }
            Walk(tx, reference.BlockTableRecord, world, path + reference.Handle + "/", rows, depth + 1);
        }
    }
}
