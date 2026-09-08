using System.Collections.Generic;

namespace IFCInfo
{
    /// <summary>Yêu cầu tạo Duct sau khi người dùng chọn type và ánh xạ hệ thống.</summary>
    public sealed class DuctRequest
    {
        public List<DuctPlanItem> Items;
        public long RoundTypeId, RectangularTypeId;
        public Dictionary<string, long> SystemTypes = new Dictionary<string, long>();
    }
}
