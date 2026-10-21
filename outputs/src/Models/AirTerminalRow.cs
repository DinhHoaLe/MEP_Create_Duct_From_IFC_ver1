using System;
using System.ComponentModel;

namespace IFCInfo
{
    /// <summary>A shared MEP table row for ducts and air terminals.</summary>
    public sealed class AirTerminalRow : INotifyPropertyChanged
    {
        public long NumericElementId => long.TryParse(ElementId,out long id) ? id : long.MaxValue;
        private bool selected;
        public bool IsSelected
        {
            get
            {
                return selected;
            }
            set
            {
                selected = value && CanSelect;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public IfcTerminalSource DuctSource { get; set; }
        public System.Collections.Generic.List<IfcPropertyValue> IfcProperties { get; set; } = new System.Collections.Generic.List<IfcPropertyValue>();
        public string PsetSummary => IfcProperties.Count == 0 ? "No Pset data" : IfcProperties.Count + " properties";
        public System.Collections.Generic.List<long> CorrespondingDuctIds { get; set; } = new System.Collections.Generic.List<long>();
        private string ductExistence = "Unknown";
        public bool CanSelect => ductExistence != "Already exists" && ductExistence != "Exact match"
            && ductExistence != "Size mismatch" && ductExistence != "System mismatch"
            && ductExistence != "Size and system mismatch" && ductExistence != "System unknown"
            && ductExistence != "Profile rotation mismatch";
        public string DuctExistence
        {
            get => ductExistence;
            set
            {
                ductExistence = value;
                if (!CanSelect) IsSelected = false;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DuctExistence)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSelect)));
            }
        }
        public string DuctExistenceDetail { get; set; }
        public string ElementId { get; set; }
        public string Name { get; set; }
        public string SystemType { get; set; }
        public string SystemName { get; set; }
        public string NativeShape { get; set; }
        public string Shape => !string.IsNullOrEmpty(NativeShape) ? NativeShape :
            DuctSource?.ProfileShape == "Oval" ? "Oval" : DuctSource?.DiameterMm > 0 ? "Round" :
            DuctSource?.WidthMm > 0 && DuctSource.HeightMm > 0 ?
                (Math.Abs(DuctSource.WidthMm - DuctSource.HeightMm) <= 0.1 ? "Square" : "Rectangular") : "Unknown";
        public string IfcGuid { get; set; }
        public string Elevation { get; set; }
        public string DataSource { get; set; }
    }
}
