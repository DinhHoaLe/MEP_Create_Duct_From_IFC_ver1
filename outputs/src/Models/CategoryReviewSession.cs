using System.Collections.Generic;
namespace IFCInfo
{
    public sealed class CategoryReviewSession
    {
        public long? LinkId {get;set;}
        public List<long> CategoryIds {get;set;}=new List<long>();
        public Dictionary<long,List<string>> CheckedElements {get;set;}=new Dictionary<long,List<string>>();
        public Dictionary<long,List<IfcParameterMapping>> Mappings {get;set;}=new Dictionary<long,List<IfcParameterMapping>>();
    }
}
