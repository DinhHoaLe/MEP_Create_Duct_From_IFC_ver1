using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IFCInfo
{
    // Focused IFC2X3/IFC4 reader. Geometry transforms here determine sizes only;
    // world endpoints continue to come from the geometry of the Revit link.
    public sealed class IfcSourceReader
    {
        private sealed class Entity
        {
            public string Kind;
            public string[] Args;
            public string At(int index)
            {
                return index < Args.Length ? Args[index] : "$";
            }
        }
        private readonly Dictionary<int, Entity> entities = new Dictionary<int, Entity>();
        private readonly Dictionary<int, double[]> placements = new Dictionary<int, double[]>();
        public Dictionary<string, IfcTerminalSource> Products { get; private set; } = new Dictionary<string, IfcTerminalSource>(StringComparer.Ordinal);
        public Dictionary<string, IfcTerminalSource> Terminals
        {
            get; private set;
        }
            = new Dictionary<string, IfcTerminalSource>(StringComparer.Ordinal);
        public Dictionary<string, IfcTerminalSource> Ducts
        {
            get; private set;
        }
            = new Dictionary<string, IfcTerminalSource>(StringComparer.Ordinal);
        public int SystemCount
        {
            get; private set;
        }

        public static IfcSourceReader Read(string path)
        {
            var reader = new IfcSourceReader();
            string text = File.ReadAllText(path);
            if (!Regex.IsMatch(text, @"FILE_SCHEMA\s*\(\s*\(\s*'(IFC2X3|IFC4)'\s*\)\s*\)", RegexOptions.IgnoreCase))
                throw new NotSupportedException("Hỗ trợ IFC2X3 và IFC4 dạng STEP (.ifc). Schema khác chưa được hỗ trợ.");
            var pattern = new Regex(@"^\s*#(\d+)\s*=\s*(\w+)\s*\((.*)\)\s*$", RegexOptions.Singleline);
            foreach (string statement in Split(text, ';'))
            {
                Match match = pattern.Match(statement);
                if (!match.Success)
                    continue;
                reader.entities.Add(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                    new Entity
                    {
                        Kind = match.Groups[2].Value.ToUpperInvariant(),
                        Args = Split(match.Groups[3].Value, ',').ToArray()
                    });
            }
            reader.Extract();
            return reader;
        }

        // Split outside quoted strings, nested aggregates and STEP comments.
        private static IEnumerable<string> Split(string text, char separator)
        {
            var part = new StringBuilder();
            bool quoted = false;
            int depth = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (!quoted && ch == '/' && i + 1 < text.Length && text[i + 1] == '*')
                {
                    int end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    if (end < 0)
                        throw new FormatException("IFC comment không hợp lệ.");
                    i = end + 1;
                    continue;
                }
                if (ch == '\'')
                {
                    part.Append(ch);
                    if (quoted && i + 1 < text.Length && text[i + 1] == '\'')
                    {
                        part.Append(text[++i]);
                        continue;
                    }
                    quoted = !quoted;
                    continue;
                }
                if (!quoted)
                {
                    if (ch == '(')
                        depth++;
                    if (ch == ')')
                        depth--;
                    if (ch == separator && depth == 0)
                    {
                        yield return part.ToString().Trim();
                        part.Clear();
                        continue;
                    }
                }
                part.Append(ch);
            }
            if (quoted || depth != 0)
                throw new FormatException("Cấu trúc STEP chưa hoàn chỉnh.");
            if (part.Length > 0)
                yield return part.ToString().Trim();
        }

        private static string Label(string token)
        {
            if (token == "$" || token == "*")
                return "";
            string value = token.Trim('\'').Replace("''", "'");
            value = Regex.Replace(value, @"\\X2\\([0-9A-Fa-f]+)\\X0\\", m =>
            {
                var result = new StringBuilder();
                for (int i = 0; i < m.Groups[1].Length; i += 4)
                    result.Append((char)int.Parse(m.Groups[1].Value.Substring(i, 4), NumberStyles.HexNumber));
                return result.ToString();
            });
            return value;
        }
        private static int Ref(string token)
        {
            return token.StartsWith("#", StringComparison.Ordinal) ? int.Parse(token.Substring(1), CultureInfo.InvariantCulture) : 0;
        }
        private static IEnumerable<int> Refs(string token)
        {
            if (!token.StartsWith("(", StringComparison.Ordinal))
                yield break;
            foreach (string item in Split(token.Substring(1, token.Length - 2), ','))
                if (Ref(item) != 0)
                    yield return Ref(item);
        }
        private Entity Get(int id)
        {
            Entity result;
            if (!entities.TryGetValue(id, out result))
                throw new FormatException("Không tìm thấy IFC entity #" + id);
            return result;
        }

        private void Extract()
        {
            var ids = new HashSet<int>(entities.Where(p => p.Value.Kind == "IFCAIRTERMINAL").Select(p => p.Key));
            foreach (Entity relation in entities.Values.Where(e => e.Kind == "IFCRELDEFINESBYTYPE"))
                if (Get(Ref(relation.At(5))).Kind == "IFCAIRTERMINALTYPE")
                    ids.UnionWith(Refs(relation.At(4)));
            var ductIds = new HashSet<int>(entities.Where(p => p.Value.Kind == "IFCDUCTSEGMENT").Select(p => p.Key));
            foreach (Entity relation in entities.Values.Where(e => e.Kind == "IFCRELDEFINESBYTYPE"))
                if (Get(Ref(relation.At(5))).Kind == "IFCDUCTSEGMENTTYPE")
                    ductIds.UnionWith(Refs(relation.At(4)));
            var systems = entities.Where(p => p.Value.Kind == "IFCSYSTEM" ||
                p.Value.Kind == "IFCDISTRIBUTIONSYSTEM" || p.Value.Kind == "IFCDISTRIBUTIONCIRCUIT")
                .ToDictionary(p => p.Key, p => p.Value);
            SystemCount = systems.Count;
            var membership = new Dictionary<int, HashSet<int>>();
            foreach (Entity relation in entities.Values.Where(e => e.Kind == "IFCRELASSIGNSTOGROUP"))
            {
                int system = Ref(relation.At(6));
                if (!systems.ContainsKey(system))
                    continue;
                foreach (int member in Refs(relation.At(4)))
                {
                    if (!membership.ContainsKey(member))
                        membership.Add(member, new HashSet<int>());
                    membership[member].Add(system);
                }
            }
            double? millimetres = null;
            try
            {
                Entity project = entities.Values.First(e => e.Kind == "IFCPROJECT");
                Entity unitAssignment = Get(Ref(project.At(8)));
                foreach (int unit in Refs(unitAssignment.At(0)))
                    if (Get(unit).At(1) == ".LENGTHUNIT.")
                        millimetres = UnitMetres(unit, 0) * 1000;
            }
            catch (Exception ex) when (ex is FormatException || ex is InvalidOperationException || ex is NotSupportedException)
            { /* Unknown units must never be silently interpreted as metres. */
            }

            var productIds = entities.Where(p =>
                (Ref(p.Value.At(5)) != 0 && entities.ContainsKey(Ref(p.Value.At(5))) && Get(Ref(p.Value.At(5))).Kind.EndsWith("PLACEMENT",StringComparison.Ordinal)) ||
                (Ref(p.Value.At(6)) != 0 && entities.ContainsKey(Ref(p.Value.At(6))) && Get(Ref(p.Value.At(6))).Kind == "IFCPRODUCTDEFINITIONSHAPE"))
                .Select(p=>p.Key).Union(ids).Union(ductIds).ToList();
            var propertySets = new Dictionary<int,List<Tuple<int,string>>>();
            foreach (var rel in entities.Values.Where(e=>e.Kind=="IFCRELDEFINESBYPROPERTIES"))
            foreach (int productId in Refs(rel.At(4)))
            {
                if (!propertySets.ContainsKey(productId)) propertySets[productId]=new List<Tuple<int,string>>();
                if (Ref(rel.At(5))!=0) propertySets[productId].Add(Tuple.Create(Ref(rel.At(5)),"Instance"));
                else
                {
                    string token=rel.At(5); int start=token.IndexOf('(');
                    if (start>=0) foreach (int setId in Refs(token.Substring(start+1,token.Length-start-2))) propertySets[productId].Add(Tuple.Create(setId,"Instance"));
                }
            }
            foreach (var rel in entities.Values.Where(e=>e.Kind=="IFCRELDEFINESBYTYPE"))
            foreach (int productId in Refs(rel.At(4)))
            {
                if (!propertySets.ContainsKey(productId)) propertySets[productId]=new List<Tuple<int,string>>();
                foreach (int setId in Refs(Get(Ref(rel.At(5))).At(5))) propertySets[productId].Add(Tuple.Create(setId,"Type"));
            }
            foreach (int id in productIds)
            {
                Entity product = Get(id);
                var result = new IfcTerminalSource { Guid = Label(product.At(0)), Name = Label(product.At(2)) };
                if (string.IsNullOrEmpty(result.Guid) || !product.At(0).StartsWith("'",StringComparison.Ordinal)) continue;
                if (propertySets.TryGetValue(id,out var sets))
                    foreach (var set in sets.Distinct()) ReadPropertySet(set.Item1,set.Item2,result.Properties);
                HashSet<int> groups;
                if (membership.TryGetValue(id, out groups))
                {
                    // Same ordering in both columns preserves system/type pairing.
                    var ordered = groups.OrderBy(g => Label(systems[g].At(2)), StringComparer.Ordinal).ToArray();
                    result.SystemName = string.Join("; ", ordered.Select(g => EmptyLabel(Label(systems[g].At(2)))));
                    result.SystemType = string.Join("; ", ordered.Select(g => EmptyLabel(SystemTypeLabel(systems[g]))));
                }
                if (millimetres.HasValue)
                {
                    try
                    {
                        result.ElevationMm = Placement(Ref(product.At(5)), new HashSet<int>())[11] * millimetres.Value;
                    }
                    catch (Exception ex) when (ex is FormatException || ex is NotSupportedException)
                    { /* Unsupported placement stays unavailable. */
                    }
                }
                if (ids.Contains(id))
                    Terminals.Add(result.Guid, result);
                if (Ref(product.At(6))!=0)
                {
                    try
                    {
                        ReadDuctSize(product, result, millimetres);
                    }
                    catch (Exception ex) when (ex is FormatException || ex is NotSupportedException || ex is InvalidOperationException)
                    {
                        result.GeometryError = ex.Message;
                    }
                    if (ductIds.Contains(id)) Ducts.Add(result.Guid, result);
                }
                Products[result.Guid]=result;
            }
        }
        private void ReadPropertySet(int id,string scope,List<IfcPropertyValue> output)
        {
            var set=Get(id);
            if (set.Kind!="IFCPROPERTYSET" && set.Kind!="IFCELEMENTQUANTITY") return;
            foreach (int property in Refs(set.At(set.Kind=="IFCPROPERTYSET" ? 4 : 5)))
                ReadProperty(property,scope,Label(set.At(2)),"",output,new HashSet<int>());
        }
        private void ReadProperty(int id,string scope,string setName,string prefix,List<IfcPropertyValue> output,HashSet<int> path)
        {
            if (path.Count>=32 || !path.Add(id)) return;
            var p=Get(id); string name=prefix+Label(p.At(0));
            if (p.Kind=="IFCCOMPLEXPROPERTY")
                foreach (int child in Refs(p.At(3))) ReadProperty(child,scope,setName,name+".",output,path);
            else
            {
                string value,unit="$";
                switch (p.Kind)
                {
                    case "IFCPROPERTYSINGLEVALUE": value=PropertyToken(p.At(2)); unit=p.At(3); break;
                    case "IFCPROPERTYENUMERATEDVALUE": case "IFCPROPERTYLISTVALUE": value=PropertyToken(p.At(2)); unit=p.Kind=="IFCPROPERTYLISTVALUE"?p.At(3):"$"; break;
                    case "IFCPROPERTYBOUNDEDVALUE": value="Upper="+PropertyToken(p.At(2))+"; Lower="+PropertyToken(p.At(3))+"; Setpoint="+PropertyToken(p.At(5)); unit=p.At(4); break;
                    default:
                        if (p.Kind.StartsWith("IFCQUANTITY",StringComparison.Ordinal)) { value=PropertyToken(p.At(3)); unit=p.At(2); }
                        else value=p.Kind+": "+string.Join(", ",p.Args.Skip(2).Select(PropertyToken));
                        break;
                }
                string unitText="";
                if (Ref(unit)!=0 && entities.TryGetValue(Ref(unit),out var u)) unitText=u.Kind=="IFCSIUNIT" ? (u.At(2)=="$"?"":u.At(2).Trim('.'))+u.At(3).Trim('.') : u.Kind=="IFCCONVERSIONBASEDUNIT" ? Label(u.At(2)) : unit;
                output.Add(new IfcPropertyValue { Scope=scope,SetName=setName,Name=name,Value=value,Unit=unitText });
            }
            path.Remove(id);
        }
        private static string PropertyToken(string token)
        {
            if (token=="$" || token=="*") return "";
            if (token.StartsWith("'",StringComparison.Ordinal)) return Label(token);
            if (token.StartsWith("(",StringComparison.Ordinal)) return string.Join("; ",Split(token.Substring(1,token.Length-2),',').Select(PropertyToken));
            int start=token.IndexOf('(');
            if (start>0 && token.EndsWith(")",StringComparison.Ordinal)) return PropertyToken(token.Substring(start+1,token.Length-start-2));
            return token;
        }
        private static string EmptyLabel(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Không có thông tin" : value;
        }
        private static string SystemTypeLabel(Entity system)
        {
            string predefined = system.Kind == "IFCSYSTEM" ? "$" : system.At(6);
            return predefined != "$" && predefined != ".NOTDEFINED." && predefined != ".USERDEFINED."
                ? predefined.Trim('.') : Label(system.At(4));
        }
        private void ReadDuctSize(Entity product, IfcTerminalSource result, double? mm)
        {
            if (!mm.HasValue)
                throw new NotSupportedException("Không xác định được đơn vị IFC.");
            var body = Refs(Get(Ref(product.At(6))).At(2)).Select(Get)
                .Where(e => Label(e.At(1)).Equals("Body", StringComparison.OrdinalIgnoreCase)).ToArray();
            var items = body.SelectMany(e => Refs(e.At(3))).ToArray();
            if (items.Length != 1)
                throw new NotSupportedException("Body phải chứa đúng một khối ống; tìm thấy " + items.Length + " mục hình học.");
            double[] transform = Identity();
            Entity solid = ResolveDuctSolid(items[0], ref transform, new HashSet<int>());
            double[] direction = Normalize(Vector(Ref(solid.At(2))));
            if (Math.Abs(direction[2]) < 1 - 1e-8)
                throw new NotSupportedException("Chưa hỗ trợ tiết diện đùn xiên.");
            Entity profile = Get(Ref(solid.At(0)));
            if (profile.Kind != "IFCCIRCLEPROFILEDEF" && profile.Kind != "IFCRECTANGLEPROFILEDEF")
                throw new NotSupportedException("Chưa hỗ trợ tiết diện " + profile.Kind);
            // Include solid and profile rotations before measuring nonuniform scaling.
            double[] solidAxes = AxisRotation(Ref(solid.At(1)));
            double[] profileAxes = AxisRotation(Ref(profile.At(2)));
            double[] section = Multiply(Multiply(transform, solidAxes), profileAxes);
            double[] x = TransformDirection(section, new double[] { 1, 0, 0 });
            double[] y = TransformDirection(section, new double[] { 0, 1, 0 });
            double[] z = TransformDirection(Multiply(transform, solidAxes), direction);
            if (!Orthogonal(x, y) || !Orthogonal(x, z) || !Orthogonal(y, z))
                throw new NotSupportedException("Phép biến đổi làm xiên tiết diện hoặc trục ống; chưa hỗ trợ.");
            double sx = Magnitude(x), sy = Magnitude(y), sz = Magnitude(z);
            if (profile.Kind == "IFCCIRCLEPROFILEDEF")
            {
                if (Math.Abs(sx - sy) > Math.Max(sx, sy) * 1e-8)
                    throw new NotSupportedException("Phép co giãn biến tiết diện tròn thành oval; chưa hỗ trợ.");
                result.DiameterMm = 2 * Number(profile.At(3)) * sx * mm.Value;
            }
            else if (profile.Kind == "IFCRECTANGLEPROFILEDEF")
            {
                result.WidthMm = Number(profile.At(3)) * sx * mm.Value;
                result.HeightMm = Number(profile.At(4)) * sy * mm.Value;
            }
            else
                throw new NotSupportedException("Chưa hỗ trợ tiết diện " + profile.Kind);
            result.LengthMm = Number(solid.At(3)) * sz * mm.Value;
            if (result.LengthMm <= 0 || (result.DiameterMm <= 0 && (result.WidthMm <= 0 || result.HeightMm <= 0)))
                throw new FormatException("Kích thước IFC không hợp lệ.");
        }
        private Entity ResolveDuctSolid(int id, ref double[] transform, HashSet<int> visiting)
        {
            if (visiting.Count >= 64 || !visiting.Add(id))
                throw new FormatException("Tham chiếu hình học IFC có vòng lặp hoặc quá sâu.");
            Entity item = Get(id);
            if (item.Kind == "IFCEXTRUDEDAREASOLID")
                return item;
            if (item.Kind != "IFCMAPPEDITEM")
                throw new NotSupportedException("Chưa hỗ trợ hình học " + item.Kind + "; cần khối đùn thẳng (trực tiếp hoặc qua IfcMappedItem).");
            Entity map = Get(Ref(item.At(0)));
            if (map.Kind != "IFCREPRESENTATIONMAP")
                throw new FormatException("MappingSource không phải IfcRepresentationMap.");
            var children = Refs(Get(Ref(map.At(1))).At(3)).ToArray();
            if (children.Length != 1)
                throw new NotSupportedException("Hình học tham chiếu phải chứa đúng một khối ống.");
            double[] origin = AxisRotation(Ref(map.At(0)));
            double[] inverse = Identity();
            for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                inverse[r * 4 + c] = origin[c * 4 + r];
            transform = Multiply(transform, Multiply(MappingRotationScale(Ref(item.At(1))), inverse));
            return ResolveDuctSolid(children[0], ref transform, visiting);
        }
        private double[] AxisRotation(int id)
        {
            if (id == 0) return Identity();
            Entity axis = Get(id);
            if (axis.Kind != "IFCAXIS2PLACEMENT3D" && axis.Kind != "IFCAXIS2PLACEMENT2D")
                throw new NotSupportedException("Kiểu trục hình học chưa hỗ trợ: " + axis.Kind);
            bool is3D = axis.Kind == "IFCAXIS2PLACEMENT3D";
            double[] z = is3D && Ref(axis.At(1)) != 0 ? Normalize(Vector(Ref(axis.At(1)))) : new double[] { 0, 0, 1 };
            int xr = Ref(axis.At(is3D ? 2 : 1));
            double[] x = xr == 0 ? new double[] { 1, 0, 0 } : Normalize(Vector(xr));
            double[] y = Normalize(Cross(z, x));
            return Axes(Normalize(Cross(y, z)), y, z);
        }
        private double[] MappingRotationScale(int id)
        {
            Entity op = Get(id);
            if (op.Kind != "IFCCARTESIANTRANSFORMATIONOPERATOR3D" &&
                op.Kind != "IFCCARTESIANTRANSFORMATIONOPERATOR3DNONUNIFORM")
                throw new NotSupportedException("Phép biến đổi IFC chưa hỗ trợ: " + op.Kind);
            double[] x = Ref(op.At(0)) == 0 ? new double[] { 1, 0, 0 } : Normalize(Vector(Ref(op.At(0))));
            double[] y = Ref(op.At(1)) == 0 ? new double[] { 0, 1, 0 } : Normalize(Vector(Ref(op.At(1))));
            double[] z = Ref(op.At(4)) == 0 ? new double[] { 0, 0, 1 } : Normalize(Vector(Ref(op.At(4))));
            if (!Orthogonal(x, y) || !Orthogonal(x, z) || !Orthogonal(y, z))
                throw new NotSupportedException("Các trục MappingTarget không trực giao; chưa hỗ trợ.");
            double sx = op.At(3) == "$" ? 1 : Number(op.At(3));
            double sy = op.At(5) == "$" ? sx : Number(op.At(5));
            double sz = op.At(6) == "$" ? sx : Number(op.At(6));
            if (sx <= 0 || sy <= 0 || sz <= 0)
                throw new FormatException("Hệ số co giãn IFC phải lớn hơn 0.");
            return Axes(x.Select(v => v * sx).ToArray(), y.Select(v => v * sy).ToArray(), z.Select(v => v * sz).ToArray());
        }
        private static double[] Axes(double[] x, double[] y, double[] z) => new double[] {
            x[0],y[0],z[0],0, x[1],y[1],z[1],0, x[2],y[2],z[2],0, 0,0,0,1 };
        private static double[] TransformDirection(double[] m, double[] v) => new double[] {
            m[0]*v[0]+m[1]*v[1]+m[2]*v[2], m[4]*v[0]+m[5]*v[1]+m[6]*v[2], m[8]*v[0]+m[9]*v[1]+m[10]*v[2] };
        private static double Magnitude(double[] v) => Math.Sqrt(v.Sum(a => a * a));
        private static bool Orthogonal(double[] a, double[] b) =>
            Math.Abs(Normalize(a).Zip(Normalize(b), (x, y) => x * y).Sum()) < 1e-8;
        private double UnitMetres(int id, int depth)
        {
            if (depth > 10)
                throw new FormatException("Chuỗi đơn vị IFC không hợp lệ.");
            Entity unit = Get(id);
            if (unit.Kind == "IFCSIUNIT" && unit.At(3) == ".METRE.")
            {
                var prefixes = new Dictionary<string, double> { { "$", 1 }, { ".MILLI.", 0.001 },
                    { ".CENTI.", 0.01 }, { ".DECI.", 0.1 }, { ".KILO.", 1000 }, { ".MICRO.", 0.000001 } };
                double factor;
                if (prefixes.TryGetValue(unit.At(2), out factor))
                    return factor;
            }
            if (unit.Kind == "IFCCONVERSIONBASEDUNIT")
            {
                Entity measure = Get(Ref(unit.At(3)));
                string value = measure.At(0);
                int opening = value.IndexOf('(');
                double factor = Number(value.Substring(opening + 1, value.Length - opening - 2));
                return factor * UnitMetres(Ref(measure.At(1)), depth + 1);
            }
            throw new NotSupportedException("Đơn vị IFC chưa hỗ trợ.");
        }
        private static double Number(string value)
        {
            double number;
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) ||
                double.IsNaN(number) || double.IsInfinity(number))
                throw new FormatException("Giá trị số IFC không hợp lệ.");
            return number;
        }
        private static double[] Identity()
        {
            return new double[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
        }
        private static double[] Multiply(double[] a, double[] b)
        {
            var result = new double[16];
            for (int r = 0; r < 4; r++)
            for (int c = 0; c < 4; c++)
                for (int k = 0; k < 4; k++)
                    result[r * 4 + c] += a[r * 4 + k] * b[k * 4 + c];
            return result;
        }
        private double[] Vector(int id)
        {
            string token = Get(id).At(0);
            var values = Split(token.Substring(1, token.Length - 2), ',').Select(Number).ToArray();
            if (values.Length != 2 && values.Length != 3)
                throw new FormatException("Vector IFC không hợp lệ.");
            return new[] { values[0], values[1], values.Length == 3 ? values[2] : 0 };
        }
        private static double[] Normalize(double[] v)
        {
            double length = Math.Sqrt(v.Sum(x => x * x));
            if (length < 1e-12)
                throw new FormatException("Trục IFC không hợp lệ.");
            return v.Select(x => x / length).ToArray();
        }
        private static double[] Cross(double[] a, double[] b)
        {
            return new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };
        }
        private double[] Placement(int id, HashSet<int> visiting)
        {
            if (id == 0)
                throw new NotSupportedException("IFC thiếu ObjectPlacement.");
            double[] cached;
            if (placements.TryGetValue(id, out cached))
                return cached;
            if (visiting.Count > 100 || !visiting.Add(id))
                throw new FormatException("Chuỗi placement IFC có vòng lặp.");
            Entity placement = Get(id);
            if (placement.Kind != "IFCLOCALPLACEMENT")
                throw new NotSupportedException("Chỉ hỗ trợ IfcLocalPlacement.");
            Entity axis = Get(Ref(placement.At(1)));
            if (axis.Kind != "IFCAXIS2PLACEMENT3D" && axis.Kind != "IFCAXIS2PLACEMENT2D")
                throw new NotSupportedException("Kiểu trục IFC chưa hỗ trợ.");
            double[] origin = Vector(Ref(axis.At(0)));
            bool is3D = axis.Kind == "IFCAXIS2PLACEMENT3D";
            double[] z = is3D && Ref(axis.At(1)) != 0 ? Normalize(Vector(Ref(axis.At(1)))) : new double[] { 0, 0, 1 };
            int xRef = Ref(axis.At(is3D ? 2 : 1));
            double[] x = xRef != 0 ? Normalize(Vector(xRef)) : new double[] { 1, 0, 0 };
            double[] y = Normalize(Cross(z, x));
            x = Normalize(Cross(y, z));
            var local = new[] { x[0],y[0],z[0],origin[0], x[1],y[1],z[1],origin[1],
                x[2],y[2],z[2],origin[2], 0,0,0,1 };
            int parent = Ref(placement.At(0));
            var world = Multiply(parent == 0 ? Identity() : Placement(parent, visiting), local);
            visiting.Remove(id);
            placements[id] = world;
            return world;
        }
    }
}

