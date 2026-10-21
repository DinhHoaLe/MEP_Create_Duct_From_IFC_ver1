using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace IFCInfo
{
    public sealed class DuctCreationWindow : Window
    {
        private DockPanel embeddedBody;
        private FrameworkElement embeddedHeader;
        public Action<DuctRequest> SubmitEmbedded { get; set; }
        public Action<Window> BrowseSystems {get;set;}
        public Action BackEmbedded { get; set; } public Action CloseEmbedded { get; set; }
        public FrameworkElement DetachBody()
        {
            ((Panel)embeddedBody.Parent).Children.Remove(embeddedBody);
            Content=null; embeddedHeader.Visibility=Visibility.Collapsed; embeddedBody.Margin=new Thickness(8);
            return embeddedBody;
        }
        public DuctRequest Request
        {
            get; private set;
        }
        public DuctCreationWindow(List<DuctPlanItem> items, List<string> issues,
            List<DuctChoice> roundTypes, List<DuctChoice> rectangularTypes, List<DuctChoice> systems,
            List<DuctChoice> levels, List<DuctChoice> worksets, DuctSettingsData saved, bool updating = false, List<DuctChoice> ovalTypes = null)
        {
            Title = updating ? "Preview duct updates from IFC" : "Step 3 · Create ducts from IFC";
            Width = 1200;
            Height = 690;
            MinWidth = 900;
            MinHeight = 440;
            MaxHeight = SystemParameters.WorkArea.Height;
            MaxWidth = SystemParameters.WorkArea.Width;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = UiDesign.Background;
            UseLayoutRounding = true;
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
            FontSize = 14;
            var root = new DockPanel { Margin = new Thickness(28), Background = Background };
            UiDesign.SetContent(this, root); embeddedBody=root;
            var footer = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);
            var feedback = IFCInfoWindow.Text("", 13, "#9A5B12");
            footer.Children.Add(feedback);
            root.AddHandler(System.Windows.Controls.Primitives.Selector.SelectionChangedEvent,new SelectionChangedEventHandler((s,e)=>feedback.Text=""));
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var back = IFCInfoWindow.Button("Back", false);
            back.IsCancel = true;
            back.Click += (s, e) => { if(BackEmbedded!=null) BackEmbedded(); else Close(); };
            var systemReview = IFCInfoWindow.Button("System", false); buttons.Children.Add(systemReview);
            var create = IFCInfoWindow.Button((updating ? "Update " : "Create ") + items.Count + " Duct", true);
            create.IsEnabled = items.Count > 0;
            buttons.Children.Add(create); buttons.Children.Add(back); var close=IFCInfoWindow.Button("Close",false); close.Click+=(s,e)=> {if(CloseEmbedded!=null)CloseEmbedded();else Close();}; buttons.Children.Add(close);
            footer.Children.Add(buttons);
            var header = new Grid { Margin = new Thickness(0,0,0,22) }; embeddedHeader=header;
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titlePanel = new StackPanel(); header.Children.Add(titlePanel);
            var title = IFCInfoWindow.Text(updating ? "Update ducts from IFC" : "Duct creation settings", 27, "#37322B");
            title.FontWeight = FontWeights.Bold; titlePanel.Children.Add(title);
            titlePanel.Children.Add(IFCInfoWindow.Text("Step 3 · Review and create ducts", 14, "#716B63"));
            var steps = new ContentControl { Content = UiDesign.Steps(3), Margin = new Thickness(12,0,0,0) };
            Grid.SetColumn(steps,1); header.Children.Add(steps);
            header.SizeChanged += (s,e) => steps.Visibility = header.ActualWidth < 780 ? Visibility.Collapsed : Visibility.Visible;
            DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
            var body = new StackPanel();
            var workspace=new Grid();
            workspace.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});
            workspace.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(500),MinWidth=400});
            root.Children.Add(workspace);
            var groupsBody=new StackPanel();
            workspace.Children.Add(new ScrollViewer {Content=groupsBody,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(0,0,14,0)});
            var sidebar=new Grid {Margin=new Thickness(14,0,0,0)};
            sidebar.RowDefinitions.Add(new RowDefinition {Height=new GridLength(1,GridUnitType.Star),MinHeight=140});
            sidebar.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            sidebar.RowDefinitions.Add(new RowDefinition {Height=new GridLength(1.35,GridUnitType.Star),MinHeight=170});
            Grid.SetColumn(sidebar,1);workspace.Children.Add(sidebar);
            var separator=new Border {Height=1,Background=IFCInfoWindow.Brush("#D1C8BF"),Margin=new Thickness(0,14,0,14)};
            Grid.SetRow(separator,1);sidebar.Children.Add(separator);
            var optionsScroll=new ScrollViewer {Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(0,0,8,0)};
            Grid.SetRow(optionsScroll,2);sidebar.Children.Add(optionsScroll);
            body.Children.Add(IFCInfoWindow.Text("LEVEL & GENERAL OPTIONS", 16, "#37322B"));
            groupsBody.Children.Add(IFCInfoWindow.Text("MAP BY SHAPE × SYSTEM TYPE", 18, "#37322B"));
            groupsBody.Children.Add(IFCInfoWindow.Text("Each group has its own duct type and system type. Expand a group to configure it; uncheck unwanted sources in the fixed table on the right.",13,"#716B63"));
            var summary = new Border { Background = IFCInfoWindow.Brush("#E8E2DA"), CornerRadius = new CornerRadius(10), Padding = new Thickness(18,12,18,12), Margin = new Thickness(0,0,0,16) };
            summary.Child = IFCInfoWindow.Text((updating ? "Changes: " : "Ready to create: ") + items.Count + " segments     ·     Skipped  " + issues.Count + " segments", 16, "#6F6044");
            groupsBody.Children.Add(summary);
            var editors = new Dictionary<string, DuctGroupEditor>();
            var mode=UiDesign.Field(body,"System setup","Auto applies to every group in this run; Manual edits each group.","Select mode",false);
            mode.Name="SystemSetupMode";mode.ItemsSource=new[]{"Manual","Auto"};mode.SelectedIndex=0;
            var autoOptions=new StackPanel {Visibility=Visibility.Collapsed};body.Children.Add(autoOptions);
            var autoMode=UiDesign.Field(autoOptions,"Auto strategy","Use one existing HVAC type, or map source IFC system names and classifications.","Select strategy",false);
            autoMode.Name="AutoSystemStrategy";autoMode.ItemsSource=new[]{"Use existing HVAC","Map IFC systems"};autoMode.SelectedIndex=0;
            var sharedOptions=new StackPanel {Visibility=Visibility.Collapsed};body.Children.Add(sharedOptions);
            var commonSystem=UiDesign.Field(sharedOptions,"Shared HVAC system type","Apply this Revit type to all groups.","Select HVAC system type",true);
            commonSystem.Name="SharedHvacSystem";commonSystem.ItemsSource=systems;
            var projectStatus=IFCInfoWindow.Text("",12,"#9A5B12");body.Children.Add(projectStatus);
            Action refreshProjectStatus=()=> {
                projectStatus.Text=mode.SelectedIndex==0?"Manual: configure the system in each group."
                    :autoMode.SelectedIndex==0?(commonSystem.SelectedItem==null?"Select one existing HVAC system type.":"All group system controls are locked to the shared type.")
                    :"IFC mapping applies to all groups in this run. Exact name and classification matches are reused; unmatched names create new types on Create. Unresolved or conflicting classifications require Manual.";
            };
            Action applyMode=()=> {
                bool auto=mode.SelectedIndex==1;
                autoOptions.Visibility=auto?Visibility.Visible:Visibility.Collapsed;
                sharedOptions.Visibility=auto && autoMode.SelectedIndex==0?Visibility.Visible:Visibility.Collapsed;
                foreach(var editor in editors.Values)editor.SetSystemMode(auto,autoMode.SelectedIndex==1,commonSystem.SelectedItem as DuctChoice);
                refreshProjectStatus();
            };
            mode.SelectionChanged+=(s,e)=>applyMode();
            autoMode.SelectionChanged+=(s,e)=>applyMode();
            commonSystem.SelectionChanged+=(s,e)=>applyMode();
            var groupSearch = new TextBox { MinHeight=34, Padding=new Thickness(8), Margin=new Thickness(0,12,0,12), ToolTip="Search groups by shape or system type" };
            groupsBody.Children.Add(IFCInfoWindow.Text("Find shape / system type groups",12,"#716B63"));
            groupsBody.Children.Add(groupSearch);
            groupSearch.TextChanged += (s,e) =>
            {
                foreach(var editor in editors.Values)
                    editor.Visibility=editor.Label.IndexOf(groupSearch.Text.Trim(),StringComparison.OrdinalIgnoreCase)>=0 ? Visibility.Visible : Visibility.Collapsed;
            };
            var groups = items.GroupBy(i => i.GroupKey).OrderBy(g => g.Key).ToList();
            foreach (var group in groups)
            {
                var first = group.First();
                var available = first.Round ? roundTypes : first.Oval ? (ovalTypes ?? new List<DuctChoice>()) : rectangularTypes;
                var editor = new DuctGroupEditor(group.Key, first.ShapeKey, first.Source.SystemType, group.Count(), available, systems, saved, groups.Count == 1, group.Select(i=>i.Source));
                editors.Add(group.Key, editor);
                editor.SystemBox.SelectionChanged += (s,e) => refreshProjectStatus();
                groupsBody.Children.Add(editor);
            }
            var levelMapping = new Dictionary<string, ComboBox>();
            var manualLevel = new CheckBox { Content="Manual · Map source levels to target levels", IsChecked=saved.Levels.Any(m=>m.Id>0), Margin=new Thickness(0,16,0,8) };
            body.Children.Add(manualLevel);
            body.Children.Add(IFCInfoWindow.Text("Disable Manual to select a level below the duct automatically. The IFC centerline remains unchanged.",12,"#716B63"));
            manualLevel.Checked += (s,e) => { foreach(var box in levelMapping.Values) box.IsEnabled=true; };
            manualLevel.Unchecked += (s,e) => { foreach(var box in levelMapping.Values) box.IsEnabled=false; };
            foreach (var key in items.Select(i=>i.LevelKey ?? "No source level").Distinct())
            {
                var box = UiDesign.Field(body, "Source level: " + key, "Automatically select a level below the duct, or map a target level", "Level", false);
                var choices = new List<DuctChoice> { new DuctChoice { Id = 0, Name = "Automatic by elevation" } }; choices.AddRange(levels);
                box.ItemsSource = choices;
                box.SelectedItem = choices.FirstOrDefault(c=>c.Id == saved.Levels.FirstOrDefault(m=>m.Key == key)?.Id) ?? choices[0];
                levelMapping[key] = box;
                box.IsEnabled=manualLevel.IsChecked==true;
            }
            var worksetMapping = new Dictionary<string, ComboBox>();
            if (worksets.Count > 0)
            foreach (var key in editors.Keys)
            {
                var box = UiDesign.Field(body, "Workset: " + editors[key].Label, "Map by profile and system groups", "Workset", false);
                var choices = new List<DuctChoice> { new DuctChoice { Id = 0, Name = "Current workset / preserve workset on update" } }; choices.AddRange(worksets);
                box.ItemsSource = choices;
                box.SelectedItem = choices.FirstOrDefault(c=>c.Id == saved.Worksets.FirstOrDefault(m=>m.Key == key)?.Id) ?? choices[0];
                worksetMapping[key] = box;
            }
            var policy = UiDesign.Field(body, "On error", "Applies to duct creation and fitting connections", "Choose run behavior", false);
            policy.ItemsSource = new[] { "Roll back the entire run", "Skip failures and keep successful results" }; policy.SelectedIndex = 0;
            var remember = new CheckBox { Content = "Save mappings in the Revit project", IsChecked = true, Margin = new Thickness(0,16,0,8) };
            var fittings = new CheckBox { Content = "Connect duct ends: elbow, transition, tee", Margin = new Thickness(0,8,0,8) };
            var terminals = new CheckBox { Content = "Connect host-model air terminals at duct ends (maximum 1 mm)", Margin = new Thickness(0,8,0,8) };
            foreach(var option in new[] { manualLevel, remember, fittings, terminals })
                option.Content=IFCInfoWindow.Text((string)option.Content,13,"#37322B");
            body.Children.Add(remember); body.Children.Add(fittings); body.Children.Add(terminals);
            body.Children.Add(IFCInfoWindow.Text("Maximum elbow/transition gap (mm). Use 1 for coincident ends; increase to allow Revit to adjust duct ends.",13,"#716B63"));
            var gap=new TextBox { Text="1",Width=100,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,8,0,8) }; body.Children.Add(gap);
            var preview = new DataGrid { ItemsSource = items, AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, MinHeight = 120, Margin = new Thickness(0) };
            preview.Columns.Add(new DataGridCheckBoxColumn { Header = "Include", Binding = new System.Windows.Data.Binding("Include")
                { Mode = System.Windows.Data.BindingMode.TwoWay, UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged } });
            preview.Columns.Add(new DataGridTextColumn { Header = "IFC source", Binding = new System.Windows.Data.Binding("PreviewSource"), IsReadOnly=true });
            preview.Columns.Add(new DataGridTextColumn { Header = "Shape", Binding = new System.Windows.Data.Binding("ShapeName"), IsReadOnly=true });
            preview.Columns.Add(new DataGridTextColumn { Header = "IFC system type", Binding = new System.Windows.Data.Binding("PreviewSystem"), IsReadOnly=true });
            preview.Columns.Add(new DataGridTextColumn { Header = "Target duct", Binding = new System.Windows.Data.Binding("PreviewTarget"), IsReadOnly=true });
            preview.Columns.Add(new DataGridTextColumn { Header = "Changes", Binding = new System.Windows.Data.Binding("PreviewChange"), IsReadOnly=true });
            UiDesign.StyleTable(preview);
            preview.Name="DuctSourcePreview";
            preview.FontSize=12;
            var compactHeader=new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader),preview.ColumnHeaderStyle);
            compactHeader.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(4,8,4,8)));
            compactHeader.Setters.Add(new Setter(Control.FontSizeProperty,12.0));
            preview.ColumnHeaderStyle=compactHeader;
            Grid.SetRow(preview,0); sidebar.Children.Add(preview);
            var wrappedCell=new Style(typeof(TextBlock));
            wrappedCell.Setters.Add(new Setter(TextBlock.TextWrappingProperty,TextWrapping.Wrap));
            wrappedCell.Setters.Add(new Setter(TextBlock.MarginProperty,new Thickness(4)));
            var headerText=new FrameworkElementFactory(typeof(TextBlock));
            headerText.SetValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap);
            headerText.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding());
            var headerTemplate=new DataTemplate {VisualTree=headerText};
            foreach(var column in preview.Columns)
            {
                column.Width=new DataGridLength(column.Header.ToString()=="IFC system type"?2:column.Header.ToString()=="Shape"?1.5:1,DataGridLengthUnitType.Star);
                column.HeaderTemplate=headerTemplate;
                if(column is DataGridTextColumn textColumn)textColumn.ElementStyle=wrappedCell;
            }
            var includeHeader=new CheckBox {Name="IncludeAllSources",ToolTip="Include / exclude all sources",HorizontalAlignment=HorizontalAlignment.Center};
            preview.Columns[0].Header=includeHeader;preview.Columns[0].HeaderTemplate=null; preview.Columns[0].Width=48; preview.Columns[0].HeaderStyle=IFCInfoWindow.SelectionHeaderStyle(preview.ColumnHeaderStyle);
            includeHeader.Click+=(s,e)=> {
                preview.CommitEdit(DataGridEditingUnit.Cell,true);preview.CommitEdit(DataGridEditingUnit.Row,true);
                bool include=!items.All(i=>i.Include);
                foreach(var item in items)item.Include=include;
            };
            Action refreshSelection = () =>
            {
                int count = items.Count(i => i.Include);
                includeHeader.IsEnabled=items.Count>0;includeHeader.IsChecked=count==0?false:count==items.Count?(bool?)true:null;
                create.Content = (updating ? "Update " : "Create ") + count + " Duct";
                create.IsEnabled = count > 0;
                ((TextBlock)summary.Child).Text = "Selected: " + count + " segments     ·     Unchecked: "
                    + (items.Count - count) + " segments     ·     Skipped: " + issues.Count + " segments";
                refreshProjectStatus();
            };
            System.ComponentModel.PropertyChangedEventHandler selectionChanged = (s, e) =>
            {
                if (e.PropertyName == nameof(DuctPlanItem.Include)) refreshSelection();
            };
            foreach (var item in items) item.PropertyChanged += selectionChanged;
            Closed += (s, e) => { foreach (var item in items) item.PropertyChanged -= selectionChanged; };
            refreshSelection();
            var note = IFCInfoWindow.Text("Fittings use Routing Preferences. Only unambiguous end pairs are connected; tees require three meeting ends and do not split ducts. The IFC System Name is stored in Comments.", 13, "#716B63");
            note.Margin = new Thickness(2,16,2,8); body.Children.Add(note);
            if (issues.Count > 0)
            {
                groupsBody.Children.Add(new SkippedDuctsPanel(issues));
                var exportIssues=IFCInfoWindow.Button("Export skipped sources to CSV",false);
                exportIssues.Click+=(s,e)=>
                {
                    try
                    {
                        var file=new Microsoft.Win32.SaveFileDialog { Filter="CSV (*.csv)|*.csv",FileName="IFC-skipped.csv" };
                        if (file.ShowDialog(this)==true) DuctRunRow.Export(file.FileName,issues.Select(issue=>new DuctRunRow { SourceId=issue.Split(':')[0],Status="Skipped",Reason=issue }));
                    }
                    catch (Exception ex) { feedback.Text=ex.Message; }
                };
                groupsBody.Children.Add(exportIssues);
            }
            systemReview.Click+=(s,e)=> {
                if(BrowseSystems!=null) BrowseSystems(Window.GetWindow(root) ?? this);
                else feedback.Text="System browser is available when connected to the Revit project.";
            };
            create.Click += (s, e) =>
            {
                preview.CommitEdit(DataGridEditingUnit.Cell, true); preview.CommitEdit(DataGridEditingUnit.Row, true);
                double gapMm;
                if (!double.TryParse(gap.Text,out gapMm) || double.IsNaN(gapMm) || double.IsInfinity(gapMm) || gapMm<1 || gapMm>1000)
                { feedback.Text="Fitting gap must be between 1 and 1000 mm."; return; }
                var selected = items.Where(i => i.Include).ToList();
                if (selected.Count == 0) { feedback.Text = "Check at least one row in the preview table."; return; }
                bool needsRound = selected.Any(i => i.Round), needsRectangular = selected.Any(i => !i.Round && !i.Oval);
                var systemKeys = new HashSet<string>(selected.Select(i => i.GroupKey));
                var levelKeys = new HashSet<string>(selected.Select(i => i.LevelKey ?? "No source level"));
                foreach (var key in systemKeys)
                {
                    string error = editors[key].Validate();
                    if (error != null) { feedback.Text = error; groupSearch.Text=""; editors[key].IsExpanded = true; editors[key].BringIntoView(); return; }
                }
                var newSystems = editors.Where(p => systemKeys.Contains(p.Key) && p.Value.NewSystem != null)
                    .ToDictionary(p => p.Key, p => p.Value.NewSystem);
                foreach (var group in newSystems.Values.GroupBy(n => n.Name, StringComparer.OrdinalIgnoreCase))
                    if (group.Select(n => n.TemplateId + "|" + (n.TemplateId > 0 ? "" : n.Classification)).Distinct().Count() > 1)
                    { feedback.Text = "New system type '" + group.Key + "' has different settings across groups. Choose the same template/classification or rename it."; return; }
                Request = new DuctRequest
                {
                    Items = selected,
                    RoundTypeId = needsRound ? ((DuctChoice)editors[selected.First(i => i.Round).GroupKey].TypeBox.SelectedItem).Id : 0,
                    RectangularTypeId = needsRectangular ? ((DuctChoice)editors[selected.First(i => !i.Round && !i.Oval).GroupKey].TypeBox.SelectedItem).Id : 0,
                    DuctTypes = editors.Where(p => systemKeys.Contains(p.Key)).ToDictionary(p => p.Key, p => ((DuctChoice)p.Value.TypeBox.SelectedItem).Id),
                    SystemTypes = editors.Where(p => systemKeys.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value.NewSystem != null ? 0 : ((DuctChoice)p.Value.SystemBox.SelectedItem).Id),
                    NewSystems = newSystems,
                    Levels = levelMapping.Where(p => levelKeys.Contains(p.Key)).ToDictionary(p => p.Key, p => manualLevel.IsChecked==true ? ((DuctChoice)p.Value.SelectedItem).Id : 0),
                    Worksets = worksetMapping.Where(p => systemKeys.Contains(p.Key)).ToDictionary(p => p.Key, p => ((DuctChoice)p.Value.SelectedItem).Id),
                    KeepSuccessful = policy.SelectedIndex == 1, SaveSettings = remember.IsChecked == true,
                    CreateFittings = fittings.IsChecked == true, ConnectTerminals = terminals.IsChecked == true,
                    FittingGapMm=gapMm,
                    Skipped = issues.Select(issue=>new DuctRunRow { SourceId=issue.Split(':')[0], Status="Skipped", Reason=issue }).ToList()
                };
                Request.Skipped.AddRange(items.Where(i=>!i.Include).Select(i=>new DuctRunRow { SourceId=i.Source.ElementId,IfcGuid=i.Source.IfcGuid,Status="Unchecked",Reason="Unchecked in the preview table." }));
                if(SubmitEmbedded!=null) SubmitEmbedded(Request); else DialogResult = true;
            };
        }
    }
}




