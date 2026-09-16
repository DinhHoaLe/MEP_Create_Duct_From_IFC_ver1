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
        private readonly Button zoomSource;
        private AirTerminalRow focusedSource;
        public AirTerminalRow NavigationRow { get; private set; }
        public string NavigationAction { get; private set; }
        public Func<List<AirTerminalRow>, DuctRequest> PrepareUpdates { get; set; }
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
        public Func<string,long,List<ReplacementTypeOption>> LoadPlacementFamily { get; set; }
        public List<ReplacementTypeOption> ReplacementSystems { get; set; } = new List<ReplacementTypeOption>();
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
            Title = "Tạo phần tử Revit từ IFC";
            Width = Math.Min(1500, SystemParameters.WorkArea.Width);
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
            zoomSource=Button("Zoom tới nguồn IFC",false);
            zoomSource.Visibility=Visibility.Collapsed; zoomSource.IsEnabled=false;
            zoomSource.Click+=(s,e)=> { if(focusedSource==null) return; NavigationRow=focusedSource; NavigationAction="Zoom tới nguồn IFC"; Close(); };
            buttons.Children.Add(zoomSource);
            var next = Button("Tiếp tục →", true);
            next.IsEnabled = false;
            buttons.Children.Add(next);
            var create = Button("Tạo family →", true);
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
            var description = Text("Chọn IFC link và Category nguồn, sau đó chọn Category / Type đích trong Revit.", 15, "#647FA6");
            description.Margin = new Thickness(0, 8, 0, 0);
            card.Children.Add(description);
            var linkBox = UiDesign.Field(card, "IFC link", "Chọn link nguồn từ mô hình IFC", "Chọn IFC link...", false);
            var categoryBox = UiDesign.Field(card, "Category nguồn", "Các category có phần tử trong IFC link", "Chọn Category...", true);
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
                header.Margin = new Thickness(38, 24, 38, 4);
                scroll.Content = CountPage();
                scroll.ScrollToTop();
                next.Visibility = Visibility.Collapsed;
                back.Visibility = Visibility.Visible;
                zoomSource.Visibility=Visibility.Visible;
                create.Content = "Tạo family →";
                create.Visibility = CanCreateDucts || CanReplaceCategory ? Visibility.Visible : Visibility.Collapsed;
                feedback.Text = "";
            };
            back.Click += (s, e) =>
            {
                header.Margin = new Thickness(38, 34, 38, 28);
                heading.Text = "Chọn link và Category";
                subtitle.Text = "Bước 1 · Chọn nguồn để tiếp tục";
                progress.Content = UiDesign.Steps(1);
                scroll.Content = body;
                scroll.ScrollToTop();
                next.Visibility = Visibility.Visible;
                back.Visibility = create.Visibility = Visibility.Collapsed;
                zoomSource.Visibility=Visibility.Collapsed; zoomSource.IsEnabled=false; focusedSource=null;
                feedback.Text = "Chọn IFC link và Category.";
            };
            Action openPlacement = () =>
            {
                var selected = AirTerminals.Where(row => row.IsSelected && row.CanSelect).ToList();
                if (selected.Count == 0)
                {
                    feedback.Text = "Hãy tích chọn ít nhất một phần tử nguồn.";
                    return;
                }
                if (SelectedCategory.Id==(long)Autodesk.Revit.DB.BuiltInCategory.OST_PipeCurves || SelectedCategory.Id==(long)Autodesk.Revit.DB.BuiltInCategory.OST_CableTray)
                {
                    string kind=SelectedCategory.Id==(long)Autodesk.Revit.DB.BuiltInCategory.OST_PipeCurves?"Pipe":"CableTray";
                    var curveDialog=new CurveCreationWindow(selected,ReplacementTypes,ReplacementLevels,ReplacementSystems,SelectedCategory.Id,kind) { Owner=this };
                    if(curveDialog.ShowDialog()==true) { Replacement=curveDialog.Request; Close(); }
                    return;
                }
                var dialog = new NativePlacementWindow(selected, ReplacementTypes, ReplacementLevels,ReplacementSystems,SelectedCategory.Id,
                    SelectedCategory.Name, LoadPlacementFamily) { Owner = this };
                if (dialog.ShowDialog() == true)
                {
                    Replacement = dialog.Request;
                    Close();
                }
            };
            create.Click += (s, e) =>
            {
                if (!CanCreateDucts) { openPlacement(); return; }
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
            var panel = new StackPanel { Margin = new Thickness(28, 0, 28, 20) };
            var sourceView = new System.Windows.Data.ListCollectionView(AirTerminals);
            var table = new DataGrid
            {
                ItemsSource = sourceView,
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
                SelectionUnit = DataGridSelectionUnit.FullRow,
                SelectionMode = DataGridSelectionMode.Extended,
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
                    Width = new DataGridLength(150,DataGridLengthUnitType.Star),
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
                    Width = new DataGridLength(column.Width,DataGridLengthUnitType.Star),
                    ElementStyle = cellText
                });
            }
            focusedSource=null; zoomSource.IsEnabled=false;
            table.CurrentCellChanged+=(s,e)=> { focusedSource=table.CurrentCell.Item as AirTerminalRow; zoomSource.IsEnabled=focusedSource!=null; };
            var details = new Grid();
            details.ColumnDefinitions.Add(new ColumnDefinition());
            details.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(350) });
            details.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            details.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            details.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            table.Height=380;
            details.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            var properties=new IfcPropertiesPanel { Height=380, Margin=new Thickness(12,0,0,0) };
            var propertyToolbar=new DockPanel { Margin=new Thickness(12,0,0,8),LastChildFill=true };
            var copyProperties=Button("⧉",false);
            copyProperties.ToolTip="Sao chép toàn bộ thuộc tính và giá trị đang hiển thị";
            copyProperties.Height=40; copyProperties.MinHeight=40;
            copyProperties.Padding=new Thickness(14,0,14,0);
            copyProperties.Click+=(s,e)=>
            {
                try { Clipboard.SetText(properties.AllPropertyValues()); }
                catch (System.Runtime.InteropServices.ExternalException) { feedback.Text="Clipboard đang bận. Hãy thử sao chép lại."; }
            };
            DockPanel.SetDock(copyProperties,Dock.Right); propertyToolbar.Children.Add(copyProperties);
            var export=Button("Export",false);
            export.Height=40; export.MinHeight=40; export.Padding=new Thickness(14,0,14,0); export.FontSize=14;
            export.ToolTip="Xuất các phần tử đang tích chọn: CSV, TSV, JSON, XML";
            export.Click+=(s,e)=>
            {
                var selected=AirTerminals.Where(r=>r.IsSelected).ToList();
                if(selected.Count==0) { feedback.Text="Tích chọn ít nhất một đối tượng IFC để export."; return; }
                var file=new Microsoft.Win32.SaveFileDialog {
                    Filter="CSV (*.csv)|*.csv|TSV (*.tsv)|*.tsv|JSON (*.json)|*.json|XML (*.xml)|*.xml",
                    FileName="IFC-selected",DefaultExt=".csv",AddExtension=true };
                if(file.ShowDialog(this)!=true) return;
                try {
                    IfcSelectionExport.Export(file.FileName,selected,SelectedLink?.Name,SelectedCategory?.Name,file.FilterIndex);
                    feedback.Text="Đã export "+selected.Count+" đối tượng IFC.";
                } catch(Exception ex) {feedback.Text="Không export được: "+ex.Message;}
            };
            DockPanel.SetDock(export,Dock.Right); propertyToolbar.Children.Add(export);
            properties.SelectionTotal.Margin=new Thickness(0);
            properties.SelectionTotal.VerticalAlignment=VerticalAlignment.Center;
            propertyToolbar.Children.Add(properties.SelectionTotal);
            Grid.SetColumn(propertyToolbar,1); details.Children.Add(propertyToolbar);
            Grid.SetColumn(properties,1);
            Grid.SetRow(table,1); Grid.SetRow(properties,1);
            details.Children.Add(table); details.Children.Add(properties);
            var propertyRows=AirTerminals.ToList();
            Action refreshProperties=()=>
            {
                var checkedRows=propertyRows.Where(r=>r.IsSelected).ToList();
                properties.SetSelectionCount(checkedRows.Count, AirTerminalCount ?? propertyRows.Count);
                properties.ShowSources(checkedRows.Count>0 ? checkedRows : table.SelectedItems.Cast<AirTerminalRow>().ToList());
            };
            bool refreshPending=false;
            Action queueProperties=()=>
            {
                if (refreshPending) return;
                refreshPending=true;
                panel.Dispatcher.BeginInvoke(new Action(()=> { refreshPending=false; refreshProperties(); }),
                    System.Windows.Threading.DispatcherPriority.DataBind);
            };
            PropertyChangedEventHandler checkedChanged=(s,e)=>
            {
                if (e.PropertyName==nameof(AirTerminalRow.IsSelected)) queueProperties();
            };
            bool listening=false;
            Action subscribe=()=> { if (listening) return; foreach(var row in propertyRows) row.PropertyChanged+=checkedChanged; listening=true; };
            Action unsubscribe=()=> { if (!listening) return; foreach(var row in propertyRows) row.PropertyChanged-=checkedChanged; listening=false; };
            subscribe();
            panel.Loaded+=(s,e)=> { subscribe(); queueProperties(); };
            panel.Unloaded+=(s,e)=>unsubscribe();
            table.SelectionChanged+=(s,e)=>queueProperties();
            details.SizeChanged+=(s,e)=>
            {
                bool narrow=details.ActualWidth<900;
                details.ColumnDefinitions[1].Width=narrow ? new GridLength(0) : new GridLength(350);
                Grid.SetColumn(properties,narrow?0:1); Grid.SetRow(properties,narrow?2:1);
                properties.Margin=narrow?new Thickness(0,12,0,0):new Thickness(12,0,0,0);
                Grid.SetColumn(propertyToolbar,narrow?0:1);
                Grid.SetRow(propertyToolbar,narrow?2:0);
                Grid.SetRow(properties,narrow?3:1);
            };
            panel.Children.Add(details);
            var summary = new DockPanel { Margin=new Thickness(0,0,0,8),LastChildFill=true };
            var selectionButtons=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right };
            var selectAll=Button("Chọn tất cả",false);
            var selectNone=Button("Bỏ chọn",false);
            var filter=Button("Filter",false);
            var sort=Button("Sort",false);
            foreach(var button in new[] { filter,sort,selectAll,selectNone })
            {
                button.Height=40; button.MinHeight=40;
                button.Padding=new Thickness(14,0,14,0); button.FontSize=14;
                button.HorizontalContentAlignment=HorizontalAlignment.Center;
                button.VerticalContentAlignment=VerticalAlignment.Center;
                selectionButtons.Children.Add(button);
            }
            selectAll.Click+=(s,e)=> { foreach(var row in sourceView.Cast<AirTerminalRow>().ToList()) row.IsSelected=row.CanSelect; };
            selectNone.Click+=(s,e)=> { foreach(var row in AirTerminals) row.IsSelected=false; };
            DockPanel.SetDock(selectionButtons,Dock.Right); summary.Children.Add(selectionButtons);
            var search = new TextBox
            {
                Name="IfcElementSearch", Height=40, FontSize=14,
                VerticalContentAlignment=VerticalAlignment.Center,
                Padding=new Thickness(10,0,10,0), Margin=new Thickness(0,3,8,3),
                ToolTip="Tìm theo tên, Element ID, IFC GUID hoặc hệ thống",
                BorderBrush=Brush("#DCE4ED"), Background=Brush("#FFFFFF")
            };
            var searchLayout=new Grid();
            searchLayout.Children.Add(search);
            var placeholder=Text("Tìm kiếm phần tử IFC…",14,"#60738A");
            placeholder.Margin=new Thickness(12,0,0,0);
            placeholder.VerticalAlignment=VerticalAlignment.Center;
            placeholder.IsHitTestVisible=false;
            searchLayout.Children.Add(placeholder);
            string filterMode="all";
            string filterSystem=null;
            Action applyFilter=()=>
            {
                string query=search.Text.Trim();
                placeholder.Visibility=search.Text.Length==0 ? Visibility.Visible : Visibility.Collapsed;
                sourceView.Filter=item=>
                {
                    var row=(AirTerminalRow)item;
                    return (filterMode=="all" || (filterMode=="selected" ? row.IsSelected : !row.IsSelected))
                        && (filterSystem==null || row.SystemType==filterSystem)
                        && new[] { row.ElementId,row.Name,row.IfcGuid,row.SystemType,row.SystemName }
                        .Any(value=>(value??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0);
                };
                queueProperties();
            };
            search.TextChanged+=(s,e)=>applyFilter();
            filter.Click+=(s,e)=>
            {
                var menu=new ContextMenu();
                foreach(var option in new[] { new { Label="Tất cả",Mode="all" },new { Label="Đang tích chọn",Mode="selected" },new { Label="Chưa tích chọn",Mode="unselected" } })
                {
                    var item=new MenuItem { Header=option.Label,IsCheckable=true,IsChecked=filterMode==option.Mode && filterSystem==null };
                    item.Click+=(a,b)=> { filterMode=option.Mode;filterSystem=null;filter.Content=option.Mode=="all"?"Filter":"Filter •";applyFilter(); };
                    menu.Items.Add(item);
                }
                var systems=new MenuItem { Header="System Type" };
                foreach(string system in propertyRows.Select(r=>r.SystemType).Where(v=>!string.IsNullOrEmpty(v)).Distinct().OrderBy(v=>v))
                {
                    var item=new MenuItem { Header=system,IsCheckable=true,IsChecked=filterSystem==system };
                    item.Click+=(a,b)=> {filterMode="all";filterSystem=system;filter.Content="Filter •";applyFilter();};
                    systems.Items.Add(item);
                }
                menu.Items.Add(systems);menu.PlacementTarget=filter;menu.IsOpen=true;
            };
            sort.Click+=(s,e)=>
            {
                var menu=new ContextMenu();
                foreach(var field in new[] { new { Label="Element ID",Key="ElementId" },new { Label="Tên phần tử",Key="Name" },new { Label="System Type",Key="SystemType" },new { Label="System Name",Key="SystemName" } })
                foreach(var direction in new[] { ListSortDirection.Ascending,ListSortDirection.Descending })
                {
                    var item=new MenuItem { Header=field.Label+(direction==ListSortDirection.Ascending?" ↑":" ↓") };
                    item.Click+=(a,b)=> {sourceView.SortDescriptions.Clear();sourceView.SortDescriptions.Add(new SortDescription(field.Key,direction));};
                    menu.Items.Add(item);
                }
                menu.PlacementTarget=sort;menu.IsOpen=true;
            };
            summary.Children.Add(searchLayout);
            refreshProperties();
            details.Children.Add(summary);
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
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
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









