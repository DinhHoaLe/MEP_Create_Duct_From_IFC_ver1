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
  readonly CheckBox createMissing=new CheckBox {Content="Create missing Text instance parameters on transfer / duct creation",Margin=new Thickness(6,12,6,12)};
  readonly DataGrid grid;
  internal Func<List<AirTerminalRow>> Sources;
  internal Func<List<AirTerminalRow>> SelectedSources;
  internal Func<List<string>> Targets;
  internal Func<string,string> CheckTarget;
  internal Func<List<AirTerminalRow>,List<IfcParameterMapping>,string> Transfer;
  internal ParameterMappingPanel()
  {
   Margin=new Thickness(16);
   var top=new StackPanel();SetDock(top,Dock.Top);Children.Add(top);
   top.Children.Add(new TextBlock {Text="IFC → Revit parameters",FontSize=22,FontWeight=FontWeights.SemiBold});
   top.Children.Add(new TextBlock {Text="Select an IFC property and a Revit Text instance parameter, then click Map. The catalog includes all loaded IFC elements.\nMapped values apply when creating ducts. For existing ducts, highlight source rows in Elements and click Transfer below; only ducts tracked to that IFC source are changed.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(6,12,6,12)});
   var toolbar=new WrapPanel();top.Children.Add(toolbar);
   AddButton(toolbar,"Refresh parameters",Refresh);
   AddButton(toolbar,"Add row",()=>mappings.Add(new IfcParameterMapping()));
   AddButton(toolbar,"Remove selected",()=> {foreach(var m in grid.SelectedItems.Cast<IfcParameterMapping>().ToList())mappings.Remove(m);Validate(null);});
   AddButton(toolbar,"Check mappings",()=>Validate(null));
   AddButton(toolbar,"Transfer to existing ducts",TransferExisting);
   var newParameter=new WrapPanel();top.Children.Add(newParameter);
   newParameter.Children.Add(new TextBlock {Text="New Revit parameter:",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(6)});
   var newName=new TextBox {MinWidth=250,Margin=new Thickness(6),Padding=new Thickness(8),ToolTip="Name of a new Text instance parameter"};newParameter.Children.Add(newName);
   AddButton(newParameter,"Add to target list",()=> {string name=newName.Text.Trim();if(name.Length==0){status.Text="Enter a parameter name.";return;}proposedTargets.Add(name);if(!targetChoices.Contains(name))targetChoices.Add(name);status.Text="Select '"+name+"' in Revit Parameter. Enable Create missing to create it on transfer / duct creation.";});
   top.Children.Add(createMissing);
   createMissing.Unchecked+=(s,e)=> {foreach(var m in mappings.Where(x=>x.CreateMissing))m.IsMapped=false;};
   var messageScroll=new ScrollViewer {Content=status,MaxHeight=115,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};top.Children.Add(messageScroll);
   grid=new DataGrid {ItemsSource=mappings,AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false,RowHeight=48};
   UiDesign.StyleTable(grid);
   grid.Columns.Add(ChoiceColumn("IFC Property","Source",sourceChoices));
   grid.Columns.Add(ChoiceColumn("Revit Parameter","Target",targetChoices));
   var button=new FrameworkElementFactory(typeof(Button));button.SetBinding(Button.ContentProperty,new Binding("MappingLabel"));
   var buttonStyle=IFCInfoWindow.Button("Map",false);
   button.SetValue(Control.TemplateProperty,buttonStyle.Template);
   button.SetValue(Control.BackgroundProperty,buttonStyle.Background);
   button.SetValue(Control.ForegroundProperty,buttonStyle.Foreground);
   button.SetValue(Control.BorderBrushProperty,buttonStyle.BorderBrush);
   button.SetValue(FrameworkElement.MarginProperty,new Thickness(8,5,8,5));
   button.AddHandler(Button.ClickEvent,new RoutedEventHandler((s,e)=>Map((IfcParameterMapping)((Button)s).DataContext)));
   grid.Columns.Add(new DataGridTemplateColumn {Header="Mapping",CellTemplate=new DataTemplate {VisualTree=button},Width=190,MinWidth=190});
   grid.SizeChanged+=(s,e)=> {
    double width=Math.Max(220,(grid.ActualWidth-214)/2);
    grid.Columns[0].Width=new DataGridLength(width);grid.Columns[1].Width=new DataGridLength(width);
   };
   Children.Add(grid);
   mappings.Add(new IfcParameterMapping());
  }
  static DataGridTemplateColumn ChoiceColumn(string header,string property,ObservableCollection<string> choices)
  {
   var combo=new FrameworkElementFactory(typeof(ComboBox));combo.SetValue(ItemsControl.ItemsSourceProperty,choices);
   combo.SetValue(FrameworkElement.MarginProperty,new Thickness(8,5,8,5));
   combo.SetValue(ComboBox.IsTextSearchEnabledProperty,true);
   combo.SetBinding(FrameworkElement.ToolTipProperty,new Binding(property));
   combo.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedItemProperty,new Binding(property) {Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});
   return new DataGridTemplateColumn {Header=header,CellTemplate=new DataTemplate {VisualTree=combo},Width=new DataGridLength(1,DataGridLengthUnitType.Star),MinWidth=220};
  }
  static void AddButton(Panel panel,string text,Action action){var b=IFCInfoWindow.Button(text,false);b.Margin=new Thickness(6);b.Click+=(s,e)=>action();panel.Children.Add(b);}
  internal void Refresh()
  {
   // Replace only changed catalog entries to preserve ComboBox selections and confirmed mappings.
   Sync(sourceChoices,(Sources?.Invoke()??new List<AirTerminalRow>()).SelectMany(r=>r.IfcProperties??new List<IfcPropertyValue>()).Select(IfcParameterMapping.Key));
   Sync(targetChoices,(Targets?.Invoke()??new List<string>()).Concat(proposedTargets));
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
   if(m.IsMapped){m.IsMapped=false;status.Text="Mapping removed. Click Map to apply it again.";return;}
   m.CreateMissing=createMissing.IsChecked==true;
   string error=Error(m,Sources?.Invoke()??new List<AirTerminalRow>());
   if(error!=null){status.Text=error;return;}
   if(mappings.Any(x=>x!=m&&x.IsMapped&&string.Equals(x.Target,m.Target,StringComparison.OrdinalIgnoreCase))){status.Text="This Revit parameter is already mapped.";return;}
   m.IsMapped=true;status.Text=m.Source+" → "+m.Target+". "+(CheckTarget?.Invoke(m.Target)=="Missing"?"Will create on transfer / duct creation.":"Ready.");
  }
  string Error(IfcParameterMapping m,List<AirTerminalRow> rows)
  {
   if(string.IsNullOrWhiteSpace(m.Source)||string.IsNullOrWhiteSpace(m.Target))return "Select both IFC Property and Revit Parameter.";
   string state=CheckTarget?.Invoke(m.Target)??"Catalog unavailable";
   if(state!="Ready"&&!(state=="Missing"&&m.CreateMissing))return m.Target+": "+(state=="Missing"?"Missing. Enable Create missing, or close this tool and create a Text instance parameter bound to Ducts in Revit.":state);
   try {var values=rows.Select(r=>IfcParameterMapping.Read(r,m.Source)).ToList();if(!values.Any(v=>v!=null))return m.Source+": not found in the applicable IFC sources.";}catch(Exception ex){return ex.Message;}
   return null;
  }
  internal bool Validate(DuctRequest request)
  {
   var rows=request==null?(Sources?.Invoke()??new List<AirTerminalRow>()):(request.Items??new List<DuctPlanItem>()).Select(i=>i.Source).ToList();
   var errors=new List<string>();var active=mappings.Where(m=>m.IsMapped).ToList();
   foreach(var m in active){string error=Error(m,rows);if(error!=null)errors.Add(error);}
   if(active.GroupBy(m=>m.Target,StringComparer.OrdinalIgnoreCase).Any(g=>g.Count()>1))errors.Add("Duplicate Revit target mappings.");
   int pending=mappings.Count(m=>!m.IsMapped&&(!string.IsNullOrEmpty(m.Source)||!string.IsNullOrEmpty(m.Target)));
   if(pending>0)errors.Add(pending+" row(s) pending: click Map or remove the row before creating / transferring.");
   status.Text=errors.Count>0?string.Join("\n",errors):active.Count+" mapping(s) ready. Select source rows in Elements to transfer to existing ducts.";
   if(errors.Count>0)return false;
   if(request!=null)request.ParameterMappings=Snapshot(active);
   return true;
  }
  static List<IfcParameterMapping> Snapshot(IEnumerable<IfcParameterMapping> rows)=>rows.Select(m=>new IfcParameterMapping {Source=m.Source,Target=m.Target,CreateMissing=m.CreateMissing,IsMapped=true}).ToList();
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
