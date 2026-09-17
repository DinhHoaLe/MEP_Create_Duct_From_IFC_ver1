using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace IFCInfo
{
    public sealed class IfcPropertiesPanel : Border
    {
        private readonly DataGrid properties;
        private readonly TextBlock selectionCount;
        private List<KeyValuePair<string,string>> currentValues = new List<KeyValuePair<string,string>>();
        public TextBlock SelectionTotal => selectionCount;
        public string AllPropertyValues() => string.Join(System.Environment.NewLine,
            currentValues.Select(p=>p.Key+"\t"+p.Value));
        public IfcPropertiesPanel()
        {
            Name = "IfcPropertiesPanel";
            Background = System.Windows.Media.Brushes.White;
            BorderBrush = IFCInfoWindow.Brush("#DFE6EE");
            BorderThickness = new Thickness(1); CornerRadius = new CornerRadius(10); Padding = new Thickness(12);
            var layout = new DockPanel(); Child = layout;
            var heading = IFCInfoWindow.Text("Properties · Nguồn IFC", 18, "#102A50");
            DockPanel.SetDock(heading, Dock.Top); layout.Children.Add(heading);
            heading.Margin = new Thickness(0,0,0,8);
            selectionCount = IFCInfoWindow.Text("Total: 0 / 0",12,"#60738A");
            selectionCount.Name="IfcSelectionTotal";
            selectionCount.Margin=new Thickness(0,0,0,8);
            properties = new DataGrid { Name="IfcPropertyValues", AutoGenerateColumns=false, IsReadOnly=true,
                CanUserAddRows=false, CanUserDeleteRows=false, HeadersVisibility=DataGridHeadersVisibility.Column,
                ClipboardCopyMode=DataGridClipboardCopyMode.IncludeHeader, MinRowHeight=28,
                Background=System.Windows.Media.Brushes.White, BorderThickness=new Thickness(0) };
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            style.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(5)));
            properties.Columns.Add(new DataGridTextColumn { Header="Thuộc tính", Binding=new Binding("Key"), Width=new DataGridLength(1,DataGridLengthUnitType.Star), ElementStyle=style });
            properties.Columns.Add(new DataGridTextColumn { Header="Giá trị", Binding=new Binding("Value"), Width=new DataGridLength(1.2,DataGridLengthUnitType.Star), ElementStyle=style });
            properties.GroupStyle.Add(new GroupStyle
            {
                ContainerStyle = (Style)System.Windows.Markup.XamlReader.Parse(@"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='GroupItem'>
 <Setter Property='Template'>
  <Setter.Value>
   <ControlTemplate TargetType='GroupItem'>
    <Expander IsExpanded='True' Margin='0,2,0,0'>
     <Expander.Header>
      <Border Background='#E8E8E8' BorderBrush='#D1D1D1' BorderThickness='0,0,0,1' Padding='5,3'>
       <TextBlock Text='{Binding Name}' FontWeight='SemiBold' Foreground='#303030'/>
      </Border>
     </Expander.Header>
     <ItemsPresenter/>
    </Expander>
   </ControlTemplate>
  </Setter.Value>
 </Setter>
</Style>")
            });
            layout.Children.Add(properties);
        }
        public void SetSelectionCount(int selected, int total)
            => selectionCount.Text = $"Total: {selected:N0} / {total:N0}";

        public void ShowSource(AirTerminalRow row)
            => ShowSources(row == null ? new AirTerminalRow[0] : new[] { row });

        public void ShowSources(IEnumerable<AirTerminalRow> selected)
        {
            var rows=selected.Where(r=>r!=null).Distinct().ToList();
            if (rows.Count==0)
            {
                currentValues=new List<KeyValuePair<string,string>>();
                properties.ItemsSource=currentValues;
                return;
            }
            var maps=rows.Select(r=>Values(r).GroupBy(p=>p.Key).ToDictionary(g=>g.Key,
                g=>g.Select(p=>p.Value??"").Distinct().Count()==1 ? g.First().Value??"" : "<varies>")).ToList();
            var values=new List<KeyValuePair<string,string>>();
            foreach (string key in maps.SelectMany(m=>m.Keys).Distinct())
            {
                var entries=maps.Where(m=>m.ContainsKey(key)).Select(m=>m[key]).Distinct().ToList();
                values.Add(new KeyValuePair<string,string>(key,entries.Count==1 && maps.All(m=>m.ContainsKey(key)) ? entries[0] : "<varies>"));
            }
            currentValues=values;
            var view=new ListCollectionView(values);
            view.GroupDescriptions.Add(new PropertyGroupDescription("Key",new PropertySectionConverter()));
            properties.ItemsSource=view;
            if (values.Count>0) properties.ScrollIntoView(values[0]);
        }

        private static List<KeyValuePair<string,string>> Values(AirTerminalRow row)
        {
            var values = new List<KeyValuePair<string,string>>();
            values.Add(new KeyValuePair<string,string>("Cao độ (mm)",row.Elevation));
            var duct=row.DuctSource;
            if (duct != null)
            {
                foreach (var size in new[] { new KeyValuePair<string,double>("Chiều dài (mm)",duct.LengthMm),
                    new KeyValuePair<string,double>("Rộng (mm)",duct.WidthMm),new KeyValuePair<string,double>("Cao (mm)",duct.HeightMm),
                    new KeyValuePair<string,double>("Đường kính (mm)",duct.DiameterMm) })
                    if (size.Value>0) values.Add(new KeyValuePair<string,string>(size.Key,size.Value.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
            }
            values.Add(new KeyValuePair<string,string>("System Type",row.SystemType));
            values.Add(new KeyValuePair<string,string>("System Name",row.SystemName));
            values.Add(new KeyValuePair<string,string>("Element ID trong link",row.ElementId));
            values.Add(new KeyValuePair<string,string>("IFC GUID",row.IfcGuid));
            foreach (var p in row.IfcProperties)
                values.Add(new KeyValuePair<string,string>(p.Scope+" / "+p.SetName+" / "+p.Name,
                    p.Value+(string.IsNullOrEmpty(p.Unit)?"":" ["+p.Unit+"]")));
            if (row.IfcProperties.Count==0) values.Add(new KeyValuePair<string,string>("Pset / Qto","Không có dữ liệu thuộc tính IFC đã đọc."));
            return values;
        }

        private sealed class PropertySectionConverter : IValueConverter
        {
            public object Convert(object value,System.Type targetType,object parameter,CultureInfo culture)
            {
                string key=value as string ?? "";
                if (key=="Cao độ (mm)") return "Constraints";
                if (key=="Chiều dài (mm)" || key=="Rộng (mm)" || key=="Cao (mm)" || key=="Đường kính (mm)") return "Dimensions";
                if (key=="System Type" || key=="System Name") return "Mechanical";
                if (key=="Element ID trong link" || key=="IFC GUID") return "Identity Data";
                var parts=key.Split(new[] { " / " },System.StringSplitOptions.None);
                return parts.Length>=3 ? "IFC · "+parts[1] : "IFC Property Sets";
            }
            public object ConvertBack(object value,System.Type targetType,object parameter,CultureInfo culture)
                => Binding.DoNothing;
        }
    }
}
