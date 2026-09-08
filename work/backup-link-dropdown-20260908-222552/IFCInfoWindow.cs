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
    public sealed class CategoryOption { public long Id { get; set; } public string Name { get; set; } public override string ToString() { return Name; } }
    public sealed class AirTerminalRow : INotifyPropertyChanged
    {
        private bool selected;
        public bool IsSelected
        {
            get { return selected; }
            set { selected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public IfcTerminalSource DuctSource { get; set; }
        public string ElementId { get; set; }
        public string Name { get; set; }
        public string SystemType { get; set; }
        public string SystemName { get; set; }
        public string IfcGuid { get; set; }
        public string Elevation { get; set; }
        public string DataSource { get; set; }
    }

    // Code-only WPF: all UI is contained in the DLL for Add-In Manager Manual.
    public sealed class IFCInfoWindow : Window
    {
        private readonly TextBlock feedback;
        public List<CategoryOption> Categories { get; set; } = new List<CategoryOption>();
        public CategoryOption SelectedCategory { get; private set; }
        public Action<CategoryOption> LoadCategory { get; set; }
        public bool CanCreateDucts { get; set; }
        public Func<List<AirTerminalRow>, DuctRequest> PrepareDucts { get; set; }
        public DuctRequest DuctCreationRequest { get; private set; }
        public bool CanReplaceCategory { get; set; }
        public int? AirTerminalCount { get; set; }
        public string AirTerminalError { get; set; }
        public string IfcSourceStatus { get; set; } = "Chưa đọc IFC gốc.";
        public List<AirTerminalRow> AirTerminals { get; set; } = new List<AirTerminalRow>();
        public List<ReplacementTypeOption> ReplacementTypes { get; set; } = new List<ReplacementTypeOption>();
        public List<ReplacementLevelOption> ReplacementLevels { get; set; } = new List<ReplacementLevelOption>();
        public ReplacementRequest Replacement { get; private set; }

        internal static SolidColorBrush Brush(string hex)
        {
            return (SolidColorBrush)new BrushConverter().ConvertFromString(hex);
        }

        internal static TextBlock Text(string value, double size, string color)
        {
            return new TextBlock { Text = value, FontSize = size,
                Foreground = Brush(color), TextWrapping = TextWrapping.Wrap };
        }

        public IFCInfoWindow(string fileName, string ifcPath, string linkPath,
            string instanceId, string source, string note, bool found)
        {
            Title = "IFC Info · Revit 2024";
            Width = 1180;
            MaxWidth = SystemParameters.WorkArea.Width;
            Height = 820;
            MinWidth = 540;
            MinHeight = 440;
            MaxHeight = SystemParameters.WorkArea.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 14;
            Background = Brush("#F3F6FA");
            Foreground = Brush("#172B45");

            var root = new Grid { Background = Background };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            var header = new Border { Background = Brush("#132B46"), Padding = new Thickness(28, 22, 28, 22) };
            var headerStack = new StackPanel();
            var eyebrow = Text("IFC  /  LINK INFORMATION", 11, "#9DBCD8");
            eyebrow.FontWeight = FontWeights.SemiBold;
            headerStack.Children.Add(eyebrow);
            var heading = Text("Chọn Category", 26, "#FFFFFF");
            heading.FontWeight = FontWeights.SemiBold;
            heading.Margin = new Thickness(0, 5, 0, 5);
            headerStack.Children.Add(heading);
            var subtitle = Text("Bước 1 · Chọn Category trong IFC link để tiếp tục", 13, "#C3D2E2");
            headerStack.Children.Add(subtitle);
            header.Child = headerStack;
            root.Children.Add(header);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);
            var body = new StackPanel { Margin = new Thickness(28, 22, 28, 20) };
            scroll.Content = body;

            var fileCard = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(10),
                BorderBrush = Brush("#DFE6EE"), BorderThickness = new Thickness(1), Padding = new Thickness(20) };
            var fileStack = new StackPanel();
            fileStack.Children.Add(Text(found ? "FILE IFC" : "KHÔNG CÓ DỮ LIỆU", 11, "#60738A"));
            var name = new TextBox { Text = fileName, IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
                BorderThickness = new Thickness(0), Background = Brushes.Transparent,
                Foreground = Brush("#172B45"), FontSize = 23, FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(0), Margin = new Thickness(0, 7, 0, 12) };
            fileStack.Children.Add(name);
            var badge = new Border { HorizontalAlignment = HorizontalAlignment.Left,
                Background = Brush("#EAF1F8"), CornerRadius = new CornerRadius(5), Padding = new Thickness(9, 5, 9, 5) };
            badge.Child = Text("Link instance ID  ·  " + instanceId, 12, "#395C7C");
            fileStack.Children.Add(badge);
            fileCard.Child = fileStack;
            body.Children.Add(fileCard);
            var categoryBox = new ComboBox { MinHeight = 38, Margin = new Thickness(0, 8, 0, 12) };
            var categoryPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
            categoryPanel.Children.Add(Text("CATEGORY", 14, "#172B45"));
            categoryPanel.Children.Add(categoryBox);
            var categoryHint = Text("Chọn Category rồi bấm Tiếp tục để xem phần tử ở bước 2.", 13, "#60738A");
            categoryPanel.Children.Add(categoryHint);
            body.Children.Insert(0, categoryPanel);

            body.Children.Add(PathField("ĐƯỜNG DẪN / GIÁ TRỊ IFC", ifcPath));
            body.Children.Add(PathField("ĐƯỜNG DẪN REVIT LINK", linkPath));

            var sourceCard = new Border { Background = Brush("#EAF1F8"), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16), Margin = new Thickness(0, 18, 0, 0) };
            var sourceStack = new StackPanel();
            var sourceLabel = Text("Nguồn thông tin", 13, "#28496B");
            sourceLabel.FontWeight = FontWeights.SemiBold;
            sourceStack.Children.Add(sourceLabel);
            var sourceText = Text(source, 13, "#28496B");
            sourceText.Margin = new Thickness(0, 5, 0, 8);
            sourceStack.Children.Add(sourceText);
            sourceStack.Children.Add(Text(note, 12, "#526880"));
            sourceCard.Child = sourceStack;
            body.Children.Add(sourceCard);

            var footer = new Border { Background = Brushes.White, BorderBrush = Brush("#DFE6EE"),
                BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(28, 14, 28, 14) };
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);
            var footerStack = new StackPanel();
            feedback = Text("Chọn Category để chuyển sang bước 2.", 12, "#60738A");
            feedback.Margin = new Thickness(0, 0, 0, 10);
            footerStack.Children.Add(feedback);
            var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            bool onCountPage = false;
            var back = Button("← Back", false);
            back.Visibility = Visibility.Collapsed;
            buttons.Children.Add(back);
            var copyName = Button("Sao chép tên", false);
            copyName.IsEnabled = found;
            copyName.Click += (s, e) => Copy(fileName);
            buttons.Children.Add(copyName);
            var copyAll = Button("Sao chép tất cả", true);
            copyAll.Click += (s, e) => Copy("Tên file IFC: " + fileName + "\nIFC: " + ifcPath +
                "\nRevit link: " + linkPath + "\nLink instance ID: " + instanceId +
                "\nNguồn: " + source + "\n" + note +
                (onCountPage ? "\nCategory: " + SelectedCategory.Name + "\nSố lượng: " + (AirTerminalCount.HasValue
                    ? AirTerminalCount.Value.ToString() : "Không có kết quả") +
                    "\nPhạm vi: toàn bộ model link vừa chọn; category " + SelectedCategory.Name + ", không tính element types hoặc nested links." +
                    "\n" + AirTerminalError + "\n" + IfcSourceStatus +
                    "\nZ IFC (mm): cao độ điểm đặt theo chuỗi IfcLocalPlacement trong hệ tọa độ IFC; không phải Offset/đáy thiết bị hoặc cao độ host Revit." +
                    "\nSystem Type (IFC): ObjectType của IfcSystem, có thể khác tên System Type tùy chỉnh trong Revit.\n" + SystemTableText() : ""));
            buttons.Children.Add(copyAll);
            var next = Button("Tiếp tục →", true);
            next.IsEnabled = false;
            Loaded += (s, e) => {
                categoryBox.ItemsSource = Categories;
                categoryHint.Text = Categories.Count == 0 ? "Không có Category để chọn. Hãy kiểm tra link đã được load và có phần tử." : "Chọn Category rồi bấm Tiếp tục để xem phần tử ở bước 2.";
            };
            categoryBox.SelectionChanged += (s, e) => next.IsEnabled = categoryBox.SelectedItem is CategoryOption;
            buttons.Children.Add(next);
            next.Click += (s, e) =>
            {
                SelectedCategory = categoryBox.SelectedItem as CategoryOption;
                if (SelectedCategory == null) return;
                LoadCategory?.Invoke(SelectedCategory);
                onCountPage = true;
                heading.Text = SelectedCategory.Name;
                subtitle.Text = "Bước 2 / 2 · Thống kê trong link vừa chọn";
                scroll.Content = CountPage(fileName, instanceId);
                scroll.ScrollToTop();
                next.Visibility = Visibility.Collapsed;
                back.Visibility = Visibility.Visible;
                copyName.Visibility = Visibility.Collapsed;
                feedback.Text = "Sao chép tất cả để lấy thông tin link và kết quả đếm.";
                feedback.Foreground = Brush("#60738A");
            };
            back.Click += (s, e) =>
            {
                onCountPage = false;
                heading.Text = "Chọn Category";
                subtitle.Text = "Bước 1 · Chọn Category trong IFC link để tiếp tục";
                scroll.Content = body;
                scroll.ScrollToTop();
                next.Visibility = Visibility.Visible;
                back.Visibility = Visibility.Collapsed;
                copyName.Visibility = Visibility.Visible;
                feedback.Text = "Chọn Category rồi bấm Tiếp tục.";
                feedback.Foreground = Brush("#60738A");
            };
            var close = Button("Đóng", false);
            close.IsCancel = true;
            close.Click += (s, e) => Close();
            buttons.Children.Add(close);
            var replace = Button("Thay thế Air Terminal →", true);
            replace.Visibility = Visibility.Collapsed;
            buttons.Children.Add(replace);
            next.Click += (s, e) => replace.Visibility = onCountPage && CanReplaceCategory ? Visibility.Visible : Visibility.Collapsed;
            back.Click += (s, e) => replace.Visibility = Visibility.Collapsed;
            replace.Click += (s, e) =>
            {
                var selected = AirTerminals.Where(row => row.IsSelected).ToList();
                if (selected.Count == 0)
                { feedback.Text = "Hãy tích chọn ít nhất một Air Terminal trong bảng."; return; }
                var replacementWindow = new AirTerminalReplacementWindow(selected, ReplacementTypes, ReplacementLevels) { Owner = this };
                if (replacementWindow.ShowDialog() == true)
                {
                    Replacement = replacementWindow.Request;
                    Close();
                }
            };
            var createDucts = Button("Tạo Duct →", true);
            createDucts.Visibility = Visibility.Collapsed;
            buttons.Children.Add(createDucts);
            next.Click += (s, e) => {
                createDucts.Visibility = onCountPage && CanCreateDucts ? Visibility.Visible : Visibility.Collapsed;
                if (CanCreateDucts) { subtitle.Text = "Bước 2 / 3 · Chọn ống để tạo Duct"; feedback.Text = "Tích chọn ống, rồi bấm Tạo Duct để kiểm tra hình học và chọn type."; }
            };
            back.Click += (s, e) => createDucts.Visibility = Visibility.Collapsed;
            createDucts.Click += (s, e) => {
                var selected = AirTerminals.Where(row => row.IsSelected).ToList();
                if (selected.Count == 0) { feedback.Text = "Hãy tích chọn ít nhất một ống trong bảng."; return; }
                try {
                    var request = PrepareDucts?.Invoke(selected);
                    if (request != null) { DuctCreationRequest = request; Close(); }
                }
                catch (Exception ex) { feedback.Text = "Không chuẩn bị được Duct: " + ex.Message; }
            };
            footerStack.Children.Add(buttons);
            footer.Child = footerStack;
        }

        private FrameworkElement CountPage(string fileName, string instanceId)
        {
            var panel = new StackPanel { Margin = new Thickness(28, 22, 28, 20) };
            panel.Children.Add(Text(fileName, 18, "#172B45"));
            var context = Text("Link instance ID · " + instanceId, 12, "#60738A");
            context.Margin = new Thickness(0, 6, 0, 20);
            panel.Children.Add(context);
            var card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(10),
                BorderBrush = Brush("#DFE6EE"), BorderThickness = new Thickness(1), Padding = new Thickness(24) };
            var stack = new StackPanel();
            stack.Children.Add(Text(SelectedCategory.Name.ToUpperInvariant(), 12, "#60738A"));
            var count = Text(AirTerminalCount.HasValue ? AirTerminalCount.Value.ToString("N0") : "—", 40, "#176BBD");
            count.FontWeight = FontWeights.SemiBold;
            count.Margin = new Thickness(0, 8, 0, 2);
            stack.Children.Add(count);
            stack.Children.Add(Text(AirTerminalCount.HasValue ? "phần tử trong model link" : "Chưa có kết quả đếm", 14, "#395C7C"));
            card.Child = stack;
            panel.Children.Add(card);
            var sourceStatus = Text(IfcSourceStatus, 12, "#395C7C");
            sourceStatus.Margin = new Thickness(0, 12, 0, 0);
            panel.Children.Add(sourceStatus);
            var tableLabel = Text("CHI TIẾT HỆ THỐNG", 12, "#60738A");
            tableLabel.Margin = new Thickness(0, 18, 0, 8);
            panel.Children.Add(tableLabel);
            var table = new DataGrid
            {
                ItemsSource = AirTerminals, AutoGenerateColumns = false, IsReadOnly = false,
                CanUserAddRows = false, CanUserDeleteRows = false, CanUserReorderColumns = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = Brush("#E6ECF2"),
                Background = Brushes.White, RowBackground = Brushes.White,
                AlternatingRowBackground = Brush("#F5F8FC"), BorderBrush = Brush("#DFE6EE"),
                BorderThickness = new Thickness(1), Height = 240, MinRowHeight = 34,
                ColumnHeaderHeight = 36, FontSize = 13,
                SelectionUnit = DataGridSelectionUnit.CellOrRowHeader,
                ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader,
                EnableRowVirtualization = true, EnableColumnVirtualization = true
            };
            var cellText = new Style(typeof(TextBlock));
            cellText.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(8, 6, 8, 6)));
            cellText.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            var check = new FrameworkElementFactory(typeof(CheckBox));
            check.SetValue(CheckBox.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            check.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            check.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding("IsSelected")
            { Mode = System.Windows.Data.BindingMode.TwoWay, UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged });
            table.Columns.Add(new DataGridTemplateColumn { Header = "Chọn", Width = 60,
                CellTemplate = new DataTemplate { VisualTree = check } });
            foreach (var column in new[] {
                new { Header = "Element ID", Property = "ElementId", Width = 95.0 },
                new { Header = "Phần tử", Property = "Name", Width = 220.0 },
                new { Header = "System Type", Property = "SystemType", Width = 150.0 },
                new { Header = "System Name", Property = "SystemName", Width = 220.0 },
                new { Header = "Z IFC (mm)", Property = "Elevation", Width = 110.0 },
                new { Header = "Nguồn dữ liệu", Property = "DataSource", Width = 190.0 },
                new { Header = "IFC GUID", Property = "IfcGuid", Width = 210.0 } })
            {
                table.Columns.Add(new DataGridTextColumn { Header = column.Header,
                    IsReadOnly = true,
                    Binding = new System.Windows.Data.Binding(column.Property),
                    Width = new DataGridLength(column.Width), ElementStyle = cellText });
            }
            var selectionBar = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            var selectAll = Button("Chọn tất cả", false);
            selectAll.Click += (s, e) => { foreach (var row in AirTerminals) row.IsSelected = true; };
            var selectNone = Button("Bỏ chọn", false);
            selectNone.Click += (s, e) => { foreach (var row in AirTerminals) row.IsSelected = false; };
            selectionBar.Children.Add(selectAll);
            selectionBar.Children.Add(selectNone);
            panel.Children.Add(selectionBar);
            panel.Children.Add(table);
            var parameterNote = Text("Ưu tiên quan hệ hệ thống trong IFC gốc khi khớp IFC GUID; nếu thiếu thì đọc parameter Revit. " +
                "System Type từ IFC là ObjectType của IfcSystem, có thể khác tên type tùy chỉnh trong Revit. " +
                "Z IFC (mm) là cao độ điểm đặt theo chuỗi tọa độ IFC; không phải Offset, cao độ đáy thiết bị hoặc cao độ trong model Revit chính.", 12, "#526880");
            parameterNote.Margin = new Thickness(0, 8, 0, 0);
            panel.Children.Add(parameterNote);
            var details = Text("Đếm phần tử thuộc category " + SelectedCategory.Name + " trên toàn bộ model link vừa chọn. " +
                "Không giới hạn theo view hiện tại; không tính element types, model chính hoặc nested links.", 13, "#526880");
            details.Margin = new Thickness(0, 20, 0, 14);
            panel.Children.Add(details);
            panel.Children.Add(Text("Nếu IFC được import thành Generic Models hoặc category khác, " +
                "các phần tử đó chưa được tính vào Air Terminals.", 13, "#526880"));
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
            var result = new StringBuilder("Element ID\tPhần tử\tSystem Type\tSystem Name\tZ IFC (mm)\tNguồn dữ liệu\tIFC GUID");
            foreach (var row in AirTerminals)
            {
                result.AppendLine();
                result.Append(string.Join("\t", new[] { CleanCell(row.ElementId), CleanCell(row.Name),
                    CleanCell(row.SystemType), CleanCell(row.SystemName), CleanCell(row.Elevation),
                    CleanCell(row.DataSource), CleanCell(row.IfcGuid) }));
            }
            return result.ToString();
        }

        private static string CleanCell(string value)
        {
            return (value ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
        }

        private static FrameworkElement PathField(string label, string value)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };
            panel.Children.Add(Text(label, 11, "#60738A"));
            var frame = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1), BorderBrush = Brush("#DFE6EE"),
                Margin = new Thickness(0, 7, 0, 0), Padding = new Thickness(12, 10, 12, 10) };
            frame.Child = new TextBox { Text = string.IsNullOrWhiteSpace(value) ? "Không có thông tin" : value,
                IsReadOnly = true, BorderThickness = new Thickness(0), Padding = new Thickness(0),
                Background = Brushes.Transparent, Foreground = Brush("#263E57"),
                TextWrapping = TextWrapping.Wrap, MaxHeight = 100,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            panel.Children.Add(frame);
            return panel;
        }

        internal static Button Button(string caption, bool primary)
        {
            var button = new Button { Content = caption, Padding = new Thickness(16, 10, 16, 10),
                Margin = new Thickness(6, 3, 0, 3), Cursor = System.Windows.Input.Cursors.Hand,
                Background = Brush(primary ? "#176BBD" : "#F0F4F8"),
                Foreground = primary ? Brushes.White : Brush("#263E57"),
                BorderBrush = Brush(primary ? "#176BBD" : "#DCE4ED"), BorderThickness = new Thickness(1) };
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
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




