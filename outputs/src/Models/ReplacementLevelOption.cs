namespace IFCInfo
{
    /// <summary>Một Level có thể chọn khi tạo Air Terminal.</summary>
    public sealed class ReplacementLevelOption
    {
        public long Id { get; set; }
        public string Label { get; set; }
        public override string ToString()
        {
            return Label;
        }
    }
}
