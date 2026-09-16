using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using IFCInfo;

internal static class Program
{
    private static int checks;

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "--test")
            {
                TestSelection(false);
                TestSelection(true);
                TestResultSelection();
                TestFamilyLoader();
                TestProperties();
                TestCheckedProperties();
                TestNativeCurves();
                Console.WriteLine("PASS: " + checks + " WPF regression checks.");
                return 0;
            }
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Usage: UiPreview.exe <output-directory> | --test");
                return 2;
            }
            string output = Path.GetFullPath(args[0]);
            Directory.CreateDirectory(output);
            foreach(string kind in new[] { "Pipe","CableTray" })
            {
                long category=kind=="Pipe"?(long)Autodesk.Revit.DB.BuiltInCategory.OST_PipeCurves:(long)Autodesk.Revit.DB.BuiltInCategory.OST_CableTray;
                var curveWindow=new CurveCreationWindow(new List<AirTerminalRow> { new AirTerminalRow { ElementId="123",Name=kind+" IFC",SystemType="Supply",Elevation="3200" } },
                    new List<ReplacementTypeOption> { new ReplacementTypeOption { Id=1,CategoryId=category,Kind=kind,Supported=true,Label=kind+" Type" } },
                    new List<ReplacementLevelOption> { new ReplacementLevelOption { Id=2,Label="Level 1" } },
                    new List<ReplacementTypeOption> { new ReplacementTypeOption { Id=3,Kind="Pipe",Label="Domestic Cold Water" } },category,kind);
                Render(curveWindow,output,kind+"-step3.png",900,690); curveWindow.Close();
            }
            var propertyPanel=new IfcPropertiesPanel();
            propertyPanel.ShowSource(new AirTerminalRow { ElementId="123",Name="Rectangular Duct",IfcGuid="ifc-source-guid",
                SystemType="Supply Air",SystemName="SA-01",Elevation="3200",DataSource="Khớp IFC GUID; IFC gốc",
                DuctSource=new IfcTerminalSource { WidthMm=500,HeightMm=250,LengthMm=2400 } });
            var propertyWindow=new Window { Content=propertyPanel };
            Render(propertyWindow,output,"ifc-properties.png",350,480); propertyWindow.Close();
            var items = new List<DuctPlanItem> { Item("123", false, "Supply Air", "Level 1") };
            var settings = Settings(items, false, Choices());
            Render(settings, output, "duct-settings.png", 900, 690);
            Descendants(settings).OfType<ScrollViewer>().First().ScrollToEnd();
            Render(settings, output, "duct-settings-options.png", 900, 690);
            Render(settings, output, "duct-settings-compact.png", 620, 440);
            settings.Close();

            var rows = new List<DuctRunRow> {
                new DuctRunRow { SourceId = "123", IfcGuid = "sample-guid", TargetId = "456", Status = "Đã tạo", Success = true }
            };
            var resultType = typeof(IFCInfoWindow).Assembly.GetType("IFCInfo.DuctCreationResultWindow", true);
            var result = (Window)Activator.CreateInstance(resultType, BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { rows, false, (Action)(() => { }), "Kết quả kiểm tra" }, null);
            Render(result, output, "duct-results.png", 1050, 600);
            result.Close();
            Console.WriteLine("Rendered IFC/Duct settings and result windows: " + output);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static List<DuctChoice> Choices() => new List<DuctChoice> {
        new DuctChoice { Id = 7, Name = "Supply Air" }
    };

    private static DuctPlanItem Item(string id, bool round, string system, string level) => new DuctPlanItem {
        Source = new AirTerminalRow { ElementId = id, IfcGuid = "guid-" + id, SystemType = system },
        Diameter = round ? 1 : 0, Width = round ? 0 : 1, Height = round ? 0 : 0.5, LevelKey = level
    };

    private static DuctCreationWindow Settings(List<DuctPlanItem> items, bool updating, List<DuctChoice> roundTypes) =>
        new DuctCreationWindow(items, new List<string>(), roundTypes, Choices(), Choices(), Choices(), Choices(),
            new DuctSettingsData(), updating);

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
    }

    private static void TestResultSelection()
    {
        var rows = new List<DuctRunRow> {
            new DuctRunRow { Success=true, TargetId="10" },
            new DuctRunRow { Success=true, TargetId="20" },
            new DuctRunRow { Success=true, TargetId="10, 30" },
            new DuctRunRow { Success=false, TargetId="40" },
            new DuctRunRow { Success=true, TargetId="invalid,-1" }
        };
        Assert(DuctRunRow.SuccessfulTargetIds(rows).SequenceEqual(new long[] {10,20,30}), "Select committed targets, including fitting; deduplicate and exclude failed targets");
        foreach (var row in rows) row.Success=false;
        Assert(DuctRunRow.SuccessfulTargetIds(rows).Count==0, "Rollback must leave no selected targets");
    }

    private static void TestFamilyLoader()
    {
        var category=(long)Autodesk.Revit.DB.BuiltInCategory.OST_DuctFitting;
        var window=new NativePlacementWindow(new List<AirTerminalRow> { new AirTerminalRow { ElementId="1" } },
            new List<ReplacementTypeOption>(),new List<ReplacementLevelOption>(),null,category,"Duct Fittings",
            (path,id)=>new List<ReplacementTypeOption>());
        var nodes=Descendants(window).OfType<FrameworkElement>().ToList();
        Assert(nodes.Single(n=>n.Name=="LoadPlacementFamily").Visibility==Visibility.Visible,"Missing fitting family must offer loading");
        Assert(!((Button)nodes.Single(n=>n.Name=="CreateNative")).IsEnabled,"Missing type must not be submitted");
        window.Close();
    }

    private static void TestNativeCurves()
    {
        foreach (var pair in new[] {
            Tuple.Create("Pipe",(long)Autodesk.Revit.DB.BuiltInCategory.OST_PipeCurves),
            Tuple.Create("Conduit",(long)Autodesk.Revit.DB.BuiltInCategory.OST_Conduit),
            Tuple.Create("CableTray",(long)Autodesk.Revit.DB.BuiltInCategory.OST_CableTray) })
        {
            var type=new ReplacementTypeOption { Id=10,CategoryId=(long)pair.Item2,CategoryName=pair.Item1,Kind=pair.Item1,Label="Test type",Supported=true };
            var systems=new List<ReplacementTypeOption> { new ReplacementTypeOption { Id=20,Kind="Pipe",Label="Water" } };
            var window=new NativePlacementWindow(new List<AirTerminalRow> { new AirTerminalRow { ElementId="1" } },
                new List<ReplacementTypeOption> { type },new List<ReplacementLevelOption> { new ReplacementLevelOption { Id=1,Label="L1" } },
                systems,(long)pair.Item2,pair.Item1);
            var nodes=Descendants(window).OfType<FrameworkElement>().ToList();
            Assert(((Button)nodes.Single(n=>n.Name=="CreateNative")).IsEnabled,"Valid native curve must be enabled: "+pair.Item1);
            Assert(((ComboBox)nodes.Single(n=>n.Name=="TargetSystem")).IsEnabled==(pair.Item1=="Pipe"),"Only Pipe requires piping system");
            window.Close();
        }
    }

    private static void TestProperties()
    {
        var panel=new IfcPropertiesPanel();
        var grid=Descendants(panel).OfType<DataGrid>().Single();
        var row=new AirTerminalRow { ElementId="123",Name="Duct",IfcGuid="guid",IsSelected=false };
        row.IfcProperties.Add(new IfcPropertyValue { Scope="Instance",SetName="Pset_Test",Name="Reference",Value="IFC value" });
        panel.ShowSource(row);
        Assert(grid.Items.Cast<KeyValuePair<string,string>>().Any(p=>p.Value=="IFC value"),"Properties must display selected source Pset");
        Assert(!row.IsSelected,"Inspecting properties must not select duct for creation");
        var other=new AirTerminalRow { ElementId="456",Name="Duct" };
        other.IfcProperties.Add(new IfcPropertyValue { Scope="Instance",SetName="Pset_Test",Name="Reference",Value="IFC value" });
        panel.ShowSources(new[] { row,other });
        var combined=grid.Items.Cast<KeyValuePair<string,string>>().ToDictionary(p=>p.Key,p=>p.Value);
        Assert(combined["Instance / Pset_Test / Reference"]=="IFC value","Equal parameters must display one value");
        Assert(combined["Element ID trong link"]=="<varies>","Different IDs must display varies");
        other.IfcProperties[0].Value="different";
        panel.ShowSources(new[] { row,other });
        Assert(grid.Items.Cast<KeyValuePair<string,string>>().Single(p=>p.Key=="Instance / Pset_Test / Reference").Value=="<varies>","Different parameter values must display varies");
        other.IfcProperties.Clear();
        panel.ShowSources(new[] { row,other });
        Assert(grid.Items.Cast<KeyValuePair<string,string>>().Single(p=>p.Key=="Instance / Pset_Test / Reference").Value=="<varies>","Missing parameter on one source must display varies");
        panel.ShowSource(row);
        Assert(grid.Items.Cast<KeyValuePair<string,string>>().Single(p=>p.Key=="Instance / Pset_Test / Reference").Value=="IFC value","Single selection must restore original value");
        panel.ShowSource(new AirTerminalRow { ElementId="456" });
        Assert(!grid.Items.Cast<KeyValuePair<string,string>>().Any(p=>p.Value=="IFC value"),"Changing row must clear previous IFC properties");
        panel.ShowSource(null);
        Assert(grid.Items.Count==0,"Clearing active row must clear properties");
    }

    private static void TestCheckedProperties()
    {
        var main=new IFCInfoWindow();
        var first=new AirTerminalRow { ElementId="1",SystemType="Supply",SystemName="A" };
        var second=new AirTerminalRow { ElementId="2",SystemType="Supply",SystemName="B" };
        main.AirTerminals=new List<AirTerminalRow> { first,second };
        main.AirTerminalCount=2;
        typeof(IFCInfoWindow).GetProperty("SelectedCategory").GetSetMethod(true).Invoke(main,new object[] {
            new CategoryOption { Id=(long)Autodesk.Revit.DB.BuiltInCategory.OST_DuctCurves,Name="Ducts" } });
        var page=(FrameworkElement)typeof(IFCInfoWindow).GetMethod("CountPage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,null);
        var grids=Descendants(page).OfType<DataGrid>().ToList();
        var sourceGrid=grids.Single(g=>g.Name!="IfcPropertyValues");
        var values=grids.Single(g=>g.Name=="IfcPropertyValues");
        Action flush=()=>page.Dispatcher.Invoke(new Action(()=>{}),DispatcherPriority.ContextIdle);
        Func<string,string> value=key=>values.Items.Cast<KeyValuePair<string,string>>().Single(p=>p.Key==key).Value;
        sourceGrid.SelectedItem=second;
        first.IsSelected=true; second.IsSelected=true; flush();
        Assert(value("System Name")=="<varies>","Two checked sources must override last clicked row");
        Assert(value("System Type")=="Supply","Checked sources must retain common values");
        second.IsSelected=false; flush();
        Assert(value("System Name")=="A","Unchecking must update properties immediately");
        first.IsSelected=false; flush();
        Assert(value("System Name")=="B","With no checks properties must follow highlighted row");
        sourceGrid.SelectedItems.Add(first); flush();
        Assert(value("System Name")=="<varies>","Multiple highlighted rows must still aggregate without checks");
        var total=Descendants(page).OfType<TextBlock>().Single(t=>t.Name=="IfcSelectionTotal");
        Assert(total.Text=="Total: 0 / 2","Totals count checked elements, not row focus");
        first.IsSelected=true; second.IsSelected=true; flush();
        Assert(total.Text=="Total: 2 / 2","Totals update for checked sources");
        var search=Descendants(page).OfType<TextBox>().Single(t=>t.Name=="IfcElementSearch");
        search.Text="2"; flush();
        Assert(sourceGrid.Items.Count==1 && sourceGrid.Items[0]==second,"Search finds linked element by ID");
        Assert(total.Text=="Total: 2 / 2" && first.IsSelected,"Search preserves hidden selections and totals");
        search.Text="does not exist"; flush();
        Assert(sourceGrid.Items.Count==0,"Unmatched search returns no rows");
        search.Text=""; flush();
        Assert(sourceGrid.Items.Count==2,"Clearing search restores rows");
        var propertyPanel=Descendants(page).OfType<IfcPropertiesPanel>().Single();
        Assert(propertyPanel.AllPropertyValues().Contains("System Name\t<varies>"),"Copy includes aggregated property values");
        page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        main.Close();
    }

    private static void TestSelection(bool updating)
    {
        var selected = Item("123", false, "Supply Air", "Level 1");
        var excludedRound = Item("124", true, "Unmapped", "Level 2");
        var excludedSystem = Item("125", false, "Other unmapped", "Level 3");
        var items = new List<DuctPlanItem> { selected, excludedRound, excludedSystem };
        var window = Settings(items, updating, new List<DuctChoice>());
        window.Opacity = 0;
        window.ShowInTaskbar = false;
        Exception failure = null;
        window.Loaded += (sender, args) => window.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                var nodes = Descendants(window).ToList();
                var grid = nodes.OfType<DataGrid>().Single();
                var submit = nodes.OfType<Button>().Single(b => (b.Content as string)?.EndsWith(" Duct") == true);
                grid.SelectedItem = selected;
                Assert(!grid.CanUserDeleteRows && !DataGrid.DeleteCommand.CanExecute(null, grid), "Delete must be disabled");
                DataGrid.DeleteCommand.Execute(null, grid);
                Assert(items.Count == 3, "Delete must preserve all source rows");
                submit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(window.Request == null, "Selected groups with missing mappings must be rejected");
                excludedRound.Include = false;
                excludedSystem.Include = false;
                Assert((string)submit.Content == (updating ? "Cập nhật 1 Duct" : "Tạo 1 Duct"), "Button count must track selection");
                selected.Include = false;
                Assert(!submit.IsEnabled, "No selection must disable submission");
                selected.Include = true;
                Assert(submit.IsEnabled, "Selecting a valid row must enable submission");
                submit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var request = window.Request;
                Assert(request != null && request.Items.Count == 1 && request.Items[0] == selected, "Excluded groups must not block valid rows");
                Assert(request.RoundTypeId == 0 && request.RectangularTypeId == 7, "Only selected shape needs a type");
                Assert(request.SystemTypes.Count == 1 && request.SystemTypes.ContainsKey("Rectangular|Supply Air"), "Exclude unused system mappings");
                Assert(request.Levels.Count == 1 && request.Levels.ContainsKey("Level 1"), "Exclude unused level mappings");
                Assert(request.Worksets.Count == 1 && request.Worksets.ContainsKey("Rectangular|Supply Air"), "Exclude unused workset mappings");
                Assert(request.Skipped.Count == 2 && request.Skipped.Select(r => r.SourceId).OrderBy(x => x).SequenceEqual(new[] { "124", "125" }), "All unchecked sources must be reported");
                Assert(request.Skipped.All(r => r.Status == "Không chọn" && !string.IsNullOrEmpty(r.IfcGuid)), "Skipped rows retain identity and status");
            }
            catch (Exception ex) { failure = ex; }
            finally { window.Close(); }
        }), DispatcherPriority.ApplicationIdle);
        window.ShowDialog();
        if (failure != null) throw failure;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var descendant in Descendants(child)) yield return descendant;
    }

    private static void Render(Window window, string output, string name, int width, int height)
    {
        var root = (FrameworkElement)window.Content;
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Render);
        root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(output, name))) encoder.Save(stream);
    }
}
