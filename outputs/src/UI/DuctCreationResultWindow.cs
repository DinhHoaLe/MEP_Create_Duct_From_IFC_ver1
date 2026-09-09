using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IFCInfo
{
    internal sealed class DuctCreationResultWindow : Window
    {
        internal DuctCreationResultWindow(int createdCount)
        {
            Title = "Tạo Duct từ IFC · Kết quả";
            Width = 580;
            SizeToContent = SizeToContent.Height;
            MaxHeight = SystemParameters.WorkArea.Height;
            MaxWidth = SystemParameters.WorkArea.Width;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = UiDesign.Background;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 14;
            UseLayoutRounding = true;

            var root = new DockPanel { Margin = new Thickness(28) };
            Content = root;
            var close = IFCInfoWindow.Button("Đóng", true);
            close.HorizontalAlignment = HorizontalAlignment.Right;
            close.Margin = new Thickness(0, 20, 0, 0);
            close.MinWidth = 112;
            close.IsDefault = true;
            close.IsCancel = true;
            close.Click += (s, e) => Close();
            DockPanel.SetDock(close, Dock.Bottom);
            root.Children.Add(close);

            var body = new StackPanel();
            root.Children.Add(new ScrollViewer
            {
                Content = body,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            });
            var title = IFCInfoWindow.Text(createdCount > 0 ? "Tạo Duct hoàn tất" : "Không có Duct mới được tạo", 26, "#102A50");
            title.FontWeight = FontWeights.Bold;
            body.Children.Add(title);
            var subtitle = IFCInfoWindow.Text("Kết quả tạo ống từ IFC vào model chính", 14, "#637FA5");
            subtitle.Margin = new Thickness(0, 6, 0, 20);
            body.Children.Add(subtitle);

            var summary = new StackPanel();
            var count = IFCInfoWindow.Text(createdCount.ToString("N0") + " đoạn ống đã tạo", 22, "#1367C1");
            count.FontWeight = FontWeights.SemiBold;
            summary.Children.Add(count);
            var status = IFCInfoWindow.Text(createdCount > 0
                ? "Các đoạn ống mới đang được chọn trong model chính."
                : "Các nguồn đã được tạo trước đó được bỏ qua.", 14, "#526880");
            status.Margin = new Thickness(0, 8, 0, 0);
            summary.Children.Add(status);
            body.Children.Add(new Border
            {
                Background = IFCInfoWindow.Brush("#E7F2FF"),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(18),
                Child = summary,
                Margin = new Thickness(0, 0, 0, 16)
            });

            var details = new StackPanel();
            var detailsTitle = IFCInfoWindow.Text("Thông tin sau khi tạo", 16, "#102A50");
            detailsTitle.FontWeight = FontWeights.SemiBold;
            details.Children.Add(detailsTitle);
            AddNote(details, createdCount > 0
                ? "IFC link được giữ nguyên. Có thể dùng Undo để hoàn tác lượt tạo."
                : "IFC link được giữ nguyên.");
            AddNote(details, "Chưa tạo fitting hoặc kết nối mạng ống.");
            AddNote(details, "System Name nguồn được lưu trong Comments. Revit quản lý tên hệ thống thực tế.");
            body.Children.Add(new Border
            {
                Background = Brushes.White,
                BorderBrush = IFCInfoWindow.Brush("#DFE9F6"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(18),
                Effect = UiDesign.Shadow(),
                Child = details
            });
        }

        private static void AddNote(StackPanel panel, string message)
        {
            var note = IFCInfoWindow.Text(message, 14, "#526880");
            note.Margin = new Thickness(0, 10, 0, 0);
            panel.Children.Add(note);
        }
    }
}
