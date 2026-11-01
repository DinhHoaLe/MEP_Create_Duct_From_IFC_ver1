namespace IFCInfo
{
 public sealed class ParameterOption
 {
  public string Id {get;set;}
  public string Name {get;set;}
  public string DataKind {get;set;}
  public string Binding {get;set;} public string Categories {get;set;}
  public string Discipline {get;set;}
  public override string ToString()=>Name;
 }
}
