namespace IFCInfo
{
    /// <summary>A category containing elements in the linked model.</summary>
    public sealed class CategoryOption : System.ComponentModel.INotifyPropertyChanged
    {
        bool selected;
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        public bool IsChecked {get=>selected;set {if(selected==value)return;selected=value;PropertyChanged?.Invoke(this,new System.ComponentModel.PropertyChangedEventArgs(nameof(IsChecked)));}}
        public long Id { get; set; }
        public string Name { get; set; }
        public int Count {get;set;}
        public string Kind {get;set;}
        public override string ToString()
        {
            return Name;
        }
    }
}
