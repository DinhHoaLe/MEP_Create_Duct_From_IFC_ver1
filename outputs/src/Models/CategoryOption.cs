namespace IFCInfo
{
    /// <summary>Một Category có phần tử trong model link.</summary>
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
