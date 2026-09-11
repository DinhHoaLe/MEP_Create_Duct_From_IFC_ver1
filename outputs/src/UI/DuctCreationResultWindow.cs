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
        internal DuctCreationResultWindow(List<DuctRunRow> rows,bool rolledBack,Action select,string caption="IFC · Báo cáo lượt tạo / cập nhật")
        {
            Title=caption; Width=1050; Height=750; MaxHeight=SystemParameters.WorkArea.Height;
            WindowStartupLocation=WindowStartupLocation.CenterOwner; Background=UiDesign.Background;
            FontFamily=new System.Windows.Media.FontFamily("Segoe UI"); FontSize=14;
            var root=new DockPanel { Margin=new Thickness(20),Background=Background }; Content=root;
            var summary=IFCInfoWindow.Text(rolledBack ? "Đã hoàn tác toàn bộ lượt" : "Thành công: "+rows.Count(r=>r.Success)+" thao tác · Lỗi/bỏ qua: "+rows.Count(r=>!r.Success),22,"#102A50");
            DockPanel.SetDock(summary,Dock.Top); root.Children.Add(summary);
            var buttons=new WrapPanel { Margin=new Thickness(0,12,0,0) }; DockPanel.SetDock(buttons,Dock.Bottom); root.Children.Add(buttons);
            var feedback=IFCInfoWindow.Text("",13,"#9A5B12"); DockPanel.SetDock(feedback,Dock.Bottom); root.Children.Add(feedback);
            var export=IFCInfoWindow.Button("Xuất CSV",false); buttons.Children.Add(export);
            export.Click+=(s,e)=>
            {
                try
                {
                    var file=new SaveFileDialog { Filter="CSV (*.csv)|*.csv",FileName="IFC-run-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".csv" };
                    if (file.ShowDialog(this)==true) { DuctRunRow.Export(file.FileName,rows); feedback.Text="Đã xuất báo cáo."; }
                }
                catch (Exception ex) { feedback.Text=ex.Message; }
            };
            var choose=IFCInfoWindow.Button("Chọn toàn bộ phần tử của lượt và đóng",false); buttons.Children.Add(choose); choose.IsEnabled=!rolledBack && rows.Any(r=>r.Success);
            choose.Click+=(s,e)=> { select(); Close(); };
            var close=IFCInfoWindow.Button("Đóng",true); buttons.Children.Add(close); close.Click+=(s,e)=>Close();
            var psetPanel=new StackPanel { Margin=new Thickness(0,12,0,0) }; DockPanel.SetDock(psetPanel,Dock.Bottom); root.Children.Add(psetPanel);
            psetPanel.Children.Add(IFCInfoWindow.Text("IFC Pset / Qto của dòng đang chọn",16,"#102A50"));
            var psets=new TextBox { Text="Chọn một dòng để xem thuộc tính IFC nguồn.",IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,
                Height=160,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0,6,0,0) }; psetPanel.Children.Add(psets);
            var grid=new DataGrid { ItemsSource=rows,AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,Margin=new Thickness(0,14,0,0),MinRowHeight=34,ColumnHeaderHeight=36 };
            var textStyle=new Style(typeof(TextBlock));
            textStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty,TextWrapping.Wrap));
            textStyle.Setters.Add(new Setter(TextBlock.MarginProperty,new Thickness(6)));
            foreach (var column in new[] { new[] {"Nguồn IFC","SourceId"},new[] {"IFC GUID","IfcGuid"},new[] {"Element ID đích","TargetId"},new[] {"Trạng thái","Status"},new[] {"Chi tiết","Reason"} })
                grid.Columns.Add(new DataGridTextColumn { Header=column[0],Binding=new System.Windows.Data.Binding(column[1]),ElementStyle=textStyle,
                    Width=column[1]=="Reason" ? new DataGridLength(1,DataGridLengthUnitType.Star) : new DataGridLength(column[1]=="IfcGuid" ? 200 : 130),MinWidth=90 });
            root.Children.Add(grid);
            grid.SelectionChanged+=(s,e)=>psets.Text=(grid.SelectedItem as DuctRunRow)?.IfcPsets??"Không có IFC Pset trong dữ liệu của dòng này.";
            if (rows.Count>0) grid.SelectedIndex=0;
        }
    }
}
