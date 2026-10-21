using System.Collections.Generic;

namespace IFCInfo
{
    /// <summary>Native category/type, level and placement choices for an IFC source.</summary>
    public sealed class ReplacementRequest
    {
        public long TypeId { get; set; }
        public long SourceCategoryId { get; set; }
        public long LevelId { get; set; }
        public double RotationDegrees { get; set; }
        public List<string> SourceIds { get; set; }
        public string Kind { get; set; } = "Family";
        public long SystemTypeId { get; set; }
        public Dictionary<string,long> SystemTypes { get; set; } = new Dictionary<string,long>();
        public bool KeepSuccessful { get; set; }
        public double LengthMm { get; set; }
        public long TopLevelId { get; set; }
    }
}
