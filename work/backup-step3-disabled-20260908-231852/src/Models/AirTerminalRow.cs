using System;
using System.ComponentModel;

namespace IFCInfo
{
    /// <summary>Một dòng dữ liệu MEP trong bảng, dùng chung cho Duct và Air Terminal.</summary>
    public sealed class AirTerminalRow : INotifyPropertyChanged
    {
        private bool selected;
        public bool IsSelected
        {
            get
            {
                return selected;
            }
            set
            {
                selected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public IfcTerminalSource DuctSource { get; set; }
        public string DuctExistence { get; set; } = "Chưa xác định";
        public string DuctExistenceDetail { get; set; }
        public string ElementId { get; set; }
        public string Name { get; set; }
        public string SystemType { get; set; }
        public string SystemName { get; set; }
        public string IfcGuid { get; set; }
        public string Elevation { get; set; }
        public string DataSource { get; set; }
    }
}
