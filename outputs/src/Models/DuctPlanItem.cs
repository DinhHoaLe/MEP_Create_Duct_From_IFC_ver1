using Autodesk.Revit.DB;

namespace IFCInfo
{
    /// <summary>Một đoạn Duct đã xác định hình học trong tọa độ model chính, chờ tạo.</summary>
    public sealed class DuctPlanItem
    {
        public AirTerminalRow Source;
        public XYZ Start, End, WidthAxis;
        public double Width, Height, Diameter;
        public string Key;
        public bool Round => Diameter > 0;
        public long ExistingId;
        public string LevelKey, ChangeSummary;
        public bool Include { get; set; } = true;
        public string PreviewSource => Source?.ElementId;
        public string PreviewTarget => ExistingId > 0 ? ExistingId.ToString() : "Mới";
        public string PreviewChange => ChangeSummary ?? "Tạo mới";
    }
}
