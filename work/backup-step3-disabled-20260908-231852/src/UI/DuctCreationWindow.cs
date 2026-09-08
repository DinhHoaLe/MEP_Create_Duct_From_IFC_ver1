using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace IFCInfo
{
    public sealed class DuctCreationWindow : Window
    {
        public DuctRequest Request
        {
            get; private set;
        }
        public DuctCreationWindow(List<DuctPlanItem> items, List<string> issues,
            List<DuctChoice> roundTypes, List<DuctChoice> rectangularTypes, List<DuctChoice> systems)
        {
            Title = "Bước 3 · Tạo Duct từ IFC";
            Width = 760;
            Height = 720;
            MinWidth = 540;
            MinHeight = 440;
            MaxHeight = SystemParameters.WorkArea.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = IFCInfoWindow.Brush("#F3F6FA");
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
            FontSize = 14;
            var root = new DockPanel { Margin = new Thickness(24), Background = Background };
            Content = root;
            var footer = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);
            var feedback = IFCInfoWindow.Text("", 13, "#9A5B12");
            footer.Children.Add(feedback);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var back = new Button { Content = "Quay lại", Padding = new Thickness(18, 10, 18, 10), Margin = new Thickness(8), IsCancel = true };
            back.Click += (s, e) => Close();
            buttons.Children.Add(back);
            var create = new Button { Content = "Tạo " + items.Count + " Duct", Padding = new Thickness(18, 10, 18, 10), Margin = new Thickness(8), IsEnabled = items.Count > 0 };
            buttons.Children.Add(create);
            footer.Children.Add(buttons);
            var body = new StackPanel();
            root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            body.Children.Add(IFCInfoWindow.Text("Tạo ống tại vị trí IFC", 24, "#172B45"));
            body.Children.Add(IFCInfoWindow.Text("Sẵn sàng: " + items.Count + " đoạn · Bỏ qua: " + issues.Count + " đoạn", 16, "#176BBD"));
            body.Children.Add(IFCInfoWindow.Text("Lấy đường tim, cao độ và kích thước từ ống nguồn. Level được chọn theo cao độ trong model chính.", 13, "#526880"));
            Func<string, List<DuctChoice>, ComboBox> choice = (label, values) =>
            {
                var text = IFCInfoWindow.Text(label, 14, "#172B45");
                text.Margin = new Thickness(0, 18, 0, 6);
                body.Children.Add(text);
                var box = new ComboBox { ItemsSource = values, MinHeight = 34 };
                if (values.Count == 1)
                    box.SelectedIndex = 0;
                body.Children.Add(box);
                return box;
            };
            ComboBox round = null, rectangular = null;
            if (items.Any(i => i.Round))
                round = choice("Duct Type · Ống tròn", roundTypes);
            if (items.Any(i => !i.Round))
                rectangular = choice("Duct Type · Ống chữ nhật", rectangularTypes);
            var mapping = new Dictionary<string, ComboBox>();
            foreach (string sourceType in items.Select(i => i.Source.SystemType ?? "").Distinct().OrderBy(t => t))
            {
                var box = choice("System Type nguồn: " + (string.IsNullOrEmpty(sourceType) ? "Không có thông tin" : sourceType) + " → Revit", systems);
                // Exact name only: never guess a system classification from a partial name.
                box.SelectedItem = systems.FirstOrDefault(t => string.Equals(t.Name, sourceType, StringComparison.OrdinalIgnoreCase));
                mapping.Add(sourceType, box);
            }
            body.Children.Add(IFCInfoWindow.Text("System Name nguồn được lưu trong Comments. Chưa tạo fitting hoặc nối mạng ống; IFC link được giữ nguyên.", 13, "#526880"));
            if (issues.Count > 0)
            {
                var label = IFCInfoWindow.Text("CÁC ĐOẠN BỎ QUA", 14, "#9A5B12");
                label.Margin = new Thickness(0, 18, 0, 6);
                body.Children.Add(label);
                body.Children.Add(new TextBox
                {
                    Text = string.Join(Environment.NewLine, issues),
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    Height = 140,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                });
            }
            create.Click += (s, e) =>
            {
                if ((round != null && round.SelectedItem == null) || (rectangular != null && rectangular.SelectedItem == null) || mapping.Values.Any(b => b.SelectedItem == null))
                {
                    feedback.Text = "Chọn đủ Duct Type và System Type. Nếu danh sách trống, hãy nạp type vào model chính rồi chạy lại.";
                    return;
                }
                Request = new DuctRequest
                {
                    Items = items,
                    RoundTypeId = (round?.SelectedItem as DuctChoice)?.Id ?? 0,
                    RectangularTypeId = (rectangular?.SelectedItem as DuctChoice)?.Id ?? 0,
                    SystemTypes = mapping.ToDictionary(p => p.Key, p => ((DuctChoice)p.Value.SelectedItem).Id)
                };
                DialogResult = true;
            };
        }
    }
}

