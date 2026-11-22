using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace IFCInfo
{
    internal sealed partial class ParameterMappingPanel
    {
        internal static List<IfcParameterMapping> CopyRows(IEnumerable<IfcParameterMapping> rows)=>rows.Select(m=>new IfcParameterMapping{Source=m.Source,Target=m.Target,CreateMissing=m.CreateMissing,ParameterBinding=m.ParameterBinding,ParameterType=m.ParameterType,ParameterGroup=m.ParameterGroup,ElementIds=m.ElementIds?.ToList(),System=m.System,Selected=m.Selected,IsMapped=m.IsMapped}).ToList();
        internal List<IfcParameterMapping> CaptureRows()
        {
            grid.CommitEdit(DataGridEditingUnit.Cell,true);grid.CommitEdit(DataGridEditingUnit.Row,true);return CopyRows(mappings);
        }
        internal void RestoreRows(IEnumerable<IfcParameterMapping> rows)
        {
            updatingPair=true;
            try{mappings.Clear();foreach(var row in CopyRows(rows)){if(!string.IsNullOrWhiteSpace(row.Target))proposedTargets.Add(row.Target);mappings.Add(row);}if(mappings.Count==0)mappings.Add(new IfcParameterMapping());}
            finally{updatingPair=false;}
            Refresh();
        }
        internal List<ParameterCsv.Row> SelectedParameterDefinitions()
        {
            Refresh();
            var used=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return sourceChoices.Select(source=> {
                var probe=new IfcParameterMapping{Source=source};UpdateSourceType(probe);
                var existing=mappings.FirstOrDefault(m=>m.Source==source && !string.IsNullOrWhiteSpace(m.Target));
                string kind=SuggestedType(probe.SourceType);
                string type=probe.SourceType=="Unknown" || probe.SourceType.Contains(",")?"":kind=="text"?"Text":kind=="integer"?"Integer":kind=="number"?"Number":kind=="boolean"?"Yes/No":kind=="length"?"Length":"";
                return new ParameterCsv.Row{IFCProperty=source,ParameterName=ParameterNames.Unique(existing?.Target??source.Split('|').Last().Trim(),existing==null?"IFC_":"",used),DataType=type};
            }).ToList();
        }
        void ExportSelectedParameterCsv()
        {
            try
            {
                var definitions=SelectedParameterDefinitions();
                if(definitions.Count==0){status.Text="Check or highlight IFC elements first.";return;}
                var file=new Microsoft.Win32.SaveFileDialog{Title="Export definitions from selected IFC elements",Filter="CSV (*.csv)|*.csv",FileName="IFC-selected-parameters.csv"};
                if(file.ShowDialog(Window.GetWindow(this))==true){File.WriteAllText(file.FileName,ParameterCsv.Write(definitions),new UTF8Encoding(true));status.Text="Exported "+definitions.Count+" IFC property definitions. Review names before importing.";}
            }
            catch(Exception ex){status.Text="Export failed: "+ex.Message;}
        }
        internal ParameterMappingPreset MappingPreset()=>new ParameterMappingPreset{Entries=mappings.Where(m=>m.Selected && !string.IsNullOrWhiteSpace(m.Source) && !string.IsNullOrWhiteSpace(m.Target))
            .Select(m=>new ParameterPresetEntry{Source=m.Source,Target=m.Target,DataType=(Catalog?.Invoke()??new List<ParameterOption>()).FirstOrDefault(p=>p.Name==m.Target)?.Id??m.ParameterType,Group=m.ParameterGroup,Binding=(Catalog?.Invoke()??new List<ParameterOption>()).FirstOrDefault(p=>p.Name==m.Target)?.Binding??m.ParameterBinding}).ToList()};
        void SaveMappingPreset()
        {
            try
            {
                grid.CommitEdit(DataGridEditingUnit.Cell,true);grid.CommitEdit(DataGridEditingUnit.Row,true);
                var preset=MappingPreset();
                if(preset.Entries.Count==0){status.Text="Configure at least one complete pair to save.";return;}
                string xml=ParameterMappingPreset.Write(preset);ParameterMappingPreset.Read(xml);
                var file=new Microsoft.Win32.SaveFileDialog{Title="Save reusable parameter mappings",Filter="Mapping preset (*.xml)|*.xml",FileName="IFC-parameter-mappings.xml"};
                if(file.ShowDialog(Window.GetWindow(this))==true){File.WriteAllText(file.FileName,xml,Encoding.Unicode);status.Text="Saved reusable mapping names and settings. Model element IDs and IFC values are not stored.";}
            }
            catch(Exception ex){status.Text="Save preset: "+ex.Message;}
        }
        internal void ApplyMappingPreset(ParameterMappingPreset preset)
        {
            // Validate the complete file before replacing any editable rows.
            preset=ParameterMappingPreset.Read(ParameterMappingPreset.Write(preset));
            updatingPair=true;
            try
            {
                mappings.Clear();
                foreach(var entry in preset.Entries)
                {
                    proposedTargets.Add(entry.Target);
                    mappings.Add(new IfcParameterMapping{Source=entry.Source,Target=entry.Target,ParameterType=entry.DataType,ParameterGroup=entry.Group,ParameterBinding=entry.Binding});
                }
            }
            finally {updatingPair=false;}
            Refresh();status.Text="Loaded "+preset.Entries.Count+" mappings. Missing IFC properties or incompatible targets need review. Use Create multiple from IFC to create missing targets; loading a preset does not change the model.";
        }
        void LoadMappingPreset()
        {
            try
            {
                var file=new Microsoft.Win32.OpenFileDialog{Title="Load reusable parameter mappings",Filter="Mapping preset (*.xml)|*.xml"};
                if(file.ShowDialog(Window.GetWindow(this))==true)ApplyMappingPreset(ParameterMappingPreset.Read(File.ReadAllText(file.FileName)));
            }
            catch(Exception ex){status.Text="Load preset: "+ex.Message;}
        }
        internal Window BuildValuePreview()
        {
            grid.CommitEdit(DataGridEditingUnit.Cell,true);grid.CommitEdit(DataGridEditingUnit.Row,true);
            var highlighted=SelectedSources?.Invoke()??new List<AirTerminalRow>();
            var sources=highlighted.Count>0?highlighted:Sources?.Invoke()??new List<AirTerminalRow>();
            var pairs=Snapshot(mappings.Where(m=>m.Selected && !string.IsNullOrWhiteSpace(m.Source) && !string.IsNullOrWhiteSpace(m.Target)));
            var values=Preview?.Invoke(sources,pairs)??new List<ParameterValuePreview>();
            var dialog=new Window{Title="Preview parameter values",Width=1150,Height=600,MaxHeight=SystemParameters.WorkArea.Height,MaxWidth=SystemParameters.WorkArea.Width,Background=UiDesign.Background,WindowStartupLocation=WindowStartupLocation.CenterOwner};
            if(Window.GetWindow(this)?.IsVisible==true)dialog.Owner=Window.GetWindow(this);
            var root=new DockPanel{Margin=new Thickness(20)};
            var note=new TextBlock{Text=values.Count+" values. Read-only preview; no parameters are written. Highlight rows in Elements to preview existing ducts. Length values require resolved IFC units. Type parameters affect every duct using that type.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};DockPanel.SetDock(note,Dock.Top);root.Children.Add(note);
            var close=IFCInfoWindow.Button("Close",false);close.Click+=(s,e)=>dialog.Close();DockPanel.SetDock(close,Dock.Bottom);root.Children.Add(close);
            var table=new DataGrid{Name="ParameterValuePreview",ItemsSource=values,IsReadOnly=true,AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false};
            foreach(var column in new[]{new[]{"Source ID","SourceId"},new[]{"Target ID","TargetId"},new[]{"IFC property","IFCProperty"},new[]{"Revit parameter","RevitParameter"},new[]{"Current value","CurrentValue"},new[]{"Proposed value","ProposedValue"},new[]{"Status","Status"}})
            {
                var style=new Style(typeof(TextBlock));style.Setters.Add(new Setter(TextBlock.TextWrappingProperty,TextWrapping.Wrap));style.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(4)));style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new Binding(column[1])));
                table.Columns.Add(new DataGridTextColumn{Header=column[0],Binding=new Binding(column[1]),ElementStyle=style,Width=column[1].EndsWith("Id")?new DataGridLength(80):new DataGridLength(column[1]=="Status" || column[1]=="IFCProperty"?2:1,DataGridLengthUnitType.Star)});
            }
            UiDesign.StyleTable(table);root.Children.Add(table);UiDesign.SetContent(dialog,root);return dialog;
        }
        void PreviewValues()
        {
            try
            {
                if(!mappings.Any(m=>!string.IsNullOrWhiteSpace(m.Source)&&!string.IsNullOrWhiteSpace(m.Target))){status.Text="Configure complete parameter pairs before previewing values.";return;}
                BuildValuePreview().ShowDialog();
            }
            catch(Exception ex){status.Text="Preview failed: "+ex.Message;}
        }
    }
}
