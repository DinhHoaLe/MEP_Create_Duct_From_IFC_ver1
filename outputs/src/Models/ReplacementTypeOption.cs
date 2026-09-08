namespace IFCInfo
{
    /// <summary>Một family type có thể dùng để tạo Air Terminal.</summary>
    public sealed class ReplacementTypeOption
    {
        public long Id { get; set; }
        public string Label { get; set; }
        public string Placement { get; set; }
        public bool Supported { get; set; }
        public bool NeedsHost { get; set; }
        public override string ToString()
        {
            return Label;
        }
    }
}
