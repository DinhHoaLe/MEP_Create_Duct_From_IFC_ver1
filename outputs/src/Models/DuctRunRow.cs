using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IFCInfo
{
    public sealed class DuctRunRow
    {
        public string RunId { get; set; }
        public string SourceId { get; set; }
        public string IfcGuid { get; set; }
        public string TargetId { get; set; }
        public string Status { get; set; }
        public string Reason { get; set; }
        public string IfcPsets { get; set; }
        public bool Success { get; set; }
        public static List<long> SuccessfulTargetIds(IEnumerable<DuctRunRow> rows)
        {
            var ids = new HashSet<long>();
            foreach (var row in rows.Where(r => r.Success))
                foreach (string token in (row.TargetId ?? "").Split(','))
                    if (long.TryParse(token.Trim(), out long id) && id > 0) ids.Add(id);
            return ids.OrderBy(id => id).ToList();
        }
        public static string CsvCell(string value)
        {
            value = value ?? "";
            // Prevent spreadsheet formula evaluation, including leading whitespace.
            string trimmed = value.TrimStart();
            if (trimmed.Length > 0 && "=+-@".Contains(trimmed[0])) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        public static void Export(string path, IEnumerable<DuctRunRow> rows)
        {
            var lines = new List<string> { "Run ID,Source Element ID,IFC GUID,Target Element ID,Status,Reason,IFC Psets" };
            lines.AddRange(rows.Select(r => string.Join(",", new[] { r.RunId, r.SourceId, r.IfcGuid, r.TargetId, r.Status, r.Reason,r.IfcPsets }.Select(CsvCell))));
            File.WriteAllLines(path, lines, new UTF8Encoding(true));
        }
    }
}
