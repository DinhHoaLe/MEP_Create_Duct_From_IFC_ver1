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
            Width = 900;
            Height = 690;
            MinWidth = 620;
            MinHeight = 440;
            MaxHeight = SystemParameters.WorkArea.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = UiDesign.Background;
            UseLayoutRounding = true;
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
            FontSize = 14;
            var root = new DockPanel { Margin = new Thickness(28), Background = Background };
            Content = root;
            var footer = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);
            var feedback = IFCInfoWindow.Text("", 13, "#9A5B12");
            footer.Children.Add(feedback);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var back = IFCInfoWindow.Button("Quay lại", false);
            back.IsCancel = true;
            back.Click += (s, e) => Close();
            buttons.Children.Add(back);
            var create = IFCInfoWindow.Button("Tạo " + items.Count + " Duct", true);
            create.IsEnabled = items.Count > 0;
            buttons.Children.Add(create);
            footer.Children.Add(buttons);
            var header = new Grid { Margin = new Thickness(0,0,0,22) };
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titlePanel = new StackPanel(); header.Children.Add(titlePanel);
            var title = IFCInfoWindow.Text("Thiết lập tạo Duct", 27, "#102A50");
            title.FontWeight = FontWeights.Bold; titlePanel.Children.Add(title);
            titlePanel.Children.Add(IFCInfoWindow.Text("Bước 3 · Kiểm tra và tạo ống", 14, "#637FA5"));
            var steps = new ContentControl { Content = UiDesign.Steps(3), Margin = new Thickness(12,0,0,0) };
            Grid.SetColumn(steps,1); header.Children.Add(steps);
            header.SizeChanged += (s,e) => steps.Visibility = header.ActualWidth < 780 ? Visibility.Collapsed : Visibility.Visible;
            DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
            var body = new StackPanel();
            root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            var summary = new Border { Background = IFCInfoWindow.Brush("#E7F2FF"), CornerRadius = new CornerRadius(10), Padding = new Thickness(18,12,18,12), Margin = new Thickness(0,0,0,16) };
            summary.Child = IFCInfoWindow.Text("Sẵn sàng tạo  " + items.Count + " đoạn     ·     Bỏ qua  " + issues.Count + " đoạn", 16, "#1367C1");
            body.Children.Add(summary);
            var settings = new StackPanel { Margin = new Thickness(22,0,22,22) };
            body.Children.Add(new Border { Child = settings, Background = System.Windows.Media.Brushes.White,
                CornerRadius = new CornerRadius(14), BorderBrush = IFCInfoWindow.Brush("#DFE9F6"), BorderThickness = new Thickness(1) });
            Func<string, List<DuctChoice>, ComboBox> choice = (label, values) =>
            {
                bool system = label.StartsWith("System Type");
                var box = UiDesign.Field(settings, label, system ? "Chọn hệ thống tương ứng trong model chính" : "Chọn loại ống sử dụng trong model chính",
                    system ? "Chọn System Type..." : "Chọn Duct Type...", system);
                box.ItemsSource = values;
                if (values.Count == 1) box.SelectedIndex = 0;
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
            var note = IFCInfoWindow.Text("System Name nguồn được lưu trong Comments. Chưa tạo fitting hoặc nối mạng ống; IFC link được giữ nguyên.", 13, "#526880");
            note.Margin = new Thickness(2,16,2,8); body.Children.Add(note);
            if (issues.Count > 0)
                body.Children.Add(new SkippedDuctsPanel(issues));
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



