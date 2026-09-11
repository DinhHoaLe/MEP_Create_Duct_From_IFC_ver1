namespace IFCInfo
{
    /// <summary>Type trong model chính cùng khả năng đặt và Category tương ứng.</summary>
    public sealed class ReplacementTypeOption
    {
        public long Id { get; set; }
        public string Label { get; set; }
        public string Placement { get; set; }
        public bool Supported { get; set; }
        public bool NeedsHost { get; set; }
        public long CategoryId { get; set; }
        public string CategoryName { get; set; }
        public string Kind { get; set; } = "Family";
        public string PlacementMode { get; set; }
        public override string ToString()
        {
            return Label;
        }
    }
}
