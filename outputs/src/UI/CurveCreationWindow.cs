using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace IFCInfo
{
    public sealed class CurveCreationWindow : Window
    {
        public static string SourceIssue(AirTerminalRow row,string kind)
        {
            if(kind=="CableTray" && (row.Shape=="Round" || row.Shape=="Oval"))return "Source "+row.ElementId+" has a "+row.Shape+" profile in Cable Trays. It cannot be created as a rectangular cable tray. Check the IFC category/export mapping.";
            if((kind=="Pipe" || kind=="Conduit") && (row.Shape=="Rectangular" || row.Shape=="Square" || row.Shape=="Oval"))return "Source "+row.ElementId+" is not circular and cannot be created as "+kind+".";
            return "";
        }
        private static ComboBox ReadOnlyChoice(ComboBox original,string name)
        {
            var box=new ComboBox{Name=name,Style=original.Style,MinHeight=original.MinHeight,FontSize=original.FontSize,Padding=original.Padding,IsEnabled=false,Visibility=Visibility.Collapsed};
            ((Panel)original.Parent).Children.Add(box);return box;
        }
        private static ReplacementTypeOption MatchSourceType(AirTerminalRow row,List<ReplacementTypeOption> choices)
        {
            string name=string.IsNullOrWhiteSpace(row.DuctSource?.TypeName)?IfcTypeChoice.TypeName(row):row.DuctSource.TypeName;
            var exact=choices.Where(t=>string.Equals(t.Label,name,System.StringComparison.OrdinalIgnoreCase)).ToList();
            if(exact.Count>0)return exact.Count==1?exact[0]:null;
            string sourceName=name.Split(':').Last().Trim();
            var qualified=choices.Where(t=>string.Equals((t.Label??"").Replace(" : ",":"),name.Replace(" : ",":"),System.StringComparison.OrdinalIgnoreCase)).ToList();
            if(qualified.Count>0)return qualified.Count==1?qualified[0]:null;
            var matches=choices.Where(t=>string.Equals((t.Label??"").Split(':').Last().Trim(),sourceName,System.StringComparison.OrdinalIgnoreCase)).ToList();
            return matches.Count==1?matches[0]:null;
        }
        public static string SourceService(AirTerminalRow row)
        {
            if(!string.IsNullOrWhiteSpace(row.ServiceType))return row.ServiceType.Trim();
            var values=(row.IfcProperties??new List<IfcPropertyValue>()).Where(p=>new[]{"SERVICETYPE","SERVICE"}.Contains((p.Name??"").Replace(" ","").Replace("_","").ToUpperInvariant())).Select(p=>p.Value?.Trim()).Where(v=>!string.IsNullOrWhiteSpace(v)).Distinct().ToList();
            return values.Count==1?values[0]:"";
        }
        private DockPanel embeddedBody;
        private FrameworkElement embeddedHeader;
        private Button embeddedBack;
        public System.Action<ReplacementRequest> SubmitEmbedded {get;set;}
        public System.Action BackEmbedded {get;set;}
        public System.Func<string> MappingSummary {get;set;}
        public System.Action RefreshConfirmation {get;private set;}
        private TabControl confirmationTabs;
        public System.Action RequestReview {get;set;}
        public System.Action RequestSettings {get;set;}
        public void SelectConfirmation(bool final){confirmationTabs.SelectedIndex=final?1:0;RefreshConfirmation();}
        public FrameworkElement DetachBody()
        {
            ((Panel)embeddedBody.Parent).Children.Remove(embeddedBody);Content=null;
            embeddedHeader.Visibility=Visibility.Collapsed;embeddedBody.Margin=new Thickness(8);
            embeddedBack.IsCancel=false;
            if(RequestReview!=null){var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetBinding(ContentPresenter.ContentProperty,new Binding("SelectedContent"){RelativeSource=new RelativeSource(RelativeSourceMode.TemplatedParent)});confirmationTabs.Template=new ControlTemplate(typeof(TabControl)){VisualTree=presenter};}
            return embeddedBody;
        }
        public ReplacementRequest Request { get; private set; }
        public CurveCreationWindow(List<AirTerminalRow> rows,List<ReplacementTypeOption> types,
            List<ReplacementLevelOption> levels,List<ReplacementTypeOption> systems,long categoryId,string kind,List<string> projectServices=null)
        {
            string label=kind=="Pipe"?"Pipe":kind=="Conduit"?"Conduit":"Cable Tray";
            if(kind=="CableTray")foreach(var row in rows)if(string.IsNullOrWhiteSpace(row.ServiceType))row.ServiceType=SourceService(row);
            Title="Preview & Create — "+label+" from IFC"; Width=900; Height=690; MinWidth=620; MinHeight=440;
            MaxHeight=SystemParameters.WorkArea.Height; WindowStartupLocation=WindowStartupLocation.CenterOwner;
            Background=UiDesign.Background; FontFamily=new System.Windows.Media.FontFamily("Segoe UI"); FontSize=14;
            var root=new DockPanel { Margin=new Thickness(28),Background=UiDesign.Background }; UiDesign.SetContent(this, root);
            embeddedBody=root;
            var footer=new StackPanel { Margin=new Thickness(0,14,0,0) }; DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
            var feedback=IFCInfoWindow.Text("",13,"#9A5B12"); footer.Children.Add(feedback);
            var buttons=new WrapPanel { HorizontalAlignment=HorizontalAlignment.Right }; footer.Children.Add(buttons);
            System.Func<bool> backWithin=null;
            var back=IFCInfoWindow.Button("Back",false); back.IsCancel=true; back.Click+=(s,e)=>{if(backWithin?.Invoke()==true)return;if(BackEmbedded!=null)BackEmbedded();else Close();}; buttons.Children.Add(back);
            embeddedBack=back;
            var create=IFCInfoWindow.Button("Create "+rows.Count+" "+label,true); create.Name="CreateCurve"; buttons.Children.Add(create);
            var nextReview=IFCInfoWindow.Button("Review all settings →",true);nextReview.Name="ReviewCurveSettings";buttons.Children.Add(nextReview);create.Visibility=Visibility.Collapsed;
            var header=new Grid { Margin=new Thickness(0,0,0,22) };
            embeddedHeader=header;
            header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            var titles=new StackPanel(); titles.Children.Add(IFCInfoWindow.Text("Preview & Create — "+label,27,"#37322B"));
            titles.Children.Add(IFCInfoWindow.Text("Step 4 · Review and create runs",14,"#716B63")); header.Children.Add(titles);
            var steps=new ContentControl { Content=UiDesign.Steps(4,true,null,new[]{"Source","Elements","Parameters","Preview\n& Create"}) }; Grid.SetColumn(steps,1); header.Children.Add(steps);
            header.SizeChanged+=(s,e)=>steps.Visibility=header.ActualWidth<780?Visibility.Collapsed:Visibility.Visible;
            DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
            Width=1100; MinWidth=900;
            var options=new StackPanel{Name="CurveGeneralOptions"}; var body=new StackPanel{Name="CurveSourceGroups"}; var groupsBody=body;
            var columns=UiDesign.SettingsColumns(options,body);
            columns.ColumnDefinitions[0].Width=new GridLength(340);
            columns.SizeChanged+=(s,e)=>columns.ColumnDefinitions[0].Width=new GridLength(columns.ActualWidth<850?260:340);
            var reviewTabs=new TabControl{Name="CurveConfirmationTabs"};UiDesign.StyleWorkflowTabs(reviewTabs);
            confirmationTabs=reviewTabs;
            var setupTab=new TabItem{Header="Settings",Content=columns};
            var finalBody=new DockPanel{Margin=new Thickness(8)};
            var finalTab=new TabItem{Header="Review & Create",Content=finalBody};reviewTabs.Items.Add(setupTab);reviewTabs.Items.Add(finalTab);root.Children.Add(reviewTabs);
            backWithin=()=>{if(reviewTabs.SelectedItem!=finalTab)return false;if(RequestSettings!=null)RequestSettings();else reviewTabs.SelectedItem=setupTab;return true;};
            body.Children.Add(new Border { Background=IFCInfoWindow.Brush("#E8E2DA"),CornerRadius=new CornerRadius(10),Padding=new Thickness(18,12,18,12),Margin=new Thickness(0,0,0,16),
                Child=IFCInfoWindow.Text("Selected: "+rows.Count+" segments · Geometry checked during creation",16,"#6F6044") });
            var mode=UiDesign.Field(options,"Setup mode","Manual: edit each group. Auto: use the shared choices or exact IFC names.","Select mode",false,true);
            mode.Name="CurveSetupMode";mode.ItemsSource=new[]{"Manual","Auto"};mode.SelectedIndex=0;
            System.Action<string> section=text=>{options.Children.Add(new Border{Height=1,Background=IFCInfoWindow.Brush("#DDD6CE"),Margin=new Thickness(0,16,0,12)});options.Children.Add(IFCInfoWindow.Text(text,16,"#37322B"));};
            ComboBox sharedSystem=null;CableServicePicker sharedService=null;TextBlock serviceHelp=null;
            var pipeSystems=systems.Where(t=>t.Kind=="Pipe").ToList();
            if(kind=="Pipe") {
                section("System");sharedSystem=UiDesign.Field(options,"System type","Apply to all groups in Auto mode","Follow IFC",true,true);sharedSystem.Name="SharedPipeSystem";
                sharedSystem.ItemsSource=new[]{new ReplacementTypeOption{Id=0,Label="Follow IFC — match system name"}}.Concat(pipeSystems).ToList();sharedSystem.SelectedIndex=0;
            }
            var serviceValues=(projectServices??new List<string>()).Concat(rows.Select(SourceService)).Where(v=>!string.IsNullOrWhiteSpace(v)).Distinct(System.StringComparer.OrdinalIgnoreCase).ToList();
            if(kind=="CableTray") {
                section("Service");sharedService=new CableServicePicker(serviceValues,"",true);options.Children.Add(sharedService);
                serviceHelp=IFCInfoWindow.Text("",12,"#716B63");options.Children.Add(serviceHelp);
            }
            section(label+" Type");
            var available=types.Where(t=>t.CategoryId==categoryId && t.Kind==kind && t.Supported).ToList();
            var type=UiDesign.Field(options,label+" Type","Apply to all groups in Auto mode","Follow IFC",false,true);type.Name="CurveType";
            type.ItemsSource=new[]{new ReplacementTypeOption{Id=0,Label="Follow IFC — match type name"}}.Concat(available).ToList();type.SelectedIndex=0;
            var typeBoxes=new Dictionary<string,ComboBox>();var systemBoxes=new Dictionary<string,ComboBox>();
            var typeDisplays=new Dictionary<string,TextBlock>();var systemDisplays=new Dictionary<string,TextBlock>();var typeAutoChoices=new Dictionary<string,ComboBox>();var systemAutoChoices=new Dictionary<string,ComboBox>();
            var servicePickers=new Dictionary<string,CableServicePicker>();
            var groupPanels=new Dictionary<string,StackPanel>();

            var manualTypes=new Dictionary<string,long>();var manualSystems=new Dictionary<string,long>();
            var sourceGroups=rows.GroupBy(r=>(kind=="CableTray"?SourceService(r):r.SystemType??"")+" | "+(r.Name??""));
            foreach(var group in sourceGroups) {
                var settings=new StackPanel{Margin=new Thickness(16)};
                var expander=new Expander{Header=label+" · "+group.Key+" · "+group.Count()+" segments",IsExpanded=true,Content=settings,Padding=new Thickness(10),Margin=new Thickness(0,0,0,14),Background=System.Windows.Media.Brushes.White,BorderBrush=IFCInfoWindow.Brush("#DDD6CE"),BorderThickness=new Thickness(1)};
                groupsBody.Children.Add(expander);
                foreach(var row in group)groupPanels[row.ElementId]=settings;
                foreach(var issue in group.Select(r=>SourceIssue(r,kind)).Where(v=>v!=""))settings.Children.Add(IFCInfoWindow.Text(issue,13,"#A03020"));
                ComboBox systemBox=null;
                if(kind=="Pipe") {
                    systemBox=UiDesign.Field(settings,"System Type","Assign a piping system to this group","Select system",true);systemBox.Name="GroupPipeSystem";systemBox.ItemsSource=pipeSystems;
                    systemBox.SelectedItem=pipeSystems.FirstOrDefault(t=>string.Equals(t.Label,group.First().SystemType,System.StringComparison.OrdinalIgnoreCase))??(pipeSystems.Count==1?pipeSystems[0]:null);
                }
                if(kind=="CableTray") {
                    var servicePicker=new CableServicePicker(serviceValues,SourceService(group.First()));settings.Children.Add(servicePicker);
                    foreach(var row in group)servicePickers[row.ElementId]=servicePicker;
                }
                var groupType=UiDesign.Field(settings,label+" Type","Choose a target type for this IFC source group","Select type",false);groupType.Name="GroupCurveType";groupType.ItemsSource=available;
                groupType.SelectedItem=available.FirstOrDefault(t=>string.Equals(t.Label,group.First().Name,System.StringComparison.OrdinalIgnoreCase))??(available.Count==1?available[0]:null);
                var autoType=ReadOnlyChoice(groupType,"ResolvedCurveTypeChoice");
                var typeDisplay=IFCInfoWindow.Text("",13,"#716B63");typeDisplay.Name="ResolvedIfcCurveType";typeDisplay.Visibility=Visibility.Collapsed;((Panel)groupType.Parent).Children.Add(typeDisplay);
                TextBlock systemDisplay=null;
                ComboBox autoSystem=null;
                if(systemBox!=null){autoSystem=ReadOnlyChoice(systemBox,"ResolvedPipeSystemChoice");systemDisplay=IFCInfoWindow.Text("",13,"#716B63");systemDisplay.Name="ResolvedIfcPipeSystem";systemDisplay.Visibility=Visibility.Collapsed;((Panel)systemBox.Parent).Children.Add(systemDisplay);}
                foreach(var row in group) {typeBoxes[row.ElementId]=groupType;typeDisplays[row.ElementId]=typeDisplay;typeAutoChoices[row.ElementId]=autoType;if(systemBox!=null){systemBoxes[row.ElementId]=systemBox;systemDisplays[row.ElementId]=systemDisplay;systemAutoChoices[row.ElementId]=autoSystem;}}
            }
            foreach(var panel in groupPanels.Values.Distinct())foreach(var grid in panel.Children.OfType<Grid>())grid.Margin=new Thickness(0,12,0,0);
            bool wasAuto=false;bool applyingMode=false;
            System.Action applyMode=()=> {
                if(applyingMode)return;applyingMode=true;try {
                bool auto=mode.SelectedIndex==1;
                type.IsEnabled=auto;if(sharedSystem!=null)sharedSystem.IsEnabled=auto;if(sharedService!=null){sharedService.Visibility=auto?Visibility.Visible:Visibility.Collapsed;serviceHelp.Text=auto?"Follow each IFC service, choose a filled value, or Enter new…":"Choose the Service Type directly in each group. Enter new… reveals the input below.";}
                if(auto && !wasAuto)foreach(var row in rows){string id=row.ElementId;manualTypes[id]=(typeBoxes[id].SelectedItem as ReplacementTypeOption)?.Id??0;if(systemBoxes.ContainsKey(id))manualSystems[id]=(systemBoxes[id].SelectedItem as ReplacementTypeOption)?.Id??0;}
                foreach(var row in rows) {
                    string id=row.ElementId;var box=typeBoxes[id];
                    box.IsEnabled=!auto;
                    if(auto) {var common=type.SelectedItem as ReplacementTypeOption;box.SelectedItem=common?.Id>0?common:MatchSourceType(row,available);}
                    else if(wasAuto)box.SelectedItem=available.FirstOrDefault(t=>t.Id==manualTypes[id]);
                    box.Visibility=auto?Visibility.Collapsed:Visibility.Visible;typeDisplays[id].Visibility=auto?Visibility.Visible:Visibility.Collapsed;
                    var typeChoice=typeAutoChoices[id];typeChoice.Visibility=auto?Visibility.Visible:Visibility.Collapsed;typeChoice.ItemsSource=new[]{((type.SelectedItem as ReplacementTypeOption)?.Id>0?"Project type — ":"IFC — ")+((box.SelectedItem as ReplacementTypeOption)?.Label??"Unresolved")};typeChoice.SelectedIndex=0;
                    typeDisplays[id].Text=((type.SelectedItem as ReplacementTypeOption)?.Id>0?"Project type → ":"Follow IFC → ")+((box.SelectedItem as ReplacementTypeOption)?.Label??(row.Name+" · no matching project type. Choose a shared type or switch to Manual."));
                    if(systemBoxes.TryGetValue(id,out var system)) {
                        system.IsEnabled=!auto;
                        if(auto){var common=sharedSystem.SelectedItem as ReplacementTypeOption;system.SelectedItem=common?.Id>0?common:pipeSystems.FirstOrDefault(t=>string.Equals(t.Label,row.SystemType,System.StringComparison.OrdinalIgnoreCase));}
                        else if(wasAuto)system.SelectedItem=pipeSystems.FirstOrDefault(t=>t.Id==manualSystems[id]);
                        system.Visibility=auto?Visibility.Collapsed:Visibility.Visible;systemDisplays[id].Visibility=auto?Visibility.Visible:Visibility.Collapsed;
                        var systemChoice=systemAutoChoices[id];systemChoice.Visibility=auto?Visibility.Visible:Visibility.Collapsed;systemChoice.ItemsSource=new[]{((sharedSystem.SelectedItem as ReplacementTypeOption)?.Id>0?"Project system — ":"IFC — ")+((system.SelectedItem as ReplacementTypeOption)?.Label??"Unresolved")};systemChoice.SelectedIndex=0;
                        systemDisplays[id].Text=((sharedSystem.SelectedItem as ReplacementTypeOption)?.Id>0?"Project system → ":"Follow IFC → ")+((system.SelectedItem as ReplacementTypeOption)?.Label??(row.SystemType+" · no matching piping system. Choose a shared system or switch to Manual."));
                    }
                    if(servicePickers.TryGetValue(id,out var picker))picker.SetAutomatic(auto,sharedService.FollowsIfc?SourceService(row):sharedService.Value,sharedService.IsValid,sharedService.FollowsIfc);
                }
                wasAuto=auto;
                }finally{applyingMode=false;}
            };
            mode.SelectionChanged+=(s,e)=>applyMode();type.SelectionChanged+=(s,e)=>applyMode();if(sharedSystem!=null)sharedSystem.SelectionChanged+=(s,e)=>applyMode();if(sharedService!=null)sharedService.Changed+=()=>{applyMode();RefreshConfirmation?.Invoke();};applyMode();
            foreach(var picker in servicePickers.Values.Distinct())picker.Changed+=()=>RefreshConfirmation?.Invoke();
            body=options;
            section("Level");
            var level=UiDesign.Field(body,"Target level","Applies to checked segments; preserves the IFC centerline elevation","Select level",false,true);
            level.Name="SharedCurveLevel";
            var followLevel=new ReplacementLevelOption{Id=0,Label="Follow IFC — match name / elevation"};
            var levelChoices=new[]{followLevel}.Concat(levels).ToList();level.ItemsSource=levelChoices;level.SelectedItem=followLevel;
            var levelBoxes=new Dictionary<string,ComboBox>();
            var levelNotes=new Dictionary<string,TextBlock>();
            var manualLevelValues=new Dictionary<string,long>();
            foreach(var card in rows.GroupBy(r=>groupPanels[r.ElementId]))
            foreach(var group in card.GroupBy(r=>r.SourceLevelName??"No source level")) {
                var box=UiDesign.Field(card.Key,"Level","Source level: "+group.Key+" · Preserve the IFC centerline elevation","Follow IFC",false,false,true);box.Name="GroupCurveLevel";box.ItemsSource=levelChoices;box.SelectedItem=followLevel;
                ((FrameworkElement)box.Parent).Parent.SetValue(FrameworkElement.MarginProperty,new Thickness(0,12,0,0));
                var note=IFCInfoWindow.Text("",13,"#716B63");note.Name="ResolvedIfcCurveLevel";note.Margin=new Thickness(0,6,0,0);((Panel)box.Parent).Children.Add(note);
                foreach(var row in group){levelBoxes[row.ElementId]=box;levelNotes[row.ElementId]=note;manualLevelValues[row.ElementId]=0;}
            }
            System.Func<AirTerminalRow,ReplacementLevelOption> resolveLevel=row=> {
                var choice=(mode.SelectedIndex==1?level:levelBoxes[row.ElementId]).SelectedItem as ReplacementLevelOption;
                return choice==null?null:choice.Id>0?choice:CurveLevelResolver.Resolve(row,levels);
            };
            bool levelsWereAuto=false;bool refreshingLevels=false;
            System.Action refreshLevels=()=> {
                if(refreshingLevels)return;refreshingLevels=true;try {
                bool auto=mode.SelectedIndex==1;level.IsEnabled=auto;
                if(auto && !levelsWereAuto)foreach(var row in rows)manualLevelValues[row.ElementId]=(levelBoxes[row.ElementId].SelectedItem as ReplacementLevelOption)?.Id??0;
                foreach(var row in rows) {var box=levelBoxes[row.ElementId];box.IsEnabled=!auto;box.Visibility=Visibility.Visible;}
                // Shared group controls can represent multiple rows; update each control once.
                foreach(var box in levelBoxes.Values.Distinct()) {
                    var group=rows.Where(r=>levelBoxes[r.ElementId]==box).ToList();
                    if(!auto && levelsWereAuto){box.ItemsSource=levelChoices;box.SelectedItem=levelChoices.FirstOrDefault(l=>l.Id==manualLevelValues[group[0].ElementId])??followLevel;}
                    var names=group.Select(r=>resolveLevel(r)?.Label??"Unresolved — choose a project level").Distinct();
                    var choice=(auto?level:box).SelectedItem as ReplacementLevelOption;
                    string resolvedNames=string.Join("; ",names);
                    if(auto){box.ItemsSource=new[]{new ReplacementLevelOption{Id=choice?.Id??0,Label=(choice?.Id>0?"Project level — ":"Follow IFC — ")+resolvedNames}};box.SelectedIndex=0;}
                    levelNotes[group[0].ElementId].Text=(choice?.Id>0?"Project level → ":"Follow IFC → ")+resolvedNames;
                }
                levelsWereAuto=auto;
                }finally{refreshingLevels=false;}
            };
            mode.SelectionChanged+=(s,e)=>refreshLevels();level.SelectionChanged+=(s,e)=>refreshLevels();
            foreach(var box in levelBoxes.Values.Distinct())box.SelectionChanged+=(s,e)=>{if(!levelsWereAuto)refreshLevels();};
            refreshLevels();
            var policy=UiDesign.Field(body,"On error","Run behavior for "+label,"Choose run behavior",false,true);
            policy.ItemsSource=new[] { "Roll back the entire run","Skip failures and keep successful results" }; policy.SelectedIndex=0;
            body.Children.Add(IFCInfoWindow.Text(kind!="CableTray"?"Centerline and diameter come from IFC. Automatic fitting connections are not supported here.":"Centerline, width, height and profile orientation come from IFC. Automatic fitting connections are not supported here.",13,"#716B63"));
            var preview=new DataGrid { Name="CurvePreview",AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,CanUserDeleteRows=false,Margin=new Thickness(0,16,0,8) };
            foreach(var column in new[] { new[] { "IFC source","ElementId" },new[] { "Element","Name" },new[] { "System Type","SystemType" },new[] { "Elevation (mm)","Elevation" } })
                if(kind=="Pipe" || column[1]!="SystemType")
                preview.Columns.Add(new DataGridTextColumn { Header=column[0],Binding=new Binding(column[1]),Width=new DataGridLength(1,DataGridLengthUnitType.Star) });
            if(kind=="CableTray")preview.Columns.Add(new DataGridTextColumn{Header="IFC Service Type",Binding=new Binding("ServiceType"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
            preview.Columns.Add(new DataGridTextColumn{Header="Target type",Binding=new Binding("TargetType"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
            preview.Columns.Add(new DataGridTextColumn{Header=kind=="Pipe"?"Target system":kind=="CableTray"?"Target service":"System",Binding=new Binding("TargetAssignment"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
            preview.Columns.Add(new DataGridTextColumn{Header="Target level",Binding=new Binding("TargetLevel"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
            var finalNote=IFCInfoWindow.Text("",14,"#716B63");DockPanel.SetDock(finalNote,Dock.Top);finalBody.Children.Add(finalNote);
            UiDesign.StyleTable(preview);finalBody.Children.Add(preview);
            System.Func<bool> validSettings=()=>rows.Count>0 && rows.All(r=>SourceIssue(r,kind)=="") && servicePickers.Values.All(picker=>picker.IsValid) && typeBoxes.Values.All(b=>b.SelectedItem!=null) && rows.All(r=>resolveLevel(r)!=null) && systemBoxes.Values.All(b=>b.SelectedItem!=null);
            RefreshConfirmation=()=>{
                bool final=reviewTabs.SelectedItem==finalTab;
                create.Visibility=final?Visibility.Visible:Visibility.Collapsed;nextReview.Visibility=final?Visibility.Collapsed:Visibility.Visible;
                if(!final)return;
                create.IsEnabled=validSettings();
                feedback.Text=create.IsEnabled?"":rows.Select(r=>SourceIssue(r,kind)).FirstOrDefault(v=>v!="")??"Resolve every target type, service, system and level in Settings before creating.";
                finalNote.Text=rows.Count+" selected source(s) · "+policy.SelectedItem+". "+(MappingSummary?.Invoke()??"")+" Confirm source and target information below before Create.";
                preview.ItemsSource=rows.Select(row=>new{row.ElementId,row.Name,row.SystemType,row.ServiceType,row.Elevation,TargetType=(typeBoxes[row.ElementId].SelectedItem as ReplacementTypeOption)?.Label??"Unresolved",TargetAssignment=systemBoxes.ContainsKey(row.ElementId)?(systemBoxes[row.ElementId].SelectedItem as ReplacementTypeOption)?.Label??"Unresolved":servicePickers.ContainsKey(row.ElementId)?servicePickers[row.ElementId].Value:"Not applicable",TargetLevel=resolveLevel(row)?.Label??"Unresolved"}).ToList();
            };
            reviewTabs.SelectionChanged+=(s,e)=>{if(e.Source==reviewTabs)RefreshConfirmation();};
            nextReview.Click+=(s,e)=>{if(RequestReview!=null)RequestReview();else reviewTabs.SelectedItem=finalTab;};
            create.IsEnabled=rows.Count>0;
            create.Click+=(s,e)=>
            {
                if(reviewTabs.SelectedItem!=finalTab){feedback.Text="Open Review & Create to confirm all settings first.";return;}
                if(typeBoxes.Values.Any(b=>b.SelectedItem==null) || rows.Any(r=>resolveLevel(r)==null) || systemBoxes.Values.Any(b=>b.SelectedItem==null))
                { feedback.Text=kind=="Pipe"?"Select a pipe type, level and piping system type.":"Select a target type and level. System Type is not applicable."; return; }
                if(!validSettings()){feedback.Text=rows.Select(r=>SourceIssue(r,kind)).FirstOrDefault(v=>v!="")??"Resolve all target settings before creating.";return;}
                Request=new ReplacementRequest { Kind=kind,SourceCategoryId=categoryId,TypeId=((ReplacementTypeOption)typeBoxes.Values.First().SelectedItem).Id,LevelId=resolveLevel(rows[0]).Id,SourceLevelIds=rows.ToDictionary(r=>r.ElementId,r=>resolveLevel(r).Id),
                    KeepSuccessful=policy.SelectedIndex==1,SourceIds=rows.Select(r=>r.ElementId).Distinct().ToList(),
                    SourceTypeIds=typeBoxes.ToDictionary(p=>p.Key,p=>((ReplacementTypeOption)p.Value.SelectedItem).Id),
                    SourceSystemTypeIds=systemBoxes.ToDictionary(p=>p.Key,p=>((ReplacementTypeOption)p.Value.SelectedItem).Id),
                    ServiceTypes=servicePickers.ToDictionary(p=>p.Key,p=>p.Value.Value) };
                if(Request.SourceSystemTypeIds.Count>0) Request.SystemTypeId=Request.SourceSystemTypeIds.Values.First();
                if(SubmitEmbedded!=null)SubmitEmbedded(Request);else DialogResult=true;
            };
        }
    }
}
