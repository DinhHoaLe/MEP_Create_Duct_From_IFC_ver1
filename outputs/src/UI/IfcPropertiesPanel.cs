using System.Collections.Generic;
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
        public TextBlock SelectionTotal => selectionCount;
        public string AllPropertyValues() => string.Join(System.Environment.NewLine,
            properties.Items.Cast<KeyValuePair<string,string>>().Select(p=>p.Key+"\t"+p.Value));
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
                properties.ItemsSource=new List<KeyValuePair<string,string>>(); return;
            }
            var maps=rows.Select(r=>Values(r).GroupBy(p=>p.Key).ToDictionary(g=>g.Key,
                g=>g.Select(p=>p.Value??"").Distinct().Count()==1 ? g.First().Value??"" : "<varies>")).ToList();
            var values=new List<KeyValuePair<string,string>>();
            foreach (string key in maps.SelectMany(m=>m.Keys).Distinct())
            {
                var entries=maps.Where(m=>m.ContainsKey(key)).Select(m=>m[key]).Distinct().ToList();
                values.Add(new KeyValuePair<string,string>(key,entries.Count==1 && maps.All(m=>m.ContainsKey(key)) ? entries[0] : "<varies>"));
            }
            properties.ItemsSource=values;
            if (values.Count>0) properties.ScrollIntoView(values[0]);
        }

        private static List<KeyValuePair<string,string>> Values(AirTerminalRow row)
        {
            var values = new List<KeyValuePair<string,string>>();
            values.Add(new KeyValuePair<string,string>("Element ID trong link",row.ElementId));
            values.Add(new KeyValuePair<string,string>("IFC GUID",row.IfcGuid));
            values.Add(new KeyValuePair<string,string>("System Type",row.SystemType));
            values.Add(new KeyValuePair<string,string>("System Name",row.SystemName));
            values.Add(new KeyValuePair<string,string>("Cao độ (mm)",row.Elevation));
            var duct=row.DuctSource;
            if (duct != null)
            {
                foreach (var size in new[] { new KeyValuePair<string,double>("Chiều dài (mm)",duct.LengthMm),
                    new KeyValuePair<string,double>("Rộng (mm)",duct.WidthMm),new KeyValuePair<string,double>("Cao (mm)",duct.HeightMm),
                    new KeyValuePair<string,double>("Đường kính (mm)",duct.DiameterMm) })
                    if (size.Value>0) values.Add(new KeyValuePair<string,string>(size.Key,size.Value.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
            }
            foreach (var p in row.IfcProperties)
                values.Add(new KeyValuePair<string,string>(p.Scope+" / "+p.SetName+" / "+p.Name,
                    p.Value+(string.IsNullOrEmpty(p.Unit)?"":" ["+p.Unit+"]")));
            if (row.IfcProperties.Count==0) values.Add(new KeyValuePair<string,string>("Pset / Qto","Không có dữ liệu thuộc tính IFC đã đọc."));
            return values;
        }
    }
}
