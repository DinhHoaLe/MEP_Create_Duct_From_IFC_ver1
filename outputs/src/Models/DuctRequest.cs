using System.Collections.Generic;

namespace IFCInfo
{
    /// <summary>Yêu cầu tạo Duct sau khi người dùng chọn type và ánh xạ hệ thống.</summary>
    public sealed class DuctRequest
    {
        // Ánh xạ riêng theo tiết diện để hai nhóm có thể chọn hệ thống độc lập.
        public static string SystemKey(bool round, string sourceType) => (round ? "Round|" : "Rectangular|") + (sourceType ?? "");
        public List<DuctPlanItem> Items;
        public long RoundTypeId, RectangularTypeId;
        public Dictionary<string, long> SystemTypes = new Dictionary<string, long>();
    }
}

