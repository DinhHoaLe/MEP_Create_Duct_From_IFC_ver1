namespace IFCInfo
{
    /// <summary>Một lựa chọn Duct Type hoặc System Type trong hộp thoại.</summary>
    public sealed class DuctChoice
    {
        public long Id;
        public string Name;
        public override string ToString() => Name;
    }
}
