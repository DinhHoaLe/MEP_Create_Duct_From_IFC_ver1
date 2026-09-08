using System.Collections.Generic;

namespace IFCInfo
{
    /// <summary>Các lựa chọn người dùng cho lượt tạo Air Terminal.</summary>
    public sealed class ReplacementRequest
    {
        public long TypeId { get; set; }
        public long LevelId { get; set; }
        public double RotationDegrees { get; set; }
        public List<string> SourceIds { get; set; }
    }
}
