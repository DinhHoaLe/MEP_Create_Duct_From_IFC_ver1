using Autodesk.Revit.DB;

namespace IFCInfo
{
    /// <summary>Một đoạn Duct đã xác định hình học trong tọa độ model chính, chờ tạo.</summary>
    public sealed class DuctPlanItem : System.ComponentModel.INotifyPropertyChanged
    {
        public AirTerminalRow Source;
        public XYZ Start, End, WidthAxis;
        public double Width, Height, Diameter;
        public string Key;
        public bool Round => Diameter > 0;
        public long ExistingId;
        public string LevelKey, ChangeSummary;
        private bool include = true;
        public bool Include
        {
            get => include;
            set
            {
                if (include == value) return;
                include = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Include)));
            }
        }
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        public string PreviewSource => Source?.ElementId;
        public string PreviewTarget => ExistingId > 0 ? ExistingId.ToString() : "Mới";
        public string PreviewChange => ChangeSummary ?? "Tạo mới";
    }
}
