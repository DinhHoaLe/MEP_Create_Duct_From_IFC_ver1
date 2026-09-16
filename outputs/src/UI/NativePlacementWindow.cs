using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IFCInfo
{
    public sealed class NativePlacementWindow : Window
    {
        public ReplacementRequest Request
        {
            get; private set;
        }
        public NativePlacementWindow(List<AirTerminalRow> rows,
            List<ReplacementTypeOption> types, List<ReplacementLevelOption> levels,
            List<ReplacementTypeOption> systems = null,long sourceCategoryId = 0)
        {
            Title = "Đặt phần tử native theo IFC · Revit 2024";
            Width = 780;
            Height = 700;
            MinWidth = 580;
            MinHeight = 460;
            MaxHeight = SystemParameters.WorkArea.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 14;
            Background = IFCInfoWindow.Brush("#F3F6FA");
            var root = new DockPanel { Background = Background };
            Content = root;
            var header = new Border { Background = IFCInfoWindow.Brush("#132B46"), Padding = new Thickness(24) };
            var head = new StackPanel();
            head.Children.Add(IFCInfoWindow.Text("BƯỚC 3 · ĐẶT PHẦN TỬ REVIT", 11, "#9DBCD8"));
            var title = IFCInfoWindow.Text("Chọn Category và Type đích", 24, "#FFFFFF");
            title.Margin = new Thickness(0, 8, 0, 0);
            head.Children.Add(title);
            header.Child = head;
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            var footer = new Border { Background = Brushes.White, Padding = new Thickness(24, 14, 24, 14) };
            var footerStack = new StackPanel();
            var status = IFCInfoWindow.Text("", 12, "#9A5B12");
            footerStack.Children.Add(status);
            var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = IFCInfoWindow.Button("Quay lại", false);
            cancel.IsCancel = true;
            cancel.Click += (s, e) => Close();
            buttons.Children.Add(cancel);
            var create = IFCInfoWindow.Button("Đặt " + rows.Count + " phần tử", true); create.Name="CreateNative";
            buttons.Children.Add(create);
            footerStack.Children.Add(buttons);
            footer.Child = footerStack;
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            root.Children.Add(scroll);
            var body = new StackPanel { Margin = new Thickness(24) };
            scroll.Content = body;
            body.Children.Add(IFCInfoWindow.Text("Đã chọn " + rows.Count + " phần tử từ IFC link", 18, "#172B45"));
            body.Children.Add(IFCInfoWindow.Text("IFC được giữ làm tham chiếu. Chỉ các dòng bạn đã chọn được tạo mới.", 13, "#526880"));
            Label(body,"CATEGORY TRONG MODEL REVIT CHÍNH");
            var categories=types.GroupBy(t=>t.CategoryId).Select(g=>new CategoryOption { Id=g.Key,Name=g.First().CategoryName??"Family" }).OrderBy(c=>c.Name).ToList();
            var categoryBox=new ComboBox { Name="TargetCategory",ItemsSource=categories,MinHeight=36,IsTextSearchEnabled=true }; body.Children.Add(categoryBox);
            Label(body,"TÌM FAMILY / TYPE");
            var search=new TextBox { MinHeight=30 }; body.Children.Add(search);
            Label(body, "FAMILY / TYPE ĐÍCH");
            var typeBox = new ComboBox
            {
                Name="TargetType",
                ItemsSource = types,
                MinHeight = 36,
                IsTextSearchEnabled = true,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            body.Children.Add(typeBox);
            var placement = IFCInfoWindow.Text("Chọn type đã load trong model Revit chính.", 12, "#526880");
            placement.Margin = new Thickness(0, 8, 0, 0);
            body.Children.Add(placement);
            Label(body, "LEVEL THAM CHIẾU TRONG MODEL CHÍNH");
            var levelBox = new ComboBox { ItemsSource = levels, MinHeight = 36 };
            body.Children.Add(levelBox);
            if (levels.Count > 0)
                levelBox.SelectedIndex = 0;
            var topLabel=Label(body,"LEVEL TRÊN (CHO FAMILY HAI LEVEL)");
            var topLevel=new ComboBox { ItemsSource=levels,MinHeight=36 }; body.Children.Add(topLevel);
            if (levels.Count>1) topLevel.SelectedIndex=1;
            var systemLabel=Label(body,"SYSTEM TYPE (CHO DUCT / PIPE)");
            var systemBox=new ComboBox { Name="TargetSystem",MinHeight=36 }; body.Children.Add(systemBox);
            var lengthLabel=Label(body,"CHIỀU DÀI TUYẾN MỚI (MM; 0 = GIỮ NGUỒN)");
            var length=new TextBox { Text="0",MinHeight=32,Width=160,HorizontalAlignment=HorizontalAlignment.Left }; body.Children.Add(length);
            var keep=new CheckBox { Content="Bỏ qua phần tử lỗi, giữ phần thành công",IsChecked=false,Margin=new Thickness(0,15,0,8) }; body.Children.Add(keep);
            var angleLabel=Label(body, "GÓC XOAY FAMILY MỚI (ĐỘ)");
            var angle = new TextBox { Text = "0", Padding = new Thickness(10), Width = 150, HorizontalAlignment = HorizontalAlignment.Left };
            body.Children.Add(angle);
            var notes = IFCInfoWindow.Text("Family điểm dùng điểm đặt nguồn hoặc tâm khung bao; góc xoay nhập thêm quanh Z/pháp tuyến host. " +
                "Family cần host sẽ yêu cầu chọn mặt phẳng; Adaptive yêu cầu chọn các điểm điều khiển cho từng nguồn. Family theo đường có thể chọn 2 điểm nếu không đọc được đường nguồn.\n\n" +
                "Duct/Pipe/Cable Tray/Conduit dùng đường tim; chiều dài mới giữ đầu thứ nhất và đổi đầu còn lại. " +
                "Wall/Floor/Roof dựng native theo hình học hỗ trợ, không dùng khung bao thay hình học. Các type/kích thước không khớp sẽ báo lỗi.\n\n" +
                "Kết quả sau đặt có IFC Pset/Qto (nếu có), lưu kèm phần tử và có thể xuất CSV. Đây là dữ liệu nguồn, không tự biến thành shared parameter của Revit.", 13, "#526880");
            notes.Margin = new Thickness(0, 20, 0, 0);
            body.Children.Add(notes);
            Action validate = () =>
            {
                var type = typeBox.SelectedItem as ReplacementTypeOption;
                create.IsEnabled = rows.Count > 0 && type != null && type.Supported && levelBox.SelectedItem != null &&
                    ((type.Kind!="Duct" && type.Kind!="Pipe") || systemBox.SelectedItem!=null) &&
                    (type.PlacementMode!="TwoLevelsBased" || topLevel.SelectedItem!=null);
                placement.Text = type == null ? "Chọn type đã load trong model chính." : type.Placement;
                status.Text = types.Count == 0 ? "Chưa có Family/Type trong model chính. Hãy load family rồi chạy lại tool." :
                    levels.Count == 0 ? "Model chính chưa có Level." :
                    type != null && !type.Supported ? "Kiểu đặt family này chưa được hỗ trợ. Hãy chọn type khác." : "";
            };
            typeBox.SelectionChanged += (s, e) =>
            {
                var type=typeBox.SelectedItem as ReplacementTypeOption;
                systemBox.ItemsSource=(systems??new List<ReplacementTypeOption>()).Where(t=>t.Kind==type?.Kind).ToList();
                systemBox.IsEnabled=type?.Kind=="Duct" || type?.Kind=="Pipe";
                if (systemBox.Items.Count==1) systemBox.SelectedIndex=0;
                length.IsEnabled=type!=null && new[] { "Duct","Pipe","CableTray","Conduit" }.Contains(type.Kind);
                topLabel.Visibility=topLevel.Visibility=type?.PlacementMode=="TwoLevelsBased"?Visibility.Visible:Visibility.Collapsed;
                systemLabel.Visibility=systemBox.Visibility=systemBox.IsEnabled?Visibility.Visible:Visibility.Collapsed;
                lengthLabel.Visibility=length.Visibility=length.IsEnabled?Visibility.Visible:Visibility.Collapsed;
                angle.IsEnabled=type?.Kind=="Family" && type.PlacementMode!="Adaptive" && type.PlacementMode!="CurveBased" && type.PlacementMode!="CurveDrivenStructural";
                angleLabel.Visibility=angle.Visibility=angle.IsEnabled?Visibility.Visible:Visibility.Collapsed;
                validate();
            };
            Action filter=()=>
            {
                var category=categoryBox.SelectedItem as CategoryOption;
                typeBox.ItemsSource=types.Where(t=>t.CategoryId==category?.Id && t.Label.IndexOf(search.Text,StringComparison.OrdinalIgnoreCase)>=0).ToList(); validate();
            };
            categoryBox.SelectionChanged+=(s,e)=>filter(); search.TextChanged+=(s,e)=>filter();
            systemBox.SelectionChanged+=(s,e)=>validate();
            levelBox.SelectionChanged += (s, e) => validate();
            topLevel.SelectionChanged+=(s,e)=>validate();
            angle.TextChanged += (s, e) => validate();
            topLabel.Visibility=topLevel.Visibility=systemLabel.Visibility=systemBox.Visibility=lengthLabel.Visibility=length.Visibility=angleLabel.Visibility=angle.Visibility=Visibility.Collapsed;
            angle.IsEnabled=length.IsEnabled=systemBox.IsEnabled=false;
            validate();
            categoryBox.SelectedItem=categories.FirstOrDefault(c=>c.Id==sourceCategoryId); filter();
            create.Click += (s, e) =>
            {
                double degrees=0;
                if (angle.IsEnabled && ((!double.TryParse(angle.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out degrees) &&
                     !double.TryParse(angle.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out degrees)) ||
                    double.IsNaN(degrees) || double.IsInfinity(degrees) || Math.Abs(degrees) > 360))
                {
                    status.Text = "Nhập góc xoay hợp lệ từ -360 đến 360 độ.";
                    return;
                }
                var type = typeBox.SelectedItem as ReplacementTypeOption;
                var level = levelBox.SelectedItem as ReplacementLevelOption;
                if (type == null || !type.Supported || level == null)
                    return;
                double lengthMm=0;
                if (length.IsEnabled && (!double.TryParse(length.Text,NumberStyles.Float,CultureInfo.CurrentCulture,out lengthMm) || double.IsNaN(lengthMm) || double.IsInfinity(lengthMm) || lengthMm<0))
                { status.Text="Chiều dài phải là số không âm."; return; }
                Request = new ReplacementRequest
                {
                    TypeId = type.Id,
                    LevelId = level.Id,
                    RotationDegrees = degrees,
                    Kind=type.Kind,SystemTypeId=(systemBox.SelectedItem as ReplacementTypeOption)?.Id??0,
                    TopLevelId=(topLevel.SelectedItem as ReplacementLevelOption)?.Id??0,LengthMm=lengthMm,KeepSuccessful=keep.IsChecked==true,
                    SourceIds = rows.Select(row => row.ElementId).Distinct().ToList()
                };
                DialogResult = true;
            };
        }
        private static TextBlock Label(StackPanel body, string label)
        {
            var text = IFCInfoWindow.Text(label, 11, "#60738A");
            text.Margin = new Thickness(0, 20, 0, 8);
            body.Children.Add(text);
            return text;
        }
    }
}
