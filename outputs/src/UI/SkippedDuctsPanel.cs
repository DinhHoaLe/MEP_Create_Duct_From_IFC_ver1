using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace IFCInfo
{
    /// <summary>Bảng chỉ đọc hiển thị các đoạn bị bỏ qua và lý do đầy đủ.</summary>
    internal sealed class SkippedDuctsPanel : Border
    {
        public SkippedDuctsPanel(List<string> issues)
        {
            Background = Brushes.White;
            BorderBrush = IFCInfoWindow.Brush("#ECDDC4");
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(12);
            Margin = new Thickness(0, 16, 0, 8);
            Padding = new Thickness(18);
            var body = new StackPanel();
            Child = body;
            var header = new DockPanel();
            var badge = new Border { Background = IFCInfoWindow.Brush("#FFF0D8"), CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10,3,10,3), VerticalAlignment = VerticalAlignment.Center };
            badge.Child = IFCInfoWindow.Text(issues.Count + " đoạn", 13, "#986017");
            DockPanel.SetDock(badge, Dock.Right); header.Children.Add(badge);
            var title = IFCInfoWindow.Text("Các đoạn bỏ qua", 18, "#694B23");
            title.FontWeight = FontWeights.SemiBold; header.Children.Add(title); body.Children.Add(header);
            var hint = IFCInfoWindow.Text("Các đoạn dưới đây sẽ không được tạo trong lượt này. Chọn ô và nhấn Ctrl+C để sao chép.", 13, "#738098");
            hint.Margin = new Thickness(0,7,0,14); body.Children.Add(hint);
            var rows = issues.Select(issue => {
                string text = issue ?? "";
                int split = text.IndexOf(':');
                long id;
                bool hasId = split > 0 && long.TryParse(text.Substring(0,split).Trim(), out id);
                return new { ElementId = hasId ? text.Substring(0,split).Trim() : "—",
                    Reason = hasId ? text.Substring(split+1).Trim() : text };
            }).ToList();
            var table = new DataGrid { ItemsSource = rows, AutoGenerateColumns = false, IsReadOnly = true,
                CanUserAddRows = false, CanUserDeleteRows = false, CanUserReorderColumns = false,
                HeadersVisibility = DataGridHeadersVisibility.Column, RowHeaderWidth = 0,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = IFCInfoWindow.Brush("#E8EDF5"), BorderBrush = IFCInfoWindow.Brush("#E3EAF3"),
                Background = Brushes.White, RowBackground = Brushes.White, AlternatingRowBackground = IFCInfoWindow.Brush("#F7FAFE"),
                MinRowHeight = 46, ColumnHeaderHeight = 38, FontSize = 14,
                MaxHeight = 300, MinHeight = 90, SelectionUnit = DataGridSelectionUnit.CellOrRowHeader,
                ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader,
                EnableRowVirtualization = true, EnableColumnVirtualization = true };
            var headerStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, IFCInfoWindow.Brush("#EDF3FA")));
            headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, IFCInfoWindow.Brush("#4C6485")));
            headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12,8,12,8)));
            headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            table.ColumnHeaderStyle = headerStyle;
            var textStyle = new Style(typeof(TextBlock));
            textStyle.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(12,10,12,10)));
            textStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            textStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, IFCInfoWindow.Brush("#344D6C")));
            textStyle.Setters.Add(new Setter(TextBlock.ToolTipProperty, new Binding("Reason")));
            table.Columns.Add(new DataGridTextColumn { Header = "Element ID", Binding = new Binding("ElementId"), Width = 115, ElementStyle = textStyle });
            table.Columns.Add(new DataGridTextColumn { Header = "Lý do bỏ qua", MinWidth = 240, Binding = new Binding("Reason"), Width = new DataGridLength(1, DataGridLengthUnitType.Star), ElementStyle = textStyle });
            table.SizeChanged += (s, e) => table.Columns[1].Width = new DataGridLength(Math.Max(240, table.ActualWidth - 137));
            body.Children.Add(table);
        }
    }
}


