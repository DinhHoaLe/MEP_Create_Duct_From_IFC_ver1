namespace IFCInfo
{
    /// <summary>An IFC link option with load state and source path.</summary>
    public sealed class LinkOption
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string IfcPath { get; set; }
        public bool IsLoaded { get; set; }
        public override string ToString()
        {
            return Name + (IsLoaded ? "" : " (unloaded)");
        }
    }
}
