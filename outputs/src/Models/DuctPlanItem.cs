using Autodesk.Revit.DB;

namespace IFCInfo
{
    /// <summary>A planned duct segment with geometry in host-model coordinates.</summary>
    public sealed class DuctPlanItem : System.ComponentModel.INotifyPropertyChanged
    {
        public AirTerminalRow Source;
        public XYZ Start, End, WidthAxis;
        public double Width, Height, Diameter;
        public string Key;
        public bool Round => Diameter > 0;
        public bool Oval;
        public string ShapeKey => Round ? "Round" : Oval ? "Oval" : System.Math.Abs(Width - Height) <= 0.1 / 304.8 ? "Square" : "Rectangular";
        public string ShapeName => DuctRequest.ShapeLabel(ShapeKey);
        public string GroupKey => DuctRequest.SystemKey(ShapeKey, Source?.SystemType);
        public string PreviewSystem => Source?.SystemType;
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
        public string PreviewTarget => ExistingId > 0 ? ExistingId.ToString() : "New";
        public string PreviewChange => ChangeSummary ?? "Create new";
    }
}
