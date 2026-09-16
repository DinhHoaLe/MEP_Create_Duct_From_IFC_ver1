using System.Collections.Generic;

namespace IFCInfo
{
    /// <summary>Lựa chọn Category/Type native, Level và cách đặt theo nguồn IFC.</summary>
    public sealed class ReplacementRequest
    {
        public long TypeId { get; set; }
        public long LevelId { get; set; }
        public double RotationDegrees { get; set; }
        public List<string> SourceIds { get; set; }
        public string Kind { get; set; } = "Family";
        public long SystemTypeId { get; set; }
        public bool KeepSuccessful { get; set; }
        public double LengthMm { get; set; }
        public long TopLevelId { get; set; }
    }
}
