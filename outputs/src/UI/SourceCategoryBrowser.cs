using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace IFCInfo
{
    internal sealed class SourceCategoryBrowser : DockPanel
    {
        readonly TextBox search=new TextBox{Name="SourceCategorySearch",MinHeight=36,Padding=new Thickness(8),ToolTip="Filter source categories by name"};
        readonly TextBlock summary=new TextBlock{Margin=new Thickness(4,10,4,10),TextWrapping=TextWrapping.Wrap};
        readonly TabControl tabs=new TabControl{Name="SourceCategoryKinds"};
        readonly List<DataGrid> tables=new List<DataGrid>();
        readonly List<ListCollectionView> views=new List<ListCollectionView>();
        readonly List<CheckBox> selectHeaders=new List<CheckBox>();
        List<CategoryOption> categories=new List<CategoryOption>();
        bool changing;
        bool compactLayout;
        internal string KindFilter {get;set;}
        internal List<CategoryOption> Selected => categories.Where(c=>c.IsChecked).ToList();
        internal event Action SelectionChanged;
        internal SourceCategoryBrowser()
        {
            Margin=new Thickness(0,14,0,0);
            var searchHost=new Grid();searchHost.Children.Add(search);
            var hint=new TextBlock{Text="Search categories…",Margin=new Thickness(10,0,0,0),VerticalAlignment=VerticalAlignment.Center,Foreground=IFCInfoWindow.Brush("#716B63"),IsHitTestVisible=false};searchHost.Children.Add(hint);
            SetDock(searchHost,Dock.Top);Children.Add(searchHost);
            SetDock(summary,Dock.Top);Children.Add(summary);
            UiDesign.StyleWorkflowTabs(tabs);Children.Add(tabs);
            search.TextChanged+=(s,e)=> {hint.Visibility=search.Text.Length==0?Visibility.Visible:Visibility.Collapsed;Filter();};
        }
        internal void SetCategories(List<CategoryOption> values)
        {
            changing=true;
            try
            {
                foreach(var row in categories)row.PropertyChanged-=RowChanged;
                categories=values??new List<CategoryOption>();tables.Clear();views.Clear();selectHeaders.Clear();tabs.Items.Clear();search.Clear();
                foreach(string kind in new[]{"Line","Family"})
                {
                    if(KindFilter!=null && KindFilter!=kind)continue;
                    var rows=categories.Where(c=>(c.Kind=="Line"?"Line":"Family")==kind).ToList();
                    var view=new ListCollectionView(rows);view.SortDescriptions.Add(new SortDescription("Name",ListSortDirection.Ascending));views.Add(view);
                    var table=new DataGrid{Name=kind=="Line"?"LineSourceCategories":"FamilySourceCategories",ItemsSource=view,AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false,SelectionMode=DataGridSelectionMode.Single,SelectionUnit=DataGridSelectionUnit.FullRow,IsSynchronizedWithCurrentItem=false};
                    var check=new FrameworkElementFactory(typeof(CheckBox));check.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);check.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
                    check.SetBinding(CheckBox.IsCheckedProperty,new Binding("IsChecked"){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});
                    var selectAll=new CheckBox{Name=kind=="Line"?"CheckAllLineCategories":"CheckAllFamilyCategories",ToolTip="Check / clear all visible categories in this group",HorizontalAlignment=HorizontalAlignment.Center};selectHeaders.Add(selectAll);
                    selectAll.Click+=(s,e)=> {bool select=!view.Cast<CategoryOption>().All(c=>c.IsChecked);foreach(CategoryOption row in view)row.IsChecked=select;};
                    table.Columns.Add(new DataGridTemplateColumn{Header=selectAll,HeaderStyle=IFCInfoWindow.SelectionHeaderStyle(table.ColumnHeaderStyle),CellTemplate=new DataTemplate{VisualTree=check},Width=44});
                    var textStyle=new Style(typeof(TextBlock));textStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(10,8,10,8)));
                    table.Columns.Add(new DataGridTextColumn{Header="Category",Binding=new Binding("Name"),SortMemberPath="Name",IsReadOnly=true,Width=new DataGridLength(1,DataGridLengthUnitType.Star),ElementStyle=textStyle});
                    table.Columns.Add(new DataGridTextColumn{Header="Elements in link",Binding=new Binding("Count"),SortMemberPath="Count",IsReadOnly=true,Width=150,ElementStyle=textStyle});
                    table.SizeChanged+=(s,e)=>table.Columns[1].Width=new DataGridLength(Math.Max(120,table.ActualWidth-44-150-22));
                    UiDesign.StyleTable(table);tables.Add(table);
                    tabs.Items.Add(new TabItem{Header=(kind=="Line"?"Line categories":"Family categories")+" ("+rows.Count+")",Content=table});
                }
                tabs.SelectedIndex=KindFilter!=null?0:categories.Any(c=>c.Kind=="Line")?0:1;
                foreach(var row in categories)row.PropertyChanged+=RowChanged;
            }
            finally {changing=false;}
            Filter();SetCompact(compactLayout);SelectionChanged?.Invoke();
        }
        void RowChanged(object sender,PropertyChangedEventArgs e){UpdateSummary();SelectionChanged?.Invoke();}
        internal void SelectCategory(long id)
        {
            var value=categories.FirstOrDefault(c=>c.Id==id);if(value==null)return;
            search.Clear();tabs.SelectedIndex=KindFilter!=null?0:value.Kind=="Line"?0:1;value.IsChecked=true;
        }
        internal void SetCompact(bool compact)
        {
            compactLayout=compact;
            summary.Visibility=compact?Visibility.Collapsed:Visibility.Visible;
            Margin=new Thickness(0,compact?6:14,0,0);search.MinHeight=compact?30:36;
            foreach(TabItem tab in tabs.Items){tab.Padding=compact?new Thickness(12,6,12,6):new Thickness(22,12,22,12);tab.MinHeight=compact?32:44;}
        }
        void Filter()
        {
            if(changing)return;
            string query=search.Text.Trim();
            foreach(var view in views)view.Filter=item=>(((CategoryOption)item).Name??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0;
            UpdateSummary();
        }
        void UpdateSummary()
        {
            summary.Text=Selected.Count+" / "+categories.Count+" categories checked · "+Selected.Sum(c=>c.Count)+" elements. Checks are kept across filters and groups. Continue to review elements by category.";
            for(int i=0;i<selectHeaders.Count;i++)
            {
                var visible=views[i].Cast<CategoryOption>().ToList();int count=visible.Count(c=>c.IsChecked);
                selectHeaders[i].IsEnabled=visible.Count>0;selectHeaders[i].IsChecked=count==0?false:count==visible.Count?(bool?)true:null;
            }
        }
    }
}
