using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IFCInfo
{
    public sealed class AirTerminalReplacementWindow : Window
    {
        public ReplacementRequest Request
        {
            get; private set;
        }
        public AirTerminalReplacementWindow(List<AirTerminalRow> rows,
            List<ReplacementTypeOption> types, List<ReplacementLevelOption> levels)
        {
            Title = "Thay thế Air Terminal · Revit 2024";
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
            head.Children.Add(IFCInfoWindow.Text("BƯỚC 3 · FAMILY THAY THẾ", 11, "#9DBCD8"));
            var title = IFCInfoWindow.Text("Tạo Air Terminal trong model chính", 24, "#FFFFFF");
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
            var create = IFCInfoWindow.Button("Tạo " + rows.Count + " Air Terminals", true);
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
            Label(body, "FAMILY / TYPE AIR TERMINAL");
            var typeBox = new ComboBox
            {
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
            Label(body, "GÓC XOAY FAMILY MỚI (ĐỘ)");
            var angle = new TextBox { Text = "0", Padding = new Thickness(10), Width = 150, HorizontalAlignment = HorizontalAlignment.Left };
            body.Children.Add(angle);
            var notes = IFCInfoWindow.Text("Vị trí: dùng điểm đặt nguồn nếu có; nếu IFC chỉ có hình học, dùng tâm khung bao. " +
                "Tool tính cả phép dịch chuyển và xoay của link. Family mới dùng hướng mặc định cộng góc trên: quanh Z nếu không host, " +
                "quanh pháp tuyến mặt nếu có host. Không tự sao chép hướng của hình học IFC.\n\n" +
                "Family cần host: sau khi bấm Tạo, chọn một mặt host phẳng trong model chính. Tool chiếu các vị trí lên mặt này; " +
                "phần tử ngoài phạm vi mặt host sẽ làm hủy lượt tạo.\n\n" +
                "System Name / Type từ IFC được lưu làm thông tin nguồn. Family mới chưa được nối ống gió hoặc gán vào mạng MEP.", 13, "#526880");
            notes.Margin = new Thickness(0, 20, 0, 0);
            body.Children.Add(notes);
            Action validate = () =>
            {
                var type = typeBox.SelectedItem as ReplacementTypeOption;
                create.IsEnabled = rows.Count > 0 && type != null && type.Supported && levelBox.SelectedItem != null;
                placement.Text = type == null ? "Chọn type đã load trong model chính." : type.Placement;
                status.Text = types.Count == 0 ? "Chưa có Air Terminal Family/Type. Hãy load family rồi chạy lại tool." :
                    levels.Count == 0 ? "Model chính chưa có Level." :
                    type != null && !type.Supported ? "Kiểu đặt family này chưa được hỗ trợ. Hãy chọn type khác." : "";
            };
            typeBox.SelectionChanged += (s, e) => validate();
            levelBox.SelectionChanged += (s, e) => validate();
            angle.TextChanged += (s, e) => validate();
            validate();
            create.Click += (s, e) =>
            {
                double degrees;
                if ((!double.TryParse(angle.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out degrees) &&
                     !double.TryParse(angle.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out degrees)) ||
                    double.IsNaN(degrees) || double.IsInfinity(degrees) || Math.Abs(degrees) > 360)
                {
                    status.Text = "Nhập góc xoay hợp lệ từ -360 đến 360 độ.";
                    return;
                }
                var type = typeBox.SelectedItem as ReplacementTypeOption;
                var level = levelBox.SelectedItem as ReplacementLevelOption;
                if (type == null || !type.Supported || level == null)
                    return;
                Request = new ReplacementRequest
                {
                    TypeId = type.Id,
                    LevelId = level.Id,
                    RotationDegrees = degrees,
                    SourceIds = rows.Select(row => row.ElementId).Distinct().ToList()
                };
                DialogResult = true;
            };
        }
        private static void Label(StackPanel body, string label)
        {
            var text = IFCInfoWindow.Text(label, 11, "#60738A");
            text.Margin = new Thickness(0, 20, 0, 8);
            body.Children.Add(text);
        }
    }
}
