using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace IFCInfo
{
    public static class IfcSelectionCsv
    {
        public static List<Dictionary<string,string>> Records(IEnumerable<AirTerminalRow> rows,string link,string category)
        {
            return rows.Where(r=>r.IsSelected).Select(r=>
            {
                var record=new Dictionary<string,string> {
                    {"IFC Link",link},{"Category",category},{"Element ID",r.ElementId},{"IFC GUID",r.IfcGuid},
                    {"Name",r.Name},{"System Type",r.SystemType},{"System Name",r.SystemName},
                    {"Elevation (mm)",r.Elevation},{"Data Source",r.DataSource} };
                if(r.DuctSource!=null)
                {
                    var source=r.DuctSource;
                    foreach(var size in new[] {new KeyValuePair<string,double>("Length (mm)",source.LengthMm),
                        new KeyValuePair<string,double>("Width (mm)",source.WidthMm),new KeyValuePair<string,double>("Height (mm)",source.HeightMm),
                        new KeyValuePair<string,double>("Diameter (mm)",source.DiameterMm)})
                        record[size.Key]=size.Value.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
                }
                foreach(var p in r.IfcProperties)
                {
                    string key=p.Scope+" / "+p.SetName+" / "+p.Name;
                    string unique=key; int index=2;
                    while(record.ContainsKey(unique)) unique=key+" ("+(index++)+")";
                    record[unique]=p.Value+(string.IsNullOrEmpty(p.Unit)?"":" ["+p.Unit+"]");
                }
                return record;
            }).ToList();
        }
        public static List<string> Lines(IEnumerable<AirTerminalRow> rows,string link,string category)
        {
            var records=Records(rows,link,category);
            var names=new[] {"IFC Link","Category","Element ID","IFC GUID","Name","System Type","System Name","Elevation (mm)","Data Source"}
                .Concat(records.SelectMany(r=>r.Keys)).Distinct().ToList();
            var lines=new List<string> {string.Join(",",names.Select(DuctRunRow.CsvCell))};
            lines.AddRange(records.Select(r=>string.Join(",",names.Select(n=>DuctRunRow.CsvCell(r.ContainsKey(n)?r[n]:"")))));
            return lines;
        }
        public static void Export(string path,IEnumerable<AirTerminalRow> rows,string link,string category)
            => File.WriteAllLines(path,Lines(rows,link,category),new UTF8Encoding(true));
    }
}
