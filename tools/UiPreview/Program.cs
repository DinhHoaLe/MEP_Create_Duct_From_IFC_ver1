using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using IFCInfo;

internal static class Program
{
    private static int checks;
    private static void TestParameterMapping()
    {
        var row=new AirTerminalRow {IsSelected=true,IfcProperties=new List<IfcPropertyValue> {
            new IfcPropertyValue {Scope="Instance",SetName="A",Name="Code",Value="01"},
            new IfcPropertyValue {Scope="Instance",SetName="B",Name="Code",Value="02"},
            new IfcPropertyValue {Name="Length",Value="125",Unit="mm"}}};
        Assert(IfcParameterMapping.Read(row,IfcParameterMapping.Key(row.IfcProperties[0]))=="01","Qualified source must distinguish sets");
        Assert(IfcParameterMapping.Read(row,"Length")=="125 mm","Preserve IFC units");
        Assert(IfcParameterMapping.Read(row,"Missing")==null,"Missing source must not clear target");
        bool ambiguous=false;try {IfcParameterMapping.Read(row,"Code");}catch(InvalidOperationException){ambiguous=true;}
        Assert(ambiguous,"Ambiguous source names must fail");
        var window=new IFCInfoWindow {AirTerminals=new List<AirTerminalRow>{row},CheckParameterTarget=n=>"Missing"};
        var tabs=Descendants(window).OfType<TabControl>().Single(t=>t.Name=="WorkflowTabs");
        var panel=tabs.Items.Cast<TabItem>().Single(t=>Equals(t.Header,"Parameters")).Content;
        var mappings=(System.Collections.IList)panel.GetType().GetField("mappings",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(panel);
        var validate=panel.GetType().GetMethod("Validate",BindingFlags.NonPublic|BindingFlags.Instance);
        var mapping=new IfcParameterMapping {Source="Length",Target="IFC Length"};mappings.Add(mapping);
        var request=new DuctRequest {Items=new List<DuctPlanItem>{new DuctPlanItem {Source=row}}};
        Assert(!(bool)validate.Invoke(panel,new object[]{request}),"Missing target requires create choice");
        mapping.CreateMissing=true;mapping.IsMapped=true;
        Assert((bool)validate.Invoke(panel,new object[]{request})&&request.ParameterMappings.Count==1,"Create choice enters request");
        mappings.Add(new IfcParameterMapping {Source="Length",Target="IFC Length",CreateMissing=true,IsMapped=true});
        Assert(!(bool)validate.Invoke(panel,new object[]{request}),"Duplicate targets block creation");
        mappings.RemoveAt(mappings.Count-1);
        mapping.Source="Code";
        Assert(!mapping.IsMapped,"Changing a choice must require mapping again");
        Assert(!(bool)validate.Invoke(panel,new object[]{request}),"Unconfirmed rows must not silently transfer");
        row.IsSelected=false;
        panel.GetType().GetMethod("Refresh",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(panel,null);
        var choices=(System.Collections.IList)panel.GetType().GetField("sourceChoices",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(panel);
        Assert(choices.Count==3,"Catalog must load without selecting ducts for creation");
        var grid=(DataGrid)panel.GetType().GetField("grid",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(panel);
        Assert(grid.Columns.Count==3 && grid.Columns.All(c=>c is DataGridTemplateColumn),"Parameter table must have two choice columns and a mapping button");
        mappings.Clear();row.DuctExistence="Already exists";
        window.CheckParameterTarget=n=>"Ready";
        var existingMapping=new IfcParameterMapping {Source="Length",Target="IFC Length"};mappings.Add(existingMapping);
        panel.GetType().GetMethod("Map",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(panel,new object[]{existingMapping});
        Assert(existingMapping.IsMapped,"Mapping button must confirm a valid existing parameter");
        bool transferred=false;
        window.TransferParameters=(rows,maps)=> {transferred=rows.Count==1&&rows[0]==row&&maps.Count==1&&maps[0].Target=="IFC Length";return "Transferred";};
        typeof(IFCInfoWindow).GetField("parameterSelection",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(window,new List<AirTerminalRow>{row});
        panel.GetType().GetMethod("TransferExisting",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(panel,null);
        Assert(transferred&&!row.CanSelect&&!row.IsSelected,"Existing ducts must transfer independently of creation checkboxes");
        transferred=false;existingMapping.Target="Changed";
        panel.GetType().GetMethod("TransferExisting",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(panel,null);
        Assert(!transferred,"Changing target must block transfer until mapped again");
        window.Close();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnableWindow(IntPtr window, [MarshalAs(UnmanagedType.Bool)] bool enable);

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "--test")
            {
                TestParameterMapping(); TestSelection(false);
                TestSelection(true);
                TestResultSelection();
                TestFamilyLoader();
                TestProperties();
                TestCheckedProperties();
                TestExports();
                TestMainReviewLayout();
                TestIndependentMinimize();
                TestNativeCurves();
                TestDuctGroups();
                TestLoading();
                TestResumeSession();
                TestEmbeddedSettings();
                TestPersistentCreationAndHeaders();
                TestIfcClassification();
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
            var tabPreview=new IFCInfoWindow();
            var tabItems=new List<DuctPlanItem> {Item("304421",false,"LU - ABLUFT","Level 1"),Item("305185",false,"LU - ABLUFT","Level 1")};
            foreach(var item in tabItems) item.Source.IsSelected=true;
            tabPreview.AirTerminals=tabItems.Select(i=>i.Source).ToList();
            var previewTabs=Descendants(tabPreview).OfType<TabControl>().Single(t=>t.Name=="WorkflowTabs");
            ((TabItem)previewTabs.Items[2]).IsEnabled=true;
            var tabEditor=Settings(tabItems,false,Choices());
            tabPreview.OpenDuctSettings(tabEditor,r=>{});
            Render(tabPreview,output,"duct-settings-tabs.png",1500,850);
            tabPreview.AirTerminals[0].IfcProperties=new List<IfcPropertyValue> {
                new IfcPropertyValue {Scope="Instance",SetName="Pset_DuctSegmentCommon",Name="Reference",Value="D-001"},
                new IfcPropertyValue {Scope="Instance",SetName="Dimensions",Name="Width",Value="500",Unit="mm"}};
            tabPreview.LoadParameterTargets=()=>new List<string>{"IFC Reference","IFC Width"};
            tabPreview.CheckParameterTarget=n=>"Ready";
            ((TabItem)previewTabs.Items[3]).IsSelected=true;
            var parameterBody=((TabItem)previewTabs.Items[3]).Content;
            var previewMappings=(System.Collections.IList)parameterBody.GetType().GetField("mappings",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(parameterBody);
            previewMappings.Clear();
            previewMappings.Add(new IfcParameterMapping {Source=IfcParameterMapping.Key(tabPreview.AirTerminals[0].IfcProperties[0]),Target="IFC Reference",IsMapped=true});
            previewMappings.Add(new IfcParameterMapping {Source=IfcParameterMapping.Key(tabPreview.AirTerminals[0].IfcProperties[1]),Target="IFC Width"});
            Render(tabPreview,output,"parameters.png",1500,850);
            tabPreview.Close();tabEditor.Close();
            var sourceWindow = new IFCInfoWindow();
            Render(sourceWindow, output, "ifc-source.png", 1100, 790);
            sourceWindow.Close();
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
                SystemType="Supply Air",SystemName="SA-01",Elevation="3200",DataSource="IFC GUID matched; original IFC",
                DuctSource=new IfcTerminalSource { WidthMm=500,HeightMm=250,LengthMm=2400 } });
            var propertyWindow=new Window { Content=propertyPanel };
            Render(propertyWindow,output,"ifc-properties.png",350,480); propertyWindow.Close();
            var reviewMain=new IFCInfoWindow {
                AirTerminalCount=2,CanCreateDucts=true,
                AirTerminals=new List<AirTerminalRow> {
                    new AirTerminalRow { ElementId="123",Name="Rectangular Duct",IfcGuid="ifc-source-guid",
                        SystemType="Supply Air",SystemName="SA-01",Elevation="3200",
                        DuctSource=new IfcTerminalSource { WidthMm=500,HeightMm=250,LengthMm=2400 } },
                    new AirTerminalRow { ElementId="124",Name="Round Duct",IfcGuid="ifc-round-guid",
                        SystemType="Return Air",SystemName="RA-01",Elevation="3300",
                        DuctSource=new IfcTerminalSource { DiameterMm=300,LengthMm=1800 } }
                }
            };
            var reviewPage=(FrameworkElement)typeof(IFCInfoWindow).GetMethod("CountPage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(reviewMain,null);
            var reviewViewer=Descendants(reviewPage).OfType<IfcObjectViewer>().Single();
            reviewViewer.ShowModel(PreviewBox());
            Descendants(reviewPage).OfType<Button>().Single(b=>(b.ToolTip as string)=="Zoom to IFC source").Visibility=Visibility.Visible;
            var reviewWindow=new Window { Content=reviewPage };
            Render(reviewWindow,output,"ifc-review-viewer.png",1450,620);
            reviewViewer.ShowMultipleSelection(2);
            Render(reviewWindow,output,"ifc-review-multiple.png",1450,620);
            reviewWindow.Close(); reviewMain.Close();
            var items = new List<DuctPlanItem> { Item("123", false, "Supply Air", "Level 1") };
            var settings = Settings(items, false, Choices());
            Render(settings, output, "duct-settings.png", 1200, 760);
            Descendants(settings).OfType<ScrollViewer>().First().ScrollToEnd();
            Render(settings, output, "duct-settings-options.png", 1200, 760);
            Render(settings, output, "duct-settings-compact.png", 900, 540);
            settings.Close();
            var square=Item("124",false,"Supply Air","Level 2"); square.Height=square.Width;
            var oval=Item("126",false,"Exhaust IFC","Level 3"); oval.Oval=true;
            var batch=new DuctCreationWindow(new List<DuctPlanItem> { items[0],square,Item("125",true,"Supply Air","Level 1"),oval },
                new List<string>(),Choices(),Choices(),Choices(),Choices(),Choices(),new DuctSettingsData(),false,Choices());
            Render(batch,output,"duct-batch-groups.png",1200,760);
            var ovalGroup=Descendants(batch).OfType<Expander>().Single(x=>x.Header.ToString().Contains("Oval"));
            ovalGroup.IsExpanded=true; batch.UpdateLayout();
            Descendants(ovalGroup).OfType<CheckBox>().Single(x=>x.Name=="CreateNewSystem").IsChecked=true;
            batch.UpdateLayout();
            Descendants(ovalGroup).OfType<ComboBox>().Single(x=>x.Name=="SystemClassification").SelectedItem="ExhaustAir";
            Render(batch,output,"duct-new-system.png",1200,900); batch.Close();

            var rows = new List<DuctRunRow> {
                new DuctRunRow { SourceId = "123", IfcGuid = "sample-guid", TargetId = "456", Status = "Created", Success = true }
            };
            var resultType = typeof(IFCInfoWindow).Assembly.GetType("IFCInfo.DuctCreationResultWindow", true);
            var result = (Window)Activator.CreateInstance(resultType, BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { rows, false, (Action)(() => { }), "Test results" }, null);
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

    private static IfcPreviewMesh PreviewBox()
    {
        var model=new IfcPreviewMesh { ElementId=123,Name="Rectangular Duct" };
        foreach(double x in new[] {-4.0,4.0})
        foreach(double y in new[] {-1.0,1.0})
        foreach(double z in new[] {-0.6,0.6})
        {
            model.Points.Add(new Point3D(x,y,z));
            var normal=new Vector3D(x,y,z); normal.Normalize(); model.Normals.Add(normal);
        }
        model.Indices.AddRange(new[] {
            0,2,3,0,3,1,4,5,7,4,7,6,0,1,5,0,5,4,
            2,6,7,2,7,3,0,4,6,0,6,2,1,3,7,1,7,5 });
        return model;
    }

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

    private static void TestResumeSession()
    {
        var link = new LinkOption { Id=101, Name="Project3.ifc", IsLoaded=true, IfcPath="remembered-source.ifc" };
        long categoryId = (long)Autodesk.Revit.DB.BuiltInCategory.OST_DuctCurves;
        int loads = 0;
        for (int run=0; run<2; run++)
        {
            int currentRun=run;
            var main = new IFCInfoWindow
            {
                Links=new List<LinkOption> { link }, InitialLinkId=101, InitialCategoryId=categoryId,
                Opacity=0, ShowInTaskbar=false
            };
            main.LoadLink=option =>
            {
                Assert(ReferenceEquals(option,link),"Resume must retain the link option and remembered IFC path");
                main.Categories=new List<CategoryOption> { new CategoryOption { Id=categoryId,Name="Ducts" } };
                main.CanCreateDucts=true;
                main.LoadCategory=category =>
                {
                    loads++;
                    main.AirTerminals=new List<AirTerminalRow> {
                        new AirTerminalRow { ElementId="1",Name="First duct",DuctExistence=currentRun==0?"Not created":"Exact match" },
                        new AirTerminalRow { ElementId="2",Name="Next duct",DuctExistence="Not created" }
                    };
                    main.AirTerminalCount=2;
                };
                main.PrepareDucts=rows => new DuctRequest { Items=rows.Select(row=>new DuctPlanItem {Source=row}).ToList() };
            };
            main.Loaded+=(s,e)=>main.Dispatcher.BeginInvoke(new Action(()=>
            {
                Assert(main.SelectedLink==link && main.SelectedCategory.Id==categoryId,"Resume must restore the selected link/category");
                Assert(main.Icon!=null && main.Icon.Width>0,"The embedded project icon must load without a loose PNG");
                Assert(main.DuctCreationRequest==null && main.Replacement==null,"Resume must not reuse a previous creation request");
                Assert(main.AirTerminals[0].CanSelect==(currentRun==0),"Refreshed fully covered ducts must not be selectable again");
                Assert(main.AirTerminals[1].CanSelect,"Another source must remain available for the next run");
                main.AirTerminals[currentRun].IsSelected=true;
                Descendants(main).OfType<Button>().Single(b=>(b.Content as string)=="Duct settings →")
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }));
            main.ShowDialog();
            Assert(main.DuctCreationRequest.Items.Count==1 && main.DuctCreationRequest.Items[0].Source.ElementId==(currentRun+1).ToString(),
                "Each run must submit only its newly checked source");
        }
        Assert(loads==2,"Resuming must reload source comparison data for each run");
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
            var curve=new CurveCreationWindow(new List<AirTerminalRow> {new AirTerminalRow {ElementId="1",SystemType="Water"}},
                new List<ReplacementTypeOption> {type},new List<ReplacementLevelOption> {new ReplacementLevelOption {Id=1,Label="L1"}},
                systems,pair.Item2,pair.Item1) {Opacity=0,ShowInTaskbar=false};
            var curveNodes=Descendants(curve).OfType<FrameworkElement>().ToList();
            Assert(curveNodes.OfType<ComboBox>().All(c=>c.MinHeight==36),"Curve settings must use compact fields");
            Assert(curveNodes.OfType<ComboBox>().Count()==(pair.Item1=="Pipe"?4:3),"Only Pipe has a system mapping in curve step 3");
            curve.Loaded+=(s,e)=>curve.Dispatcher.BeginInvoke(new Action(()=>
                curveNodes.OfType<Button>().Single(b=>b.Name=="CreateCurve").RaiseEvent(new RoutedEventArgs(Button.ClickEvent))));
            curve.ShowDialog();
            Assert(curve.Request!=null && curve.Request.Kind==pair.Item1 && curve.Request.SourceCategoryId==pair.Item2,"Curve step 3 must preserve kind and category");
            Assert(curve.Request.SystemTypeId==(pair.Item1=="Pipe"?20:0),"Only Pipe must submit a system type");
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
        Assert(combined["Element ID in link"]=="<varies>","Different IDs must display varies");
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

    private static void TestMainReviewLayout()
    {
        var main=new IFCInfoWindow();
        Assert(main.ShowInTaskbar,"The tool needs its own taskbar entry so it can minimize independently from Revit");
        var row=new AirTerminalRow { ElementId="123",Name="Duct",SystemType="Supply",SystemName="SA",Elevation="3200" };
        var other=new AirTerminalRow { ElementId="124",Name="Duct 2",SystemType="Return",SystemName="RA",Elevation="3300" };
        main.AirTerminals=new List<AirTerminalRow> { row,other };
        main.AirTerminalCount=2;
        main.CanCreateDucts=true;
        int navigations=0;
        main.NavigateSource=(selected,action)=> { if(selected==row && action=="Zoom to IFC source") navigations++; };
        var page=(FrameworkElement)typeof(IFCInfoWindow).GetMethod("CountPage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,null);
        var grids=Descendants(page).OfType<DataGrid>().ToList();
        var source=grids.Single(g=>g.Name!="IfcPropertyValues");
        var headers=source.Columns.Select(c=>c.Header as string).ToList();
        Assert(source.Columns.Single(c=>(c.Header as string)=="Duct comparison").MinWidth>=150,"Comparison status must not collapse into vertical text");
        Assert(headers.Contains("System Type") && headers.Contains("System Name") && headers.Contains("Shape"),
            "Step 2 must show source systems and section shape");
        source.SelectedItem=row;
        var buttons=Descendants(main).OfType<Button>().Concat(Descendants(page).OfType<Button>()).Distinct().ToList();
        Assert(!buttons.Any(b=>(b.ToolTip as string)=="Select all" || (b.ToolTip as string)=="Clear selection"),"Header checkbox replaces redundant selection buttons");
        var zoom=buttons.Single(b=>(b.ToolTip as string)=="Zoom to IFC source");
        Assert(zoom.IsEnabled,"Selecting a source row must enable IFC zoom review");
        zoom.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(navigations==1 && main.NavigationRow==null,"IFC zoom callback must keep the tool open");
        Assert(!buttons.Any(b=>(b.Content as string)=="Close"),"Main window must rely on the title-bar close button");
        var back=buttons.Single(b=>(b.Content as string)=="← Back");
        Assert(back.Parent is WrapPanel && Grid.GetColumn((WrapPanel)back.Parent)==0,"Back button must stay in the left footer");
        var propertyGrid=grids.Single(g=>g.Name=="IfcPropertyValues");
        Assert(propertyGrid.Items.GroupDescriptions.Count>0,"IFC properties must be grouped like Revit Properties");
        var viewer=Descendants(page).OfType<IfcObjectViewer>().Single();
        Assert(zoom.Parent is StackPanel && Descendants(viewer).Contains(zoom),"IFC zoom icon must sit in the viewer header");
        Assert(double.IsNaN(viewer.Height) && viewer.VerticalAlignment==VerticalAlignment.Stretch,
            "Viewer height must stretch with the table and Properties");
        source.SelectedItems.Add(other);
        Assert(!zoom.IsEnabled && viewer.StatusText.Contains("Multiple objects"),"Multiple selections must clear the viewer and disable IFC zoom");
        Assert(Descendants(viewer).OfType<System.Windows.Shapes.Path>().Any(p=>p.Width==72 && p.Visibility==Visibility.Visible),
            "Multiple selections must show a centered viewer symbol instead of an empty panel");
        var filter=buttons.Single(b=>b.Name=="FilterSources");
        filter.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var filterHeaders=filter.ContextMenu.Items.OfType<MenuItem>().Select(i=>i.Header as string).ToList();
        Assert(filterHeaders.Contains("Duct comparison") && !filterHeaders.Contains("System Type"),
            "Filter menu must follow visible table columns");
        filter.ContextMenu.IsOpen=false;
        var sort=buttons.Single(b=>b.Name=="SortSources");
        sort.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var sortHeaders=sort.ContextMenu.Items.OfType<MenuItem>().Select(i=>i.Header as string).ToList();
        Assert(sortHeaders.Any(h=>h.StartsWith("Select")) && sortHeaders.Any(h=>h.StartsWith("Duct comparison")) &&
            sortHeaders.Any(h=>h.StartsWith("Element ID")) && sortHeaders.Any(h=>h.StartsWith("Element")) &&
            !sortHeaders.Any(h=>h.StartsWith("System")),"Sort menu must follow visible table columns");
        sort.ContextMenu.IsOpen=false;
        row.ElementId="10"; other.ElementId="2";
        sort.ContextMenu.Items.OfType<MenuItem>().Single(i=>(string)i.Header=="Element ID ↑").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Assert(source.Items[0]==other,"IDs must sort numerically, with 2 before 10");
        row.IsSelected=true; other.IsSelected=true;
        filter.ContextMenu.Items.OfType<MenuItem>().Single(i=>(string)i.Header=="Checked").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        row.IsSelected=false;
        page.Dispatcher.Invoke(new Action(()=>{}),DispatcherPriority.ContextIdle);
        Assert(source.Items.Count==1 && source.Items[0]==other,"Selected filter must refresh after unchecking a row");
        main.Close();
    }

    private static void TestIndependentMinimize()
    {
        var owner=new Window { Width=200,Height=120,Opacity=0,ShowInTaskbar=false };
        owner.Show();
        var ownerHandle=new WindowInteropHelper(owner).Handle;
        var tool=new IFCInfoWindow { Width=640,Height=540,Opacity=0 };
        new WindowInteropHelper(tool).Owner=ownerHandle;
        tool.Show();
        var toolHandle=new WindowInteropHelper(tool).Handle;
        EnableWindow(ownerHandle,false); // Simulate the native lock applied by ShowDialog.
        SendMessage(toolHandle,0x0112,new IntPtr(0xF020),IntPtr.Zero);
        tool.Dispatcher.Invoke(new Action(()=>{}),DispatcherPriority.ApplicationIdle);
        Assert(tool.WindowState==WindowState.Minimized,"The minimize command must minimize only the tool");
        Assert(IsWindowEnabled(ownerHandle),"Minimizing the tool must unlock its Revit owner for interaction");
        tool.WindowState=WindowState.Normal;
        tool.Dispatcher.Invoke(new Action(()=>{}),DispatcherPriority.ApplicationIdle);
        Assert(!IsWindowEnabled(ownerHandle),"Restoring the tool must restore modal owner behavior");
        EnableWindow(ownerHandle,true);
        tool.Close(); owner.Close();
    }

    private static void TestPersistentCreationAndHeaders()
    {
        var main=new IFCInfoWindow {Opacity=0,ShowInTaskbar=false};
        var a=new AirTerminalRow {ElementId="1"};
        var b=new AirTerminalRow {ElementId="2"};
        var blocked=new AirTerminalRow {ElementId="3",DuctExistence="Exact match"};
        main.AirTerminals=new List<AirTerminalRow> {a,b};
        var page=(FrameworkElement)typeof(IFCInfoWindow).GetMethod("CountPage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,null);
        var grid=Descendants(page).OfType<DataGrid>().Single(g=>g.Name!="IfcPropertyValues");
        var header=(CheckBox)grid.Columns[0].Header;
        header.IsChecked=true;header.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(a.IsSelected && b.IsSelected,"Source header checks all eligible rows");
        header.IsChecked=false;header.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(!a.IsSelected && !b.IsSelected,"Source header clears all eligible rows");
        var items=new List<DuctPlanItem> {Item("1",false,"Supply Air","Level 1"),Item("2",false,"Supply Air","Level 1")};
        var editor=Settings(items,false,Choices());
        var preview=Descendants(editor).OfType<DataGrid>().Single();
        var include=(CheckBox)preview.Columns[0].Header;
        include.IsChecked=false;include.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(items.All(i=>!i.Include),"Include header excludes every source");
        include.IsChecked=true;include.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(items.All(i=>i.Include),"Include header includes every source");
        items[0].Include=false;
        Assert(include.IsChecked==null,"Include header shows mixed selection");
        include.IsChecked=false;include.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(items.All(i=>i.Include),"Mixed Include selection selects all on next click");
        var footerButtons=Descendants(editor).OfType<Button>().Where(x=>x.Content is string).Select(x=>(string)x.Content).ToList();
        int systemIndex=footerButtons.IndexOf("System");
        Assert(systemIndex>=0 && footerButtons[systemIndex+1].StartsWith("Create ") && footerButtons[systemIndex+2]=="Back" && footerButtons[systemIndex+3]=="Close","Settings footer order: System Create Back Close");
        editor.Close();
        var tabs=Descendants(main).OfType<TabControl>().Single(t=>t.Name=="WorkflowTabs");
        ((TabItem)tabs.Items[1]).IsEnabled=true;
        int runs=0;var retained=main.AirTerminals;
        main.RunDuctCreation=()=>{runs++;Assert(main.DuctCreationRequest!=null,"Execution callback receives pending request");};
        main.Show();
        typeof(IFCInfoWindow).GetMethod("SubmitDuctRequest",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,new object[] {new DuctRequest()});
        Assert(runs==1 && main.IsVisible && ReferenceEquals(retained,main.AirTerminals),"Creating ducts must preserve the same open window and rows");
        Assert(main.DuctCreationRequest==null && tabs.SelectedIndex==1,"Completed requests clear without restarting the tool");
        page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        main.Close();
    }
    private static void TestIfcClassification()
    {
        string evidence;
        var row=new AirTerminalRow {SystemType="LU - ABLUFT"};
        Assert(IfcHvacClassification.Resolve(new[] {row},out evidence)=="ExhaustAir" && evidence.Contains("Suggested"),"Abluft must suggest an editable exhaust classification");
        row.IfcProperties.Add(new IfcPropertyValue {Name="System Classification",Value="Return Air"});
        Assert(IfcHvacClassification.Resolve(new[] {row},out evidence)=="ReturnAir" && evidence.Contains("properties"),"Explicit IFC classification must take priority over names");
        row.IfcProperties.Add(new IfcPropertyValue {Name="SystemClassification",Value="SupplyAir"});
        Assert(IfcHvacClassification.Resolve(new[] {row},out evidence)==null,"Conflicting properties must not be guessed");
        Assert(IfcHvacClassification.Resolve(new[] {new AirTerminalRow {SystemType="VENTILATION"}},out evidence)==null,"Generic ventilation must not default to supply air");
        Assert(IfcHvacClassification.Resolve(new[] {new AirTerminalRow {SystemType="Supply"},new AirTerminalRow {SystemType="Return"}},out evidence)==null,"Mixed groups must require review");
    }
    private static void TestEmbeddedSettings()
    {
        var main=new IFCInfoWindow();
        var source=new AirTerminalRow {ElementId="123",IsSelected=true};
        main.AirTerminals=new List<AirTerminalRow> {source};
        var tabs=Descendants(main).OfType<TabControl>().Single(t=>t.Name=="WorkflowTabs");
        var elements=(TabItem)tabs.Items[1]; elements.IsEnabled=true;
        var settingsTab=(TabItem)tabs.Items[2]; settingsTab.IsEnabled=true;
        var editor=Settings(new List<DuctPlanItem> {Item("123",false,"Supply Air","Level 1")},false,Choices());
        main.OpenDuctSettings(editor,r=>{});
        Assert(tabs.SelectedItem==settingsTab && !editor.IsVisible,"Duct settings must be embedded without another visible window");
        var content=settingsTab.Content;
        var grid=Descendants((DependencyObject)content).OfType<DataGrid>().Single();
        Assert(grid.Parent is Grid && Grid.GetRow(grid)==0 && Grid.GetColumn((Grid)grid.Parent)==1,"Include table must be fixed above right-side options");
        Assert(double.IsNaN(grid.Height),"Include table must stretch vertically");
        tabs.SelectedItem=elements; tabs.SelectedItem=settingsTab;
        Assert(ReferenceEquals(settingsTab.Content,content),"Tab switching must preserve unchanged settings");
        int prepared=0;
        main.CanCreateDucts=true;
        main.PrepareDucts=rows=> {prepared++;return null;};
        tabs.SelectedItem=elements;
        main.AirTerminals.Add(new AirTerminalRow {ElementId="456",IsSelected=true});
        tabs.SelectedItem=settingsTab;
        Assert(prepared==1 && settingsTab.Content==null && tabs.SelectedItem==elements,"Changed selection must invalidate stale settings");
        main.Close();editor.Close();
    }
    private static void TestExports()
    {
        var rows=new List<AirTerminalRow> {
            new AirTerminalRow { ElementId="2",Name="Duct \"A\"",Elevation="100",IsSelected=true },
            new AirTerminalRow { ElementId="10",Name="=1+1",Elevation="200",IsSelected=true },
            new AirTerminalRow { ElementId="99",Name="Excluded" } };
        string folder=Path.Combine(Path.GetTempPath(),"IfcExportTests-"+Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try {
            for(int format=1;format<=4;format++) {
                string path=Path.Combine(folder,"export-"+format);
                IfcSelectionExport.Export(path,rows,"source.ifc","Pipes",format);
                string content=File.ReadAllText(path);
                Assert(content.Contains("100") && content.Contains("200") && !content.Contains("Excluded") && !content.Contains("<varies>"),"Every export format must preserve individual selected values");
                if(format==2) Assert(content.Contains("'=1+1"),"TSV must escape spreadsheet formulas");
                if(format==4) {
                    var xml=new System.Xml.XmlDocument();xml.Load(path);
                    Assert(xml.SelectNodes("/IfcElements/Element").Count==2,"XML must contain two separate elements");
                }
            }
        } finally { foreach(string path in Directory.GetFiles(folder)) File.Delete(path); Directory.Delete(folder); }
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

    private static void TestLoading()
    {
        var owner = new Window { Width=500, Height=300, Opacity=0, ShowInTaskbar=false };
        owner.Show();
        var type=typeof(IFCInfoWindow).Assembly.GetType("IFCInfo.LoadingWindow",true);
        var busy=(IDisposable)type.GetMethod("ShowWhile",BindingFlags.Static|BindingFlags.NonPublic)
            .Invoke(null,new object[] { owner,"Checking","Checking loading state" });
        Assert(!owner.IsEnabled,"Loading prevents duplicate user actions");
        busy.Dispose();
        Assert(owner.IsEnabled,"Loading restores input after completion");
        owner.Close();
    }

    private static void TestDuctGroups()
    {
        var round=Item("1",true,"Supply Air","L1");
        var square=Item("2",false,"Supply Air","L1"); square.Height=square.Width;
        var rect=Item("3",false,"Supply Air","L1");
        var exhaust=Item("4",false,"NEW EXHAUST","L1");
        var oval=Item("5",false,"Supply Air","L1"); oval.Oval=true;
        var items=new List<DuctPlanItem> { round,square,rect,exhaust,oval };
        Assert(items.Select(i=>i.GroupKey).Distinct().Count()==5,"Separate shape and system combinations");
        Assert(new AirTerminalRow { DuctSource=new IfcTerminalSource { WidthMm=200,HeightMm=200 } }.Shape=="Square","Recognize square IFC sections");
        Assert(new AirTerminalRow().Shape=="Unknown","Do not infer missing section metadata");
        var rectTypes=new List<DuctChoice> { new DuctChoice { Id=20,Name="Rect A" },new DuctChoice { Id=21,Name="Rect B" } };
        var window=new DuctCreationWindow(items,new List<string>(),new List<DuctChoice> { new DuctChoice { Id=10,Name="Round" } },
            rectTypes,Choices(),Choices(),new List<DuctChoice>(),new DuctSettingsData(),false,
            new List<DuctChoice> { new DuctChoice { Id=30,Name="Oval" } });
        window.Opacity=0; window.ShowInTaskbar=false;
        Exception failure=null;
        window.Loaded+=(s,e)=>window.Dispatcher.BeginInvoke(new Action(()=>
        {
            try
            {
                var groups=Descendants(window).OfType<Expander>().Where(x=>x.GetType().Name=="DuctGroupEditor").ToList();
                Assert(groups.Count==5,"One editor per shape/system group");
                foreach(var group in groups) group.IsExpanded=true;
                window.UpdateLayout();
                foreach(var group in groups)
                {
                    var type=Descendants(group).OfType<ComboBox>().Single(x=>x.Name=="GroupDuctType");
                    if(type.Items.Count==2) type.SelectedIndex=group.Header.ToString().StartsWith("Square")?1:0;
                }
                var newGroup=groups.Single(x=>x.Header.ToString().Contains("NEW EXHAUST"));
                Descendants(newGroup).OfType<CheckBox>().Single(x=>x.Name=="CreateNewSystem").IsChecked=true;
                window.UpdateLayout();
                var submit=Descendants(window).OfType<Button>().Single(x=>(x.Content as string)=="Create 5 Duct");
                var autoClassification=Descendants(newGroup).OfType<ComboBox>().Single(x=>x.Name=="SystemClassification");
                Assert((string)autoClassification.SelectedItem=="ExhaustAir","IFC exhaust name should prefill classification");
                var followIfc=Descendants(newGroup).OfType<CheckBox>().Single(x=>x.Name=="FollowIfcClassification");
                var template=Descendants(newGroup).OfType<ComboBox>().Single(x=>x.Name=="SystemTemplate");
                Assert(!autoClassification.IsEnabled && !template.IsEnabled,"Follow IFC locks both template and classification");
                followIfc.IsChecked=false;
                Assert(autoClassification.IsEnabled && template.IsEnabled,"Manual mode unlocks both fields");
                autoClassification.SelectedIndex=-1;
                submit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(window.Request==null,"Unresolved classification must still be validated");
                Descendants(newGroup).OfType<ComboBox>().Single(x=>x.Name=="SystemClassification").SelectedItem="ExhaustAir";
                var mode=Descendants(window).OfType<ComboBox>().Single(x=>x.Name=="SystemSetupMode");
                var common=Descendants(window).OfType<ComboBox>().Single(x=>x.Name=="SharedHvacSystem");
                mode.SelectedIndex=1;
                Assert(groups.All(g=>!Descendants(g).OfType<ComboBox>().Single(x=>x.Name=="GroupSystemType").IsEnabled),"Auto locks every group system");
                submit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(window.Request==null,"Auto existing requires a shared type");
                common.SelectedIndex=0;
                Assert(groups.All(g=>ReferenceEquals(Descendants(g).OfType<ComboBox>().Single(x=>x.Name=="GroupSystemType").SelectedItem,common.SelectedItem)),"Shared HVAC selection applies to every group");
                var strategy=Descendants(window).OfType<ComboBox>().Single(x=>x.Name=="AutoSystemStrategy");
                strategy.SelectedIndex=1;
                Assert(groups.Where(g=>!ReferenceEquals(g,newGroup)).All(g=>Descendants(g).OfType<ComboBox>().Single(x=>x.Name=="GroupSystemType").SelectedItem!=null),"IFC mapping reuses existing names even without classification metadata");
                Assert(Descendants(newGroup).OfType<CheckBox>().Single(x=>x.Name=="CreateNewSystem").IsChecked==true,"Only a missing system is planned for creation");
                Assert(groups.All(g=>!Descendants(g).OfType<CheckBox>().Single(x=>x.Name=="CreateNewSystem").IsEnabled),"IFC auto mapping locks new-system controls");
                Assert(Descendants(newGroup).OfType<ComboBox>().Single(x=>x.Name=="SystemClassification").SelectedItem.ToString()=="ExhaustAir","IFC auto mapping uses source classification");
                mode.SelectedIndex=0;
                var projectBox=Descendants(newGroup).OfType<ComboBox>().Single(x=>x.Name=="GroupSystemType");
                Assert(Descendants(newGroup).OfType<CheckBox>().Single(x=>x.Name=="CreateNewSystem").IsChecked==true && projectBox.SelectedItem==null,"Manual restores prior configuration");
                submit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var request=window.Request;
                Assert(request!=null && request.DuctTypes.Count==5,"Submit all selected groups in one request");
                Assert(request.DuctTypes[square.GroupKey]==21 && request.DuctTypes[rect.GroupKey]==20 && request.DuctTypes[oval.GroupKey]==30,"Preserve per-group type assignments including square and oval");
                Assert(request.NewSystems.Count==1 && request.NewSystems[exhaust.GroupKey].Classification=="ExhaustAir","Carry the new system plan without mutating the model in UI");
                Assert(request.SystemTypes[exhaust.GroupKey]==0 && request.SystemTypes[round.GroupKey]==7,"Keep pending and existing systems distinct");
            }
            catch(Exception ex) { failure=ex; }
            finally { window.Close(); }
        }),DispatcherPriority.ApplicationIdle);
        window.ShowDialog(); if(failure!=null) throw failure;
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
                Assert((string)submit.Content == (updating ? "Update 1 Duct" : "Create 1 Duct"), "Button count must track selection");
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
                Assert(request.Skipped.All(r => r.Status == "Unchecked" && !string.IsNullOrEmpty(r.IfcGuid)), "Skipped rows retain identity and status");
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


