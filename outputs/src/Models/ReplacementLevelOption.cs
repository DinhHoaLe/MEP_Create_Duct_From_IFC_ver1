namespace IFCInfo
{
    /// <summary>A level option for air terminal placement.</summary>
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
