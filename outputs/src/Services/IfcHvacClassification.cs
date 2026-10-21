using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
namespace IFCInfo
{
    public static class IfcHvacClassification
    {
        public static string Resolve(IEnumerable<AirTerminalRow> rows,out string evidence)
        {
            var all=rows.ToList();
            var values=all.SelectMany(r=>r.IfcProperties)
                .Where(p=>IsClassification(p.Name)).Select(p=>Map(p.Value,false)).Where(v=>v!=null).Distinct().ToList();
            if(values.Count>1) {evidence="Conflicting IFC classifications. Select a classification manually.";return null;}
            if(values.Count==1) {evidence="From IFC classification properties.";return values[0];}
            var guesses=all.Select(r=>Map(r.SystemType,true)).ToList();
            var unique=guesses.Where(v=>v!=null).Distinct().ToList();
            if(unique.Count==1 && guesses.All(v=>v!=null)) {evidence="Suggested from IFC system name; verify the classification.";return unique[0];}
            evidence="IFC has no unambiguous HVAC classification. Select one manually.";
            return null;
        }
        private static bool IsClassification(string name)
        {
            string n=Regex.Replace(name??"","[^a-zA-Z]","").ToLowerInvariant();
            return n=="systemclassification" || n=="systemclassificationname" || n=="hvacclassification";
        }
        private static string Map(string value,bool allowName)
        {
            string normalized=Regex.Replace(value??"","[^a-zA-Z]","").ToUpperInvariant();
            switch(normalized) {
                case "SUPPLYAIR":return "SupplyAir";
                case "RETURNAIR":return "ReturnAir";
                case "EXHAUSTAIR":return "ExhaustAir";
                case "OTHERAIR":return "OtherAir";
            }
            if(!allowName)return null;
            var tokens=Regex.Split((value??"").ToUpperInvariant(),"[^A-Z]+");
            var matches=new List<string>();
            if(tokens.Contains("ZULUFT") || tokens.Contains("SUPPLY"))matches.Add("SupplyAir");
            if(tokens.Contains("RUCKLUFT") || tokens.Contains("RETURN"))matches.Add("ReturnAir");
            if(tokens.Contains("ABLUFT") || tokens.Contains("FORTLUFT") || tokens.Contains("EXHAUST"))matches.Add("ExhaustAir");
            return matches.Distinct().Count()==1?matches[0]:null;
        }
    }
}