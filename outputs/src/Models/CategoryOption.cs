namespace IFCInfo
{
    /// <summary>A category containing elements in the linked model.</summary>
    public sealed class CategoryOption
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public override string ToString()
        {
            return Name;
        }
    }
}
