using System;
using Grid = System.Windows.Controls.Grid;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;

namespace IFCInfo
{
    internal static class ProjectSystemBrowser
    {
        internal sealed class Entry
        {
            public string Kind {get;set;}
            public string Name {get;set;}
            public string Id {get;set;}
            public List<Value> Properties {get;set;}
        }
        internal sealed class Value {public string Property {get;set;} public string Text {get;set;}}
        internal static List<Entry> Read(Document doc)
        {
            var elements=new FilteredElementCollector(doc).OfClass(typeof(MechanicalSystemType)).ToElements()
                .Concat(new FilteredElementCollector(doc).OfClass(typeof(MechanicalSystem)).ToElements());
            return elements.Select(el=> {
                var values=new List<Value> {
                    new Value {Property="Element ID",Text=el.Id.Number().ToString()},
                    new Value {Property="Name",Text=el.Name},
                    new Value {Property="Kind",Text=el is ElementType?"System type":"System instance"}
                };
                foreach(Parameter p in el.Parameters) {
                    string value;
                    try {value=p.AsValueString() ?? (p.StorageType==StorageType.String?p.AsString():p.StorageType==StorageType.Integer?p.AsInteger().ToString():p.StorageType==StorageType.Double?p.AsDouble().ToString():p.StorageType==StorageType.ElementId?p.AsElementId().Number().ToString():"");}
                    catch {value="(unavailable)";}
                    values.Add(new Value {Property=p.Definition.Name,Text=value??""});
                }
                return new Entry {Name=el.Name,Id=el.Id.Number().ToString(),Kind=el is ElementType?"Type":"Instance",Properties=values};
            }).OrderBy(x=>x.Kind).ThenBy(x=>x.Name).ToList();
        }
        internal static void Show(Window owner,List<Entry> entries)
        {
            var window=new Window {Title="Revit duct systems",Owner=owner,Width=1100,Height=650,MaxWidth=SystemParameters.WorkArea.Width,MaxHeight=SystemParameters.WorkArea.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=UiDesign.Background};
            var root=new DockPanel {Margin=new Thickness(20)};
            var close=IFCInfoWindow.Button("Close",false);close.HorizontalAlignment=HorizontalAlignment.Right;close.Click+=(s,e)=>window.Close();DockPanel.SetDock(close,Dock.Bottom);root.Children.Add(close);
            var heading=IFCInfoWindow.Text("All HVAC system types and system instances in the current Revit project",18,"#37322B");DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
            var grid=new Grid {Margin=new Thickness(0,14,0,14)};grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition());
            var list=new DataGrid {ItemsSource=entries,IsReadOnly=true,AutoGenerateColumns=false,CanUserAddRows=false,Margin=new Thickness(0,0,12,0)};
            foreach(var key in new[]{"Kind","Name","Id"})list.Columns.Add(new DataGridTextColumn {Header=key,Binding=new System.Windows.Data.Binding(key),Width=new DataGridLength(key=="Name"?3:1,DataGridLengthUnitType.Star)});
            var properties=new DataGrid {IsReadOnly=true,AutoGenerateColumns=false,CanUserAddRows=false};
            properties.Columns.Add(new DataGridTextColumn {Header="Property",Binding=new System.Windows.Data.Binding("Property"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
            properties.Columns.Add(new DataGridTextColumn {Header="Value",Binding=new System.Windows.Data.Binding("Text"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
            list.SelectionChanged+=(s,e)=>properties.ItemsSource=(list.SelectedItem as Entry)?.Properties;
            UiDesign.StyleTable(list);UiDesign.StyleTable(properties);grid.Children.Add(list);Grid.SetColumn(properties,1);grid.Children.Add(properties);root.Children.Add(grid);
            if(entries.Count>0)list.SelectedIndex=0;
            UiDesign.SetContent(window,root);window.ShowDialog();
        }
    }
}
