using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IFCInfo
{
    public sealed class IfcTerminalSource
    {
        public string Guid { get; set; }
        public string Name { get; set; }
        public string SystemName { get; set; }
        public string SystemType { get; set; }
        public double? ElevationMm { get; set; }
    }

    // Focused STEP reader for IFC2X3 local placements and system membership.
    // Does not interpret geometry, Revit offsets, or map/georeferencing conversions.
    public sealed class IfcSourceReader
    {
        private sealed class Entity
        {
            public string Kind;
            public string[] Args;
            public string At(int index) { return index < Args.Length ? Args[index] : "$"; }
        }
        private readonly Dictionary<int, Entity> entities = new Dictionary<int, Entity>();
        private readonly Dictionary<int, double[]> placements = new Dictionary<int, double[]>();
        public Dictionary<string, IfcTerminalSource> Terminals { get; private set; }
            = new Dictionary<string, IfcTerminalSource>(StringComparer.Ordinal);
        public int SystemCount { get; private set; }

        public static IfcSourceReader Read(string path)
        {
            var reader = new IfcSourceReader();
            string text = File.ReadAllText(path);
            if (!Regex.IsMatch(text, @"FILE_SCHEMA\s*\(\s*\(\s*'IFC2X3'", RegexOptions.IgnoreCase))
                throw new NotSupportedException("Bản reader này hỗ trợ IFC2X3 (.ifc), như file Snowdon đang test.");
            var pattern = new Regex(@"^\s*#(\d+)\s*=\s*(\w+)\s*\((.*)\)\s*$", RegexOptions.Singleline);
            foreach (string statement in Split(text, ';'))
            {
                Match match = pattern.Match(statement);
                if (!match.Success) continue;
                reader.entities.Add(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                    new Entity { Kind = match.Groups[2].Value.ToUpperInvariant(),
                        Args = Split(match.Groups[3].Value, ',').ToArray() });
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
                    if (end < 0) throw new FormatException("IFC comment không hợp lệ.");
                    i = end + 1;
                    continue;
                }
                if (ch == '\'')
                {
                    part.Append(ch);
                    if (quoted && i + 1 < text.Length && text[i + 1] == '\'')
                    { part.Append(text[++i]); continue; }
                    quoted = !quoted;
                    continue;
                }
                if (!quoted)
                {
                    if (ch == '(') depth++;
                    if (ch == ')') depth--;
                    if (ch == separator && depth == 0)
                    { yield return part.ToString().Trim(); part.Clear(); continue; }
                }
                part.Append(ch);
            }
            if (quoted || depth != 0) throw new FormatException("Cấu trúc STEP chưa hoàn chỉnh.");
            if (part.Length > 0) yield return part.ToString().Trim();
        }

        private static string Label(string token)
        {
            if (token == "$" || token == "*") return "";
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
        { return token.StartsWith("#", StringComparison.Ordinal) ? int.Parse(token.Substring(1), CultureInfo.InvariantCulture) : 0; }
        private static IEnumerable<int> Refs(string token)
        {
            if (!token.StartsWith("(", StringComparison.Ordinal)) yield break;
            foreach (string item in Split(token.Substring(1, token.Length - 2), ','))
                if (Ref(item) != 0) yield return Ref(item);
        }
        private Entity Get(int id)
        {
            Entity result;
            if (!entities.TryGetValue(id, out result)) throw new FormatException("Không tìm thấy IFC entity #" + id);
            return result;
        }

        private void Extract()
        {
            var ids = new HashSet<int>(entities.Where(p => p.Value.Kind == "IFCAIRTERMINAL").Select(p => p.Key));
            foreach (Entity relation in entities.Values.Where(e => e.Kind == "IFCRELDEFINESBYTYPE"))
                if (Get(Ref(relation.At(5))).Kind == "IFCAIRTERMINALTYPE")
                    ids.UnionWith(Refs(relation.At(4)));
            var systems = entities.Where(p => p.Value.Kind == "IFCSYSTEM").ToDictionary(p => p.Key, p => p.Value);
            SystemCount = systems.Count;
            var membership = new Dictionary<int, HashSet<int>>();
            foreach (Entity relation in entities.Values.Where(e => e.Kind == "IFCRELASSIGNSTOGROUP"))
            {
                int system = Ref(relation.At(6));
                if (!systems.ContainsKey(system)) continue;
                foreach (int member in Refs(relation.At(4)))
                {
                    if (!membership.ContainsKey(member)) membership.Add(member, new HashSet<int>());
                    membership[member].Add(system);
                }
            }
            double? millimetres = null;
            try
            {
                Entity project = entities.Values.First(e => e.Kind == "IFCPROJECT");
                Entity unitAssignment = Get(Ref(project.At(8)));
                foreach (int unit in Refs(unitAssignment.At(0)))
                    if (Get(unit).At(1) == ".LENGTHUNIT.") millimetres = UnitMetres(unit, 0) * 1000;
            }
            catch (Exception ex) when (ex is FormatException || ex is InvalidOperationException || ex is NotSupportedException)
            { /* Unknown units must never be silently interpreted as metres. */ }

            foreach (int id in ids)
            {
                Entity product = Get(id);
                var result = new IfcTerminalSource { Guid = Label(product.At(0)), Name = Label(product.At(2)) };
                HashSet<int> groups;
                if (membership.TryGetValue(id, out groups))
                {
                    // Same ordering in both columns preserves system/type pairing.
                    var ordered = groups.OrderBy(g => Label(systems[g].At(2)), StringComparer.Ordinal).ToArray();
                    result.SystemName = string.Join("; ", ordered.Select(g => EmptyLabel(Label(systems[g].At(2)))));
                    result.SystemType = string.Join("; ", ordered.Select(g => EmptyLabel(Label(systems[g].At(4)))));
                }
                if (millimetres.HasValue)
                {
                    try { result.ElevationMm = Placement(Ref(product.At(5)), new HashSet<int>())[11] * millimetres.Value; }
                    catch (Exception ex) when (ex is FormatException || ex is NotSupportedException)
                    { /* Unsupported placement stays unavailable. */ }
                }
                Terminals.Add(result.Guid, result);
            }
        }
        private static string EmptyLabel(string value) { return string.IsNullOrWhiteSpace(value) ? "Không có thông tin" : value; }
        private double UnitMetres(int id, int depth)
        {
            if (depth > 10) throw new FormatException("Chuỗi đơn vị IFC không hợp lệ.");
            Entity unit = Get(id);
            if (unit.Kind == "IFCSIUNIT" && unit.At(3) == ".METRE.")
            {
                var prefixes = new Dictionary<string, double> { { "$", 1 }, { ".MILLI.", 0.001 },
                    { ".CENTI.", 0.01 }, { ".DECI.", 0.1 }, { ".KILO.", 1000 }, { ".MICRO.", 0.000001 } };
                double factor;
                if (prefixes.TryGetValue(unit.At(2), out factor)) return factor;
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
        private static double Number(string value) { return double.Parse(value, CultureInfo.InvariantCulture); }
        private static double[] Identity() { return new double[] { 1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1 }; }
        private static double[] Multiply(double[] a, double[] b)
        {
            var result = new double[16];
            for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++)
                for (int k = 0; k < 4; k++) result[r * 4 + c] += a[r * 4 + k] * b[k * 4 + c];
            return result;
        }
        private double[] Vector(int id)
        {
            string token = Get(id).At(0);
            var values = Split(token.Substring(1, token.Length - 2), ',').Select(Number).ToArray();
            if (values.Length != 2 && values.Length != 3) throw new FormatException("Vector IFC không hợp lệ.");
            return new[] { values[0], values[1], values.Length == 3 ? values[2] : 0 };
        }
        private static double[] Normalize(double[] v)
        {
            double length = Math.Sqrt(v.Sum(x => x * x));
            if (length < 1e-12) throw new FormatException("Trục IFC không hợp lệ.");
            return v.Select(x => x / length).ToArray();
        }
        private static double[] Cross(double[] a, double[] b)
        { return new[] { a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0] }; }
        private double[] Placement(int id, HashSet<int> visiting)
        {
            if (id == 0) throw new NotSupportedException("IFC thiếu ObjectPlacement.");
            double[] cached;
            if (placements.TryGetValue(id, out cached)) return cached;
            if (visiting.Count > 100 || !visiting.Add(id)) throw new FormatException("Chuỗi placement IFC có vòng lặp.");
            Entity placement = Get(id);
            if (placement.Kind != "IFCLOCALPLACEMENT") throw new NotSupportedException("Chỉ hỗ trợ IfcLocalPlacement.");
            Entity axis = Get(Ref(placement.At(1)));
            if (axis.Kind != "IFCAXIS2PLACEMENT3D" && axis.Kind != "IFCAXIS2PLACEMENT2D")
                throw new NotSupportedException("Kiểu trục IFC chưa hỗ trợ.");
            double[] origin = Vector(Ref(axis.At(0)));
            bool is3D = axis.Kind == "IFCAXIS2PLACEMENT3D";
            double[] z = is3D && Ref(axis.At(1)) != 0 ? Normalize(Vector(Ref(axis.At(1)))) : new double[] { 0,0,1 };
            int xRef = Ref(axis.At(is3D ? 2 : 1));
            double[] x = xRef != 0 ? Normalize(Vector(xRef)) : new double[] { 1,0,0 };
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
