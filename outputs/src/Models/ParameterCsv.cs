using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IFCInfo
{
    public static class ParameterCsv
    {
        public sealed class Row
        {
            public string IFCProperty { get; set; }
            public string ParameterName { get; set; }
            public string DataType { get; set; }
        }
        public static string Write(IEnumerable<Row> rows)
        {
            Func<string,string> cell=s=>"\""+(s??"").Replace("\"","\"\"")+"\"";
            return "IFCProperty,ParameterName,DataType\r\n"+string.Join("\r\n",rows.Select(r=>string.Join(",",new[]{r.IFCProperty,r.ParameterName,r.DataType}.Select(cell))))+"\r\n";
        }

        public static List<Row> Read(string text)
        {
            var records = new List<List<string>>();
            var record = new List<string>(); var cell = new StringBuilder();
            bool quoted = false, closed = false;
            text = (text ?? "").TrimStart('\uFEFF');
            for (int i = 0; i <= text.Length; i++)
            {
                char c = i == text.Length ? '\n' : text[i];
                if (quoted)
                {
                    if (i == text.Length) throw new FormatException("CSV contains an unclosed quoted field.");
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                        else { quoted = false; closed = true; }
                    }
                    else cell.Append(c);
                }
                else if (c == ',' || c == '\r' || c == '\n')
                {
                    record.Add(cell.ToString()); cell.Clear(); closed = false;
                    if (c != ',')
                    {
                        if (record.Any(v => !string.IsNullOrWhiteSpace(v))) records.Add(record);
                        record = new List<string>();
                        if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    }
                }
                else if (c == '"' && cell.Length == 0 && !closed) quoted = true;
                else if (closed || c == '"') throw new FormatException("CSV contains an invalid quoted field.");
                else cell.Append(c);
            }
            if (records.Count == 0) throw new FormatException("CSV is empty.");
            var header = records[0].Select(h => h.Trim()).ToList();
            if (header.Distinct(StringComparer.OrdinalIgnoreCase).Count() != header.Count)
                throw new FormatException("CSV has duplicate column names.");
            int source = header.FindIndex(h => h.Equals("IFCProperty", StringComparison.OrdinalIgnoreCase));
            int target = header.FindIndex(h => h.Equals("ParameterName", StringComparison.OrdinalIgnoreCase));
            int type = header.FindIndex(h => h.Equals("DataType", StringComparison.OrdinalIgnoreCase));
            if (source < 0 || target < 0) throw new FormatException("CSV requires IFCProperty and ParameterName columns; DataType is optional.");
            var result = new List<Row>();
            for (int i = 1; i < records.Count; i++)
            {
                var values = records[i];
                if (values.Count != header.Count) throw new FormatException("CSV record " + (i + 1) + " has a different column count.");
                result.Add(new Row { IFCProperty = values[source].Trim(), ParameterName = values[target].Trim(), DataType = type < 0 ? "" : values[type].Trim() });
            }
            if (result.Count == 0) throw new FormatException("CSV has no parameter records.");
            return result;
        }
    }
}
