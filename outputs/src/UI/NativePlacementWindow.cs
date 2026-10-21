using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IFCInfo
{
    public sealed class NativePlacementWindow : Window
    {
        public ReplacementRequest Request
        {
            get; private set;
        }
        public NativePlacementWindow(List<AirTerminalRow> rows,
            List<ReplacementTypeOption> types, List<ReplacementLevelOption> levels,
            List<ReplacementTypeOption> systems = null,long sourceCategoryId = 0,
            string sourceCategoryName = null, Func<string,long,List<ReplacementTypeOption>> loadFamily = null)
        {
            Title = "Place native elements from IFC";
            Width = 1100;
            Height = 700;
            MinWidth = 900;
            MinHeight = 460;
            MaxHeight = SystemParameters.WorkArea.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 14;
            Background = IFCInfoWindow.Brush("#F5F2EE");
            var root = new DockPanel { Background = Background };
            UiDesign.SetContent(this, root);
            var header = new Border { Background = IFCInfoWindow.Brush("#37322B"), Padding = new Thickness(24) };
            var head = new StackPanel();
            head.Children.Add(IFCInfoWindow.Text("STEP 3 · PLACE REVIT ELEMENTS", 11, "#B3A491"));
            var title = IFCInfoWindow.Text("Select a family / type in the IFC category", 24, "#FFFFFF");
            title.Margin = new Thickness(0, 8, 0, 0);
            head.Children.Add(title);
            header.Child = head;
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            var footer = new Border { Background = Brushes.White, Padding = new Thickness(24, 14, 24, 14) };
            var footerStack = new StackPanel();
            var status = IFCInfoWindow.Text("", 12, "#9A5B12");
            footerStack.Children.Add(status);
            var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = IFCInfoWindow.Button("Back", false);
            cancel.IsCancel = true;
            cancel.Click += (s, e) => Close();
            buttons.Children.Add(cancel);
            var create = IFCInfoWindow.Button("Place " + rows.Count + " elements from IFC", true); create.Name="CreateNative";
            buttons.Children.Add(create);
            footerStack.Children.Add(buttons);
            footer.Child = footerStack;
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);

            var options = new StackPanel { Margin = new Thickness(24) };
            var body = new StackPanel { Margin = new Thickness(24) };
            root.Children.Add(UiDesign.SettingsColumns(options, body));
            body.Children.Add(IFCInfoWindow.Text("Selected " + rows.Count + " elements from the IFC link", 18, "#37322B"));
            body.Children.Add(IFCInfoWindow.Text("The IFC remains a reference. Only checked source rows are created.", 13, "#716B63"));
            Label(body,"IFC CATEGORY · FIXED");
            var categories=new List<CategoryOption> { new CategoryOption { Id=sourceCategoryId,
                Name=sourceCategoryName??types.FirstOrDefault(t=>t.CategoryId==sourceCategoryId)?.CategoryName??"Source category unknown" } };
            var categoryBox=new ComboBox { Name="TargetCategory",ItemsSource=categories,MinHeight=36,IsEnabled=false }; body.Children.Add(categoryBox);
            Label(body,"FIND FAMILY / TYPE");
            var search=new TextBox { MinHeight=30 }; body.Children.Add(search);
            Label(body, "TARGET FAMILY / TYPE");
            var typeBox = new ComboBox
            {
                Name="TargetType",
                ItemsSource = types,
                MinHeight = 36,
                IsTextSearchEnabled = true,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            body.Children.Add(typeBox);
            var load = IFCInfoWindow.Button("Load Family (.rfa)…", false);
            load.Name="LoadPlacementFamily";
            load.Visibility=loadFamily==null ? Visibility.Collapsed : Visibility.Visible;
            body.Children.Add(load);
            var placement = IFCInfoWindow.Text("Select a type loaded in the host Revit model.", 12, "#716B63");
            placement.Margin = new Thickness(0, 8, 0, 0);
            body.Children.Add(placement);
            body = options;
            Label(body, "REFERENCE LEVEL IN HOST MODEL");
            var levelBox = new ComboBox { ItemsSource = levels, MinHeight = 36 };
            body.Children.Add(levelBox);
            if (levels.Count > 0)
                levelBox.SelectedIndex = 0;
            var topLabel=Label(body,"TOP LEVEL (TWO-LEVEL FAMILIES)");
            var topLevel=new ComboBox { ItemsSource=levels,MinHeight=36 }; body.Children.Add(topLevel);
            if (levels.Count>1) topLevel.SelectedIndex=1;
            var systemLabel=Label(body,"SYSTEM TYPE (DUCT / PIPE)");
            var systemBox=new ComboBox { Name="TargetSystem",MinHeight=36 }; body.Children.Add(systemBox);
            var lengthLabel=Label(body,"NEW RUN LENGTH (MM; 0 = KEEP SOURCE)");
            var length=new TextBox { Text="0",MinHeight=32,Width=160,HorizontalAlignment=HorizontalAlignment.Left }; body.Children.Add(length);
            var keep=new CheckBox { Content="Skip failed elements and keep successful results",IsChecked=false,Margin=new Thickness(0,15,0,8) }; body.Children.Add(keep);
            var angleLabel=Label(body, "ADDITIONAL FAMILY ROTATION (DEGREES)");
            var angle = new TextBox { Text = "0", Padding = new Thickness(10), Width = 150, HorizontalAlignment = HorizontalAlignment.Left };
            body.Children.Add(angle);
            var notes = IFCInfoWindow.Text("Point families use the source insertion point or bounding-box center. Additional rotation is around Z or the host normal. " +
                "Hosted families require a planar host face. Adaptive families require control points for each source. Line-based families can use two picked points if the source path cannot be read.\n\n" +
                "Straight ducts, pipes, conduits and cable trays use the IFC centerline and dimensions. A new length keeps the first endpoint fixed. Unsupported curved, hollow or complex geometry is reported without guessing dimensions. Duct fittings use a family in the selected category.\n\n" +
                "IFC Pset/Qto data, when available, is stored with placed elements and can be exported to CSV. Source properties are not automatically converted to Revit shared parameters.", 13, "#716B63");
            notes.Margin = new Thickness(0, 20, 0, 0);
            body.Children.Add(notes);
            Action validate = () =>
            {
                var type = typeBox.SelectedItem as ReplacementTypeOption;
                create.IsEnabled = sourceCategoryId != 0 && rows.Count > 0 && type != null && type.CategoryId==sourceCategoryId && type.Supported && levelBox.SelectedItem != null &&
                    ((type.Kind!="Duct" && type.Kind!="Pipe") || systemBox.SelectedItem!=null) &&
                    (type.PlacementMode!="TwoLevelsBased" || topLevel.SelectedItem!=null);
                placement.Text = type == null ? "Select a type loaded in the host model." : type.Placement;
                status.Text = typeBox.Items.Count == 0 ? "No family/type matches this category or search. Clear the search or load a family (.rfa)." :
                    levels.Count == 0 ? "The host model has no levels." :
                    type != null && !type.Supported ? type.Placement : type==null ? "Select a target family / type to continue." : "";
            };
            typeBox.SelectionChanged += (s, e) =>
            {
                var type=typeBox.SelectedItem as ReplacementTypeOption;
                systemBox.ItemsSource=(systems??new List<ReplacementTypeOption>()).Where(t=>t.Kind==type?.Kind).ToList();
                systemBox.IsEnabled=type?.Kind=="Duct" || type?.Kind=="Pipe";
                if (systemBox.Items.Count==1) systemBox.SelectedIndex=0;
                length.IsEnabled=type!=null && new[] { "Duct","Pipe","CableTray","Conduit" }.Contains(type.Kind);
                topLabel.Visibility=topLevel.Visibility=type?.PlacementMode=="TwoLevelsBased"?Visibility.Visible:Visibility.Collapsed;
                systemLabel.Visibility=systemBox.Visibility=systemBox.IsEnabled?Visibility.Visible:Visibility.Collapsed;
                lengthLabel.Visibility=length.Visibility=length.IsEnabled?Visibility.Visible:Visibility.Collapsed;
                angle.IsEnabled=type?.Kind=="Family" && type.PlacementMode!="Adaptive" && type.PlacementMode!="CurveBased" && type.PlacementMode!="CurveDrivenStructural";
                angleLabel.Visibility=angle.Visibility=angle.IsEnabled?Visibility.Visible:Visibility.Collapsed;
                validate();
            };
            Action filter=()=>
            {
                var category=categoryBox.SelectedItem as CategoryOption;
                var previous=typeBox.SelectedItem as ReplacementTypeOption;
                var matches=types.Where(t=>sourceCategoryId!=0 && t.CategoryId==sourceCategoryId && t.Label.IndexOf(search.Text,StringComparison.OrdinalIgnoreCase)>=0).ToList();
                typeBox.ItemsSource=matches;
                typeBox.SelectedItem=matches.FirstOrDefault(t=>t.Id==previous?.Id) ?? (matches.Count==1 ? matches[0] : null);
                validate();
            };
            load.Click += (s,e) =>
            {
                var picker=new Microsoft.Win32.OpenFileDialog { Title="Load a family into the host Revit model",Filter="Revit Family (*.rfa)|*.rfa",CheckFileExists=true };
                if (picker.ShowDialog(this)!=true) return;
                try
                {
                    types=loadFamily(picker.FileName,sourceCategoryId);
                    search.Text="";
                    filter();
                }
                catch (Exception ex) { status.Text="Could not load family: "+ex.Message; }
            };
            categoryBox.SelectionChanged+=(s,e)=>filter(); search.TextChanged+=(s,e)=>filter();
            systemBox.SelectionChanged+=(s,e)=>validate();
            levelBox.SelectionChanged += (s, e) => validate();
            topLevel.SelectionChanged+=(s,e)=>validate();
            angle.TextChanged += (s, e) => validate();
            topLabel.Visibility=topLevel.Visibility=systemLabel.Visibility=systemBox.Visibility=lengthLabel.Visibility=length.Visibility=angleLabel.Visibility=angle.Visibility=Visibility.Collapsed;
            angle.IsEnabled=length.IsEnabled=systemBox.IsEnabled=false;
            validate();
            categoryBox.SelectedItem=categories.FirstOrDefault(c=>c.Id==sourceCategoryId); filter();
            create.Click += (s, e) =>
            {
                double degrees=0;
                if (angle.IsEnabled && ((!double.TryParse(angle.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out degrees) &&
                     !double.TryParse(angle.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out degrees)) ||
                    double.IsNaN(degrees) || double.IsInfinity(degrees) || Math.Abs(degrees) > 360))
                {
                    status.Text = "Enter a rotation between -360 and 360 degrees.";
                    return;
                }
                var type = typeBox.SelectedItem as ReplacementTypeOption;
                var level = levelBox.SelectedItem as ReplacementLevelOption;
                if (sourceCategoryId==0 || type == null || type.CategoryId!=sourceCategoryId || !type.Supported || level == null)
                    return;
                double lengthMm=0;
                if (length.IsEnabled && (!double.TryParse(length.Text,NumberStyles.Float,CultureInfo.CurrentCulture,out lengthMm) || double.IsNaN(lengthMm) || double.IsInfinity(lengthMm) || lengthMm<0))
                { status.Text="Length must be a nonnegative number."; return; }
                Request = new ReplacementRequest
                {
                    TypeId = type.Id,
                    SourceCategoryId = sourceCategoryId,
                    LevelId = level.Id,
                    RotationDegrees = degrees,
                    Kind=type.Kind,SystemTypeId=(systemBox.SelectedItem as ReplacementTypeOption)?.Id??0,
                    TopLevelId=(topLevel.SelectedItem as ReplacementLevelOption)?.Id??0,LengthMm=lengthMm,KeepSuccessful=keep.IsChecked==true,
                    SourceIds = rows.Select(row => row.ElementId).Distinct().ToList()
                };
                DialogResult = true;
            };
        }
        private static TextBlock Label(StackPanel body, string label)
        {
            var text = IFCInfoWindow.Text(label, 11, "#716B63");
            text.Margin = new Thickness(0, 20, 0, 8);
            body.Children.Add(text);
            return text;
        }
    }
}
