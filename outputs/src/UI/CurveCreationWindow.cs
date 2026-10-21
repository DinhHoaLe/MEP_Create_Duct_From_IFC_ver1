using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace IFCInfo
{
    public sealed class CurveCreationWindow : Window
    {
        public ReplacementRequest Request { get; private set; }
        public CurveCreationWindow(List<AirTerminalRow> rows,List<ReplacementTypeOption> types,
            List<ReplacementLevelOption> levels,List<ReplacementTypeOption> systems,long categoryId,string kind)
        {
            string label=kind=="Pipe"?"Pipe":kind=="Conduit"?"Conduit":"Cable Tray";
            Title="Step 3 · Create "+label+" from IFC"; Width=900; Height=690; MinWidth=620; MinHeight=440;
            MaxHeight=SystemParameters.WorkArea.Height; WindowStartupLocation=WindowStartupLocation.CenterOwner;
            Background=UiDesign.Background; FontFamily=new System.Windows.Media.FontFamily("Segoe UI"); FontSize=14;
            var root=new DockPanel { Margin=new Thickness(28),Background=UiDesign.Background }; UiDesign.SetContent(this, root);
            var footer=new StackPanel { Margin=new Thickness(0,14,0,0) }; DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
            var feedback=IFCInfoWindow.Text("",13,"#9A5B12"); footer.Children.Add(feedback);
            var buttons=new WrapPanel { HorizontalAlignment=HorizontalAlignment.Right }; footer.Children.Add(buttons);
            var back=IFCInfoWindow.Button("Back",false); back.IsCancel=true; back.Click+=(s,e)=>Close(); buttons.Children.Add(back);
            var create=IFCInfoWindow.Button("Create "+rows.Count+" "+label,true); create.Name="CreateCurve"; buttons.Children.Add(create);
            var header=new Grid { Margin=new Thickness(0,0,0,22) };
            header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            var titles=new StackPanel(); titles.Children.Add(IFCInfoWindow.Text("Creation settings for "+label,27,"#37322B"));
            titles.Children.Add(IFCInfoWindow.Text("Step 3 · Review and create runs",14,"#716B63")); header.Children.Add(titles);
            var steps=new ContentControl { Content=UiDesign.Steps(3) }; Grid.SetColumn(steps,1); header.Children.Add(steps);
            header.SizeChanged+=(s,e)=>steps.Visibility=header.ActualWidth<780?Visibility.Collapsed:Visibility.Visible;
            DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
            Width=1100; MinWidth=900;
            var options=new StackPanel(); var body=new StackPanel(); var groupsBody=body;
            root.Children.Add(UiDesign.SettingsColumns(options,body));
            body.Children.Add(new Border { Background=IFCInfoWindow.Brush("#E8E2DA"),CornerRadius=new CornerRadius(10),Padding=new Thickness(18,12,18,12),Margin=new Thickness(0,0,0,16),
                Child=IFCInfoWindow.Text("Selected: "+rows.Count+" segments · Geometry checked during creation",16,"#6F6044") });
            var settings=new StackPanel { Margin=new Thickness(16,12,16,14) };
            body.Children.Add(new Border { Child=settings,Background=System.Windows.Media.Brushes.White,CornerRadius=new CornerRadius(16),BorderBrush=IFCInfoWindow.Brush("#DDD6CE"),BorderThickness=new Thickness(1) });
            settings.Children.Add(IFCInfoWindow.Text(label+" · "+rows.Count+" segments",20,"#37322B"));
            var type=UiDesign.Field(settings,label+" Type","Type in the same category in the host model","Select type",false,true); type.Name="CurveType";
            type.ItemsSource=types.Where(t=>t.CategoryId==categoryId && t.Kind==kind && t.Supported).ToList(); if(type.Items.Count==1) type.SelectedIndex=0;
            var mappings=new Dictionary<string,ComboBox>();
            if(kind=="Pipe") foreach(string key in rows.Select(r=>r.SystemType??"").Distinct())
            {
                var box=UiDesign.Field(settings,"System Type: "+(key==""?"No information":key),"Map IFC to piping system types","Select system",false,true);
                box.ItemsSource=systems.Where(t=>t.Kind=="Pipe").ToList(); if(box.Items.Count==1) box.SelectedIndex=0; mappings[key]=box;
            }
            body=options;
            var level=UiDesign.Field(body,"Target level","Applies to checked segments; preserves the IFC centerline elevation","Select level",false,true);
            level.ItemsSource=levels; if(level.Items.Count>0) level.SelectedIndex=0;
            var policy=UiDesign.Field(body,"On error","Run behavior for "+label,"Choose run behavior",false,true);
            policy.ItemsSource=new[] { "Roll back the entire run","Skip failures and keep successful results" }; policy.SelectedIndex=0;
            body.Children.Add(IFCInfoWindow.Text(kind!="CableTray"?"Centerline and diameter come from IFC. Automatic fitting connections are not supported here.":"Centerline, width, height and profile orientation come from IFC. Automatic fitting connections are not supported here.",13,"#716B63"));
            var preview=new DataGrid { ItemsSource=rows,Name="CurvePreview",AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,CanUserDeleteRows=false,Height=190,Margin=new Thickness(0,16,0,8) };
            foreach(var column in new[] { new[] { "IFC source","ElementId" },new[] { "Element","Name" },new[] { "System Type","SystemType" },new[] { "Elevation (mm)","Elevation" } })
                preview.Columns.Add(new DataGridTextColumn { Header=column[0],Binding=new Binding(column[1]),Width=new DataGridLength(1,DataGridLengthUnitType.Star) });
            UiDesign.StyleTable(preview); groupsBody.Children.Add(preview);
            create.IsEnabled=rows.Count>0;
            create.Click+=(s,e)=>
            {
                if(!(type.SelectedItem is ReplacementTypeOption selectedType) || !(level.SelectedItem is ReplacementLevelOption selectedLevel) || mappings.Values.Any(b=>b.SelectedItem==null))
                { feedback.Text="Select a type, level and system type. If a type is missing, load it into the host model and reopen the tool."; return; }
                Request=new ReplacementRequest { Kind=kind,SourceCategoryId=categoryId,TypeId=selectedType.Id,LevelId=selectedLevel.Id,
                    KeepSuccessful=policy.SelectedIndex==1,SourceIds=rows.Select(r=>r.ElementId).Distinct().ToList(),
                    SystemTypes=mappings.ToDictionary(p=>p.Key,p=>((ReplacementTypeOption)p.Value.SelectedItem).Id) };
                if(Request.SystemTypes.Count>0) Request.SystemTypeId=Request.SystemTypes.Values.First();
                DialogResult=true;
            };
        }
    }
}
