using System.Collections.Generic;
using System.Linq;
namespace IFCInfo
{
 internal sealed class IfcTypeChoice
 {
  public string Name,Shape;
  public List<AirTerminalRow> Rows;
  public override string ToString()=>Name+" · "+Shape;
  internal static string TypeName(AirTerminalRow r)=>r.IfcProperties.FirstOrDefault(p=>p.Scope=="Type" && (p.Name=="Name" || p.Name=="Reference"))?.Value ?? r.DuctSource?.Name ?? r.Name;
  internal static List<IfcTypeChoice> Collect(IEnumerable<AirTerminalRow> rows)=>rows.Where(r=>!string.IsNullOrWhiteSpace(TypeName(r))).GroupBy(r=>new {Name=TypeName(r),r.Shape}).Select(g=>new IfcTypeChoice{Name=g.Key.Name,Shape=g.Key.Shape,Rows=g.ToList()}).OrderBy(x=>x.Name).ToList();
 }
}
