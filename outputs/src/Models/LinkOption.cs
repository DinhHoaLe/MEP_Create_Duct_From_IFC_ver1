namespace IFCInfo
{
    /// <summary>Một IFC link dùng trong dropdown, kèm trạng thái load và đường dẫn nguồn.</summary>
    public sealed class LinkOption
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string IfcPath { get; set; }
        public bool IsLoaded { get; set; }
        public override string ToString()
        {
            return Name + (IsLoaded ? "" : " (chưa load)");
        }
    }
}
