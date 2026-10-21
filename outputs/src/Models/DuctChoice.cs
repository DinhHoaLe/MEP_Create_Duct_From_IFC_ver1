namespace IFCInfo
{
    /// <summary>A duct type or system type choice in the settings dialog.</summary>
    public sealed class DuctChoice
    {
        public long Id;
        public string Name; public string Classification;
        public override string ToString() => Name;
    }
}
