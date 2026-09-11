namespace IFCInfo
{
    /// <summary>Dữ liệu đọc từ IFC theo GUID: hệ thống, cao độ và kích thước nếu là Duct.</summary>
    public sealed class IfcTerminalSource
    {
        public string Guid { get; set; }
        public string Name { get; set; }
        public string SystemName { get; set; }
        public string SystemType { get; set; }
        public double? ElevationMm { get; set; }
        public double LengthMm { get; set; }
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }
        public double DiameterMm { get; set; }
        public string GeometryError { get; set; }
        public System.Collections.Generic.List<IfcPropertyValue> Properties { get; set; } = new System.Collections.Generic.List<IfcPropertyValue>();
    }
    public sealed class IfcPropertyValue
    {
        public string Scope { get; set; }
        public string SetName { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public string Unit { get; set; }
        public override string ToString() => Scope + " | " + SetName + "." + Name + " = " + Value + (string.IsNullOrEmpty(Unit) ? "" : " [" + Unit + "]");
    }
}
