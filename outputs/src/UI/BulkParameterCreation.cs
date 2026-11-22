using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace IFCInfo
{
    internal sealed class BulkParameterRow : INotifyPropertyChanged
    {
        bool include; string name, state;
        public event PropertyChangedEventHandler PropertyChanged;
        void Changed(string property) => PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(property));
        public bool Include { get=>include; set { include=value; Changed(nameof(Include)); } }
        public string Source { get; set; }
        public string Name { get=>name; set {name=value; Changed(nameof(Name));} }
        public string TypeId { get; set; }
        public string DataType { get; set; }
        internal string TypeError { get; set; }
        internal bool Completed { get; set; }
        public string State { get=>state; set {state=value; Changed(nameof(State));} }
    }

    internal sealed partial class ParameterMappingPanel
    {
        internal static ParameterOption InferParameterType(IEnumerable<ParameterOption> options,string kind)
            => options.FirstOrDefault(x=>string.Equals(x.DataKind,kind,StringComparison.OrdinalIgnoreCase))
                ?? options.FirstOrDefault(x=>x.Id.Equals(kind,StringComparison.OrdinalIgnoreCase)
                    || (kind=="boolean" && x.Id=="Yes/No")
                    || x.Id.IndexOf(":"+kind+"-",StringComparison.OrdinalIgnoreCase)>=0);

        List<ParameterOption> CreationTypes()
        {
            var options=Types?.Invoke()??new List<ParameterOption>();
            return options.Count>0 ? options : new[]{"Text","Integer","Number","Yes/No"}
                .Select(t=>new ParameterOption{Id=t,Name=t,DataKind=t=="Yes/No"?"boolean":t.ToLowerInvariant(),Discipline="Common"}).ToList();
        }

        internal List<BulkParameterRow> PlanParameters(IEnumerable<ParameterCsv.Row> input)
        {
            var types=CreationTypes();
            var result=new List<BulkParameterRow>();
            foreach(var item in input)
            {
                var probe=new IfcParameterMapping{Source=item.IFCProperty};UpdateSourceType(probe);
                string error=null;
                bool known=sourceChoices.Contains(item.IFCProperty);
                string kind=SuggestedType(probe.SourceType);
                if(!string.IsNullOrWhiteSpace(item.IFCProperty) && !known)error="IFC property is not in the selected sources. Use its full qualified name.";
                else if(!string.IsNullOrWhiteSpace(item.IFCProperty) && (probe.SourceType=="Unknown" || probe.SourceType.Contains(",")))error="IFC data type is missing or inconsistent. Use Manual.";
                if(!string.IsNullOrWhiteSpace(item.DataType))
                {
                    string explicitKind=SuggestedType(item.DataType);
                    if(!new[]{"Text","Integer","Number","Yes/No","Length"}.Contains(item.DataType,StringComparer.OrdinalIgnoreCase))error="DataType must be Text, Integer, Number, Yes/No or Length.";
                    else if(known && explicitKind!=kind)error="CSV data type does not match the IFC property.";
                    kind=explicitKind;
                }
                else if(string.IsNullOrWhiteSpace(item.IFCProperty))error="Provide DataType when IFCProperty is blank.";
                var type=InferParameterType(types,kind);
                if(type==null || !new[]{"text","integer","number","boolean","length"}.Contains(kind))error=error??"No supported Revit data type. Use Manual.";
                var row=new BulkParameterRow {Source=item.IFCProperty,Name=item.ParameterName,TypeId=type?.Id,DataType=type?.Name??"Unknown",TypeError=error,State=error??"Ready to create or reuse",Include=error==null};
                result.Add(row);
            }
            foreach(var group in result.GroupBy(r=>r.Name,StringComparer.OrdinalIgnoreCase).Where(g=>g.Count()>1))
                foreach(var row in group){row.Include=false;row.State="Duplicate target name. Edit the name before selecting.";}
            return result;
        }

        void ImportParameterCsv()
        {
            try
            {
                var file=new Microsoft.Win32.OpenFileDialog{Title="Import parameter definitions and mappings",Filter="CSV (*.csv)|*.csv"};
                if(file.ShowDialog(Window.GetWindow(this))==true)
                    BuildBulkParameterDialog(ParameterCsv.Read(File.ReadAllText(file.FileName,Encoding.UTF8))).ShowDialog();
            }
            catch(Exception ex){status.Text="CSV import: "+ex.Message;}
        }

        void ExportParameterCsvTemplate()
        {
            try
            {
                var file=new Microsoft.Win32.SaveFileDialog{Title="Export parameter CSV template",Filter="CSV (*.csv)|*.csv",FileName="IFC-parameter-template.csv"};
                if(file.ShowDialog(Window.GetWindow(this))==true)
                    File.WriteAllText(file.FileName,"IFCProperty,ParameterName,DataType\r\nIFC GUID,IFC Guid,Text\r\nIFC Element ID,IFC Element ID,Integer\r\n",new UTF8Encoding(true));
            }
            catch(Exception ex){status.Text="CSV template: "+ex.Message;}
        }

        internal Window BuildBulkParameterDialog(List<ParameterCsv.Row> imported=null)
        {
            Refresh();
            var rows=PlanParameters(imported??sourceChoices.Select(s=>new ParameterCsv.Row{IFCProperty=s,ParameterName=mappings.FirstOrDefault(m=>m.Source==s && !string.IsNullOrWhiteSpace(m.Target))?.Target??s.Split('|').Last().Trim()}).ToList());
            var dialog=new Window {Title=imported==null?"Create multiple parameters from IFC":"Import CSV parameters",Width=1050,Height=650,MaxHeight=SystemParameters.WorkArea.Height,MaxWidth=SystemParameters.WorkArea.Width,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=UiDesign.Background};
            if(Window.GetWindow(this)?.IsVisible==true)dialog.Owner=Window.GetWindow(this);
            var root=new DockPanel{Margin=new Thickness(20)};
            var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
            top.Children.Add(new TextBlock{Text="Review names and inferred data types. Selected parameters are created now and mapped to the checked IFC elements. Existing compatible parameters are reused. Each creation has its own transaction; successes remain if another row fails.",TextWrapping=TextWrapping.Wrap});
            var options=new WrapPanel();top.Children.Add(options);
            AddButton(options,"Select all",()=>{foreach(var row in rows.Where(r=>r.TypeId!=null && r.TypeError==null && !r.Completed))row.Include=true;});
            AddButton(options,"Clear selection",()=>{foreach(var row in rows)row.Include=false;});
            var prefix=new TextBox{Name="BulkParameterPrefix",Text="IFC_",Width=120,MinHeight=32,Margin=new Thickness(8),ToolTip="Prefix for selected parameter names"};options.Children.Add(prefix);
            AddButton(options,"Apply prefix / resolve names",()=> {
                var selected=rows.Where(r=>r.Include && !r.Completed).ToList();
                var used=new HashSet<string>((Targets?.Invoke()??new List<string>()).Concat(rows.Except(selected).Select(r=>r.Name)),StringComparer.OrdinalIgnoreCase);
                foreach(var row in selected){string name=row.Name??"";if(prefix.Text.Length>0 && name.StartsWith(prefix.Text,StringComparison.Ordinal))name=name.Substring(prefix.Text.Length);row.Name=ParameterNames.Unique(name,prefix.Text,used);}
            });
            var groups=Groups?.Invoke()??new List<ParameterOption>();if(groups.Count==0)groups.Add(new ParameterOption{Id="Data",Name="Data"});
            var groupBox=UiDesign.Field(top,"Group parameter under","","Select group",false,true);groupBox.ItemsSource=groups;groupBox.SelectedItem=groups.FirstOrDefault(g=>g.Id=="Data" || g.Id.Contains(":data-"))??groups[0];
            var binding=UiDesign.Field(top,"Parameter binding","Type values affect every duct using the same type.","Select binding",false,true);binding.ItemsSource=new[]{"Instance","Type"};binding.SelectedIndex=0;
            var footer=new StackPanel();DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            var report=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4)};footer.Children.Add(report);
            var buttons=new WrapPanel();footer.Children.Add(buttons);
            var table=new DataGrid{Name="BulkIfcParameters",ItemsSource=rows,AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false};
            table.Columns.Add(new DataGridCheckBoxColumn{Header="Include",Binding=new Binding("Include"){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged}});
            foreach(var property in new[]{"Source","Name","DataType","State"})table.Columns.Add(new DataGridTextColumn{Header=property=="Name"?"Revit parameter name":property=="Source"?"IFC property":property=="DataType"?"Data type":"Result",Binding=new Binding(property),IsReadOnly=property!="Name",Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
            UiDesign.StyleTable(table);root.Children.Add(table);
            AddButton(buttons,"Create selected",()=>
            {
                table.CommitEdit(DataGridEditingUnit.Cell,true);table.CommitEdit(DataGridEditingUnit.Row,true);
                var selected=rows.Where(r=>r.Include).ToList();
                if(selected.Count==0){report.Text="Select at least one parameter.";return;}
                if(selected.GroupBy(r=>(r.Name??"").Trim(),StringComparer.OrdinalIgnoreCase).Any(g=>g.Count()>1)
                    || selected.Where(r=>!string.IsNullOrWhiteSpace(r.Source)).GroupBy(r=>r.Source).Any(g=>g.Count()>1))
                {report.Text="Duplicate parameter names or IFC properties. Rename or uncheck conflicting rows.";return;}
                int success=0,failed=0;
                foreach(var row in selected)
                {
                    try
                    {
                        if(row.TypeId==null || row.TypeError!=null)throw new InvalidOperationException(row.TypeError??"Resolve the data type first, or uncheck this row.");
                        string name=(row.Name??"").Trim();
                        if(string.IsNullOrWhiteSpace(name))throw new InvalidOperationException("Enter a parameter name.");
                        if(mappings.Any(m=>m.Source!=row.Source && string.Equals(m.Target,name,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("Target is already used by another IFC property.");
                        var existing=mappings.FirstOrDefault(m=>m.Source==row.Source && !string.IsNullOrWhiteSpace(m.Target));
                        if(existing!=null && !string.Equals(existing.Target,name,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("IFC property already mapped. Remove its previous mapping first.");
                        string state=CheckTarget?.Invoke(name)??"Catalog unavailable";
                        bool reused=state=="Ready";
                        if(reused)
                        {
                            var target=(Catalog?.Invoke()??new List<ParameterOption>()).FirstOrDefault(p=>p.Name==name);
                            if(target==null || target.Id!=row.TypeId || target.Binding!=(string)binding.SelectedItem)throw new InvalidOperationException("Existing parameter has a different data type or binding.");
                        }
                        else if(state!="Missing")throw new InvalidOperationException(state);
                        else if(CreateParameter==null)throw new InvalidOperationException("No Revit project is connected.");
                        else CreateParameter(name,row.TypeId,((ParameterOption)groupBox.SelectedItem).Id,(string)binding.SelectedItem);
                        Refresh();
                        if(!string.IsNullOrWhiteSpace(row.Source))
                        {
                            var mapping=mappings.FirstOrDefault(m=>m.Source==row.Source);
                            if(mapping==null){mapping=new IfcParameterMapping{Source=row.Source};mappings.Add(mapping);}
                            mapping.Target=name;
                            if(mapping.Target!=name)throw new InvalidOperationException("Parameter exists, but mapping was rejected. Check source type and target compatibility.");
                        }
                        row.State=(reused?"Reused":"Created")+(string.IsNullOrWhiteSpace(row.Source)?"":" and mapped");row.Completed=true;row.Include=false;success++;
                        if(!string.IsNullOrWhiteSpace(row.Source))
                        {
                            var saved=mappings.First(m=>m.Source==row.Source);saved.ParameterType=row.TypeId;saved.ParameterGroup=((ParameterOption)groupBox.SelectedItem).Id;saved.ParameterBinding=(string)binding.SelectedItem;
                        }
                    }
                    catch(Exception ex){row.State="Error: "+ex.Message;failed++;}
                }
                report.Text=success+" succeeded; "+failed+" failed. Successful parameters remain in the project. Values are written when ducts are created.";
            });
            AddButton(buttons,"Close",()=>dialog.Close());
            UiDesign.SetContent(dialog,root);return dialog;
        }
    }
}
