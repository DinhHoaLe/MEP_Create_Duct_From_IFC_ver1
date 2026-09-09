using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IFCInfo
{
    // Code-only WPF: all UI is contained in the DLL for Add-In Manager Manual.
    public sealed class IFCInfoWindow : Window
    {
        private readonly TextBlock feedback;
        public List<LinkOption> Links { get; set; } = new List<LinkOption>();
        public LinkOption SelectedLink
        {
            get; private set;
        }
        public Action<LinkOption> LoadLink
        {
            get; set;
        }
        public List<CategoryOption> Categories { get; set; } = new List<CategoryOption>();
        public CategoryOption SelectedCategory
        {
            get; private set;
        }
        public Action<CategoryOption> LoadCategory
        {
            get; set;
        }
        public bool CanCreateDucts
        {
            get; set;
        }
        public Func<List<AirTerminalRow>, DuctRequest> PrepareDucts
        {
            get; set;
        }
        public DuctRequest DuctCreationRequest
        {
            get; private set;
        }
        public bool CanReplaceCategory
        {
            get; set;
        }
        public int? AirTerminalCount
        {
            get; set;
        }
        public string AirTerminalError
        {
            get; set;
        }
        public string IfcSourceStatus { get; set; } = "Chưa đọc IFC gốc.";
        public List<AirTerminalRow> AirTerminals { get; set; } = new List<AirTerminalRow>();
        public List<ReplacementTypeOption> ReplacementTypes { get; set; } = new List<ReplacementTypeOption>();
        public List<ReplacementLevelOption> ReplacementLevels { get; set; } = new List<ReplacementLevelOption>();
        public ReplacementRequest Replacement
        {
            get; private set;
        }

        internal static SolidColorBrush Brush(string hex)
        {
            return (SolidColorBrush)new BrushConverter().ConvertFromString(hex);
        }

        internal static TextBlock Text(string value, double size, string color)
        {
            return new TextBlock
            {
                Text = value,
                FontSize = size,
                Foreground = Brush(color),
                TextWrapping = TextWrapping.Wrap
            };
        }

        public IFCInfoWindow()
        {
            Title = "Tạo MEP từ IFC";
            Width = 1160;
            Height = 800;
            MinWidth = 640;
            MinHeight = 540;
            MaxWidth = SystemParameters.WorkArea.Width;
            MaxHeight = SystemParameters.WorkArea.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 14;
            Background = UiDesign.Background;
            Foreground = Brush("#102A50");
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            var root = new DockPanel { Background = Background };
            Content = root;
            var header = new Grid { Margin = new Thickness(38, 34, 38, 28) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(UiDesign.Logo());
            var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(titles, 1);
            header.Children.Add(titles);
            var heading = Text("Chọn link và Category", 30, "#0C244B");
            heading.FontWeight = FontWeights.Bold;
            var subtitle = Text("Bước 1 · Chọn nguồn để tiếp tục", 17, "#617BA2");
            subtitle.Margin = new Thickness(0, 8, 0, 0);
            titles.Children.Add(heading);
            titles.Children.Add(subtitle);
            var progress = new ContentControl { Content = UiDesign.Steps(1), Margin = new Thickness(20, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(progress, 2);
            header.Children.Add(progress);
            header.SizeChanged += (s, e) => progress.Visibility = header.ActualWidth < 960 ? Visibility.Collapsed : Visibility.Visible;
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);
            var footer = new Grid { Margin = new Thickness(38, 20, 38, 26) };
            footer.ColumnDefinitions.Add(new ColumnDefinition());
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);
            var status = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 8, 16, 8) };
            var info = Text("ⓘ", 24, "#7A96BA");
            info.Margin = new Thickness(0, 0, 14, 0);
            status.Children.Add(info);
            feedback = Text("Chọn IFC link và Category để tiếp tục.", 14, "#647FA6");
            feedback.MaxWidth = 310;
            status.Children.Add(feedback);
            footer.Children.Add(status);
            var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(buttons, 1);
            footer.Children.Add(buttons);
            footer.SizeChanged += (s, e) =>
            {
                bool compact = footer.ActualWidth < 850 || buttons.DesiredSize.Width > footer.ActualWidth - 300;
                Grid.SetRow(buttons, compact ? 1 : 0);
                Grid.SetColumn(buttons, compact ? 0 : 1);
                Grid.SetColumnSpan(buttons, compact ? 2 : 1);
                Grid.SetColumnSpan(status, compact ? 2 : 1);
            };
            var back = Button("← Quay lại", false);
            back.Visibility = Visibility.Collapsed;
            buttons.Children.Add(back);
            var copy = Button("Sao chép bảng", false);
            copy.Visibility = Visibility.Collapsed;
            buttons.Children.Add(copy);
            copy.Click += (s, e) => Copy(SystemTableText());
            var next = Button("Tiếp tục →", true);
            next.IsEnabled = false;
            buttons.Children.Add(next);
            var replace = Button("Thay thế Air Terminal →", true);
            replace.Visibility = Visibility.Collapsed;
            buttons.Children.Add(replace);
            var create = Button("Tạo Duct →", true);
            create.Visibility = Visibility.Collapsed;
            buttons.Children.Add(create);
            var close = Button("Đóng", false);
            close.IsCancel = true;
            close.Click += (s, e) => Close();
            buttons.Children.Add(close);
            var body = new StackPanel { Margin = new Thickness(38, 6, 38, 20) };
            var card = new StackPanel { Margin = new Thickness(34, 25, 34, 32) };
            body.Children.Add(new Border
            {
                Child = card,
                Background = Brushes.White,
                CornerRadius = new CornerRadius(16),
                BorderBrush = Brush("#E0EAF7"),
                BorderThickness = new Thickness(1),
                Effect = UiDesign.Shadow()
            });
            var sourceTitle = Text("Thông tin nguồn", 22, "#102A50");
            sourceTitle.FontWeight = FontWeights.SemiBold;
            card.Children.Add(sourceTitle);
            var description = Text("Chọn IFC link và Category để tạo MEP từ mô hình IFC.", 15, "#647FA6");
            description.Margin = new Thickness(0, 8, 0, 0);
            card.Children.Add(description);
            var linkBox = UiDesign.Field(card, "IFC link", "Chọn link nguồn từ mô hình IFC", "Chọn IFC link...", false);
            var categoryBox = UiDesign.Field(card, "Category", "Chọn Category cần tạo MEP", "Chọn Category...", true);
            categoryBox.IsEnabled = false;
            var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            root.Children.Add(scroll);
            Loaded += (s, e) =>
            {
                linkBox.ItemsSource = Links;
                feedback.Text = Links.Count == 0 ? "Không tìm thấy IFC link trong model. Hãy link IFC rồi mở lại tool." : "Chọn IFC link và Category.";
            };
            linkBox.SelectionChanged += (s, e) =>
            {
                next.IsEnabled = false;
                categoryBox.ItemsSource = null;
                categoryBox.IsEnabled = false;
                SelectedLink = linkBox.SelectedItem as LinkOption;
                SelectedCategory = null;
                Replacement = null;
                DuctCreationRequest = null;
                Categories = new List<CategoryOption>();
                AirTerminals = new List<AirTerminalRow>();
                LoadCategory = null;
                PrepareDucts = null;
                CanCreateDucts = false;
                CanReplaceCategory = false;
                if (SelectedLink == null)
                    return;
                if (!SelectedLink.IsLoaded)
                {
                    feedback.Text = "Link chưa được load. Hãy load link trong Revit rồi mở lại tool.";
                    return;
                }
                try
                {
                    LoadLink?.Invoke(SelectedLink);
                    categoryBox.ItemsSource = Categories;
                    categoryBox.IsEnabled = Categories.Count > 0;
                    feedback.Text = Categories.Count == 0 ? "Link không có Category chứa phần tử." : "Chọn Category rồi bấm Tiếp tục.";
                }
                catch (Exception ex) { feedback.Text = "Không đọc được link: " + ex.Message; }
            };
            categoryBox.SelectionChanged += (s, e) => next.IsEnabled = categoryBox.SelectedItem is CategoryOption;
            next.Click += (s, e) =>
            {
                SelectedCategory = categoryBox.SelectedItem as CategoryOption;
                if (SelectedCategory == null || LoadCategory == null)
                    return;
                try
                {
                    LoadCategory(SelectedCategory);
                }
                catch (Exception ex) { feedback.Text = "Không đọc được Category: " + ex.Message; return; }
                heading.Text = SelectedCategory.Name;
                progress.Content = UiDesign.Steps(2);
                subtitle.Text = "Bước 2 · Chọn phần tử";
                scroll.Content = CountPage();
                scroll.ScrollToTop();
                next.Visibility = Visibility.Collapsed;
                back.Visibility = Visibility.Visible;
                copy.Visibility = Visibility.Visible;
                replace.Visibility = CanReplaceCategory ? Visibility.Visible : Visibility.Collapsed;
                create.Visibility = CanCreateDucts ? Visibility.Visible : Visibility.Collapsed;
                feedback.Text = "Tích chọn các phần tử cần tạo trong model chính.";
            };
            back.Click += (s, e) =>
            {
                heading.Text = "Chọn link và Category";
                subtitle.Text = "Bước 1 · Chọn nguồn để tiếp tục";
                progress.Content = UiDesign.Steps(1);
                scroll.Content = body;
                scroll.ScrollToTop();
                next.Visibility = Visibility.Visible;
                back.Visibility = copy.Visibility = replace.Visibility = create.Visibility = Visibility.Collapsed;
                feedback.Text = "Chọn IFC link và Category.";
            };
            replace.Click += (s, e) =>
            {
                var selected = AirTerminals.Where(row => row.IsSelected && row.CanSelect).ToList();
                if (selected.Count == 0)
                {
                    feedback.Text = "Hãy tích chọn ít nhất một Air Terminal.";
                    return;
                }
                var dialog = new AirTerminalReplacementWindow(selected, ReplacementTypes, ReplacementLevels) { Owner = this };
                if (dialog.ShowDialog() == true)
                {
                    Replacement = dialog.Request;
                    Close();
                }
            };
            create.Click += (s, e) =>
            {
                var selected = AirTerminals.Where(row => row.IsSelected && row.CanSelect).ToList();
                if (selected.Count == 0)
                {
                    feedback.Text = "Hãy tích chọn ít nhất một ống.";
                    return;
                }
                try
                {
                    var request = PrepareDucts?.Invoke(selected);
                    if (request != null)
                    {
                        DuctCreationRequest = request;
                        Close();
                    }
                }
                catch (Exception ex) { feedback.Text = "Không chuẩn bị được Duct: " + ex.Message; }
            };
        }

        private FrameworkElement CountPage()
        {
            var panel = new StackPanel { Margin = new Thickness(28, 22, 28, 20) };
            var card = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(10),
                BorderBrush = Brush("#DFE6EE"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(24)
            };
            var stack = new StackPanel();
            stack.Children.Add(Text(SelectedCategory.Name.ToUpperInvariant(), 12, "#60738A"));
            var count = Text(AirTerminalCount.HasValue ? AirTerminalCount.Value.ToString("N0") : "—", 40, "#176BBD");
            count.FontWeight = FontWeights.SemiBold;
            count.Margin = new Thickness(0, 8, 0, 2);
            stack.Children.Add(count);
            stack.Children.Add(Text(AirTerminalCount.HasValue ? "phần tử trong model link" : "Chưa có kết quả đếm", 14, "#395C7C"));
            card.Child = stack;
            panel.Children.Add(card);
            var tableLabel = Text("CHI TIẾT HỆ THỐNG", 12, "#60738A");
            tableLabel.Margin = new Thickness(0, 18, 0, 8);
            panel.Children.Add(tableLabel);
            var table = new DataGrid
            {
                ItemsSource = AirTerminals,
                AutoGenerateColumns = false,
                IsReadOnly = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserReorderColumns = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = Brush("#E6ECF2"),
                Background = Brushes.White,
                RowBackground = Brushes.White,
                AlternatingRowBackground = Brush("#F5F8FC"),
                BorderBrush = Brush("#DFE6EE"),
                BorderThickness = new Thickness(1),
                Height = 240,
                MinRowHeight = 34,
                ColumnHeaderHeight = 36,
                FontSize = 13,
                SelectionUnit = DataGridSelectionUnit.CellOrRowHeader,
                ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader,
                EnableRowVirtualization = true,
                EnableColumnVirtualization = true
            };
            var cellText = new Style(typeof(TextBlock));
            cellText.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(8, 6, 8, 6)));
            cellText.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            var check = new FrameworkElementFactory(typeof(CheckBox));
            check.SetValue(CheckBox.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            check.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            check.SetBinding(CheckBox.IsEnabledProperty, new System.Windows.Data.Binding("CanSelect"));
            check.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding("IsSelected")
            {
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
            });
            table.Columns.Add(new DataGridTemplateColumn
            {
                Header = "Chọn",
                Width = 60,
                CellTemplate = new DataTemplate { VisualTree = check }
            });
            if (CanCreateDucts)
            {
                var statusStyle = new Style(typeof(TextBlock), cellText);
                statusStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
                statusStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brush("#9A6A16")));
                statusStyle.Setters.Add(new Setter(TextBlock.ToolTipProperty, new System.Windows.Data.Binding("DuctExistenceDetail")));
                var exists = new DataTrigger { Binding = new System.Windows.Data.Binding("DuctExistence"), Value = "Khớp hoàn toàn" };
                exists.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brush("#16815D")));
                statusStyle.Triggers.Add(exists);
                foreach (string mismatch in new[] { "Sai kích thước", "Sai hệ thống", "Sai kích thước và hệ thống" })
                {
                    var error = new DataTrigger { Binding = new System.Windows.Data.Binding("DuctExistence"), Value = mismatch };
                    error.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brush("#B54747")));
                    statusStyle.Triggers.Add(error);
                }
                var missing = new DataTrigger { Binding = new System.Windows.Data.Binding("DuctExistence"), Value = "Chưa tồn tại" };
                missing.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brush("#637FA5")));
                statusStyle.Triggers.Add(missing);
                table.Columns.Add(new DataGridTextColumn
                {
                    Header = "Đối chiếu Duct",
                    Width = 215,
                    IsReadOnly = true,
                    Binding = new System.Windows.Data.Binding("DuctExistence"),
                    ElementStyle = statusStyle
                });
            }
            foreach (var column in new[] {
                new { Header = "Element ID", Property = "ElementId", Width = 95.0 },
                new { Header = "Phần tử", Property = "Name", Width = 220.0 },
                new { Header = "System Type", Property = "SystemType", Width = 150.0 },
                new { Header = "System Name", Property = "SystemName", Width = 220.0 },
                new { Header = "Cao độ (mm)", Property = "Elevation", Width = 110.0 } })
            {
                table.Columns.Add(new DataGridTextColumn
                {
                    Header = column.Header,
                    IsReadOnly = true,
                    Binding = new System.Windows.Data.Binding(column.Property),
                    Width = new DataGridLength(column.Width),
                    ElementStyle = cellText
                });
            }
            var selectionBar = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            var selectAll = Button("Chọn tất cả", false);
            selectAll.Click += (s, e) => { foreach (var row in AirTerminals) row.IsSelected = row.CanSelect; };
            var selectNone = Button("Bỏ chọn", false);
            selectNone.Click += (s, e) => { foreach (var row in AirTerminals) row.IsSelected = false; };
            selectionBar.Children.Add(selectAll);
            selectionBar.Children.Add(selectNone);
            panel.Children.Add(selectionBar);
            panel.Children.Add(table);
            if (!AirTerminalCount.HasValue)
            {
                var error = Text(AirTerminalError ?? "Không đọc được số lượng. Hãy chạy lại tool.", 13, "#9A5B12");
                error.Margin = new Thickness(0, 16, 0, 0);
                panel.Children.Add(error);
            }
            return panel;
        }

        public string SystemTableText()
        {
            var result = new StringBuilder((CanCreateDucts ? "Duct đã tồn tại\t" : "") + "Element ID\tPhần tử\tSystem Type\tSystem Name\tCao độ (mm)");
            foreach (var row in AirTerminals)
            {
                result.AppendLine();
                if (CanCreateDucts)
                    result.Append(CleanCell(row.DuctExistence) + "\t");
                result.Append(string.Join("\t", new[] { CleanCell(row.ElementId), CleanCell(row.Name),
                    CleanCell(row.SystemType), CleanCell(row.SystemName), CleanCell(row.Elevation) }));
            }
            return result.ToString();
        }

        private static string CleanCell(string value)
        {
            return (value ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
        }

        internal static Button Button(string caption, bool primary)
        {
            var button = new Button
            {
                Content = caption,
                Padding = new Thickness(24, 13, 24, 13),
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                MinHeight = 48,
                Margin = new Thickness(6, 3, 0, 3),
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = primary ? (Brush)new LinearGradientBrush(Color.FromRgb(30, 137, 255), Color.FromRgb(12, 111, 236), 90) : Brush("#F7FAFF"),
                Foreground = primary ? Brushes.White : Brush("#17335B"),
                Effect = UiDesign.Shadow(primary ? .18 : .06),
                BorderBrush = Brush(primary ? "#176BBD" : "#DCE4ED"),
                BorderThickness = new Thickness(1)
            };
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            border.AppendChild(presenter);
            var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(UIElement.OpacityProperty, 0.85));
            template.Triggers.Add(hover);
            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45));
            template.Triggers.Add(disabled);
            button.Template = template;
            return button;
        }

        private void Copy(string value)
        {
            try
            {
                Clipboard.SetText(value);
                feedback.Text = "Đã sao chép vào clipboard.";
                feedback.Foreground = Brush("#16744B");
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                feedback.Text = "Clipboard đang bận. Vui lòng thử lại.";
                feedback.Foreground = Brush("#9A5B12");
            }
        }
    }
}









