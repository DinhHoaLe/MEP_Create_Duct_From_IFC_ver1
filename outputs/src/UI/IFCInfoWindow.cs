using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace IFCInfo
{
    // Code-only WPF: all UI is contained in the DLL for Add-In Manager Manual.
    public sealed class IFCInfoWindow : Window
    {
        public Action RunDuctCreation {get;set;}
        private bool creationBusy;
        private void SubmitDuctRequest(DuctRequest request)
        {
            if(creationBusy)return;
            if(!parameterPanel.Validate(request)){workflowTabs.SelectedItem=parametersTab;return;}
            DuctCreationRequest=request;
            if(RunDuctCreation==null){Close();return;}
            creationBusy=true;
            try {RunDuctCreation();}
            catch(Exception ex){feedback.Text="Creation failed: "+ex.Message;}
            finally {
                DuctCreationRequest=null;creationBusy=false;
                settingsTab.Content=null;settingsSelection=null;
                embeddedSettings?.Close();embeddedSettings=null;
                workflowTabs.SelectedItem=elementsTab;
            }
        }
        private TabControl workflowTabs;
        private TabItem sourceTab, elementsTab, settingsTab, parametersTab;
        private ParameterMappingPanel parameterPanel;
        public Func<string,string> CheckParameterTarget {get;set;}
        public Func<List<string>> LoadParameterTargets {get;set;}
        public Func<List<AirTerminalRow>,List<IfcParameterMapping>,string> TransferParameters {get;set;}
        private List<AirTerminalRow> parameterSelection=new List<AirTerminalRow>();
        private string settingsSelection;
        private DuctCreationWindow embeddedSettings;
        private string SelectionKey() => string.Join("|",AirTerminals.Where(r=>r.IsSelected && r.CanSelect).Select(r=>r.ElementId).OrderBy(id=>id));
        internal static Style SelectionHeaderStyle(Style basis)
        {
            var style=new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader),basis);
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Center));
            style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty,VerticalAlignment.Center));
            style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(0,8,0,8)));
            return style;
        }
        public void OpenDuctSettings(DuctCreationWindow editor, Action<DuctRequest> completed)
        {
            if(embeddedSettings!=null) embeddedSettings.Close();
            embeddedSettings=editor;
            settingsSelection=SelectionKey();
            editor.BackEmbedded=()=>workflowTabs.SelectedItem=elementsTab; editor.CloseEmbedded=()=>Close();
            editor.SubmitEmbedded=request=> {completed(request);SubmitDuctRequest(request);};
            settingsTab.Content=editor.DetachBody();
            workflowTabs.SelectedItem=settingsTab;
        }
        private readonly TextBlock feedback;
        private readonly Button zoomSource;
        private AirTerminalRow focusedSource;
        private HwndSource nativeSource;
        private IntPtr nativeOwner;
        private bool detachedForMinimize;
        private const int WmSysCommand = 0x0112;
        private const int ScMinimize = 0xF020;
        private const int GwlHwndParent = -8;
        private const int SwMinimize = 6;

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnableWindow(IntPtr window, [MarshalAs(UnmanagedType.Bool)] bool enable);
        public AirTerminalRow NavigationRow { get; private set; }
        public string NavigationAction { get; private set; }
        public Action<AirTerminalRow,string> NavigateSource { get; set; }
        public Func<List<AirTerminalRow>, DuctRequest> PrepareUpdates { get; set; }
        public List<LinkOption> Links { get; set; } = new List<LinkOption>();
        public long? InitialLinkId { get; set; }
        public long? InitialCategoryId { get; set; }
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
        public Func<AirTerminalRow, IfcPreviewMesh> LoadPreview { get; set; }
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
        public string IfcSourceStatus { get; set; } = "The original IFC has not been read.";
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
            Title = "Create Duct from IFC";
            Width = Math.Min(1500, SystemParameters.WorkArea.Width);
            Height = 800;
            MinWidth = 640;
            MinHeight = 540;
            MaxWidth = SystemParameters.WorkArea.Width;
            MaxHeight = SystemParameters.WorkArea.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            // Keep a separate taskbar entry so the tool can be restored without
            // minimizing or restoring Revit along with it.
            ShowInTaskbar = true;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 14;
            Background = UiDesign.Background;
            Foreground = Brush("#37322B");
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            SourceInitialized += OnSourceInitialized;
            StateChanged += OnWindowStateChanged;
            Closed += OnWindowClosed;
            Closed += (s,e)=>embeddedSettings?.Close();
            var root = new DockPanel { Background = Background };
            UiDesign.SetContent(this, root);
            var header = new Grid { Margin = new Thickness(28, 20, 28, 12) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(UiDesign.Logo());
            var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(titles, 1);
            header.Children.Add(titles);
            var heading = Text("Select link and category", 30, "#37322B");
            heading.FontWeight = FontWeights.Bold;
            var subtitle = Text("Step 1 · Select a source to continue", 17, "#716B63");
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
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.ColumnDefinitions.Add(new ColumnDefinition());
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);
            var status = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 8, 16, 8) };
            feedback = Text("Select an IFC link and category to continue.", 14, "#716B63");
            feedback.MaxWidth = 310;
            status.Children.Add(feedback);
            Grid.SetColumn(status, 1);
            footer.Children.Add(status);
            var leftButtons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Left };
            Grid.SetColumn(leftButtons, 0);
            footer.Children.Add(leftButtons);
            var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(buttons, 2);
            footer.Children.Add(buttons);
            footer.SizeChanged += (s, e) =>
            {
                bool compact = footer.ActualWidth < 850 || buttons.DesiredSize.Width > footer.ActualWidth - 300;
                Grid.SetRow(status, 0); Grid.SetColumn(status, compact ? 0 : 1); Grid.SetColumnSpan(status, compact ? 3 : 1);
                Grid.SetRow(leftButtons, compact ? 1 : 0); Grid.SetColumn(leftButtons, 0);
                Grid.SetRow(buttons, compact ? 1 : 0); Grid.SetColumn(buttons, 2);
            };
            var back = Button("← Back", false);
            back.Visibility = Visibility.Collapsed;
            leftButtons.Children.Add(back);
            zoomSource=IconButton("Zoom to IFC source",
                "M10,3 A7,7 0 1 0 10,17 A7,7 0 1 0 10,3 M15,15 L21,21 M10,7 L10,13 M7,10 L13,10");
            zoomSource.Visibility=Visibility.Collapsed; zoomSource.IsEnabled=false;
            zoomSource.Click+=(s,e)=>ZoomToIfcSource(focusedSource);
            var next = Button("Continue →", true);
            next.IsEnabled = false;
            buttons.Children.Add(next);
            var create = Button("Create family →", true);
            create.Visibility = Visibility.Collapsed;
            buttons.Children.Add(create);
            var body = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            var card = new StackPanel { Margin = new Thickness(34, 25, 34, 32) };
            body.Children.Add(new Border
            {
                Child = card,
                Background = Brushes.White,
                CornerRadius = new CornerRadius(16),
                BorderBrush = Brush("#DDD6CE"),
                BorderThickness = new Thickness(1),
                Effect = UiDesign.Shadow()
            });
            var sourceTitle = Text("Source information", 22, "#37322B");
            sourceTitle.FontWeight = FontWeights.SemiBold;
            card.Children.Add(sourceTitle);
            var description = Text("Select an IFC link and source category, then choose the target category / type in Revit.", 15, "#716B63");
            description.Margin = new Thickness(0, 8, 0, 0);
            card.Children.Add(description);
            var linkBox = UiDesign.Field(card, "IFC link", "Select the source IFC model link", "Select IFC link...", false);
            var categoryBox = UiDesign.Field(card, "Source category", "Categories containing elements in the IFC link", "Select category...", true);
            categoryBox.IsEnabled = false;
            var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            workflowTabs=new TabControl {Name="WorkflowTabs",Background=Background,BorderThickness=new Thickness(0),Margin=new Thickness(20,4,20,0)};
            sourceTab=new TabItem {Header="Source",Content=scroll,Padding=new Thickness(20,10,20,10)};
            elementsTab=new TabItem {Header="Elements",IsEnabled=false,Padding=new Thickness(20,10,20,10)};
            settingsTab=new TabItem {Header="Duct Settings",IsEnabled=false,Padding=new Thickness(20,10,20,10)};
            UiDesign.StyleWorkflowTabs(workflowTabs);
            workflowTabs.Items.Add(sourceTab);workflowTabs.Items.Add(elementsTab);workflowTabs.Items.Add(settingsTab);
            parameterPanel=new ParameterMappingPanel {Sources=()=>AirTerminals,
                SelectedSources=()=>parameterSelection.Where(r=>AirTerminals.Contains(r)).ToList(),
                Targets=()=>LoadParameterTargets?.Invoke()??new List<string>(),
                Transfer=(rows,maps)=>TransferParameters?.Invoke(rows,maps)??"Transfer unavailable for this category.",
                CheckTarget=name=>CheckParameterTarget?.Invoke(name)??"Catalog unavailable"};
            parametersTab=new TabItem {Header="Parameters",Content=parameterPanel};
            workflowTabs.Items.Add(parametersTab);
            root.Children.Add(workflowTabs);
            workflowTabs.SelectionChanged+=(s,e)=>
            {
                if(e.Source!=workflowTabs)return;
                bool parameters=workflowTabs.SelectedItem==parametersTab;
                if(parameters)parameterPanel.Refresh();
                bool source=workflowTabs.SelectedItem==sourceTab;
                bool settings=workflowTabs.SelectedItem==settingsTab;
                footer.Visibility=settings?Visibility.Collapsed:Visibility.Visible;
                next.Visibility=source?Visibility.Visible:Visibility.Collapsed;
                back.Visibility=source||settings?Visibility.Collapsed:Visibility.Visible;
                create.Visibility=!source&&!settings&&(CanCreateDucts||CanReplaceCategory)?Visibility.Visible:Visibility.Collapsed;
                heading.Text=parameters?"Parameters":source?"Select link and category":settings?"Duct settings":SelectedCategory?.Name??"Elements";
                subtitle.Text=parameters?"Map IFC properties and transfer values":source?"Select an IFC source":settings?"Configure selected ducts":"Review IFC elements";
                progress.Content=UiDesign.Steps(source?1:settings?3:2);
                if(settings && (settingsTab.Content==null || settingsSelection!=SelectionKey()))
                {
                    settingsTab.Content=null;
                    create.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    if(settingsTab.Content==null)workflowTabs.SelectedItem=elementsTab;
                }
            };
            Loaded += (s, e) =>
            {
                linkBox.ItemsSource = Links;
                feedback.Text = Links.Count == 0 ? "No IFC link was found. Link an IFC model, then reopen the tool." : "Select an IFC link and category.";
                if (InitialLinkId.HasValue)
                {
                    linkBox.SelectedItem = Links.FirstOrDefault(link => link.Id == InitialLinkId.Value);
                    if (InitialCategoryId.HasValue)
                        categoryBox.SelectedItem = Categories.FirstOrDefault(category => category.Id == InitialCategoryId.Value);
                    if (next.IsEnabled)
                        next.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                }
            };
            linkBox.SelectionChanged += (s, e) =>
            {
                next.IsEnabled = false;
                categoryBox.ItemsSource = null;
                categoryBox.IsEnabled = false;
                elementsTab.Content=null; elementsTab.IsEnabled=false;
                settingsTab.Content=null; settingsTab.IsEnabled=false;
                SelectedLink = linkBox.SelectedItem as LinkOption;
                SelectedCategory = null;
                Replacement = null;
                DuctCreationRequest = null;
                Categories = new List<CategoryOption>();
                AirTerminals = new List<AirTerminalRow>();
                LoadCategory = null;
                LoadPreview = null;
                PrepareDucts = null;
                parameterSelection.Clear();
                TransferParameters=null; LoadParameterTargets=null; CheckParameterTarget=null;
                CanCreateDucts = false;
                CanReplaceCategory = false;
                if (SelectedLink == null)
                    return;
                if (!SelectedLink.IsLoaded)
                {
                    feedback.Text = "The link is unloaded. Load it in Revit, then reopen the tool.";
                    return;
                }
                try
                {
                    LoadLink?.Invoke(SelectedLink);
                    categoryBox.ItemsSource = Categories;
                    categoryBox.IsEnabled = Categories.Count > 0;
                    feedback.Text = Categories.Count == 0 ? "The link has no categories containing supported elements." : "Select a category, then click Continue.";
                }
                catch (Exception ex) { feedback.Text = "Could not read link: " + ex.Message; }
            };
            categoryBox.SelectionChanged += (s, e) => {
                next.IsEnabled = categoryBox.SelectedItem is CategoryOption;
                elementsTab.Content=null;elementsTab.IsEnabled=false;settingsTab.Content=null;settingsTab.IsEnabled=false;
            };
            next.Click += (s, e) =>
            {
                SelectedCategory = categoryBox.SelectedItem as CategoryOption;
                if (SelectedCategory == null || LoadCategory == null)
                    return;
                try
                {
                    using (LoadingWindow.ShowWhile(this, "Reading IFC data", "Reading properties and geometry, and comparing Revit elements…"))
                        LoadCategory(SelectedCategory);
                }
                catch (Exception ex) { feedback.Text = "Could not read category: " + ex.Message; return; }
                heading.Text = SelectedCategory.Name;
                progress.Content = UiDesign.Steps(2);
                subtitle.Text = "Step 2 · Select elements";
                header.Margin = new Thickness(38, 24, 38, 4);
                elementsTab.Content=new ScrollViewer {Content=CountPage(),VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
                elementsTab.IsEnabled=true;settingsTab.Content=null;settingsTab.IsEnabled=CanCreateDucts;
                workflowTabs.SelectedItem=elementsTab;
                scroll.ScrollToTop();
                next.Visibility = Visibility.Collapsed;
                back.Visibility = Visibility.Visible;
                zoomSource.Visibility=Visibility.Visible;
                create.Content = CanCreateDucts ? "Duct settings →" : "Create family →";
                create.Visibility = CanCreateDucts || CanReplaceCategory ? Visibility.Visible : Visibility.Collapsed;
                feedback.Text = "";
            };
            back.Click += (s, e) =>
            {
                header.Margin = new Thickness(28, 20, 28, 12);
                heading.Text = "Select link and category";
                subtitle.Text = "Step 1 · Select a source to continue";
                progress.Content = UiDesign.Steps(1);
                workflowTabs.SelectedItem=sourceTab;
                scroll.ScrollToTop();
                next.Visibility = Visibility.Visible;
                back.Visibility = create.Visibility = Visibility.Collapsed;
                zoomSource.Visibility=Visibility.Collapsed; zoomSource.IsEnabled=false; focusedSource=null;
                feedback.Text = "Select an IFC link and category.";
            };
            Action openPlacement = () =>
            {
                var selected = AirTerminals.Where(row => row.IsSelected && row.CanSelect).ToList();
                if (selected.Count == 0)
                {
                    feedback.Text = "Check at least one source element.";
                    return;
                }
                if (SelectedCategory.Id==(long)Autodesk.Revit.DB.BuiltInCategory.OST_PipeCurves || SelectedCategory.Id==(long)Autodesk.Revit.DB.BuiltInCategory.OST_CableTray || SelectedCategory.Id==(long)Autodesk.Revit.DB.BuiltInCategory.OST_Conduit)
                {
                    string kind=SelectedCategory.Id==(long)Autodesk.Revit.DB.BuiltInCategory.OST_PipeCurves?"Pipe":SelectedCategory.Id==(long)Autodesk.Revit.DB.BuiltInCategory.OST_Conduit?"Conduit":"CableTray";
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
                    feedback.Text = "Check at least one duct.";
                    return;
                }
                try
                {
                    if(settingsTab.Content!=null && settingsSelection==SelectionKey()) {workflowTabs.SelectedItem=settingsTab;return;}
                    var request = PrepareDucts?.Invoke(selected);
                    if (request != null)
                    {
                        SubmitDuctRequest(request);
                    }
                }
                catch (Exception ex) { feedback.Text = "Could not prepare ducts: " + ex.Message; }
            };
        }

        private FrameworkElement CountPage()
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
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
                HorizontalGridLinesBrush = Brush("#DDD6CE"),
                Background = Brushes.White,
                RowBackground = Brushes.White,
                AlternatingRowBackground = Brush("#F5F2EE"),
                BorderBrush = Brush("#DDD6CE"),
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
            UiDesign.StyleTable(table);
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
            var selectHeader=new CheckBox {Name="SelectAllSources",ToolTip="Select / clear all visible eligible elements",HorizontalAlignment=HorizontalAlignment.Center};
            selectHeader.Click+=(s,e)=> {var eligible=sourceView.Cast<AirTerminalRow>().Where(r=>r.CanSelect).ToList(); bool select=!eligible.All(r=>r.IsSelected); foreach(var row in eligible)row.IsSelected=select;};
            Action refreshSelectHeader=()=> {
                var eligible=sourceView.Cast<AirTerminalRow>().Where(r=>r.CanSelect).ToList();
                int count=eligible.Count(r=>r.IsSelected);
                selectHeader.IsEnabled=eligible.Count>0;
                selectHeader.IsChecked=count==0?false:count==eligible.Count?(bool?)true:null;
            };
            table.Columns.Add(new DataGridTemplateColumn
            {
                Header = selectHeader, HeaderStyle = SelectionHeaderStyle(table.ColumnHeaderStyle),
                Width = 60,
                CellTemplate = new DataTemplate { VisualTree = check }
            });
            if (CanCreateDucts)
            {
                var statusStyle = new Style(typeof(TextBlock), cellText);
                statusStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.NoWrap));
                statusStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
                statusStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
                statusStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brush("#9A6A16")));
                statusStyle.Setters.Add(new Setter(TextBlock.ToolTipProperty, new System.Windows.Data.Binding("DuctExistenceDetail")));
                var exists = new DataTrigger { Binding = new System.Windows.Data.Binding("DuctExistence"), Value = "Exact match" };
                exists.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brush("#16815D")));
                statusStyle.Triggers.Add(exists);
                foreach (string mismatch in new[] { "Size mismatch", "System mismatch", "Size and system mismatch" })
                {
                    var error = new DataTrigger { Binding = new System.Windows.Data.Binding("DuctExistence"), Value = mismatch };
                    error.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brush("#B54747")));
                    statusStyle.Triggers.Add(error);
                }
                var missing = new DataTrigger { Binding = new System.Windows.Data.Binding("DuctExistence"), Value = "Not created" };
                missing.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brush("#716B63")));
                statusStyle.Triggers.Add(missing);
                table.Columns.Add(new DataGridTextColumn
                {
                    Header = "Duct comparison",
                    Width = new DataGridLength(160),
                    MinWidth = 150,
                    IsReadOnly = true,
                    Binding = new System.Windows.Data.Binding("DuctExistence"),
                    ElementStyle = statusStyle
                });
            }
            foreach (var column in new[] {
                new { Header = "Element ID", Property = "ElementId", Width = 95.0 },
                new { Header = "Element", Property = "Name", Width = 220.0 },
                new { Header = "System Type", Property = "SystemType", Width = 150.0 },
                new { Header = "System Name", Property = "SystemName", Width = 150.0 },
                new { Header = "Shape", Property = "Shape", Width = 100.0 } })
            {
                table.Columns.Add(new DataGridTextColumn
                {
                    Header = column.Header,
                    IsReadOnly = true,
                    Binding = new System.Windows.Data.Binding(column.Property),
                    Width = new DataGridLength(column.Width,DataGridLengthUnitType.Star),
                    MinWidth = column.Property == "Name" ? 160 : column.Property == "Shape" ? 100 : 120,
                    ElementStyle = cellText
                });
            }
            focusedSource=null; zoomSource.IsEnabled=false;
            parameterSelection.Clear();
            IfcObjectViewer previewViewer=null;
            Action updateFocusedSource=()=>
            {
                parameterSelection=table.SelectedItems.Cast<AirTerminalRow>().ToList();
                var selectedRows=AirTerminals.Where(r=>r.IsSelected)
                    .Concat(table.SelectedItems.Cast<AirTerminalRow>()).Distinct().ToList();
                focusedSource=selectedRows.Count==1 ? selectedRows[0] : null;
                zoomSource.IsEnabled=focusedSource!=null;
                if (previewViewer!=null)
                {
                    if (selectedRows.Count>1)
                    {
                        previewViewer.ShowMultipleSelection(selectedRows.Count);
                        return;
                    }
                    try { previewViewer.ShowModel(focusedSource==null ? null : LoadPreview?.Invoke(focusedSource)); }
                    catch (Exception ex) { previewViewer.Clear("Could not read preview geometry: "+ex.Message); feedback.Text="Could not read preview geometry: "+ex.Message; }
                }
            };
            table.CurrentCellChanged+=(s,e)=>updateFocusedSource();
            table.SelectionChanged+=(s,e)=>updateFocusedSource();
            table.MouseDoubleClick+=(s,e)=>
            {
                updateFocusedSource();
                ZoomToIfcSource(focusedSource);
            };
            var details = new Grid();
            details.ColumnDefinitions.Add(new ColumnDefinition());
            details.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(350) });
            details.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(380) });
            details.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            details.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            details.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            table.Height=380;
            details.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            details.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            var properties=new IfcPropertiesPanel { Height=380, Margin=new Thickness(12,0,0,0) };
            previewViewer=new IfcObjectViewer { MinHeight=380,Margin=new Thickness(12,0,0,0),VerticalAlignment=VerticalAlignment.Stretch };
            previewViewer.AddHeaderAction(zoomSource);
            var propertyToolbar=new DockPanel { Margin=new Thickness(12,0,0,8),LastChildFill=true };
            var copyProperties=IconButton("Copy properties",
                "M8,8 L20,8 20,20 8,20 Z M4,4 L16,4 16,8 M4,4 L4,16 8,16");
            copyProperties.Click+=(s,e)=>
            {
                try { Clipboard.SetText(properties.AllPropertyValues()); }
                catch (System.Runtime.InteropServices.ExternalException) { feedback.Text="The clipboard is busy. Try copying again."; }
            };
            DockPanel.SetDock(copyProperties,Dock.Right); propertyToolbar.Children.Add(copyProperties);
            var export=IconButton("Export data (CSV, TSV, JSON, XML)",
                "M12,3 L12,15 M7,10 L12,15 17,10 M5,18 L5,21 19,21 19,18");
            export.Click+=(s,e)=>
            {
                var selected=AirTerminals.Where(r=>r.IsSelected).ToList();
                if(selected.Count==0) { feedback.Text="Check at least one IFC object to export."; return; }
                var file=new Microsoft.Win32.SaveFileDialog {
                    Filter="CSV (*.csv)|*.csv|TSV (*.tsv)|*.tsv|JSON (*.json)|*.json|XML (*.xml)|*.xml",
                    FileName="IFC-selected",DefaultExt=".csv",AddExtension=true };
                if(file.ShowDialog(this)!=true) return;
                try {
                    IfcSelectionExport.Export(file.FileName,selected,SelectedLink?.Name,SelectedCategory?.Name,file.FilterIndex);
                    feedback.Text="Exported "+selected.Count+" IFC objects.";
                } catch(Exception ex) {feedback.Text="Could not export: "+ex.Message;}
            };
            DockPanel.SetDock(export,Dock.Right); propertyToolbar.Children.Add(export);
            properties.SelectionTotal.Margin=new Thickness(0);
            properties.SelectionTotal.VerticalAlignment=VerticalAlignment.Center;
            propertyToolbar.Children.Add(properties.SelectionTotal);
            Grid.SetColumn(propertyToolbar,1); details.Children.Add(propertyToolbar);
            Grid.SetColumn(properties,1);
            Grid.SetRow(table,1); Grid.SetRow(properties,1);
            Grid.SetColumn(previewViewer,2); Grid.SetRow(previewViewer,0); Grid.SetRowSpan(previewViewer,2);
            details.Children.Add(table); details.Children.Add(properties); details.Children.Add(previewViewer);
            var propertyRows=AirTerminals.ToList();
            Action refreshProperties=()=>
            {
                refreshSelectHeader();
                var checkedRows=propertyRows.Where(r=>r.IsSelected).ToList();
                properties.SetSelectionCount(checkedRows.Count, AirTerminalCount ?? propertyRows.Count);
                properties.ShowSources(checkedRows.Count>0 ? checkedRows : table.SelectedItems.Cast<AirTerminalRow>().ToList());
            };
            bool refreshPending=false;
            Action refreshSelectionView=()=>{};
            bool previewPending=false;
            Action queueProperties=()=>
            {
                if (refreshPending) return;
                refreshPending=true;
                panel.Dispatcher.BeginInvoke(new Action(()=> { refreshSelectionView(); refreshProperties(); refreshPending=false; }),
                    System.Windows.Threading.DispatcherPriority.DataBind);
            };
            Action queuePreview=()=>
            {
                if (previewPending) return;
                previewPending=true;
                panel.Dispatcher.BeginInvoke(new Action(()=> { previewPending=false; updateFocusedSource(); }),
                    System.Windows.Threading.DispatcherPriority.DataBind);
            };
            PropertyChangedEventHandler checkedChanged=(s,e)=>
            {
                if (e.PropertyName==nameof(AirTerminalRow.IsSelected)) { queueProperties(); queuePreview(); }
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
                bool stackProperties=details.ActualWidth<900;
                bool stackViewer=details.ActualWidth<1250;
                details.ColumnDefinitions[1].Width=stackProperties ? new GridLength(0) : new GridLength(350);
                details.ColumnDefinitions[2].Width=stackViewer ? new GridLength(0) : new GridLength(380);
                Grid.SetColumn(properties,stackProperties?0:1); Grid.SetRow(properties,stackProperties?3:1);
                properties.Margin=stackProperties?new Thickness(0,0,0,0):new Thickness(12,0,0,0);
                Grid.SetColumn(propertyToolbar,stackProperties?0:1);
                Grid.SetRow(propertyToolbar,stackProperties?2:0);
                if (stackViewer)
                {
                    Grid.SetColumn(previewViewer,0); Grid.SetColumnSpan(previewViewer,stackProperties?1:2);
                    Grid.SetRow(previewViewer,4); Grid.SetRowSpan(previewViewer,1);
                    previewViewer.Height=380; previewViewer.Margin=new Thickness(0,12,0,0);
                }
                else
                {
                    Grid.SetColumn(previewViewer,2); Grid.SetColumnSpan(previewViewer,1);
                    Grid.SetRow(previewViewer,0); Grid.SetRowSpan(previewViewer,2);
                    previewViewer.Height=double.NaN; previewViewer.Margin=new Thickness(12,0,0,0);
                }
            };
            panel.Children.Add(details);
            var summary = new DockPanel { Margin=new Thickness(0,0,0,8),LastChildFill=true };
            var selectionButtons=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right };
            var filter=IconButton("Filter elements",
                "M3,4 L21,4 14,12 14,19 10,21 10,12 Z");
            var sort=IconButton("Sort elements",
                "M4,6 L14,6 M4,12 L11,12 M4,18 L8,18 M18,5 L18,19 M14,15 L18,19 22,15");
            filter.Name="FilterSources"; sort.Name="SortSources";
            foreach(var button in new[] { filter,sort })
            {
                button.HorizontalContentAlignment=HorizontalAlignment.Center;
                button.VerticalContentAlignment=VerticalAlignment.Center;
                selectionButtons.Children.Add(button);
            }
            DockPanel.SetDock(selectionButtons,Dock.Right); summary.Children.Add(selectionButtons);
            var search = new TextBox
            {
                Name="IfcElementSearch", Height=40, FontSize=14,
                VerticalContentAlignment=VerticalAlignment.Center,
                Padding=new Thickness(10,0,10,0), Margin=new Thickness(0,3,8,3),
                ToolTip="Search by element ID or name",
                BorderBrush=Brush("#DDD6CE"), Background=Brush("#FFFFFF")
            };
            var searchLayout=new Grid();
            searchLayout.Children.Add(search);
            var placeholder=Text("Search IFC elements…",14,"#716B63");
            placeholder.Margin=new Thickness(12,0,0,0);
            placeholder.VerticalAlignment=VerticalAlignment.Center;
            placeholder.IsHitTestVisible=false;
            searchLayout.Children.Add(placeholder);
            string filterMode="all";
            string filterExistence=null;
            refreshSelectionView=()=>
            {
                if (!sourceView.IsEditingItem && !sourceView.IsAddingNew &&
                    (filterMode!="all" || sourceView.SortDescriptions.Any(d=>d.PropertyName=="IsSelected"))) sourceView.Refresh();
            };
            Action applyFilter=()=>
            {
                string query=search.Text.Trim();
                placeholder.Visibility=search.Text.Length==0 ? Visibility.Visible : Visibility.Collapsed;
                sourceView.Filter=item=>
                {
                    var row=(AirTerminalRow)item;
                    return (filterMode=="all" || (filterMode=="selected" ? row.IsSelected : !row.IsSelected))
                        && (filterExistence==null || row.DuctExistence==filterExistence)
                        && new[] { row.ElementId,row.Name }
                        .Any(value=>(value??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0);
                };
                queueProperties();
            };
            search.TextChanged+=(s,e)=>applyFilter();
            filter.Click+=(s,e)=>
            {
                var menu=new ContextMenu();
                foreach(var option in new[] { new { Label="All",Mode="all" },new { Label="Checked",Mode="selected" },new { Label="Unchecked",Mode="unselected" } })
                {
                    var item=new MenuItem { Header=option.Label,IsCheckable=true,IsChecked=filterMode==option.Mode && filterExistence==null };
                    item.Click+=(a,b)=> { filterMode=option.Mode;filterExistence=null;SetIconButtonActive(filter,option.Mode!="all");applyFilter(); };
                    menu.Items.Add(item);
                }
                if (CanCreateDucts)
                {
                    var existence=new MenuItem { Header="Duct comparison" };
                    foreach(string value in propertyRows.Select(r=>r.DuctExistence).Where(v=>!string.IsNullOrEmpty(v)).Distinct().OrderBy(v=>v))
                    {
                        var item=new MenuItem { Header=value,IsCheckable=true,IsChecked=filterExistence==value };
                        item.Click+=(a,b)=> {filterMode="all";filterExistence=value;SetIconButtonActive(filter,true);applyFilter();};
                        existence.Items.Add(item);
                    }
                    menu.Items.Add(existence);
                }
                filter.ContextMenu=menu;menu.PlacementTarget=filter;menu.IsOpen=true;
            };
            sort.Click+=(s,e)=>
            {
                var menu=new ContextMenu();
                var fields=new List<KeyValuePair<string,string>> {
                    new KeyValuePair<string,string>("Select","IsSelected"),
                    new KeyValuePair<string,string>("Element ID","NumericElementId"),
                    new KeyValuePair<string,string>("Element","Name") };
                if (CanCreateDucts) fields.Insert(1,new KeyValuePair<string,string>("Duct comparison","DuctExistence"));
                foreach(var field in fields)
                foreach(var direction in new[] { ListSortDirection.Ascending,ListSortDirection.Descending })
                {
                    var item=new MenuItem { Header=field.Key+(direction==ListSortDirection.Ascending?" ↑":" ↓") };
                    item.Click+=(a,b)=> {sourceView.SortDescriptions.Clear();sourceView.SortDescriptions.Add(new SortDescription(field.Value,direction));};
                    menu.Items.Add(item);
                }
                sort.ContextMenu=menu;menu.PlacementTarget=sort;menu.IsOpen=true;
            };
            summary.Children.Add(searchLayout);
            refreshProperties();
            details.Children.Add(summary);
            if (!AirTerminalCount.HasValue)
            {
                var error = Text(AirTerminalError ?? "Could not read the count. Run the tool again.", 13, "#9A5B12");
                error.Margin = new Thickness(0, 16, 0, 0);
                panel.Children.Add(error);
            }
            return panel;
        }

        public string SystemTableText()
        {
            var result = new StringBuilder((CanCreateDucts ? "Duct comparison\t" : "") + "Element ID\tElement\tSystem Type\tSystem Name\tElevation (mm)");
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
                Padding = new Thickness(20, 10, 20, 10),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                MinHeight = 40,
                Margin = new Thickness(6, 3, 0, 3),
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = primary ? Brush("#62543C") : Brushes.White,
                Foreground = primary ? Brushes.White : Brush("#37322B"),
                BorderBrush = Brush(primary ? "#6F6044" : "#DDD6CE"),
                BorderThickness = new Thickness(primary ? 0 : 1)
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
            hover.Setters.Add(new Setter(Control.BackgroundProperty, Brush(primary ? "#514631" : "#EEE8E1")));
            template.Triggers.Add(hover);
            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45));
            template.Triggers.Add(disabled);
            button.Template = template;
            return button;
        }

        internal static Button IconButton(string tooltip, string geometry)
        {
            var icon = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(geometry),
                Stroke = Brush("#37322B"),
                StrokeThickness = 1.8,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = Brushes.Transparent,
                Stretch = Stretch.Uniform,
                Width = 20,
                Height = 20,
                IsHitTestVisible = false
            };
            var button = Button(string.Empty, false);
            button.Content = icon;
            button.ToolTip = tooltip;
            button.Width = 44;
            button.Height = 40;
            button.MinHeight = 40;
            button.Padding = new Thickness(11, 9, 11, 9);
            button.Margin = new Thickness(6, 3, 0, 3);
            return button;
        }

        private static void SetIconButtonActive(Button button, bool active)
        {
            button.Background = Brush(active ? "#E8E2DA" : "#F5F2EE");
            button.BorderBrush = Brush(active ? "#6F6044" : "#DDD6CE");
        }

        private void ZoomToIfcSource(AirTerminalRow row)
        {
            if (row==null) return;
            const string action="Zoom to IFC source";
            if (NavigateSource!=null)
            {
                try
                {
                    NavigateSource(row,action);
                    feedback.Text="Zoomed to the IFC source · tool remains open.";
                    feedback.Foreground=Brush("#16744B");
                }
                catch (Exception ex)
                {
                    feedback.Text="Could not zoom to the IFC source: "+ex.Message;
                    feedback.Foreground=Brush("#B54747");
                }
                return;
            }
            // Fallback for hosts without a navigation callback.
            NavigationRow=row; NavigationAction=action; Close();
        }

        private void OnSourceInitialized(object sender, EventArgs args)
        {
            var helper=new WindowInteropHelper(this);
            nativeOwner=helper.Owner;
            nativeSource=HwndSource.FromHwnd(helper.Handle);
            nativeSource?.AddHook(WindowMessageHook);
        }

        private IntPtr WindowMessageHook(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if(message==WmSysCommand && (wParam.ToInt64() & 0xFFF0L)==ScMinimize && nativeOwner!=IntPtr.Zero)
            {
                // An owned native window normally minimizes together with Revit.
                // Detach and temporarily release ShowDialog's native owner lock so
                // the user can inspect/orbit the zoomed element in Revit.
                SetWindowLongPtr(window,GwlHwndParent,IntPtr.Zero);
                EnableWindow(nativeOwner,true);
                detachedForMinimize=true;
                ShowWindow(window,SwMinimize);
                handled=true;
            }
            return IntPtr.Zero;
        }

        private void OnWindowStateChanged(object sender, EventArgs args)
        {
            if(!detachedForMinimize || WindowState==WindowState.Minimized || nativeOwner==IntPtr.Zero) return;
            var handle=new WindowInteropHelper(this).Handle;
            // Restore modal behavior before showing the tool again.
            EnableWindow(nativeOwner,false);
            if(handle!=IntPtr.Zero) SetWindowLongPtr(handle,GwlHwndParent,nativeOwner);
            detachedForMinimize=false;
        }

        private void OnWindowClosed(object sender, EventArgs args)
        {
            if(nativeSource!=null) nativeSource.RemoveHook(WindowMessageHook);
            nativeSource=null;
        }

        private void Copy(string value)
        {
            try
            {
                Clipboard.SetText(value);
                feedback.Text = "Copied to clipboard.";
                feedback.Foreground = Brush("#16744B");
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                feedback.Text = "The clipboard is busy. Please try again.";
                feedback.Foreground = Brush("#9A5B12");
            }
        }
    }
}










