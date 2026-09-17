using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace IFCInfo
{
    public sealed class CurveCreationWindow : Window
    {
        public ReplacementRequest Request { get; private set; }
        public CurveCreationWindow(List<AirTerminalRow> rows,List<ReplacementTypeOption> types,
            List<ReplacementLevelOption> levels,List<ReplacementTypeOption> systems,long categoryId,string kind)
        {
            string label=kind=="Pipe"?"Pipe":kind=="Conduit"?"Conduit":"Cable Tray";
            Title="Bước 3 · Tạo "+label+" từ IFC"; Width=900; Height=690; MinWidth=620; MinHeight=440;
            MaxHeight=SystemParameters.WorkArea.Height; WindowStartupLocation=WindowStartupLocation.CenterOwner;
            Background=UiDesign.Background; FontFamily=new System.Windows.Media.FontFamily("Segoe UI"); FontSize=14;
            var root=new DockPanel { Margin=new Thickness(28),Background=UiDesign.Background }; Content=root;
            var footer=new StackPanel { Margin=new Thickness(0,14,0,0) }; DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
            var feedback=IFCInfoWindow.Text("",13,"#9A5B12"); footer.Children.Add(feedback);
            var buttons=new WrapPanel { HorizontalAlignment=HorizontalAlignment.Right }; footer.Children.Add(buttons);
            var back=IFCInfoWindow.Button("Quay lại",false); back.IsCancel=true; back.Click+=(s,e)=>Close(); buttons.Children.Add(back);
            var create=IFCInfoWindow.Button("Tạo "+rows.Count+" "+label,true); create.Name="CreateCurve"; buttons.Children.Add(create);
            var header=new Grid { Margin=new Thickness(0,0,0,22) };
            header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            var titles=new StackPanel(); titles.Children.Add(IFCInfoWindow.Text("Thiết lập tạo "+label,27,"#102A50"));
            titles.Children.Add(IFCInfoWindow.Text("Bước 3 · Kiểm tra và tạo tuyến",14,"#637FA5")); header.Children.Add(titles);
            var steps=new ContentControl { Content=UiDesign.Steps(3) }; Grid.SetColumn(steps,1); header.Children.Add(steps);
            header.SizeChanged+=(s,e)=>steps.Visibility=header.ActualWidth<780?Visibility.Collapsed:Visibility.Visible;
            DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
            var body=new StackPanel(); root.Children.Add(new ScrollViewer { Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto });
            body.Children.Add(new Border { Background=IFCInfoWindow.Brush("#E7F2FF"),CornerRadius=new CornerRadius(10),Padding=new Thickness(18,12,18,12),Margin=new Thickness(0,0,0,16),
                Child=IFCInfoWindow.Text("Đã chọn: "+rows.Count+" đoạn · Kiểm tra hình học khi tạo",16,"#1367C1") });
            var settings=new StackPanel { Margin=new Thickness(16,12,16,14) };
            body.Children.Add(new Border { Child=settings,Background=System.Windows.Media.Brushes.White,CornerRadius=new CornerRadius(14),BorderBrush=IFCInfoWindow.Brush("#DFE9F6"),BorderThickness=new Thickness(1) });
            settings.Children.Add(IFCInfoWindow.Text(label+" · "+rows.Count+" đoạn",20,"#102A50"));
            var type=UiDesign.Field(settings,label+" Type","Type cùng Category trong model chính","Chọn Type",false,true); type.Name="CurveType";
            type.ItemsSource=types.Where(t=>t.CategoryId==categoryId && t.Kind==kind && t.Supported).ToList(); if(type.Items.Count==1) type.SelectedIndex=0;
            var mappings=new Dictionary<string,ComboBox>();
            if(kind=="Pipe") foreach(string key in rows.Select(r=>r.SystemType??"").Distinct())
            {
                var box=UiDesign.Field(settings,"System Type: "+(key==""?"Không có thông tin":key),"Ánh xạ IFC sang Piping System Type","Chọn hệ thống",false,true);
                box.ItemsSource=systems.Where(t=>t.Kind=="Pipe").ToList(); if(box.Items.Count==1) box.SelectedIndex=0; mappings[key]=box;
            }
            var level=UiDesign.Field(settings,"Level đích","Áp dụng cho các đoạn đã chọn; giữ cao độ đường tim IFC","Chọn Level",false,true);
            level.ItemsSource=levels; if(level.Items.Count>0) level.SelectedIndex=0;
            var policy=UiDesign.Field(settings,"Khi có lỗi","Cách xử lý lượt tạo "+label,"Chọn cách xử lý",false,true);
            policy.ItemsSource=new[] { "Hoàn tác toàn bộ","Bỏ qua lỗi, giữ phần thành công" }; policy.SelectedIndex=0;
            var note=IFCInfoWindow.Text(kind!="CableTray"?"Đường tim và đường kính lấy từ IFC. Chưa tự nối fitting.":"Đường tim, chiều rộng, chiều cao và hướng tiết diện lấy từ IFC. Chưa tự nối fitting.",13,"#526880");
            note.Margin=new Thickness(0,10,0,0);body.Children.Add(note);
            var preview=new DataGrid { ItemsSource=rows,Name="CurvePreview",AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,CanUserDeleteRows=false,Height=190,Margin=new Thickness(0,16,0,8) };
            foreach(var column in new[] { new[] { "Nguồn IFC","ElementId" },new[] { "Phần tử","Name" },new[] { "System Type","SystemType" },new[] { "Cao độ (mm)","Elevation" } })
                preview.Columns.Add(new DataGridTextColumn { Header=column[0],Binding=new Binding(column[1]),Width=new DataGridLength(1,DataGridLengthUnitType.Star) });
            body.Children.Add(preview);
            create.IsEnabled=rows.Count>0;
            create.Click+=(s,e)=>
            {
                if(!(type.SelectedItem is ReplacementTypeOption selectedType) || !(level.SelectedItem is ReplacementLevelOption selectedLevel) || mappings.Values.Any(b=>b.SelectedItem==null))
                { feedback.Text=(kind=="Pipe"?"Chọn đủ Type, Level và System Type.":"Chọn đủ Type và Level.")+" Nếu thiếu type, nạp type vào model chính rồi mở lại tool."; return; }
                Request=new ReplacementRequest { Kind=kind,SourceCategoryId=categoryId,TypeId=selectedType.Id,LevelId=selectedLevel.Id,
                    KeepSuccessful=policy.SelectedIndex==1,SourceIds=rows.Select(r=>r.ElementId).Distinct().ToList(),
                    SystemTypes=mappings.ToDictionary(p=>p.Key,p=>((ReplacementTypeOption)p.Value.SelectedItem).Id) };
                if(Request.SystemTypes.Count>0) Request.SystemTypeId=Request.SystemTypes.Values.First();
                DialogResult=true;
            };
        }
    }
}
