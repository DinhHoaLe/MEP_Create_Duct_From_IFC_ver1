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
        public List<AirTerminalRow> IfcSources {get;set;}
        public Func<string,long,string,string,DuctChoice> CreateSystemType {get;set;}
        public Func<string,long,long> CreateDuctType {get;set;}
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
            workspace.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(420)});
            workspace.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star),MinWidth=400});
            root.Children.Add(workspace);
            var groupsBody=new StackPanel();
            var groupScroll=new ScrollViewer {Content=groupsBody,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(14,0,0,0)};Grid.SetColumn(groupScroll,1);workspace.Children.Add(groupScroll);
            var sidebar=new Grid {Margin=new Thickness(0,0,14,0)};
            sidebar.RowDefinitions.Add(new RowDefinition {Height=new GridLength(0),MinHeight=0});
            sidebar.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            sidebar.RowDefinitions.Add(new RowDefinition {Height=new GridLength(1.35,GridUnitType.Star),MinHeight=170});
            Grid.SetColumn(sidebar,0);workspace.Children.Add(sidebar);
            var separator=new Border {Height=1,Background=IFCInfoWindow.Brush("#D1C8BF"),Margin=new Thickness(0,14,0,14)};
            Grid.SetRow(separator,1);separator.Visibility=Visibility.Collapsed;sidebar.Children.Add(separator);
            var optionsScroll=new ScrollViewer {Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(0,0,8,0)};
            Grid.SetRow(optionsScroll,0);Grid.SetRowSpan(optionsScroll,3);sidebar.Children.Add(optionsScroll);



            var summary = new Border { Padding = new Thickness(0,0,0,0), Margin = new Thickness(0,0,0,16) };
            summary.Child = IFCInfoWindow.Text((updating ? "Changes: " : "Ready to create: ") + items.Count + " segments     ·     Skipped  " + issues.Count + " segments", 16, "#6F6044");
            groupsBody.Children.Add(summary);
            var editors = new Dictionary<string, DuctGroupEditor>();
            var mode=UiDesign.Field(body,"Setup mode","","Select mode",false,true);
            mode.Name="SystemSetupMode";mode.ItemsSource=new[]{"Manual","Auto"};mode.SelectedIndex=0;
            Action<string> section=label=> {body.Children.Add(new Border {Height=1,Background=IFCInfoWindow.Brush("#DDD6CE"),Margin=new Thickness(0,16,0,12)});body.Children.Add(IFCInfoWindow.Text(label,16,"#37322B"));};
            section("System");
            var commonSystem=UiDesign.Field(body,"System type","","Optional: apply to all groups",true,true);
            var followIfcChoice=new DuctChoice{Id=0,Name="Follow IFC — create missing system types"};
            commonSystem.Name="SharedHvacSystem";commonSystem.ItemsSource=new[]{followIfcChoice}.Concat(systems).ToList();commonSystem.SelectedItem=followIfcChoice;
            var projectStatus=IFCInfoWindow.Text("",12,"#9A5B12");body.Children.Add(projectStatus);
            var followIfcDuctChoice=new DuctChoice{Id=0,Name="Follow IFC — create missing duct types"};
            ComboBox sharedTypes=null;
            Action refreshProjectStatus=()=>projectStatus.Text=mode.SelectedIndex==1?"Follow IFC reuses matches and creates missing types using compatible templates.":"";
            Action applyMode=()=> {
                bool auto=mode.SelectedIndex==1;
                commonSystem.IsEnabled=auto;
                if(sharedTypes!=null)sharedTypes.IsEnabled=auto && sharedTypes.Items.Count>0;

                foreach(var pair in editors) {
                    pair.Value.AllowAutoSystemCreation=true;
                    var selectedSystem=commonSystem.SelectedItem as DuctChoice;
                    pair.Value.SetSystemMode(auto,selectedSystem==null || selectedSystem.Id==0,selectedSystem);
                    var selectedType=sharedTypes?.SelectedItem as DuctChoice;
                    pair.Value.SetDuctMode(auto,items.Where(i=>i.GroupKey==pair.Key).Select(i=>i.Source),selectedType?.Id>0?selectedType:null,true);
                }
                refreshProjectStatus();
            };
            mode.SelectionChanged+=(s,e)=>applyMode();

            commonSystem.SelectionChanged+=(s,e)=>applyMode();
            var newSystem=IFCInfoWindow.Button("Create a new system type…",false);newSystem.Name="CreateSystemType";body.Children.Add(newSystem);
            newSystem.Click+=(s,e)=> {
                var dialog=new Window{Title="Create System Type",Width=560,Height=640,Owner=Window.GetWindow(root),WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=UiDesign.Background};
                var content=new StackPanel{Margin=new Thickness(24)};
                var creationMode=UiDesign.Field(content,"Creation mode","","Select mode",false,true);creationMode.Name="SystemCreationMode";creationMode.ItemsSource=new[]{"Manual","Follow IFC"};creationMode.SelectedIndex=0;
                var sourcePanel=new StackPanel{Visibility=Visibility.Collapsed};content.Children.Add(sourcePanel);
                var ifcSystem=UiDesign.Field(sourcePanel,"IFC system","","Select IFC system",false,true);ifcSystem.Name="IfcSystemSource";
                var sourceRows=IfcSources??items.Select(i=>i.Source).ToList();
                ifcSystem.ItemsSource=sourceRows.Select(r=>r.SystemType).Where(n=>!string.IsNullOrWhiteSpace(n) && n!="No information").Distinct().OrderBy(n=>n).ToList();
                content.Children.Add(new TextBlock{Text="Name"});var name=new TextBox{Name="NewSystemTypeName",Padding=new Thickness(8)};content.Children.Add(name);
                var template=UiDesign.Field(content,"Template","","Select template",false,true);
                template.ItemsSource=new[]{new DuctChoice{Id=0,Name="Create from classification"}}.Concat(systems).ToList();template.SelectedIndex=0;
                var classification=UiDesign.Field(content,"System classification","","Select classification",false,true);classification.Name="NewSystemClassification";classification.ItemsSource=new[]{"SupplyAir","ReturnAir","ExhaustAir","OtherAir"};
                bool syncingClassification=false;
                template.Name="NewSystemTemplate";
                template.SelectionChanged+=(a,b)=> {
                    var chosen=template.SelectedItem as DuctChoice;
                    if(chosen?.Id>0){syncingClassification=true;classification.SelectedItem=chosen.Classification;syncingClassification=false;}
                };
                classification.SelectionChanged+=(a,b)=> {
                    if(syncingClassification || creationMode.SelectedIndex!=0)return;
                    var chosen=template.SelectedItem as DuctChoice;
                    if(chosen?.Id>0 && chosen.Classification!=classification.SelectedItem as string)template.SelectedIndex=0;
                };
                content.Children.Add(new TextBlock{Text="System abbreviation",Margin=new Thickness(0,10,0,4)});
                var abbreviation=new TextBox{Name="NewSystemAbbreviation",Padding=new Thickness(8)};content.Children.Add(abbreviation);
                var message=new TextBlock{TextWrapping=TextWrapping.Wrap};content.Children.Add(message);
                Action followSource=()=> {
                    bool auto=creationMode.SelectedIndex==1;sourcePanel.Visibility=auto?Visibility.Visible:Visibility.Collapsed;
                    name.IsReadOnly=auto;template.IsEnabled=!auto;classification.IsEnabled=!auto;message.Text="";
                    if(!auto)return;
                    name.Text=ifcSystem.SelectedItem as string??"";
                    string evidence;var inferred=IfcHvacClassification.Resolve(sourceRows.Where(r=>r.SystemType==name.Text),out evidence);
                    var match=systems.FirstOrDefault(t=>string.Equals(t.Name,name.Text,StringComparison.OrdinalIgnoreCase));
                    template.SelectedItem=template.Items.Cast<DuctChoice>().FirstOrDefault(t=>t.Id==(match?.Id??0));
                    classification.SelectedItem=match?.Classification??inferred;classification.IsEnabled=false;
                    if(classification.SelectedItem==null)message.Text="IFC classification is missing or conflicting. Select Manual.";
                };
                creationMode.SelectionChanged+=(a,b)=>followSource();ifcSystem.SelectionChanged+=(a,b)=>followSource();
                var add=IFCInfoWindow.Button("Create",true);content.Children.Add(add);
                add.Click+=(a,b)=> {try {
                    if(CreateSystemType==null)throw new InvalidOperationException("Connect to a Revit project first.");
                    var chosen=(DuctChoice)template.SelectedItem;
                    if(chosen.Id==0 && classification.SelectedItem==null)throw new InvalidOperationException("Select a system classification.");
                    var created=CreateSystemType(name.Text.Trim(),chosen.Id,classification.SelectedItem as string,abbreviation.Text.Trim());
                    systems.Add(created);
                    foreach(var editor in editors.Values)editor.AddSystemChoice(created);
                    commonSystem.ItemsSource=new[]{followIfcChoice}.Concat(systems).ToList();commonSystem.SelectedItem=created;
                    if(mode.SelectedIndex==0)foreach(var editor in editors.Values)editor.SelectManualSystem(created);
                    dialog.Close();
                }catch(Exception ex){message.Text=ex.Message;}};
                UiDesign.SetContent(dialog,new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});dialog.ShowDialog();
            };
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
            section("Duct Type");
            sharedTypes=UiDesign.Field(body,"Duct type","","Optional: apply to all groups",false,true);
            sharedTypes.Name="SharedDuctType";

            Action refreshTypes=()=> {
                var selectedGroups=items.Where(i=>i.Include).Select(i=>i.GroupKey).Distinct().ToList();
                var available=selectedGroups.Count==0?new List<DuctChoice>():editors[selectedGroups[0]].TypeBox.Items.Cast<DuctChoice>().ToList();
                foreach(var key in selectedGroups.Skip(1))available=available.Where(t=>editors[key].TypeBox.Items.Cast<DuctChoice>().Any(x=>x.Id==t.Id)).ToList();
                available.Insert(0,followIfcDuctChoice);var previous=sharedTypes.SelectedItem as DuctChoice;sharedTypes.ItemsSource=available;
                sharedTypes.SelectedItem=available.FirstOrDefault(x=>x.Id==previous?.Id)??followIfcDuctChoice;
                sharedTypes.IsEnabled=mode.SelectedIndex==1;
            };
            sharedTypes.SelectionChanged+=(s,e)=>applyMode();
            var newDuct=IFCInfoWindow.Button("Create a new duct type…",false);body.Children.Add(newDuct);
            newDuct.Name="CreateDuctType";
            newDuct.Click+=(s,e)=> {
                var dialog=new Window{Title="Create Duct Type",Width=560,Height=640,Owner=Window.GetWindow(root),WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=UiDesign.Background};
                var content=new StackPanel{Margin=new Thickness(24)};
                var creationMode=UiDesign.Field(content,"Creation mode","","Select mode",false,true);creationMode.Name="DuctTypeCreationMode";creationMode.ItemsSource=new[]{"Manual","Follow IFC"};creationMode.SelectedIndex=0;
                var sourcePanel=new StackPanel{Visibility=Visibility.Collapsed};content.Children.Add(sourcePanel);
                var ifcType=UiDesign.Field(sourcePanel,"IFC duct type","","Select IFC duct type",false,true);ifcType.Name="IfcDuctTypeSource";
                ifcType.ItemsSource=IfcTypeChoice.Collect(IfcSources??items.Select(i=>i.Source).ToList());
                var shape=UiDesign.Field(content,"Profile","","Select profile",false,true);shape.Name="NewDuctProfile";shape.ItemsSource=new[]{"Rectangular","Round","Oval"};
                content.Children.Add(new TextBlock{Text="Name"});var typeName=new TextBox{Name="NewDuctTypeName",Padding=new Thickness(8)};content.Children.Add(typeName);
                var template=UiDesign.Field(content,"Duplicate existing type","","Select template",false,true);template.Name="NewDuctTemplate";
                var message=new TextBlock{TextWrapping=TextWrapping.Wrap};content.Children.Add(message);
                Func<string,List<DuctChoice>> profileTypes=profile=>profile=="Round"?roundTypes:profile=="Oval"?(ovalTypes??new List<DuctChoice>()):rectangularTypes;
                Action refreshTemplate=()=> {
                    var profile=shape.SelectedItem as string;
                    var available=profile==null?new List<DuctChoice>():profileTypes(profile);
                    template.ItemsSource=available;template.SelectedItem=available.Count==1?available[0]:null;
                    message.Text=profile==null?"Select a profile.":available.Count==0?"No Revit template exists for this profile. Load one first.":"";
                };
                shape.SelectionChanged+=(a,b)=>refreshTemplate();
                Action followSource=()=> {
                    bool auto=creationMode.SelectedIndex==1;sourcePanel.Visibility=auto?Visibility.Visible:Visibility.Collapsed;
                    shape.IsEnabled=!auto;typeName.IsReadOnly=auto;
                    if(!auto){refreshTemplate();return;}
                    var source=ifcType.SelectedItem as IfcTypeChoice;
                    typeName.Text=source?.Name??"";
                    shape.SelectedItem=source?.Shape=="Square"?"Rectangular":source?.Shape;
                    refreshTemplate();
                    if(source!=null && template.ItemsSource!=null)template.SelectedItem=template.Items.Cast<DuctChoice>().FirstOrDefault(t=>string.Equals(t.Name,source.Name,StringComparison.OrdinalIgnoreCase))??template.SelectedItem;
                };
                creationMode.SelectionChanged+=(a,b)=>followSource();ifcType.SelectionChanged+=(a,b)=>followSource();
                var add=IFCInfoWindow.Button("Create",true);content.Children.Add(add);
                add.Click+=(a,b)=>{try {
                    if(CreateDuctType==null)throw new InvalidOperationException("Connect to a Revit project first.");
                    if(template.SelectedItem==null)throw new InvalidOperationException("Select a Revit template with the chosen profile.");
                    long id=CreateDuctType(typeName.Text.Trim(),((DuctChoice)template.SelectedItem).Id);
                    var choice=new DuctChoice{Id=id,Name=typeName.Text.Trim()};string profile=(string)shape.SelectedItem;
                    if(profile=="Oval" && ovalTypes==null)ovalTypes=new List<DuctChoice>();
                    profileTypes(profile).Add(choice);
                    foreach(var pair in editors) {
                        var first=items.First(i=>i.GroupKey==pair.Key);
                        bool matches=profile=="Round"?first.Round:profile=="Oval"?first.Oval:!first.Round&&!first.Oval;
                        if(!matches)continue;
                        var list=pair.Value.TypeBox.Items.Cast<DuctChoice>().ToList();if(!list.Any(t=>t.Id==id))list.Add(choice);pair.Value.TypeBox.ItemsSource=list;
                        if(mode.SelectedIndex==0)pair.Value.TypeBox.SelectedItem=choice;
                    }
                    refreshTypes();
                    var common=sharedTypes.Items.Cast<DuctChoice>().FirstOrDefault(t=>t.Id==id);if(common!=null)sharedTypes.SelectedItem=common;
                    dialog.Close();
                }catch(Exception ex){message.Text=ex.Message;}};
                UiDesign.SetContent(dialog,new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});dialog.ShowDialog();
            };            refreshTypes();applyMode();
            section("Level");
            var levelMapping = new Dictionary<string, ComboBox>();
            var manualLevelValues=new Dictionary<string,long>();
            var followLevel=new DuctChoice{Id=0,Name="Follow IFC — match name / elevation"};
            var commonLevel=UiDesign.Field(body,"Level","","Select level",false,true);commonLevel.Name="SharedLevel";
            commonLevel.ItemsSource=new[]{followLevel}.Concat(levels).ToList();commonLevel.SelectedItem=followLevel;
            groupsBody.Children.Add(new Border{Height=1,Background=IFCInfoWindow.Brush("#DDD6CE"),Margin=new Thickness(0,16,0,12)});
            groupsBody.Children.Add(IFCInfoWindow.Text("Level",16,"#37322B"));
            foreach(var key in items.Select(i=>i.LevelKey??"No source level").Distinct()) {
                var box=UiDesign.Field(groupsBody,"Source level: "+key,"","Select target level",false,true);box.Name="GroupLevel";
                var choices=new[]{new DuctChoice{Id=0,Name="Automatic by elevation"}}.Concat(levels).ToList();
                box.ItemsSource=choices;
                long savedId=saved.Levels.FirstOrDefault(m=>m.Key==key)?.Id??0;
                box.SelectedItem=choices.FirstOrDefault(c=>c.Id==savedId)??choices[0];
                manualLevelValues[key]=((DuctChoice)box.SelectedItem).Id;
                levelMapping[key]=box;
            }
            bool autoLevels=false;
            Action applyLevels=()=> {
                bool auto=mode.SelectedIndex==1;commonLevel.IsEnabled=auto;
                if(auto && !autoLevels)foreach(var pair in levelMapping)manualLevelValues[pair.Key]=(pair.Value.SelectedItem as DuctChoice)?.Id??0;
                foreach(var pair in levelMapping) {
                    var choices=new[]{new DuctChoice{Id=0,Name="Automatic by elevation"}}.Concat(levels).ToList();
                    pair.Value.IsEnabled=!auto;
                    if(auto) {
                        var selected=commonLevel.SelectedItem as DuctChoice;
                        var resolved=selected?.Id>0?selected:levels.FirstOrDefault(l=>string.Equals(l.Name,pair.Key,StringComparison.OrdinalIgnoreCase));
                        var display=new DuctChoice{Id=resolved?.Id??0,Name=selected?.Id>0?resolved.Name:"Follow IFC — "+(resolved?.Name??"by elevation")};
                        pair.Value.ItemsSource=new[]{display};pair.Value.SelectedItem=display;
                    } else if(autoLevels) {
                        pair.Value.ItemsSource=choices;pair.Value.SelectedItem=choices.FirstOrDefault(c=>c.Id==manualLevelValues[pair.Key])??choices[0];
                    }
                }
                autoLevels=auto;
            };
            mode.SelectionChanged+=(s,e)=>applyLevels();commonLevel.SelectionChanged+=(s,e)=>applyLevels();applyLevels();            var worksetMapping = new Dictionary<string, ComboBox>();
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
            foreach(var option in new[] { remember, fittings, terminals })
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
            preview.Name="DuctSourcePreview";preview.Visibility=Visibility.Collapsed;
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
                refreshTypes();
                int count = items.Count(i => i.Include);
                includeHeader.IsEnabled=items.Count>0;includeHeader.IsChecked=count==0?false:count==items.Count?(bool?)true:null;
                create.Content = (updating ? "Update " : "Create ") + count + " Duct";
                create.IsEnabled = count > 0;
                ((TextBlock)summary.Child).Text=count+" selected"+(issues.Count>0?" · "+issues.Count+" skipped":"");
                refreshProjectStatus();
            };
            System.ComponentModel.PropertyChangedEventHandler selectionChanged = (s, e) =>
            {
                if (e.PropertyName == nameof(DuctPlanItem.Include)) refreshSelection();
            };
            foreach (var item in items) item.PropertyChanged += selectionChanged;
            Closed += (s, e) => { foreach (var item in items) item.PropertyChanged -= selectionChanged; };
            refreshSelection();
            var note = IFCInfoWindow.Text("Fittings use Routing Preferences. Only unambiguous end pairs are connected; tees require three meeting ends and do not split ducts.", 13, "#716B63");
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
                    NewDuctTypes=editors.Where(p=>systemKeys.Contains(p.Key) && p.Value.NewType!=null).ToDictionary(p=>p.Key,p=>p.Value.NewType),
                    Levels = levelMapping.Where(p => levelKeys.Contains(p.Key)).ToDictionary(p => p.Key, p => ((DuctChoice)p.Value.SelectedItem).Id),
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




