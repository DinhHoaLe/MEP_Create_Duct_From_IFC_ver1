using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
namespace IFCInfo
{
    internal sealed class DuctCreationResultWindow : Window
    {
        internal DuctCreationResultWindow(List<DuctRunRow> rows,bool rolledBack,Action select,string caption="IFC · Create / update report")
        {
            Title=caption; Width=1050; Height=750; MaxHeight=SystemParameters.WorkArea.Height;
            WindowStartupLocation=WindowStartupLocation.CenterOwner; Background=UiDesign.Background;
            FontFamily=new System.Windows.Media.FontFamily("Segoe UI"); FontSize=14;
            var root=new DockPanel { Margin=new Thickness(20),Background=Background }; UiDesign.SetContent(this, root);
            var summary=IFCInfoWindow.Text(rolledBack ? "The entire run was rolled back" : "Successful: "+rows.Count(r=>r.Success)+" operations · Failed/skipped: "+rows.Count(r=>!r.Success),22,"#37322B");
            DockPanel.SetDock(summary,Dock.Top); root.Children.Add(summary);
            var buttons=new WrapPanel { Margin=new Thickness(0,12,0,0) }; DockPanel.SetDock(buttons,Dock.Bottom); root.Children.Add(buttons);
            var feedback=IFCInfoWindow.Text("",13,"#9A5B12"); DockPanel.SetDock(feedback,Dock.Bottom); root.Children.Add(feedback);
            var export=IFCInfoWindow.Button("Export CSV",false); buttons.Children.Add(export);
            export.Click+=(s,e)=>
            {
                try
                {
                    var file=new SaveFileDialog { Filter="CSV (*.csv)|*.csv",FileName="IFC-run-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".csv" };
                    if (file.ShowDialog(this)==true) { DuctRunRow.Export(file.FileName,rows); feedback.Text="Report exported."; }
                }
                catch (Exception ex) { feedback.Text=ex.Message; }
            };
            var choose=IFCInfoWindow.Button("Select created elements and return",false); buttons.Children.Add(choose); choose.IsEnabled=!rolledBack && rows.Any(r=>r.Success);
            choose.Click+=(s,e)=> { select(); Close(); };
            var close=IFCInfoWindow.Button("Back to tool",true); buttons.Children.Add(close); close.Click+=(s,e)=>Close();
            var grid=new DataGrid { ItemsSource=rows,AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,Margin=new Thickness(0,14,0,0),MinRowHeight=34,ColumnHeaderHeight=36 };
            UiDesign.StyleTable(grid);
            var textStyle=new Style(typeof(TextBlock));
            textStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty,TextWrapping.Wrap));
            textStyle.Setters.Add(new Setter(TextBlock.MarginProperty,new Thickness(6)));
            foreach (var column in new[] { new[] {"IFC source","SourceId"},new[] {"IFC GUID","IfcGuid"},new[] {"Target ID","TargetId"},new[] {"Status","Status"},new[] {"Details","Reason"} })
                grid.Columns.Add(new DataGridTextColumn { Header=column[0],Binding=new System.Windows.Data.Binding(column[1]),ElementStyle=textStyle,
                    Width=column[1]=="Reason" ? new DataGridLength(1,DataGridLengthUnitType.Star) : new DataGridLength(column[1]=="IfcGuid" ? 200 : 130),MinWidth=90 });
            root.Children.Add(grid);
            if (rows.Count>0) grid.SelectedIndex=0;
        }
    }
}
