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
                selected = value && CanSelect;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public IfcTerminalSource DuctSource { get; set; }
        private string ductExistence = "Chưa xác định";
        public bool CanSelect => ductExistence != "Đã tồn tại" && ductExistence != "Khớp hoàn toàn"
            && ductExistence != "Sai kích thước" && ductExistence != "Sai hệ thống"
            && ductExistence != "Sai kích thước và hệ thống" && ductExistence != "Chưa xác định hệ thống";
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
        public string IfcGuid { get; set; }
        public string Elevation { get; set; }
        public string DataSource { get; set; }
    }
}
