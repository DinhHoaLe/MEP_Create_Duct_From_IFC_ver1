using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
namespace IFCInfo
{
 internal sealed class ParameterMappingPanel : DockPanel
 {
  readonly ObservableCollection<IfcParameterMapping> mappings=new ObservableCollection<IfcParameterMapping>();
  readonly ObservableCollection<string> sourceChoices=new ObservableCollection<string>();
  readonly ObservableCollection<string> targetChoices=new ObservableCollection<string>();
  readonly HashSet<string> proposedTargets=new HashSet<string>();
  readonly TextBlock status=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8)};
  readonly CheckBox createMissing=new CheckBox {Content="Create missing Text instance parameters during duct creation",Margin=new Thickness(6,12,6,12)};
  readonly DataGrid grid;
 readonly TextBlock selectionSummary=new TextBlock {Margin=new Thickness(6),TextWrapping=TextWrapping.Wrap};
  internal Func<List<AirTerminalRow>> Sources;
  internal Func<List<AirTerminalRow>> SelectedSources;
  internal Func<List<string>> Targets;
  internal Func<string,string> CheckTarget; internal Action<string,string,string,string> CreateParameter; internal Func<List<ParameterOption>> Types,Groups,Catalog;
  internal Func<List<AirTerminalRow>,List<IfcParameterMapping>,string> Transfer;
  internal ParameterMappingPanel()
  {
   Margin=new Thickness(16);
   var top=new StackPanel();SetDock(top,Dock.Top);Children.Add(top);

   top.Children.Add(selectionSummary);
   var toolbar=new WrapPanel();top.Children.Add(toolbar);
   AddButton(toolbar,"Refresh parameters",Refresh);
   AddButton(toolbar,"Add parameters…",AddParameters);
   AddButton(toolbar,"Create parameter…",CreateParameterDialog);
   AddButton(toolbar,"Project Parameters",ShowProjectParameters);

   var messageScroll=new ScrollViewer {Content=status,MaxHeight=115,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};top.Children.Add(messageScroll);
   grid=new DataGrid {ItemsSource=mappings,AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false,RowHeight=48};
   UiDesign.StyleTable(grid);
   grid.Columns.Add(ChoiceColumn("IFC Property","Source",sourceChoices));
   grid.Columns.Add(ChoiceColumn("Revit Parameter","Target",targetChoices));
   var typeStyle=new Style(typeof(TextBlock));typeStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty,VerticalAlignment.Center));typeStyle.Setters.Add(new Setter(TextBlock.MarginProperty,new Thickness(8,0,8,0)));typeStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty,TextWrapping.Wrap));
   grid.Columns.Add(new DataGridTextColumn {Header="IFC data type",Binding=new Binding("SourceType"),IsReadOnly=true,Width=190,ElementStyle=typeStyle});
   grid.Columns.Add(new DataGridTextColumn {Header="Revit data type",Binding=new Binding("TargetType"),IsReadOnly=true,Width=130,ElementStyle=typeStyle});
   var remove=new FrameworkElementFactory(typeof(Button));remove.SetValue(ContentControl.ContentProperty,"Remove");
   remove.SetValue(FrameworkElement.MarginProperty,new Thickness(6));
   var removeStyle=IFCInfoWindow.Button("Remove",false);
   remove.SetValue(Control.TemplateProperty,removeStyle.Template);remove.SetValue(Control.BackgroundProperty,removeStyle.Background);remove.SetValue(Control.ForegroundProperty,removeStyle.Foreground);remove.SetValue(Control.BorderBrushProperty,removeStyle.BorderBrush);
   remove.AddHandler(Button.ClickEvent,new RoutedEventHandler((sender,e)=>{mappings.Remove((IfcParameterMapping)((Button)sender).DataContext);Validate(null);}));
   grid.Columns.Add(new DataGridTemplateColumn {Header="Remove",CellTemplate=new DataTemplate{VisualTree=remove},Width=110});
   grid.SizeChanged+=(s,e)=> {double width=Math.Max(220,(grid.ActualWidth-454)/2);grid.Columns[0].Width=new DataGridLength(width);grid.Columns[1].Width=new DataGridLength(width);};
   Children.Add(grid);
   mappings.CollectionChanged+=(s,e)=> {
     if(e.NewItems!=null)foreach(IfcParameterMapping m in e.NewItems){m.PropertyChanged+=(sender,args)=>{if(args.PropertyName=="Source" || args.PropertyName=="Target")OnPairChanged(m,args.PropertyName);};UpdateSourceType(m);m.TargetType=(Catalog?.Invoke()??new List<ParameterOption>()).FirstOrDefault(x=>x.Name==m.Target)?.Discipline??"";}
   };
   mappings.Add(new IfcParameterMapping());
  }
  DataGridTemplateColumn ChoiceColumn(string header,string property,ObservableCollection<string> choices)
  {
   var combo=new FrameworkElementFactory(typeof(ComboBox));
   combo.AddHandler(FrameworkElement.LoadedEvent,new RoutedEventHandler((sender,e)=> {
      var box=(ComboBox)sender;
      if(box.ItemsSource is ListCollectionView existing){existing.Refresh();return;}
      var view=new ListCollectionView(choices);
      view.Filter=value=>AvailableChoices(box.DataContext as IfcParameterMapping,property,choices).Contains(value as string);
      box.ItemsSource=view;
      box.DropDownOpened+=(a,b)=>view.Refresh();
   }));
   var host=new StackPanel();var sample=UiDesign.Field(host,"","","Select parameter…",false,true);
   combo.SetValue(Control.StyleProperty,sample.Style);
   combo.SetValue(Control.MinHeightProperty,36.0);combo.SetValue(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Left);combo.SetValue(Control.VerticalContentAlignmentProperty,VerticalAlignment.Center);
   combo.SetValue(FrameworkElement.MarginProperty,new Thickness(8,5,8,5));
   combo.SetValue(ComboBox.IsTextSearchEnabledProperty,true);combo.SetValue(System.Windows.Controls.Primitives.Selector.IsSynchronizedWithCurrentItemProperty,false);
   combo.AddHandler(System.Windows.Controls.Primitives.Selector.SelectionChangedEvent,new SelectionChangedEventHandler((sender,e)=> {var row=((ComboBox)sender).DataContext as IfcParameterMapping;if(row!=null)grid.SelectedItem=row;}));
   combo.SetBinding(FrameworkElement.ToolTipProperty,new Binding(property));
   combo.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedItemProperty,new Binding(property) {Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});
   return new DataGridTemplateColumn {Header=header,CellTemplate=new DataTemplate {VisualTree=combo},Width=new DataGridLength(1,DataGridLengthUnitType.Star),MinWidth=220};
  }
  internal List<string> AvailableChoices(IfcParameterMapping row,string property,IEnumerable<string> choices)
  {
   var used=new HashSet<string>(mappings.Where(m=>m!=row).Select(m=>property=="Source"?m.Source:m.Target).Where(v=>!string.IsNullOrEmpty(v)),StringComparer.OrdinalIgnoreCase);
   return choices.Where(v=>(!used.Contains(v) || v==(property=="Source"?row?.Source:row?.Target)) && (property=="Source" || CompatibleTarget(row,v))).ToList();
  }
  internal bool CompatibleTarget(IfcParameterMapping row,string target)
  {
   var catalog=Catalog?.Invoke()??new List<ParameterOption>();
   if(catalog.Count==0)return true;
   if(row==null || string.IsNullOrWhiteSpace(row.Source))return false;
   UpdateSourceType(row);
   if(string.IsNullOrWhiteSpace(row.SourceType) || row.SourceType=="Unknown" || row.SourceType.Contains(","))return false;
   var candidate=catalog.FirstOrDefault(p=>p.Name==target);
   return candidate!=null && !string.IsNullOrEmpty(candidate.DataKind) &&
       candidate.DataKind==SuggestedType(row.SourceType) && CheckTarget?.Invoke(target)=="Ready";
  }
  static void AddButton(Panel panel,string text,Action action){var b=IFCInfoWindow.Button(text,false);b.Margin=new Thickness(6);b.Click+=(s,e)=>action();panel.Children.Add(b);}
  internal static string SuggestedType(string sourceType)
  {
   string t=(sourceType??"").ToUpperInvariant();
   if(t.Contains("VOLUMETRICFLOW") || t=="AIR FLOW")return "airFlow";
   if(t.Contains("LENGTH") || t.Contains("QUANTITYLENGTH"))return "length";
   if(t.Contains("AREA"))return "area";
   if(t.Contains("VOLUME"))return "volume";
   if(t.Contains("PRESSURE"))return "hvacPressure";
   if(t.Contains("TEMPERATURE"))return "hvacTemperature";
   if(t.Contains("BOOLEAN") || t.Contains("LOGICAL") || t=="YES/NO")return "boolean";
   if(t.Contains("INTEGER") || t=="ELEMENT ID")return "integer";
   if(t.Contains("REAL") || t.Contains("RATIO") || t.Contains("NUMERIC") || t=="NUMBER")return "number";
   return "text";
  }
  void CreateParameterDialog()=>BuildParameterDialog().ShowDialog();
  internal Window BuildParameterDialog()
  {
   var dialog=new Window {Title="Parameter Properties",Width=560,Height=720,MaxHeight=SystemParameters.WorkArea.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=UiDesign.Background};
   if(Window.GetWindow(this)?.IsVisible==true)dialog.Owner=Window.GetWindow(this);
   var root=new StackPanel {Margin=new Thickness(24)};
   var mode=UiDesign.Field(root,"Create parameter","","Select mode",false,true);
   mode.Name="ParameterCreationMode";mode.ItemsSource=new[]{"Manual","Follow IFC property"};mode.SelectedIndex=0;
   var sourcePanel=new StackPanel {Visibility=Visibility.Collapsed};root.Children.Add(sourcePanel);
   var source=UiDesign.Field(sourcePanel,"IFC property","","Select IFC property",false,true);
   source.Name="CreationIfcProperty";source.ItemsSource=sourceChoices;
   var map=grid.SelectedItem as IfcParameterMapping;
   source.SelectedItem=map?.Source;
   root.Children.Add(new TextBlock {Text="Name",Margin=new Thickness(0,14,0,6)});
   var name=new TextBox {Name="CreatedParameterName",Padding=new Thickness(8)};root.Children.Add(name);
   var options=Types?.Invoke()??new List<ParameterOption>();
   if(options.Count==0)options=new[]{"Text","Integer","Number","Yes/No"}.Select(x=>new ParameterOption {Id=x,Name=x,Discipline="Common"}).ToList();
   var discipline=UiDesign.Field(root,"Discipline","","Select discipline",false,true);
   discipline.Name="CreatedParameterDiscipline";
   discipline.ItemsSource=options.Select(x=>x.Discipline).Distinct().OrderBy(x=>x).ToList();
   var type=UiDesign.Field(root,"Data type","","Select type",false,true);type.Name="CreatedParameterType";
   discipline.SelectionChanged+=(s,e)=> {type.ItemsSource=options.Where(x=>x.Discipline==(string)discipline.SelectedItem).ToList();type.SelectedIndex=0;};
   discipline.SelectedItem=options.FirstOrDefault(x=>x.Id=="Text" || x.Id.Contains(":text-"))?.Discipline??options[0].Discipline;
   type.SelectedItem=options.FirstOrDefault(x=>x.Id=="Text" || x.Id.Contains(":text-"))??options[0];
   var group=UiDesign.Field(root,"Group parameter under","","Select group",false,true);
   var groups=Groups?.Invoke()??new List<ParameterOption>();if(groups.Count==0)groups=new[]{"Data","Identity Data"}.Select(x=>new ParameterOption{Id=x,Name=x}).ToList();
   group.ItemsSource=groups;group.SelectedItem=groups.FirstOrDefault(x=>x.Id.Contains(":data-"))??groups[0];
   var binding=UiDesign.Field(root,"Parameter binding","","Select binding",false,true);
   binding.Name="CreatedParameterBinding";binding.ItemsSource=new[]{"Instance","Type"};binding.SelectedIndex=0;
   var bindingNote=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)};root.Children.Add(bindingNote);
   binding.SelectionChanged+=(s,e)=>bindingNote.Text=binding.SelectedIndex==1?"Type values also affect existing ducts that use the same Duct Type.":"";
   root.Children.Add(new TextBlock {Text="Shared parameter · Ducts",Margin=new Thickness(0,12,0,0)});
   var feedback=new TextBlock {TextWrapping=TextWrapping.Wrap};root.Children.Add(feedback);
   Action follow=()=> {
      bool auto=mode.SelectedIndex==1;sourcePanel.Visibility=auto?Visibility.Visible:Visibility.Collapsed;
      name.IsReadOnly=auto;discipline.IsEnabled=!auto;type.IsEnabled=!auto;feedback.Text="";
      if(!auto)return;
      var property=source.SelectedItem as string;
      if(string.IsNullOrEmpty(property)){name.Text="";type.SelectedItem=null;feedback.Text="Select an IFC property.";return;}
      var probe=new IfcParameterMapping {Source=property};UpdateSourceType(probe);
      name.Text=property.Split('|').Last().Trim();
      if(string.IsNullOrWhiteSpace(probe.SourceType) || probe.SourceType=="Unknown" || probe.SourceType.Contains(",")) {
          type.SelectedItem=null;feedback.Text="IFC type is missing or inconsistent. Use Manual.";return;
      }
      string key=SuggestedType(probe.SourceType);
      var inferred=options.FirstOrDefault(x=>x.Id.Equals(key,StringComparison.OrdinalIgnoreCase) || x.Id.IndexOf(":"+key+"-",StringComparison.OrdinalIgnoreCase)>=0);
      if(inferred==null){type.SelectedItem=null;feedback.Text="No matching Revit type. Use Manual.";return;}
      discipline.SelectedItem=inferred.Discipline;type.SelectedItem=inferred;
   };
   mode.SelectionChanged+=(s,e)=>follow();source.SelectionChanged+=(s,e)=>follow();
   var buttons=new WrapPanel();root.Children.Add(buttons);
   AddButton(buttons,"Create",()=> {
      try {
       if(CreateParameter==null)throw new InvalidOperationException("No Revit project is connected.");
       if(type.SelectedItem==null || group.SelectedItem==null)throw new InvalidOperationException("Select data type and parameter group.");
       var selectedMap=map;
       if(mode.SelectedIndex==1)selectedMap=mappings.FirstOrDefault(m=>m.Source==(source.SelectedItem as string));
       if(selectedMap!=null && mappings.Any(m=>m!=selectedMap && string.Equals(m.Target,name.Text.Trim(),StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("This target parameter is already used.");
       CreateParameter(name.Text.Trim(),((ParameterOption)type.SelectedItem).Id,((ParameterOption)group.SelectedItem).Id,(string)binding.SelectedItem);
       Refresh();
       if(mode.SelectedIndex==1 && selectedMap==null){selectedMap=NewMapping();selectedMap.Source=(string)source.SelectedItem;mappings.Add(selectedMap);}
       if(selectedMap!=null)selectedMap.Target=name.Text.Trim();
       status.Text="Created: "+name.Text.Trim()+" · "+((ParameterOption)type.SelectedItem).Name;dialog.Close();
      } catch(Exception ex){feedback.Text=ex.Message;}
   });
   AddButton(buttons,"Cancel",()=>dialog.Close());
   UiDesign.SetContent(dialog,new ScrollViewer {Content=root,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
   return dialog;
  }  void AddParameters()
  {
   var dialog=new Window {Title="Add IFC parameters",Width=620,Height=500,Owner=Window.GetWindow(this),WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=UiDesign.Background};
   var root=new DockPanel {Margin=new Thickness(16)};
   var note=new TextBlock {Text="Select several IFC properties (Ctrl / Shift). All pairs apply to the elements checked in Elements.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(6)};
   DockPanel.SetDock(note,Dock.Top);root.Children.Add(note);
   var list=new ListBox {ItemsSource=AvailableChoices(null,"Source",sourceChoices),SelectionMode=SelectionMode.Extended};
   var add=IFCInfoWindow.Button("Add selected parameters",true);DockPanel.SetDock(add,Dock.Bottom);root.Children.Add(add);
   add.Click+=(s,e)=> {
    foreach(string source in list.SelectedItems) {
     var row=NewMapping();row.Source=source;
     if(mappings.Any(m=>m.Source==source && m.System==row.System))continue;
     mappings.Add(row);
    }
    foreach(var empty in mappings.Where(m=>string.IsNullOrEmpty(m.Source)&&string.IsNullOrEmpty(m.Target)).ToList())mappings.Remove(empty);
    dialog.Close();
   };
   root.Children.Add(list);UiDesign.SetContent(dialog,root);dialog.ShowDialog();
  }
  bool updatingPair;
  void OnPairChanged(IfcParameterMapping row,string property)
  {
   if(updatingPair)return;
   updatingPair=true;
   try {
    if(property=="Source" && !string.IsNullOrEmpty(row.Source) && mappings.Any(m=>m!=row && m.Source==row.Source)) {
        row.Source=null;status.Text="This IFC property is already used. Edit its existing row.";
    } else if(property=="Target" && !string.IsNullOrEmpty(row.Target) && mappings.Any(m=>m!=row && string.Equals(m.Target,row.Target,StringComparison.OrdinalIgnoreCase))) {
        row.Target=null;status.Text="This Revit parameter is already used. Choose another target.";
    }
    UpdateSourceType(row);
    if(!string.IsNullOrEmpty(row.Target) && !CompatibleTarget(row,row.Target))row.Target=null;
    row.TargetType=(Catalog?.Invoke()??new List<ParameterOption>()).FirstOrDefault(x=>x.Name==row.Target)?.Discipline??"";
   } finally {updatingPair=false;}
   ValidateCore(null,false);
  }
  void ShowProjectParameters()
  {
   var rows=Catalog?.Invoke()??new List<ParameterOption>();
   var dialog=new Window {Title="Project Parameters",Width=1000,Height=600,MaxWidth=SystemParameters.WorkArea.Width,MaxHeight=SystemParameters.WorkArea.Height,Owner=Window.GetWindow(this),WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=UiDesign.Background};
   var root=new DockPanel {Margin=new Thickness(20)};
   var title=new TextBlock {Text=rows.Count+" project parameter(s)",FontSize=20,Margin=new Thickness(0,0,0,12)};DockPanel.SetDock(title,Dock.Top);root.Children.Add(title);
   var close=IFCInfoWindow.Button("Close",false);close.Click+=(s,e)=>dialog.Close();DockPanel.SetDock(close,Dock.Bottom);root.Children.Add(close);
   var list=new DataGrid {ItemsSource=rows,AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false};
   foreach(var pair in new[]{new[]{"Name","Name"},new[]{"Data type","Discipline"},new[]{"Binding","Binding"},new[]{"Categories","Categories"}})
      list.Columns.Add(new DataGridTextColumn {Header=pair[0],Binding=new Binding(pair[1]),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
   UiDesign.StyleTable(list);root.Children.Add(list);UiDesign.SetContent(dialog,root);dialog.ShowDialog();
  }
  static string FriendlyType(string type)
  {
   switch(SuggestedType(type)) {
    case "text":return type=="Unknown"?"Unknown":new[]{"IFCLABEL","IFCTEXT","IFCIDENTIFIER","TEXT"}.Contains(type.ToUpperInvariant())?"Text":type;
    case "boolean":return "Yes/No";
    case "integer":return "Integer";
    case "number":return "Number";
    case "airFlow":return "Air Flow";
    case "length":return "Length";
    case "area":return "Area";
    case "volume":return "Volume";
    case "hvacPressure":return "Pressure";
    case "hvacTemperature":return "Temperature";
    default:return type;
   }
  }
  void UpdateSourceType(IfcParameterMapping map)
  {
   if(map.Source=="IFC GUID" || map.Source=="IFC Element ID"){map.SourceType=map.Source=="IFC GUID"?"Text":"Integer";return;}
   var types=(Sources?.Invoke()??new List<AirTerminalRow>()).SelectMany(r=>r.IfcProperties).Where(p=>IfcParameterMapping.Key(p)==map.Source || p.Name==map.Source).Select(p=>string.IsNullOrEmpty(p.DataType)?"Unknown":p.DataType).Distinct().ToList();
   map.SourceType=types.Count==0?"Unknown":string.Join(", ",types.Select(FriendlyType).Distinct());
  }
  IfcParameterMapping NewMapping()=>new IfcParameterMapping();
  internal void Refresh()
  {
   // Replace only changed catalog entries to preserve ComboBox selections and confirmed mappings.
   var selected=Sources?.Invoke()??new List<AirTerminalRow>();selectionSummary.Text=selected.Count+" selected element(s)";
   Sync(sourceChoices,(Sources?.Invoke()??new List<AirTerminalRow>()).SelectMany(r=>r.IfcProperties??new List<IfcPropertyValue>()).Select(IfcParameterMapping.Key).Concat(selected.Count>0?new[]{"IFC GUID","IFC Element ID"}:new string[0]));
   Sync(targetChoices,(Targets?.Invoke()??new List<string>()).Concat(proposedTargets));
   foreach(var m in mappings){m.ElementIds=null;m.System=null;m.Selected=true;UpdateSourceType(m);m.TargetType=(Catalog?.Invoke()??new List<ParameterOption>()).FirstOrDefault(x=>x.Name==m.Target)?.Discipline??"";}
   Validate(null);
  }
  static void Sync(ObservableCollection<string> choices,IEnumerable<string> values)
  {
   var list=values.Distinct().OrderBy(x=>x).ToList();
   foreach(var value in choices.Where(x=>!list.Contains(x)).ToList())choices.Remove(value);
   foreach(var value in list)if(!choices.Contains(value))choices.Add(value);
  }
  internal void Map(IfcParameterMapping m)
  {
   grid.SelectedItem=m;
   if(m.IsMapped){m.IsMapped=false;status.Text="Mapping removed. Click Map to apply it again.";return;}
   m.CreateMissing=createMissing.IsChecked==true;
   string error=Error(m,Sources?.Invoke()??new List<AirTerminalRow>());
   if(error!=null){status.Text=error;return;}
   if(mappings.Any(x=>x!=m&&x.IsMapped&&x.Selected&&!string.IsNullOrWhiteSpace(m.Target)&&string.Equals(x.Target,m.Target,StringComparison.OrdinalIgnoreCase)&&(x.ElementIds==null || m.ElementIds==null || x.ElementIds.Intersect(m.ElementIds).Any()))){status.Text="This Revit parameter is already mapped.";return;}
   m.IsMapped=true;status.Text=m.Source+" → "+m.Target+". "+(CheckTarget?.Invoke(m.Target)=="Missing"?"Will create during duct creation.":"Ready.");
  }
  string Error(IfcParameterMapping m,List<AirTerminalRow> rows)
  {
   if(string.IsNullOrWhiteSpace(m.Source)||string.IsNullOrWhiteSpace(m.Target))return "Select both IFC Property and Revit Parameter.";
   if(!CompatibleTarget(m,m.Target))return "Choose a writable Revit parameter matching the IFC data type.";
   string state=CheckTarget?.Invoke(m.Target)??"Catalog unavailable";
   if(state!="Ready"&&!(state=="Missing"&&m.CreateMissing))return m.Target+": "+(state=="Missing"?"Missing. Use Create parameter to add a Text instance parameter bound to Ducts.":state);
   try {var values=rows.Where(m.Applies).Select(r=>IfcParameterMapping.Read(r,m.Source)).ToList();if(!values.Any(v=>v!=null))return m.Source+": not found in the applicable IFC sources.";}catch(Exception ex){return ex.Message;}
   return null;
  }
  internal bool Ready => ValidateCore(null,false);
  internal bool ValidateNext()
  {
   if(mappings.Count==0 || mappings.All(m=>string.IsNullOrWhiteSpace(m.Source)&&string.IsNullOrWhiteSpace(m.Target))){status.Text="Missing parameter: add an IFC property and select a Revit Parameter before continuing.";return false;}
   return ValidateCore(null,true);
  }
  internal bool Validate(DuctRequest request)=>ValidateCore(request,true);
  bool ValidateCore(DuctRequest request,bool commit)
  {
   var rows=request==null?(Sources?.Invoke()??new List<AirTerminalRow>()):(request.Items??new List<DuctPlanItem>()).Select(i=>i.Source).ToList();
   if(commit){grid.CommitEdit(DataGridEditingUnit.Cell,true);grid.CommitEdit(DataGridEditingUnit.Row,true);}
   var errors=new List<string>();var active=mappings.Where(m=>m.Selected && (!string.IsNullOrEmpty(m.Source)||!string.IsNullOrEmpty(m.Target))).ToList();
   foreach(var m in active)if(createMissing.IsChecked==true)m.CreateMissing=true;
   foreach(var m in active){string error=Error(m,rows);if(error!=null)errors.Add("Row "+(mappings.IndexOf(m)+1)+": "+error);}
   if(active.GroupBy(m=>m.Source).Any(g=>g.Count()>1))errors.Add("Each IFC property may be used only once.");
   if(active.Any(m=>active.Any(x=>x!=m&&!string.IsNullOrWhiteSpace(m.Target)&&string.Equals(x.Target,m.Target,StringComparison.OrdinalIgnoreCase)&&(x.ElementIds==null || m.ElementIds==null || x.ElementIds.Intersect(m.ElementIds).Any()))))errors.Add("Duplicate Revit target mappings.");
   status.Text=errors.Count>0?string.Join("\n",errors):active.Count==0?"No properties configured. Use Add parameters to choose values to write.":active.Count+" parameter(s) will be assigned to "+rows.Count+" selected duct(s) when Create is clicked.";
   if(errors.Count>0)return false;
   if(request!=null)request.ParameterMappings=Snapshot(active);
   return true;
  }
  static List<IfcParameterMapping> Snapshot(IEnumerable<IfcParameterMapping> rows)=>rows.Select(m=>new IfcParameterMapping {Source=m.Source,Target=m.Target,CreateMissing=m.CreateMissing,ParameterBinding=m.ParameterBinding,ParameterType=m.ParameterType,ParameterGroup=m.ParameterGroup,ElementIds=m.ElementIds?.ToList(),System=m.System,IsMapped=true}).ToList();
  void TransferExisting()
  {
   var rows=SelectedSources?.Invoke()??new List<AirTerminalRow>();
   if(rows.Count==0){status.Text="Highlight source rows in Elements first (Ctrl / Shift for multiple rows).";return;}
   var request=new DuctRequest {Items=rows.Select(r=>new DuctPlanItem {Source=r}).ToList()};
   if(!Validate(request))return;
   if(request.ParameterMappings.Count==0){status.Text="Map at least one parameter first.";return;}
   try {status.Text=Transfer?.Invoke(rows,request.ParameterMappings)??"Transfer unavailable.";}catch(Exception ex){status.Text="Transfer failed; changes rolled back. "+ex.Message;}
  }
 }
}
