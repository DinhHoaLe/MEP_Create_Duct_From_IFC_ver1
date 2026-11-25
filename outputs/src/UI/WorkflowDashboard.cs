using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IFCInfo
{
    internal sealed class DashboardSelection
    {
        internal string Task = "Create from IFC";
        internal string Discipline = "MEP";
        internal string Kind = "Line";
        internal bool IsReview => Task == "Review / Compare";
        internal bool IsTransfer => Task == "Transfer parameters";
        internal bool IsUpdate => Task == "Update from IFC";
        internal bool IsAvailable => Discipline == "MEP" && (!(IsTransfer || IsUpdate) || Kind == "Line");
        internal string Summary => Task + " / " + Discipline + " / " + (Kind == "Family" ? "Family Instance" : Kind);
        internal DashboardSelection Copy() => new DashboardSelection { Task=Task, Discipline=Discipline, Kind=Kind };
        internal bool Includes(CategoryOption category) => IsAvailable && category.Kind == Kind &&
            (!(IsTransfer || IsUpdate) || SupportedCategories.IsLine(category.Id));
    }

    // Dashboard navigation only: model actions remain in the existing workflow/services.
    internal sealed class WorkflowDashboard : Window
    {
        readonly DashboardSelection choice;
        readonly List<Tuple<Button,string,string>> choices = new List<Tuple<Button,string,string>>();
        readonly TextBlock summary = IFCInfoWindow.Text("",16,"#37322B");
        readonly TextBlock scope = IFCInfoWindow.Text("",14,"#716B63");
        readonly Button open = IFCInfoWindow.Button("Open workspace →",true);
        internal DashboardSelection Selection { get; private set; }

        internal WorkflowDashboard(DashboardSelection initial)
        {
            choice=(initial ?? new DashboardSelection()).Copy();
            Title="IFC to Revit — Dashboard";
            Width=Math.Min(1180,SystemParameters.WorkArea.Width);
            Height=Math.Min(880,SystemParameters.WorkArea.Height);
            MinWidth=640;MinHeight=540;MaxWidth=SystemParameters.WorkArea.Width;MaxHeight=SystemParameters.WorkArea.Height;
            WindowStartupLocation=WindowStartupLocation.CenterOwner;
            FontFamily=new FontFamily("Segoe UI");FontSize=14;UseLayoutRounding=true;
            var root=new DockPanel();
            UiDesign.SetContent(this,root);
            var footer=new DockPanel { Margin=new Thickness(24,12,24,20),LastChildFill=true };
            DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            DockPanel.SetDock(open,Dock.Right);footer.Children.Add(open);
            var close=IFCInfoWindow.Button("Close",false);close.Click+=(s,e)=>Close();
            DockPanel.SetDock(close,Dock.Left);footer.Children.Add(close);
            var body=new StackPanel { Margin=new Thickness(28,24,28,8) };
            root.Children.Add(new ScrollViewer { Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled });
            var heading=IFCInfoWindow.Text("What would you like to do?",30,"#37322B");heading.FontWeight=FontWeights.Bold;body.Children.Add(heading);
            var subtitle=IFCInfoWindow.Text("Choose a task, discipline and element kind. The workspace uses the existing tool screens.",16,"#716B63");subtitle.Margin=new Thickness(0,8,0,20);body.Children.Add(subtitle);
            AddGroup(body,"01 · Select a task","Task",new[]{
                new[]{"Create from IFC","Create native elements using the current creation and placement screens."},
                new[]{"Update from IFC","Update duct geometry or mapped parameters on tracked MEP lines."},
                new[]{"Transfer parameters","Transfer mapped IFC values to tracked Ducts, Pipes, Cable Trays and Conduits."},
                new[]{"Review / Compare","Inspect IFC elements, properties, geometry and duct comparisons."}});
            AddGroup(body,"02 · Select a discipline","Discipline",new[]{
                new[]{"MEP","Current scope · supported MEP categories and Generic Models."},
                new[]{"Architecture","Planned · no architectural categories enabled in the current workflow."},
                new[]{"Structure","Planned · no structural categories enabled in the current workflow."}});
            AddGroup(body,"03 · Select an element kind","Kind",new[]{
                new[]{"Line","Ducts, pipes, cable trays and conduits."},
                new[]{"Family","Family Instance · fittings, accessories, equipment and other placed components."}});
            var note=new StackPanel { Margin=new Thickness(8,14,8,4) };note.Children.Add(summary);scope.Margin=new Thickness(0,6,0,0);note.Children.Add(scope);body.Children.Add(note);
            open.Name="OpenDashboardWorkspace";
            open.Click+=(s,e)=> { if(!choice.IsAvailable)return;Selection=choice.Copy();DialogResult=true; };
            Refresh();
        }

        void AddGroup(Panel parent,string title,string key,string[][] options)
        {
            var group=new StackPanel { Margin=new Thickness(18) };
            group.Children.Add(IFCInfoWindow.Text(title,18,"#37322B"));
            var wrap=new WrapPanel { Margin=new Thickness(0,10,0,0) };group.Children.Add(wrap);
            foreach(var option in options)
            {
                string value=option[0];
                var content=new StackPanel();
                var label=IFCInfoWindow.Text(value=="Family"?"Family Instance":value,15,"#37322B");label.FontWeight=FontWeights.SemiBold;
                content.Children.Add(label);
                var detail=IFCInfoWindow.Text(option[1],13,"#716B63");detail.Margin=new Thickness(0,8,0,0);content.Children.Add(detail);
                var button=IFCInfoWindow.Button("",false);button.Content=content;button.Width=238;button.MinHeight=106;
                button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.VerticalContentAlignment=VerticalAlignment.Top;
                button.Margin=new Thickness(0,0,10,10);button.ToolTip=option[1];
                System.Windows.Automation.AutomationProperties.SetName(button,value=="Family"?"Family Instance":value);
                choices.Add(Tuple.Create(button,key,value));
                button.Click+=(s,e)=> {if(key=="Task")choice.Task=value;else if(key=="Discipline")choice.Discipline=value;else choice.Kind=value;Refresh();};
                wrap.Children.Add(button);
            }
            parent.Children.Add(new Border { Child=group,Background=Brushes.White,BorderBrush=IFCInfoWindow.Brush("#DDD6CE"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(14),Margin=new Thickness(0,0,0,14) });
        }

        void Refresh()
        {
            foreach(var item in choices)
            {
                string current=item.Item2=="Task"?choice.Task:item.Item2=="Discipline"?choice.Discipline:choice.Kind;
                bool selected=current==item.Item3;
                item.Item1.Background=IFCInfoWindow.Brush(selected?"#E8E2DA":"#FFFFFF");
                item.Item1.BorderBrush=IFCInfoWindow.Brush(selected?"#6F6044":"#DDD6CE");
                item.Item1.BorderThickness=new Thickness(selected?2:1);
            }
            summary.Text=choice.Summary;
            scope.Text=choice.Discipline!="MEP"?"Planned: this discipline is not enabled in the current source code.":
                (choice.IsTransfer || choice.IsUpdate) && choice.Kind!="Line"?"Select Line to update Duct, Pipe, Cable Tray or Conduit parameters.":
                choice.IsUpdate?"Highlight sources in Elements. Ducts support geometry updates; Pipe, Cable Tray and Conduit update mapped parameters only.":
                choice.IsTransfer?"Highlight source rows in Elements, configure Parameters, then update tracked line parameter values without changing geometry.":
                choice.IsReview?"Read-only workspace: element inspection, exports and existing duct comparison. Model-changing screens are disabled.":
                "Source → Elements → Parameters → Preview & Create. Create and transfer mapped parameters for all four line categories.";
            open.IsEnabled=choice.IsAvailable;
        }
    }
}
