using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml;

namespace IFCInfo
{
    public static class IfcSelectionExport
    {
        public static void Export(string path,IEnumerable<AirTerminalRow> rows,string link,string category,int format)
        {
            var selected=rows.Where(r=>r.IsSelected).ToList();
            if(format==1) { IfcSelectionCsv.Export(path,selected,link,category); return; }
            var data=IfcSelectionCsv.Records(selected,link,category);
            var names=data.SelectMany(r=>r.Keys).Distinct().ToArray();
            var records=data.Select(r=>names.Select(n=>r.ContainsKey(n)?r[n]:"").ToArray()).ToList();
            if(format==2)
            {
                File.WriteAllLines(path,new[] {string.Join("\t",names.Select(DuctRunRow.CsvCell))}.Concat(records.Select(r=>string.Join("\t",r.Select(DuctRunRow.CsvCell)))),new UTF8Encoding(true));
            }
            else if(format==3)
            {
                File.WriteAllText(path,new JavaScriptSerializer {MaxJsonLength=int.MaxValue}.Serialize(data),new UTF8Encoding(false));
            }
            else if(format==4)
            {
                using(var writer=XmlWriter.Create(path,new XmlWriterSettings {Indent=true,Encoding=new UTF8Encoding(false)}))
                {
                    writer.WriteStartElement("IfcElements");
                    foreach(var record in records)
                    {
                        writer.WriteStartElement("Element");
                        for(int i=0;i<names.Length;i++) {
                            writer.WriteStartElement("Property"); writer.WriteAttributeString("name",names[i]);
                            writer.WriteString(record[i]??"");writer.WriteEndElement();
                        }
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }
            }
            else throw new ArgumentOutOfRangeException(nameof(format));
        }
    }
}
