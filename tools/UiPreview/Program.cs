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
    private static void RenderServiceAndSlope(string output)
    {
        Directory.CreateDirectory(output);
        long cat=(long)Autodesk.Revit.DB.BuiltInCategory.OST_CableTray;
        var row=new AirTerminalRow{ElementId="101",Name="Cable Tray with Fittings : Default",SourceLevelName="Level 1",ServiceType="Power"};
        var w=new CurveCreationWindow(new List<AirTerminalRow>{row},new List<ReplacementTypeOption>{new ReplacementTypeOption{Id=10,Label=row.Name,Kind="CableTray",CategoryId=cat,Supported=true}},new List<ReplacementLevelOption>{new ReplacementLevelOption{Id=1,Label="Level 1"}},new List<ReplacementTypeOption>(),cat,"CableTray",new List<string>{"Power","Data","Lighting"});
        var nodes=Descendants(w).OfType<FrameworkElement>().ToList();
        Render(w,output,"cable-service-manual-existing.png",1440,900);
        var choice=nodes.OfType<ComboBox>().Single(c=>c.Name=="GroupServiceFollowMode");
        choice.SelectedItem=choice.Items.OfType<ServiceTypeChoice>().Single(c=>c.Kind=="New");nodes.OfType<TextBox>().Single(c=>c.Name=="GroupServiceType").Text="Emergency Power";
        Render(w,output,"cable-service-manual-new.png",1440,900);
        nodes.OfType<ComboBox>().Single(c=>c.Name=="CurveSetupMode").SelectedIndex=1;
        Render(w,output,"cable-service-auto-ifc.png",1440,900);w.Close();
        var main=new IFCInfoWindow();typeof(IFCInfoWindow).GetProperty("SelectedCategory").SetValue(main,new CategoryOption{Id=(long)Autodesk.Revit.DB.BuiltInCategory.OST_PipeCurves,Name="Pipes"},null);
        main.AirTerminals=new List<AirTerminalRow>{new AirTerminalRow{ElementId="201",Name="Pipe — horizontal",SlopePercent=0},new AirTerminalRow{ElementId="202",Name="Pipe — slope 2%",SlopePercent=2},new AirTerminalRow{ElementId="203",Name="Pipe — vertical",IsVerticalPipe=true}};
        var page=(FrameworkElement)typeof(IFCInfoWindow).GetMethod("CountPage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,null);
        var preview=new Window{Content=page};Render(preview,output,"pipe-elements-slope.png",1440,650);preview.Close();main.Close();
    }

    private static void TestServicesAndSlope()
    {
        var picker=new CableServicePicker(new[]{"Power","Data"," power ",""},"Power");
        var choices=picker.Selector.Items.OfType<ServiceTypeChoice>().ToList();
        Assert(choices.Count(c=>c.Kind=="Existing")==2 && choices.All(c=>c.Kind!="Follow"),"Manual service list contains filled values, no Follow IFC and no duplicates");
        Assert(picker.Value=="Power" && picker.IsValid && picker.NewValue.Visibility==Visibility.Collapsed,"Manual preselects a matching filled service and hides the new-value input");
        picker.Selector.SelectedItem=choices.Single(c=>c.Value=="Data");Assert(picker.Value=="Data","Manual can select an existing project service");
        picker.Selector.SelectedItem=choices.Single(c=>c.Kind=="New");
        Assert(picker.NewValue.Visibility==Visibility.Visible && !picker.IsValid,"Enter new shows its input and rejects empty text");
        picker.NewValue.Text="Emergency";Assert(picker.IsValid && picker.Value=="Emergency","New service value is retained for creation");
        picker.SetAutomatic(true,"IFC Power");Assert(picker.Value=="IFC Power" && picker.Selector.Visibility==Visibility.Collapsed && picker.AutoDisplay.SelectedItem.ToString().Contains("IFC Power"),"Auto shows the resolved IFC service instead of a Manual policy selector");
        picker.SetAutomatic(false,"");Assert(picker.Value=="Emergency" && picker.NewValue.Visibility==Visibility.Visible,"Returning to Manual restores the existing custom value");
        picker.SetAutomatic(true,"");Assert(picker.IsValid && picker.AutoDisplay.SelectedItem.ToString().Contains("No source service"),"Missing IFC service is visibly reported in Auto");
        var missing=new CableServicePicker(new[]{"Power"},"");Assert(missing.Selector.SelectedItem==null && !missing.IsValid,"Manual without source service requires a real choice instead of silently following IFC");
        missing.Selector.SelectedItem=missing.Selector.Items.OfType<ServiceTypeChoice>().Single(c=>c.Kind=="Default");Assert(missing.IsValid && missing.Value=="","Explicit Keep native default allows an empty service");
        var row=new AirTerminalRow();PipeSlope.Set(row,10,0,.2);Assert(Math.Abs(row.SlopePercent.Value-2)<1e-9 && row.Slope=="2","Pipe slope is rise over horizontal run in percent");
        PipeSlope.Set(row,-10,0,-.2);Assert(row.Slope=="2","Endpoint order does not change slope magnitude");
        PipeSlope.Set(row,3,4,.05);Assert(row.Slope=="1","Slope uses horizontal XY length, not spatial pipe length");
        PipeSlope.Set(row,1,0,0);Assert(row.Slope=="0","Horizontal pipes explicitly report zero percent");
        PipeSlope.Set(row,0,0,10);Assert(row.Slope=="Vertical" && !row.SlopePercent.HasValue,"Vertical pipes are not reported as zero slope");
        PipeSlope.Set(row,0,0,0);Assert(row.Slope=="Unavailable","Degenerate centerlines have no fabricated slope");
        PipeSlope.Set(row,double.NaN,0,1);Assert(row.Slope=="Unavailable","Non-finite geometry is rejected");
        foreach(bool pipe in new[]{true,false}) {
            var w=new IFCInfoWindow();
            typeof(IFCInfoWindow).GetProperty("SelectedCategory").SetValue(w,new CategoryOption{Id=(long)(pipe?Autodesk.Revit.DB.BuiltInCategory.OST_PipeCurves:Autodesk.Revit.DB.BuiltInCategory.OST_CableTray),Name=pipe?"Pipes":"Cable Trays"},null);
            w.AirTerminals=new List<AirTerminalRow>{new AirTerminalRow{ElementId="1",SlopePercent=2}};
            var page=(FrameworkElement)typeof(IFCInfoWindow).GetMethod("CountPage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(w,null);
            var table=Descendants(page).OfType<DataGrid>().First();
            Assert(table.Columns.Any(c=>Equals(c.Header,"Slope (%)"))==pipe,"Elements shows Slope (%) only for Pipes: "+pipe);w.Close();
        }
    }

    private static void TestUnifiedSettings(string output=null)
    {
        if(output!=null)Directory.CreateDirectory(output);
        foreach(string kind in new[]{"Pipe","CableTray","Conduit"}) {
            long cat=(long)(kind=="Pipe"?Autodesk.Revit.DB.BuiltInCategory.OST_PipeCurves:kind=="CableTray"?Autodesk.Revit.DB.BuiltInCategory.OST_CableTray:Autodesk.Revit.DB.BuiltInCategory.OST_Conduit);
            var rows=new List<AirTerminalRow>{new AirTerminalRow{ElementId="101",Name=kind+" source",SystemType="Supply",SourceLevelName="Level 1",ServiceType="Power"}};
            var types=new List<ReplacementTypeOption>{new ReplacementTypeOption{Id=10,Label=kind+" source",Kind=kind,CategoryId=cat,Supported=true}};
            var levels=new List<ReplacementLevelOption>{new ReplacementLevelOption{Id=1,Label="Level 1",ElevationMm=0},new ReplacementLevelOption{Id=2,Label="Level 2",ElevationMm=4000}};
            var w=new CurveCreationWindow(rows,types,levels,new List<ReplacementTypeOption>{new ReplacementTypeOption{Id=20,Label="Supply",Kind="Pipe"}},cat,kind);
            var nodes=Descendants(w).OfType<FrameworkElement>().ToList();
            var card=nodes.OfType<Expander>().Single();
            var level=nodes.OfType<ComboBox>().Single(c=>c.Name=="GroupCurveLevel");
            Assert(Descendants(card).Contains(level),"Level belongs inside the source group card: "+kind);
            var levelGrid=(Grid)((FrameworkElement)level.Parent).Parent;
            Assert(levelGrid.ColumnDefinitions[0].Width.Value==70 && level.MinHeight==46 && level.FontSize==16,"Level uses the same icon and full-size field as Type: "+kind);
            if(kind=="CableTray") {
                var service=nodes.OfType<ComboBox>().Single(c=>c.Name=="GroupServiceFollowMode");
                var serviceGrid=(Grid)((FrameworkElement)service.Parent).Parent;
                Assert(serviceGrid.ColumnDefinitions[0].Width.Value==70 && service.MinHeight==46,"Cable Tray Service Type uses the same icon/field layout");
                service.SelectedIndex=1;nodes.OfType<TextBox>().Single(c=>c.Name=="GroupServiceType").Text="Emergency";
            }
            if(output!=null)Render(w,output,kind+"-manual-unified.png",1440,900);
            var mode=nodes.OfType<ComboBox>().Single(c=>c.Name=="CurveSetupMode");mode.SelectedIndex=1;
            Assert(level.Visibility==Visibility.Visible && !level.IsEnabled && ((ReplacementLevelOption)level.SelectedItem).Label.Contains("Level 1"),"Auto keeps the resolved Level selector visible in its card: "+kind);
            if(kind=="CableTray")Assert(nodes.OfType<ComboBox>().Single(c=>c.Name=="ResolvedServiceChoice").Visibility==Visibility.Visible,"Auto keeps the styled service selector visible");
            if(output!=null)Render(w,output,kind+"-auto-unified.png",1440,900);
            mode.SelectedIndex=0;
            if(kind=="CableTray")Assert(nodes.OfType<ComboBox>().Single(c=>c.Name=="GroupServiceFollowMode").SelectedIndex==1 && nodes.OfType<TextBox>().Single(c=>c.Name=="GroupServiceType").Text=="Emergency","Service policy and value survive switching Auto then Manual");
            w.Close();
        }
        var dlevels=new List<DuctChoice>{new DuctChoice{Id=1,Name="Level 1"},new DuctChoice{Id=2,Name="Level 2"}};
        var systems=new List<DuctChoice>{new DuctChoice{Id=20,Name="Supply Air"}};
        var typesD=new List<DuctChoice>{new DuctChoice{Id=10,Name="Standard duct"}};
        var first=Item("1",false,"Supply Air","Level 1");first.Source.Name="Standard duct";
        var second=Item("2",true,"Supply Air","Level 1");second.Source.Name="Standard duct";
        var d=new DuctCreationWindow(new List<DuctPlanItem>{first,second},new List<string>(),typesD,typesD,systems,dlevels,new List<DuctChoice>(),new DuctSettingsData());
        var dn=Descendants(d).OfType<FrameworkElement>().ToList();
        var cards=dn.OfType<Expander>().Where(e=>e.GetType().Name=="DuctGroupEditor").ToList();
        Assert(cards.Count==2 && cards.All(e=>Descendants(e).OfType<ComboBox>().Any(c=>c.Name=="GroupLevel")),"Every Duct shape/system card contains Level, including a shared source level");
        foreach(var c in cards)c.IsExpanded=true;
        var boxes=dn.OfType<ComboBox>().Where(c=>c.Name=="GroupLevel").ToList();
        Assert(boxes.All(c=>c.MinHeight==46 && ((Grid)((FrameworkElement)c.Parent).Parent).ColumnDefinitions[0].Width.Value==70),"Duct Level uses the same full field design as System and Duct Type");
        boxes[0].SelectedItem=dlevels[1];
        Assert(boxes.All(c=>((DuctChoice)c.SelectedItem).Id==2),"Duplicated source-level selectors stay synchronized across duct cards");
        var dm=dn.OfType<ComboBox>().Single(c=>c.Name=="SystemSetupMode");dm.SelectedIndex=1;
        Assert(boxes.All(c=>c.Visibility==Visibility.Visible && !c.IsEnabled && ((DuctChoice)c.SelectedItem).Name.Contains("Level 1")),"Duct Auto displays resolved Follow IFC Level inside each group");
        dm.SelectedIndex=0;
        Assert(boxes.All(c=>((DuctChoice)c.SelectedItem).Id==2 && c.IsEnabled),"Duct Manual level overrides survive Auto and back");
        d.SubmitEmbedded=value=>{};
        dn.OfType<Button>().Single(b=>(b.Content as string)=="Create 2 Duct").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(d.Request!=null && d.Request.Levels["Level 1"]==2,"Duct submit carries the synchronized manual level mapping");d.Close();
        // One fully expanded card for a clear rendered comparison with Pipe/Cable Tray.
        var renderDuct=new DuctCreationWindow(new List<DuctPlanItem>{first},new List<string>(),typesD,typesD,systems,dlevels,new List<DuctChoice>(),new DuctSettingsData());
        if(output!=null)Render(renderDuct,output,"Duct-manual-unified.png",1440,900);
        Descendants(renderDuct).OfType<ComboBox>().Single(c=>c.Name=="SystemSetupMode").SelectedIndex=1;
        if(output!=null)Render(renderDuct,output,"Duct-auto-unified.png",1440,900);renderDuct.Close();
    }

    private static void TestCurveLevels()
    {
        Assert(CurveCreationWindow.SourceIssue(new AirTerminalRow{ElementId="9",NativeShape="Round"},"CableTray").Contains("IFC category"),"Round conduit imported into Cable Trays is reported before creation");
        var levels=new List<ReplacementLevelOption>{new ReplacementLevelOption{Id=1,Label="L1",ElevationMm=0},new ReplacementLevelOption{Id=2,Label="L2",ElevationMm=4000}};
        Assert(CurveLevelResolver.Resolve(new AirTerminalRow{SourceLevelName="L2"},levels)?.Id==2,"Follow IFC matches the source level name");
        Assert(CurveLevelResolver.Resolve(new AirTerminalRow{HostLevelElevationMm=4000},levels)?.Id==2,"Follow IFC matches transformed source level elevation");
        Assert(CurveLevelResolver.Resolve(new AirTerminalRow{HostCenterElevationMm=4800},levels)?.Id==2,"Missing source level uses host-coordinate centerline floor");
        Assert(CurveLevelResolver.Resolve(new AirTerminalRow{HostLevelElevationMm=9000},levels)==null,"Known missing storey requires an explicit mapping");
        Assert(CurveLevelResolver.Resolve(new AirTerminalRow(),levels)==null,"No IFC level/elevation never defaults to the first level");
        Assert(CurveLevelResolver.Resolve(new AirTerminalRow{HostCenterElevationMm=-500},levels)==null,"Below all host levels remains unresolved");
        Assert(CurveLevelResolver.Resolve(new AirTerminalRow{HostLevelElevationMm=0},levels.Concat(new[]{new ReplacementLevelOption{Id=3,Label="Duplicate",ElevationMm=0}}))==null,"Coincident levels are not chosen arbitrarily");
        var trayCategory=(long)Autodesk.Revit.DB.BuiltInCategory.OST_CableTray;
        var traySource=new AirTerminalRow{ElementId="77",Name="Cable Tray with Fittings-Default-973916",SourceLevelName="L1",DuctSource=new IfcTerminalSource{TypeName="Cable Tray with Fittings:Default",WidthMm=304.8,HeightMm=101.6}};
        var trayTypes=new List<ReplacementTypeOption>{new ReplacementTypeOption{Id=70,Label="Other Family : Default",Kind="CableTray",CategoryId=trayCategory,Supported=true},new ReplacementTypeOption{Id=71,Label="Cable Tray with Fittings : Default",Kind="CableTray",CategoryId=trayCategory,Supported=true}};
        var trayWindow=new CurveCreationWindow(new List<AirTerminalRow>{traySource},trayTypes,levels,new List<ReplacementTypeOption>(),trayCategory,"CableTray");
        var trayNodes=Descendants(trayWindow).OfType<FrameworkElement>().ToList();
        trayNodes.OfType<ComboBox>().Single(c=>c.Name=="CurveSetupMode").SelectedIndex=1;
        Assert(((ReplacementTypeOption)trayNodes.OfType<ComboBox>().Single(c=>c.Name=="GroupCurveType").SelectedItem).Id==71,"Follow IFC matches exact source family/type instead of numeric instance name or another Default type");
        traySource.NativeShape="Round";
        trayNodes.OfType<Button>().Single(b=>b.Name=="ReviewCurveSettings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(!trayNodes.OfType<Button>().Single(b=>b.Name=="CreateCurve").IsEnabled,"Mismatched round source disables tray creation before a transaction");
        trayWindow.Close();
        foreach(string kind in new[]{"Pipe","CableTray","Conduit"}) {
            long cat=(long)(kind=="Pipe"?Autodesk.Revit.DB.BuiltInCategory.OST_PipeCurves:kind=="CableTray"?Autodesk.Revit.DB.BuiltInCategory.OST_CableTray:Autodesk.Revit.DB.BuiltInCategory.OST_Conduit);
            var rows=new List<AirTerminalRow>{new AirTerminalRow{ElementId="1",Name="T",SourceLevelName="L1",SystemType="Water",ServiceType="Power"},new AirTerminalRow{ElementId="2",Name="T",SourceLevelName="L2",SystemType="Water",ServiceType="Data"}};
            var w=new CurveCreationWindow(rows,new List<ReplacementTypeOption>{new ReplacementTypeOption{Id=10,Label="T",Kind=kind,CategoryId=cat,Supported=true}},levels,new List<ReplacementTypeOption>{new ReplacementTypeOption{Id=20,Label="Water",Kind="Pipe"}},cat,kind);
            var nodes=Descendants(w).OfType<FrameworkElement>().ToList();
            var mode=nodes.OfType<ComboBox>().Single(c=>c.Name=="CurveSetupMode");
            var shared=nodes.OfType<ComboBox>().Single(c=>c.Name=="SharedCurveLevel");
            var groups=nodes.OfType<ComboBox>().Where(c=>c.Name=="GroupCurveLevel").ToList();
            Assert(groups.Count==2 && groups.All(c=>((ReplacementLevelOption)c.SelectedItem).Id==0 && c.IsEnabled),"Manual has Follow IFC for each source level: "+kind);
            if(kind=="CableTray")Assert(nodes.OfType<ComboBox>().Where(c=>c.Name=="GroupServiceFollowMode").All(c=>(c.SelectedItem as ServiceTypeChoice)?.Kind=="Existing" && c.IsEnabled),"Cable Tray Manual explicitly follows each source service");
            groups[0].SelectedItem=levels[1];
            mode.SelectedIndex=1;
            Assert(shared.IsEnabled && ((ReplacementLevelOption)shared.SelectedItem).Id==0 && groups.All(c=>!c.IsEnabled),"Auto defaults to shared Follow IFC: "+kind);
            ReplacementRequest request=null;w.SubmitEmbedded=value=>request=value;
            nodes.OfType<Button>().Single(b=>b.Name=="ReviewCurveSettings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            nodes.OfType<Button>().Single(b=>b.Name=="CreateCurve").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert(request!=null && request.SourceLevelIds["1"]==1 && request.SourceLevelIds["2"]==2,"One run carries different source levels: "+kind);
            shared.SelectedItem=levels[0];w.RefreshConfirmation();
            nodes.OfType<Button>().Single(b=>b.Name=="CreateCurve").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert(request.SourceLevelIds.Values.All(id=>id==1),"Explicit Auto level overrides all sources: "+kind);
            mode.SelectedIndex=0;
            Assert(((ReplacementLevelOption)groups[0].SelectedItem).Id==2 && ((ReplacementLevelOption)groups[1].SelectedItem).Id==0,"Auto to Manual restores individual level choices: "+kind);
            rows[1].SourceLevelName="Missing";rows[1].HostLevelElevationMm=9000;w.RefreshConfirmation();
            Assert(!nodes.OfType<Button>().Single(b=>b.Name=="CreateCurve").IsEnabled,"Unresolved Follow IFC level blocks creation: "+kind);
            w.Close();
        }
    }
    private static void TestCurveGroupModes()
    {
        Assert(CurveCreationWindow.SourceService(new AirTerminalRow{SystemType="Not a service"})=="","Cable Tray service must not be inferred from IFC system type");
        var source=new AirTerminalRow{IfcProperties=new List<IfcPropertyValue>{new IfcPropertyValue{Name="Service Type",Value="Power"}}};
        Assert(CurveCreationWindow.SourceService(source)=="Power","Cable Tray reads an explicit IFC service property");
        source.ServiceType="Data";Assert(CurveCreationWindow.SourceService(source)=="Data","Native source Service Type takes precedence over IFC fallback");
        foreach(string kind in new[]{"Pipe","CableTray"})
        {
            long category=(long)(kind=="Pipe"?Autodesk.Revit.DB.BuiltInCategory.OST_PipeCurves:Autodesk.Revit.DB.BuiltInCategory.OST_CableTray);
            var rows=new List<AirTerminalRow>{new AirTerminalRow{ElementId="1",Name="A",SourceLevelName="L1",SystemType="Supply",ServiceType="Power"},new AirTerminalRow{ElementId="2",Name="B",SourceLevelName="L1",SystemType="Return",ServiceType="Data"}};
            var types=new List<ReplacementTypeOption>{new ReplacementTypeOption{Id=10,Label="A",Kind=kind,CategoryId=category,Supported=true},new ReplacementTypeOption{Id=11,Label="B",Kind=kind,CategoryId=category,Supported=true}};
            var systems=new List<ReplacementTypeOption>{new ReplacementTypeOption{Id=20,Label="Supply",Kind="Pipe"},new ReplacementTypeOption{Id=21,Label="Return",Kind="Pipe"}};
            var window=new CurveCreationWindow(rows,types,new List<ReplacementLevelOption>{new ReplacementLevelOption{Id=1,Label="L1"}},systems,category,kind);
            var nodes=Descendants(window).OfType<FrameworkElement>().ToList();
            var mode=nodes.OfType<ComboBox>().Single(c=>c.Name=="CurveSetupMode");
            var common=nodes.OfType<ComboBox>().Single(c=>c.Name=="CurveType");
            var groupTypes=nodes.OfType<ComboBox>().Where(c=>c.Name=="GroupCurveType").ToList();
            Assert(groupTypes.Count==2 && groupTypes.All(c=>c.IsEnabled) && !common.IsEnabled,"Manual exposes independent source group types: "+kind);
            mode.SelectedIndex=1;
            Assert(nodes.OfType<TextBlock>().Where(t=>t.Name=="ResolvedIfcCurveType").All(t=>t.Visibility==Visibility.Visible && t.Text.StartsWith("Follow IFC → ")),"Follow IFC is explicit in resolved group type displays: "+kind);
            common.SelectedItem=types[0];
            Assert(common.IsEnabled && groupTypes.All(c=>!c.IsEnabled && ((ReplacementTypeOption)c.SelectedItem).Id==10),"Auto applies a shared type to all groups: "+kind);
            if(kind=="CableTray") {
                Assert(nodes.OfType<ComboBox>().Single(c=>c.Name=="ServiceFollowMode").SelectedIndex==0 && nodes.OfType<TextBlock>().Where(t=>t.Name=="ResolvedIfcService").All(t=>t.Visibility==Visibility.Visible && t.Text.StartsWith("Follow IFC → ")),"Cable Tray Auto explicitly follows source service in both shared and group UI");
                var sharedChoice=nodes.OfType<ComboBox>().Single(c=>c.Name=="ServiceFollowMode");sharedChoice.SelectedItem=sharedChoice.Items.OfType<ServiceTypeChoice>().Single(v=>v.Kind=="New");
                nodes.OfType<TextBox>().Single(t=>t.Name=="SharedServiceType").Text="Emergency";
                Assert(nodes.OfType<ComboBox>().Where(t=>t.Name=="ResolvedServiceChoice" && t.Visibility==Visibility.Visible).All(t=>t.SelectedItem.ToString().Contains("Emergency")),"Auto cable tray service applies to each group without a system selector");
            }
            mode.SelectedIndex=0;
            Assert(groupTypes.Select(c=>((ReplacementTypeOption)c.SelectedItem).Id).OrderBy(id=>id).SequenceEqual(new long[]{10,11}),"Returning to Manual restores group type choices: "+kind);
            ReplacementRequest request=null;window.SubmitEmbedded=r=>request=r;
            nodes.OfType<Button>().Single(b=>b.Name=="CreateCurve").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert(request==null,"Settings cannot directly create elements before final confirmation");
            nodes.OfType<Button>().Single(b=>b.Name=="ReviewCurveSettings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var confirmation=nodes.OfType<TabControl>().Single(t=>t.Name=="CurveConfirmationTabs");
            Assert(confirmation.SelectedIndex==1 && !Descendants((DependencyObject)((TabItem)confirmation.Items[0]).Content).OfType<DataGrid>().Any(),"Source table appears only in the final confirmation tab");
            var reviewed=nodes.OfType<DataGrid>().Single(t=>t.Name=="CurvePreview");
            Assert(reviewed.Items.Count==2 && (string)reviewed.Items[0].GetType().GetProperty("TargetType").GetValue(reviewed.Items[0],null)=="A","Final confirmation contains current target selections, not source data alone");
            nodes.OfType<Button>().Single(b=>b.Name=="CreateCurve").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert(request!=null && request.SourceTypeIds["1"]==10 && request.SourceTypeIds["2"]==11,"Requests retain separate target types for source groups: "+kind);
            if(kind=="CableTray")Assert(request.ServiceTypes["1"]=="Power" && request.ServiceTypes["2"]=="Data" && request.SystemTypeId==0,"Cable Tray request carries restored services and no native system ID");
            else Assert(request.SourceSystemTypeIds["1"]==20 && request.SourceSystemTypeIds["2"]==21,"Pipe request carries a separate piping system for each source group");
            window.Close();
        }
        var unresolved=new CurveCreationWindow(new List<AirTerminalRow>{new AirTerminalRow{ElementId="1",Name="Unmatched source"}},new List<ReplacementTypeOption>{new ReplacementTypeOption{Id=10,Label="Unrelated project type",Kind="Conduit",CategoryId=(long)Autodesk.Revit.DB.BuiltInCategory.OST_Conduit,Supported=true}},new List<ReplacementLevelOption>{new ReplacementLevelOption{Id=1,Label="L1"}},new List<ReplacementTypeOption>(),(long)Autodesk.Revit.DB.BuiltInCategory.OST_Conduit,"Conduit");
        var unresolvedNodes=Descendants(unresolved).OfType<FrameworkElement>().ToList();
        unresolvedNodes.OfType<ComboBox>().Single(c=>c.Name=="CurveSetupMode").SelectedIndex=1;
        Assert(unresolvedNodes.OfType<ComboBox>().Single(c=>c.Name=="GroupCurveType").SelectedItem==null && unresolvedNodes.OfType<TextBlock>().Single(t=>t.Name=="ResolvedIfcCurveType").Text.Contains("no matching project type"),"Follow IFC does not silently use an unrelated sole project type");
        unresolvedNodes.OfType<Button>().Single(b=>b.Name=="ReviewCurveSettings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(!unresolvedNodes.OfType<Button>().Single(b=>b.Name=="CreateCurve").IsEnabled,"Final creation is disabled while a Follow IFC match remains unresolved");
        unresolved.Close();
    }
    private static void TestExistingLineParameterTasks()
    {
        foreach(string task in new[]{"Transfer parameters","Update from IFC"})
        foreach(long category in new[]{(long)Autodesk.Revit.DB.BuiltInCategory.OST_PipeCurves,(long)Autodesk.Revit.DB.BuiltInCategory.OST_CableTray,(long)Autodesk.Revit.DB.BuiltInCategory.OST_Conduit})
        {
            var main=new IFCInfoWindow{Links=new List<LinkOption>{new LinkOption{Id=1,Name="Existing.ifc",IsLoaded=true}},Opacity=0,ShowInTaskbar=false};
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var choiceType=typeof(IFCInfoWindow).Assembly.GetType("IFCInfo.DashboardSelection");
            var choice=Activator.CreateInstance(choiceType,true);choiceType.GetField("Task",flags).SetValue(choice,task);
            typeof(IFCInfoWindow).GetProperty("DashboardChoice",flags).SetValue(main,choice,null);
            int transfers=0,geometry=0;
            main.LoadLink=link=> {
                main.Categories=new List<CategoryOption>{new CategoryOption{Id=category,Name="Line",Kind="Line"}};
                main.CheckParameterTarget=n=>"Ready";
                main.TransferParameters=(rows,maps)=>{Assert(rows.Count==1 && maps.Count==1,"Existing line task transfers its highlighted row and validated mapping");transfers++;return "Updated";};
                main.PrepareUpdates=rows=>{geometry++;throw new Exception("Non-duct parameter task must not prepare duct geometry");};
                main.LoadCategory=c=>{main.CanCreateDucts=false;main.CanReplaceCategory=true;main.AirTerminalCount=1;main.AirTerminals=new List<AirTerminalRow>{new AirTerminalRow{ElementId="101"}};};
            };
            Exception failure=null;
            main.Loaded+=(s,e)=>main.Dispatcher.BeginInvoke(new Action(()=> {
                try {
                    Descendants(main).OfType<ComboBox>().Single(c=>Equals(c.Tag,"Select IFC link...")).SelectedIndex=0;
                    main.Categories[0].IsChecked=true;
                    Descendants(main).OfType<Button>().Single(b=>Equals(b.Content,"Continue →")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    var workflow=Descendants(main).OfType<TabControl>().Single(t=>t.Name=="WorkflowTabs");
                    Descendants((DependencyObject)((TabItem)workflow.Items[1]).Content).OfType<DataGrid>().Single(t=>t.Name!="IfcPropertyValues").SelectedItem=main.AirTerminals[0];
                    Descendants(main).OfType<Button>().Single(b=>Equals(b.Content,"Next: Parameters →")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    var panel=((TabItem)workflow.Items[2]).Content;
                    var pairs=(System.Collections.IList)panel.GetType().GetField("mappings",flags).GetValue(panel);
                    pairs.Add(new IfcParameterMapping{Source="IFC Element ID",Target="IFC_ID"});
                    Assert(((TabItem)workflow.Items[3]).Visibility==Visibility.Collapsed,"Existing non-duct parameter task exposes no geometry creation step");
                    Descendants(main).OfType<Button>().Single(b=>Equals(b.Content,"Update parameter values")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert(transfers==1 && geometry==0 && main.Replacement==null && main.DuctCreationRequest==null,"Existing parameter update does not create or replace geometry: "+task+" / "+category);
                }catch(Exception ex){failure=ex;}finally{main.Close();}
            }),DispatcherPriority.ApplicationIdle);
            main.ShowDialog();if(failure!=null)throw failure;
        }
    }
    private static void TestLineParameterRequests()
    {
        foreach(var entry in new[]{Tuple.Create("Pipe",(long)Autodesk.Revit.DB.BuiltInCategory.OST_PipeCurves),Tuple.Create("CableTray",(long)Autodesk.Revit.DB.BuiltInCategory.OST_CableTray),Tuple.Create("Conduit",(long)Autodesk.Revit.DB.BuiltInCategory.OST_Conduit)})
        {
            var main=new IFCInfoWindow{Links=new List<LinkOption>{new LinkOption{Id=1,Name="Line.ifc",IsLoaded=true}},Opacity=0,ShowInTaskbar=false};
            main.LoadLink=link=> {
                main.Categories=new List<CategoryOption>{new CategoryOption{Id=entry.Item2,Name=entry.Item1,Kind="Line"}};
                main.CheckParameterTarget=n=>"Ready";
                main.LoadCategory=c=>{main.CanCreateDucts=false;main.CanReplaceCategory=true;main.AirTerminalCount=1;main.AirTerminals=new List<AirTerminalRow>{new AirTerminalRow{ElementId="101",Name="Line source",SystemType="IFC metadata"}};};
            };
            main.ReplacementTypes=new List<ReplacementTypeOption>{new ReplacementTypeOption{Id=10,CategoryId=entry.Item2,Kind=entry.Item1,Supported=true,Label="Target type"}};
            main.ReplacementLevels=new List<ReplacementLevelOption>{new ReplacementLevelOption{Id=1,Label="L1"}};
            main.ReplacementSystems=new List<ReplacementTypeOption>{new ReplacementTypeOption{Id=20,Kind="Pipe",Label="Water"}};
            Exception failure=null;
            main.Loaded+=(s,e)=>main.Dispatcher.BeginInvoke(new Action(()=> {
                try {
                    Descendants(main).OfType<ComboBox>().Single(c=>Equals(c.Tag,"Select IFC link...")).SelectedIndex=0;
                    main.Categories[0].IsChecked=true;
                    Descendants(main).OfType<Button>().Single(b=>Equals(b.Content,"Continue →")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    main.AirTerminals[0].IsSelected=true;
                    Descendants(main).OfType<Button>().Single(b=>Equals(b.Content,"Next: Parameters →")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    var workflow=Descendants(main).OfType<TabControl>().Single(t=>t.Name=="WorkflowTabs");
                    var panel=((TabItem)workflow.Items[2]).Content;
                    Assert(((UIElement)panel).IsEnabled,"Line parameter editor is enabled: "+entry.Item1);
                    Assert(Equals(((TabItem)workflow.Items[3]).Header,"Preview & Create"),"Line workflow uses the shared preview label: "+entry.Item1);
                    var pairs=(System.Collections.IList)panel.GetType().GetField("mappings",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(panel);
                    pairs.Add(new IfcParameterMapping{Source="IFC Element ID",Target="IFC_SourceId"});
                    Descendants(main).OfType<Button>().Single(b=>Equals(b.Content,"Next: Preview & Create →")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert(workflow.SelectedIndex==3 && main.OwnedWindows.Count==0,"Native line preview is embedded in the final tab without a modal window: "+entry.Item1);
                    var nodes=Descendants((DependencyObject)((TabItem)workflow.Items[3]).Content).OfType<FrameworkElement>().ToList();
                    var table=nodes.OfType<DataGrid>().Single(t=>t.Name=="CurvePreview");
                    Assert(table.Columns.Any(c=>Equals(c.Header,"System Type"))==(entry.Item1=="Pipe"),"Only pipes show a native System Type column: "+entry.Item1);
                    var embeddedContent=((TabItem)workflow.Items[3]).Content;
                    var back=nodes.OfType<Button>().Single(b=>Equals(b.Content,"Back"));
                    back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert(!back.IsCancel && workflow.SelectedIndex==2 && main.IsVisible,"Embedded Back returns to Parameters without closing the tool");
                    workflow.SelectedIndex=3;
                    Assert(ReferenceEquals(((TabItem)workflow.Items[3]).Content,embeddedContent),"Reopening Preview retains its embedded controls and selected settings");
                    nodes.OfType<Button>().Single(b=>b.Name=="ReviewCurveSettings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert(workflow.SelectedIndex==4 && Equals(((TabItem)workflow.Items[4]).Header,"Review & Create"),"Curve confirmation is the fifth main workflow tab, not a nested tab: "+entry.Item1);
                    nodes.OfType<Button>().Single(b=>b.Name=="CreateCurve").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert(main.Replacement!=null && main.Replacement.ParameterMappings.Count==1 && main.Replacement.ParameterMappings[0].Target=="IFC_SourceId","Confirmed line creation request carries parameter mappings: "+entry.Item1);
                    Assert(main.Replacement.SystemTypeId==(entry.Item1=="Pipe"?20:0),"CT and Conduit need no System Type when carrying mappings");
                }catch(Exception ex){failure=ex;}finally{main.Close();}
            }),DispatcherPriority.ApplicationIdle);
            main.ShowDialog();if(failure!=null)throw failure;
        }
    }
    private static void TestParameterCreationCleanup()
    {
        var main=new IFCInfoWindow();int created=0;
        main.CreateParameter=(n,t,g,b)=>created++;
        var panel=typeof(IFCInfoWindow).GetField("parameterPanel",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);
        var pairs=(System.Collections.IList)panel.GetType().GetField("mappings",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(panel);
        var draft=new IfcParameterMapping{Source="IFC GUID"};pairs.Add(draft);
        var dialog=(Window)panel.GetType().GetMethod("BuildParameterDialog",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,null);
        Descendants(dialog).OfType<TextBox>().Single(t=>t.Name=="CreatedParameterName").Text="New manual parameter";
        Descendants(dialog).OfType<Button>().Single(b=>Equals(b.Content,"Create")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(created==1 && !pairs.Cast<IfcParameterMapping>().Any(m=>string.IsNullOrWhiteSpace(m.Source)&&string.IsNullOrWhiteSpace(m.Target)),"Successful manual parameter creation removes placeholder rows");
        Assert(pairs.Contains(draft),"Placeholder cleanup preserves incomplete source mapping drafts");
        main.Close();
    }
    private static void TestSourceRefreshAndPipeParameters()
    {
        long duct=(long)Autodesk.Revit.DB.BuiltInCategory.OST_DuctCurves,pipe=(long)Autodesk.Revit.DB.BuiltInCategory.OST_PipeCurves;
        int refreshes=0,loads=0;
        var main=new IFCInfoWindow{Links=new List<LinkOption>{new LinkOption{Id=1,Name="Refresh.ifc",IsLoaded=true}},Opacity=0,ShowInTaskbar=false};
        main.RefreshLinks=()=>{refreshes++;return new List<LinkOption>{new LinkOption{Id=1,Name="Refreshed.ifc",IsLoaded=true},new LinkOption{Id=2,Name="New.ifc",IsLoaded=true}};};
        main.LoadLink=link=> {
            loads++;main.Categories=new List<CategoryOption>{new CategoryOption{Id=duct,Name="Ducts",Kind="Line"},new CategoryOption{Id=pipe,Name="Pipes",Kind="Line"}};
            main.LoadCategory=category=> {main.CanCreateDucts=category.Id==duct;main.CanReplaceCategory=true;main.AirTerminalCount=1;main.AirTerminals=new List<AirTerminalRow>{new AirTerminalRow{ElementId="101",Name="Source"}};};
        };
        Exception failure=null;
        main.Loaded+=(s,e)=>main.Dispatcher.BeginInvoke(new Action(()=> {
            try {
                var workflow=Descendants(main).OfType<TabControl>().Single(t=>t.Name=="WorkflowTabs");
                var links=Descendants(main).OfType<ComboBox>().Single(c=>Equals(c.Tag,"Select IFC link..."));links.SelectedIndex=0;
                main.UpdateLayout();
                var refresh=Descendants(main).OfType<Button>().Single(b=>b.Name=="RefreshAllSources");
                var sourceSurface=(FrameworkElement)((TabItem)workflow.Items[0]).Content;
                var refreshPosition=refresh.TransformToAncestor(sourceSurface).Transform(new Point());
                var linkPosition=links.TransformToAncestor(sourceSurface).Transform(new Point());
                Assert(refreshPosition.Y<linkPosition.Y && refreshPosition.X>linkPosition.X+links.ActualWidth/2,"Source refresh is a normal button at the top right above the link picker");
                foreach(var c in main.Categories)c.IsChecked=true;
                Descendants(main).OfType<Button>().Single(b=>Equals(b.Content,"Continue →")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var panel=typeof(IFCInfoWindow).GetField("parameterPanel",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);
                var pairs=(System.Collections.IList)panel.GetType().GetField("mappings",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(panel);
                pairs.Add(new IfcParameterMapping{Source="IFC GUID",Target="Duct draft"});
                Descendants(main).OfType<ComboBox>().Single(c=>c.Name=="ReviewCategoryPicker").SelectedItem=main.ReviewCategories.Single(c=>c.Id==pipe);
                workflow.SelectedIndex=2;
                Assert(ReferenceEquals(((TabItem)workflow.Items[2]).Content,panel) && ((UIElement)panel).IsEnabled,"Pipe uses the enabled parameter editor with its own mappings");
                workflow.SelectedIndex=1;
                Assert(Descendants(main).OfType<Button>().Any(b=>Equals(b.Content,"Next: Parameters →") && b.IsVisible),"Pipe Elements goes to Parameters before preview and creation");
                workflow.SelectedIndex=0;
                Descendants(main).OfType<Button>().Single(b=>b.Name=="RefreshAllSources").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(refreshes==1 && loads==2 && main.Links.Count==2 && main.SelectedLink.Name=="Refreshed.ifc","Source refresh reloads link catalog and selected link data");
                Assert(main.Categories.All(c=>c.IsChecked),"Source refresh retains checked categories still present in the link");
                Descendants(main).OfType<Button>().Single(b=>Equals(b.Content,"Continue →")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(pairs.Cast<IfcParameterMapping>().Any(m=>m.Target=="Duct draft"),"Source refresh retains per-category duct parameter drafts");
                workflow.SelectedIndex=2;
                Assert(ReferenceEquals(((TabItem)workflow.Items[2]).Content,panel) && ((UIElement)panel).IsEnabled,"Returning from Pipe to Duct restores the enabled parameter panel");
            } catch(Exception ex){failure=ex;}finally{main.Close();}
        }),DispatcherPriority.ApplicationIdle);
        main.ShowDialog();if(failure!=null)throw failure;
    }
    private static void TestExpandedFamilyCategories()
    {
        foreach(var entry in new[]{Tuple.Create((long)Autodesk.Revit.DB.BuiltInCategory.OST_MechanicalEquipment,"Mechanical Equipment"),Tuple.Create((long)Autodesk.Revit.DB.BuiltInCategory.OST_DuctTerminal,"Air Terminals"),Tuple.Create((long)Autodesk.Revit.DB.BuiltInCategory.OST_ElectricalFixtures,"Electrical Fixtures"),Tuple.Create((long)Autodesk.Revit.DB.BuiltInCategory.OST_GenericModel,"Generic Models")})
        {
            long category=entry.Item1;
            var matching=new ReplacementTypeOption{Id=100,CategoryId=category,CategoryName=entry.Item2,Kind="Family",Label="Matching family",Supported=true,PlacementMode="OneLevelBased"};
            var other=new ReplacementTypeOption{Id=101,CategoryId=(long)Autodesk.Revit.DB.BuiltInCategory.OST_DuctFitting,Kind="Family",Label="Wrong category",Supported=true};
            var levels=new List<ReplacementLevelOption>{new ReplacementLevelOption{Id=1,Label="Level 1"}};
            var window=new NativePlacementWindow(new List<AirTerminalRow>{new AirTerminalRow{ElementId="1",CategoryId=(long)category,CategoryName=category.ToString()}},
                new List<ReplacementTypeOption>{matching,other},levels,null,(long)category,category.ToString(),(file,id)=>new List<ReplacementTypeOption>());
            var nodes=Descendants(window).OfType<FrameworkElement>().ToList();
            var target=nodes.OfType<ComboBox>().Single(c=>c.Name=="TargetType");
            Assert(target.Items.Count==1 && ((ReplacementTypeOption)target.SelectedItem).CategoryId==(long)category,"Expanded family placement only offers a type in the source category: "+category);
            Assert(nodes.OfType<Button>().Single(b=>b.Name=="CreateNative").IsEnabled,"A loaded compatible family and level allow placement setup: "+category);
            window.Close();
            window=new NativePlacementWindow(new List<AirTerminalRow>{new AirTerminalRow{ElementId="1"}},new List<ReplacementTypeOption>{other},levels,null,(long)category,category.ToString(),(file,id)=>new List<ReplacementTypeOption>());
            nodes=Descendants(window).OfType<FrameworkElement>().ToList();
            Assert(!nodes.OfType<Button>().Single(b=>b.Name=="CreateNative").IsEnabled && nodes.OfType<Button>().Single(b=>b.Name=="LoadPlacementFamily").Visibility==Visibility.Visible,"New family categories require a matching loaded type and offer RFA loading when missing: "+category);
            window.Close();
        }
    }
    private static void TestStep2CategoryColumn()
    {
        var category=(long)Autodesk.Revit.DB.BuiltInCategory.OST_MechanicalEquipment;
        var main=new IFCInfoWindow{AirTerminals=new List<AirTerminalRow>{new AirTerminalRow{ElementId="1",Name="AHU",CategoryId=category,CategoryName="Mechanical Equipment"},new AirTerminalRow{ElementId="2",Name="Pump"}},AirTerminalCount=2};
        typeof(IFCInfoWindow).GetProperty("SelectedCategory").GetSetMethod(true).Invoke(main,new object[]{new CategoryOption{Id=category,Name="Mechanical Equipment",Kind="Family"}});
        var page=(FrameworkElement)typeof(IFCInfoWindow).GetMethod("CountPage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,null);
        var table=Descendants(page).OfType<DataGrid>().Single(t=>t.Name!="IfcPropertyValues");
        Assert(table.Columns.OfType<DataGridTextColumn>().Any(c=>Equals(c.Header,"Category") && ((System.Windows.Data.Binding)c.Binding).Path.Path=="CategoryName"),"Step 2 binds the Category column to per-element category metadata");
        Assert(main.AirTerminals.All(r=>r.CategoryId==category && r.CategoryName=="Mechanical Equipment"),"Rows without category metadata inherit the active source category");
        var search=Descendants(page).OfType<TextBox>().Single(t=>t.Name=="IfcElementSearch");search.Text="Mechanical Equipment";
        Assert(table.Items.Count==2,"Step 2 search matches category names without relying on element names");
        search.Text="Lighting Fixtures";Assert(table.Items.Count==0,"Category search excludes unrelated categories");search.Text="";
        var sort=Descendants(page).OfType<Button>().Single(b=>b.Name=="SortSources");sort.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(sort.ContextMenu.Items.OfType<MenuItem>().Any(m=>Equals(m.Header,"Category ↑")),"Step 2 sort menu includes Category");sort.ContextMenu.IsOpen=false;
        Assert(main.SystemTableText().Contains("Element ID\tCategory\tElement") && main.SystemTableText().Contains("Mechanical Equipment"),"Copied element tables include the Category column");
        main.Close();
    }
    private static void TestProductivityTools()
    {
        var used=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"IFC_Code"};
        Assert(ParameterNames.Unique("Code","IFC_",used)=="IFC_Code_2","Prefix resolves existing names without case-sensitive duplicates");
        Assert(!ParameterNames.Unique("A/B","IFC:",used).Contains(":"),"Generated prefixes and names replace invalid characters");
        var preset=new ParameterMappingPreset{Entries=new List<ParameterPresetEntry>{new ParameterPresetEntry{Source="IFC GUID",Target="Saved Guid",DataType="Text",Group="Identity Data",Binding="Instance"}}};
        var restored=ParameterMappingPreset.Read(ParameterMappingPreset.Write(preset));
        Assert(restored.Entries.Single().Target=="Saved Guid" && restored.Entries[0].Group=="Identity Data","Reusable preset round-trips mapping names and settings");
        bool rejected=false;try{ParameterMappingPreset.Read("<!DOCTYPE x [<!ENTITY x SYSTEM 'file:///missing'>]><ParameterMappingPreset/>");}catch(Exception){rejected=true;}
        Assert(rejected,"Mapping presets reject DTD documents");
        var row=new AirTerminalRow{ElementId="42",IfcGuid="guid42",DuctExistence="Exact match",IfcProperties=new List<IfcPropertyValue>{
            new IfcPropertyValue{Scope="Instance",SetName="A",Name="Code",DataType="IFCLABEL",Value="Value A"},
            new IfcPropertyValue{Scope="Type",SetName="B",Name="Code",DataType="IFCLABEL",Value="Value B"}}};
        int transfers=0,creations=0,previews=0;
        var main=new IFCInfoWindow{AirTerminals=new List<AirTerminalRow>{row},CheckParameterTarget=n=>"Ready",LoadParameterTargets=()=>new List<string>{"Saved Guid"}};
        typeof(IFCInfoWindow).GetField("parameterSelection",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(main,new List<AirTerminalRow>{row});
        main.RunDuctCreation=()=>creations++;
        main.TransferParameters=(sources,maps)=>{transfers++;Assert(sources.Single()==row && maps.Single().Target=="Saved Guid","Update existing passes highlighted locked duct sources and the saved mapping");return "Transferred";};
        main.PreviewParameters=(sources,maps)=>{previews++;return new List<ParameterValuePreview>{new ParameterValuePreview{SourceId=sources.Single().ElementId,TargetId="100",RevitParameter=maps.Single().Target,CurrentValue="old",ProposedValue="guid42",Status="Ready"}};};
        var tabs=Descendants(main).OfType<TabControl>().Single(t=>t.Name=="WorkflowTabs");
        var panel=((TabItem)tabs.Items[2]).Content;
        panel.GetType().GetMethod("ApplyMappingPreset",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,new object[]{restored});
        var definitions=(List<ParameterCsv.Row>)panel.GetType().GetMethod("SelectedParameterDefinitions",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,null);
        Assert(definitions.Count==4 && definitions.Where(d=>d.IFCProperty.Contains("Code")).Select(d=>d.ParameterName).Distinct().Count()==2,"Selected IFC CSV preserves qualified names and resolves repeated property names");
        Assert(ParameterCsv.Read(ParameterCsv.Write(definitions)).Count==4,"Exported selected definitions can be imported without editing headers or quoting");
        var preview=(Window)panel.GetType().GetMethod("BuildValuePreview",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,null);
        var values=Descendants(preview).OfType<DataGrid>().Single();
        Assert(values.IsReadOnly && values.Items.Count==1 && previews==1 && transfers==0 && creations==0,"Value preview reads current/proposed values without writing or creating ducts");
        preview.Close();
        var tools=Descendants((DependencyObject)panel).OfType<WrapPanel>().Single(p=>p.Name=="ParameterTools");
        var actions=tools.Children.OfType<Button>().Where(b=>b.ContextMenu!=null).SelectMany(b=>b.ContextMenu.Items.OfType<MenuItem>()).ToList();
        Assert(actions.Count==11 && tools.Children.Count==5 && actions.All(a=>a.ToolTip!=null),"Compact parameter menus retain all actions with hover names");
        actions.Single(a=>Equals(a.Header,"Update existing elements")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Assert(transfers==1 && creations==0,"Update existing writes parameters through its separate action without creating geometry");
        var saved=(ParameterMappingPreset)panel.GetType().GetMethod("MappingPreset",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,null);
        Assert(saved.Entries.Count==1 && saved.Entries[0].Source=="IFC GUID","Saving a loaded preset retains portable source names");
        var invalid=new ParameterMappingPreset{Entries=new List<ParameterPresetEntry>{restored.Entries[0],restored.Entries[0]}};
        rejected=false;try{panel.GetType().GetMethod("ApplyMappingPreset",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,new object[]{invalid});}catch(Exception){rejected=true;}
        Assert(rejected && ((ParameterMappingPreset)panel.GetType().GetMethod("MappingPreset",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,null)).Entries.Count==1,"Invalid presets do not replace existing mappings");
        main.Close();
    }
    private static void TestSourceBrowser()
    {
        long duct=(long)Autodesk.Revit.DB.BuiltInCategory.OST_DuctCurves, fitting=(long)Autodesk.Revit.DB.BuiltInCategory.OST_DuctFitting,pipe=(long)Autodesk.Revit.DB.BuiltInCategory.OST_PipeCurves;
        int loads=0;
        var main=new IFCInfoWindow{Links=new List<LinkOption>{new LinkOption{Id=1,Name="Browser.ifc",IsLoaded=true}},Opacity=0,ShowInTaskbar=false};
        Action<IFCInfoWindow> configure=target=>target.LoadLink=link=> {
            target.Categories=new List<CategoryOption>{new CategoryOption{Id=duct,Name="Ducts",Kind="Line",Count=3117},new CategoryOption{Id=fitting,Name="Duct Fittings",Kind="Family",Count=2460},new CategoryOption{Id=pipe,Name="Pipes",Kind="Line",Count=12}};
            target.CheckParameterTarget=n=>"Ready";
            target.LoadCategory=category=>{loads++;target.CanCreateDucts=category.Id==duct;target.CanReplaceCategory=true;target.AirTerminalCount=2;
                target.AirTerminals=new List<AirTerminalRow>{new AirTerminalRow{ElementId=category.Id==duct?"10":"20",Name="First element"},new AirTerminalRow{ElementId=category.Id==duct?"11":"21",Name="Second element"}};};
        };
        configure(main);CategoryReviewSession savedSession=null;
        Exception failure=null;
        main.Loaded+=(s,e)=>main.Dispatcher.BeginInvoke(new Action(()=> {
            try
            {
                Descendants(main).OfType<ComboBox>().Single(c=>Equals(c.Tag,"Select IFC link...")).SelectedIndex=0;
                var tabs=Descendants(main).OfType<TabControl>().Single(t=>t.Name=="SourceCategoryKinds");
                var line=(DataGrid)((TabItem)tabs.Items[0]).Content;var family=(DataGrid)((TabItem)tabs.Items[1]).Content;
                Assert(line.Items.Count==2 && family.Items.Count==1 && loads==0,"Selecting a link lists categories, not thousands of individual elements");
                Assert(line.Columns.Count==3 && line.Columns[0] is DataGridTemplateColumn && ((CategoryOption)line.Items[0]).Count==3117,"Category tables include a checkbox before category name and count");
                Assert(((TabItem)tabs.Items[0]).Header.ToString().Contains("(2)"),"Group counts represent categories");
                main.Width=900;main.Height=540;main.UpdateLayout();main.Dispatcher.Invoke(new Action(()=>{}),DispatcherPriority.ContextIdle);main.UpdateLayout();
                var workflow=Descendants(main).OfType<TabControl>().Single(t=>t.Name=="WorkflowTabs");
                var sourceSurface=(FrameworkElement)((TabItem)workflow.Items[0]).Content;
                var linkBox=Descendants(main).OfType<ComboBox>().Single(c=>Equals(c.Tag,"Select IFC link..."));
                var frame=(FrameworkElement)main.Content;
                var next=Descendants(main).OfType<Button>().Single(b=>Equals(b.Content,"Continue →"));
                Assert(linkBox.TransformToAncestor(sourceSurface).Transform(new Point()).Y>=0 && linkBox.IsVisible,"Link picker remains visible at minimum window height");
                Assert(line.ActualHeight>=65 && line.TransformToAncestor(sourceSurface).Transform(new Point(0,line.ActualHeight)).Y<=sourceSurface.ActualHeight+1,"Category list uses available height without extending outside Source");
                Assert(next.TransformToAncestor(frame).Transform(new Point(0,next.ActualHeight)).Y<=frame.ActualHeight+1,"Continue remains inside the window at minimum height");
                ((System.Windows.Data.ListCollectionView)line.ItemsSource).SortDescriptions.Clear();
                ((System.Windows.Data.ListCollectionView)line.ItemsSource).SortDescriptions.Add(new System.ComponentModel.SortDescription("Count",System.ComponentModel.ListSortDirection.Ascending));
                Assert(((CategoryOption)line.Items[0]).Name=="Pipes","Category element counts sort numerically");
                var search=Descendants(main).OfType<TextBox>().Single(t=>t.Name=="SourceCategorySearch");search.Text="Duct";
                Assert(line.Items.Count==1 && family.Items.Count==1,"Category name filter applies to both category groups");
                ((CategoryOption)line.Items[0]).IsChecked=true;
                Assert(next.IsEnabled,"Checking a category enables Continue");
                search.Text="Pipe";
                Assert(next.IsEnabled && main.Categories.Single(c=>c.Id==duct).IsChecked,"Filtered-out category checks remain selected");
                search.Text="";tabs.SelectedIndex=1;((CategoryOption)family.Items[0]).IsChecked=true;
                Assert(main.Categories.Count(c=>c.IsChecked)==2,"Line and Family categories can be checked together");
                next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(loads==1 && main.ReviewCategories.Count==2 && main.SelectedCategory.Id==duct && main.AirTerminals.All(r=>!r.IsSelected),"All checked categories reach Step 2, starting with a line category");
                main.AirTerminals[0].IsSelected=true;
                var panel=((TabItem)workflow.Items[2]).Content;
                var pairs=(System.Collections.IList)panel.GetType().GetField("mappings",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(panel);
                pairs.Add(new IfcParameterMapping{Source="IFC Element ID",Target="Duct ID"});
                Descendants(main).OfType<ComboBox>().Single(c=>c.Name=="ReviewCategoryPicker").SelectedItem=main.ReviewCategories.Single(c=>c.Id==fitting);
                Assert(main.SelectedCategory.Id==fitting && !main.CanCreateDucts && main.AirTerminals.All(r=>!r.IsSelected),"Switching review category routes to the family builder without leaking element checks");
                main.AirTerminals[0].IsSelected=true;pairs.Add(new IfcParameterMapping{Source="IFC Element ID"});
                Descendants(main).OfType<ComboBox>().Single(c=>c.Name=="ReviewCategoryPicker").SelectedItem=main.ReviewCategories.Single(c=>c.Id==duct);
                Assert(main.AirTerminals.Single(r=>r.IsSelected).ElementId=="10" && pairs.Cast<IfcParameterMapping>().Any(m=>m.Target=="Duct ID"),"Switching back restores duct element checks and parameter mappings");
                Descendants(main).OfType<ComboBox>().Single(c=>c.Name=="ReviewCategoryPicker").SelectedItem=main.ReviewCategories.Single(c=>c.Id==fitting);
                Assert(main.AirTerminals.Single(r=>r.IsSelected).ElementId=="20" && pairs.Cast<IfcParameterMapping>().Any(m=>m.Source=="IFC Element ID" && m.Target==null),"Family checks and unfinished mapping rows are kept separately");
                savedSession=main.ExportReviewSession();
            }
            catch(Exception ex){failure=ex;}
            finally{main.Close();}
        }),DispatcherPriority.ApplicationIdle);
        main.ShowDialog();if(failure!=null)throw failure;
        var resumed=new IFCInfoWindow{Links=new List<LinkOption>{new LinkOption{Id=1,Name="Browser.ifc",IsLoaded=true}},InitialLinkId=1,InitialCategoryId=fitting,InitialReview=savedSession,Opacity=0,ShowInTaskbar=false};configure(resumed);
        resumed.Loaded+=(s,e)=>resumed.Dispatcher.BeginInvoke(new Action(()=> {
            try{Assert(resumed.ReviewCategories.Count==2 && resumed.SelectedCategory.Id==fitting && resumed.AirTerminals.Single(r=>r.IsSelected).ElementId=="20","Reopened sessions retain the checked category queue and active element checks");}
            catch(Exception ex){failure=ex;}finally{resumed.Close();}
        }),DispatcherPriority.ApplicationIdle);
        resumed.ShowDialog();if(failure!=null)throw failure;
    }
    private static void TestBulkParameterCreation()
    {
        var csv=ParameterCsv.Read("\uFEFFIFCProperty,ParameterName,DataType\r\n\"Type | Set, A | Code\",\"Code \"\"A\"\"\",Text\r\n,Standalone,Integer\r\n");
        Assert(csv.Count==2 && csv[0].IFCProperty=="Type | Set, A | Code" && csv[0].ParameterName=="Code \"A\"","CSV supports BOM, commas and escaped quotes");
        Assert(ParameterCsv.Read("IFCProperty,ParameterName\nIFC GUID,Guid")[0].DataType=="","CSV data type is optional when inferred from IFC");
        foreach(var invalid in new[]{"Source,Target\na,b","IFCProperty,ParameterName\n\"unclosed,x","IFCProperty,ParameterName\na,b,c","IFCProperty,ParameterName,ParameterName\na,b,c"})
        {
            bool failed=false;try{ParameterCsv.Read(invalid);}catch(FormatException){failed=true;}
            Assert(failed,"Invalid CSV must fail before opening a creation preview");
        }
        var row=new AirTerminalRow{ElementId="123",IfcGuid="guid",IsSelected=true,IfcProperties=new List<IfcPropertyValue>{
            new IfcPropertyValue{Scope="Type",SetName="Pset",Name="FamilyName",Value="Duct",DataType="IFCLABEL"},
            new IfcPropertyValue{Scope="Instance",SetName="Pset",Name="Unknown",Value="?"}}};
        var textType=new ParameterOption{Id="autodesk.spec:spec.string-2.0.0",Name="Text",DataKind="text",Discipline="Common"};
        var integerType=new ParameterOption{Id="Integer",Name="Integer",DataKind="integer",Discipline="Common"};
        var catalog=new List<ParameterOption>();int created=0;
        var main=new IFCInfoWindow{AirTerminals=new List<AirTerminalRow>{row},
            ParameterTypes=()=>new List<ParameterOption>{textType,integerType},ProjectParameters=()=>catalog,
            LoadParameterTargets=()=>catalog.Select(p=>p.Name).ToList(),CheckParameterTarget=n=>catalog.Any(p=>p.Name==n)?"Ready":"Missing"};
        main.CreateParameter=(name,type,group,binding)=>{created++;catalog.Add(new ParameterOption{Name=name,Id=type,DataKind=type==integerType.Id?"integer":"text",Binding=binding,Discipline=type==integerType.Id?"Integer":"Text"});};
        var tabs=Descendants(main).OfType<TabControl>().Single(t=>t.Name=="WorkflowTabs");
        var panel=((TabItem)tabs.Items[2]).Content;
        var build=panel.GetType().GetMethod("BuildBulkParameterDialog",BindingFlags.NonPublic|BindingFlags.Instance);
        var preview=(Window)build.Invoke(panel,new object[]{null});
        var table=Descendants(preview).OfType<DataGrid>().Single(t=>t.Name=="BulkIfcParameters");
        var plans=table.Items.Cast<object>().ToList();
        Func<object,string,object> value=(p,n)=>p.GetType().GetProperty(n).GetValue(p);
        Assert(plans.Count==4 && plans.Count(p=>(bool)value(p,"Include"))==3,"Bulk preview infers known IFC types and excludes unknown types");
        var family=plans.Single(p=>(string)value(p,"Name")=="FamilyName");
        Assert((string)value(family,"TypeId")==textType.Id,"Real Revit Text spec ID resolves from data kind rather than a name substring");
        var single=(Window)panel.GetType().GetMethod("BuildParameterDialog",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(panel,null);
        Descendants(single).OfType<ComboBox>().Single(c=>c.Name=="ParameterCreationMode").SelectedIndex=1;
        Descendants(single).OfType<ComboBox>().Single(c=>c.Name=="CreationIfcProperty").SelectedItem=IfcParameterMapping.Key(row.IfcProperties[0]);
        Assert(((ParameterOption)Descendants(single).OfType<ComboBox>().Single(c=>c.Name=="CreatedParameterType").SelectedItem)?.Id==textType.Id,"Single Follow IFC creation also recognizes the real Revit Text spec ID");
        single.Close();
        var submit=Descendants(preview).OfType<Button>().Single(b=>Equals(b.Content,"Create selected"));
        submit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var mappings=(System.Collections.IList)panel.GetType().GetField("mappings",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(panel);
        Assert(created==3 && mappings.Cast<IfcParameterMapping>().Count(m=>!string.IsNullOrWhiteSpace(m.Target))==3,"Bulk creation creates and maps selected IFC properties");
        var request=new DuctRequest{Items=new List<DuctPlanItem>{new DuctPlanItem{Source=row}}};
        Assert((bool)panel.GetType().GetMethod("Validate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(panel,new object[]{request}) && request.ParameterMappings.Count==3,"Bulk-created parameters enter the duct request");
        Descendants(preview).OfType<Button>().Single(b=>Equals(b.Content,"Select all")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(plans.All(p=>!(bool)value(p,"Include")),"Select all does not repeat completed creations or select unknown data types");
        preview.Close();
        var imported=ParameterCsv.Read("IFCProperty,ParameterName,DataType\nIFC GUID,IFC GUID,Text\n,Standalone,Integer");
        preview=(Window)build.Invoke(panel,new object[]{imported});
        Descendants(preview).OfType<Button>().Single(b=>Equals(b.Content,"Create selected")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(created==4 && catalog.Any(p=>p.Name=="Standalone"),"CSV reuses an existing compatible target and creates an unmapped standalone parameter");
        preview.Close();
        preview=(Window)build.Invoke(panel,new object[]{new List<ParameterCsv.Row>{new ParameterCsv.Row{IFCProperty="IFC GUID",ParameterName="Conflict"},new ParameterCsv.Row{IFCProperty="IFC Element ID",ParameterName="Conflict"}}});
        table=Descendants(preview).OfType<DataGrid>().Single();
        Assert(table.Items.Cast<object>().All(p=>!(bool)value(p,"Include")),"Duplicate target names start unchecked for review");
        Descendants(preview).OfType<Button>().Single(b=>Equals(b.Content,"Select all")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Descendants(preview).OfType<Button>().Single(b=>Equals(b.Content,"Create selected")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(created==4,"Duplicate CSV targets are rejected before creating any parameters");
        preview.Close();
        preview=(Window)build.Invoke(panel,new object[]{new List<ParameterCsv.Row>{
            new ParameterCsv.Row{IFCProperty="IFC GUID",ParameterName="Wrong type",DataType="Integer"},
            new ParameterCsv.Row{IFCProperty="Not in selected IFC",ParameterName="Wrong source",DataType="Text"}}});
        table=Descendants(preview).OfType<DataGrid>().Single();
        Assert(table.Items.Cast<object>().All(p=>!(bool)value(p,"Include")),"CSV mismatches and unknown sources are excluded from automatic creation");
        foreach(var item in table.Items.Cast<object>())item.GetType().GetProperty("Include").SetValue(item,true);
        Descendants(preview).OfType<Button>().Single(b=>Equals(b.Content,"Create selected")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(created==4,"Manually selecting invalid CSV rows cannot bypass data type and source checks");
        preview.Close();main.Close();
    }
    private static void TestParameterMapping()
    {
        var suggest=typeof(IFCInfoWindow).Assembly.GetType("IFCInfo.ParameterMappingPanel").GetMethod("SuggestedType",BindingFlags.Static|BindingFlags.NonPublic);
        Assert((string)suggest.Invoke(null,new object[]{"IFCLENGTHMEASURE"})=="length","IFC length suggests Revit length");
        Assert((string)suggest.Invoke(null,new object[]{"IFCVOLUMETRICFLOWRATEMEASURE"})=="airFlow","IFC flow suggests Revit air flow");
        Assert((string)suggest.Invoke(null,new object[]{"IFC identifier (Text)"})=="text","IFC GUID suggests Text");
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
        var validateNext=panel.GetType().GetMethod("ValidateNext",BindingFlags.NonPublic|BindingFlags.Instance);
        Assert((bool)validateNext.Invoke(panel,null),"Default blank parameter row must allow continuing without parameters");
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
        Assert(choices.Count==0,"Parameter catalog excludes unchecked sources");
        row.IsSelected=true;panel.GetType().GetMethod("Refresh",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(panel,null);
        Assert(choices.Count==5,"Parameter catalog includes GUID and element ID");
        var creation=(Window)panel.GetType().GetMethod("BuildParameterDialog",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(panel,null);
        var mode=Descendants(creation).OfType<ComboBox>().Single(c=>c.Name=="ParameterCreationMode");
        var createSource=Descendants(creation).OfType<ComboBox>().Single(c=>c.Name=="CreationIfcProperty");
        var createType=Descendants(creation).OfType<ComboBox>().Single(c=>c.Name=="CreatedParameterType");
        var createName=Descendants(creation).OfType<TextBox>().Single(c=>c.Name=="CreatedParameterName");
        Assert(mode.SelectedIndex==0 && createType.IsEnabled && !createName.IsReadOnly,"Manual creation permits name and type edits");
        mode.SelectedIndex=1;createSource.SelectedItem="IFC GUID";
        Assert(createName.Text=="IFC GUID" && createName.IsReadOnly && !createType.IsEnabled && ((ParameterOption)createType.SelectedItem).Id=="Text","Follow IFC locks inferred GUID name and Text type");
        createSource.SelectedItem="IFC Element ID";
        Assert(((ParameterOption)createType.SelectedItem).Id=="Integer","Follow IFC refreshes type when source changes");
        mode.SelectedIndex=0;
        Assert(createType.IsEnabled && !createName.IsReadOnly,"Switching to Manual unlocks inferred settings");
        var binding=Descendants(creation).OfType<ComboBox>().Single(c=>c.Name=="CreatedParameterBinding");
        Assert(binding.SelectedItem as string=="Instance" && binding.Items.Count==2,"Parameter creation offers Instance and Type");
        binding.SelectedItem="Type";
        Assert(binding.IsEnabled && Descendants(creation).OfType<TextBlock>().Any(t=>t.Text.Contains("existing elements")),"Type binding remains editable and explains shared type effects");
        creation.Close();
        var grid=(DataGrid)panel.GetType().GetField("grid",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(panel);
        Assert(grid.Columns.Count==5 && grid.Columns.OfType<DataGridTemplateColumn>().Count()==3,"Parameter table must have two choice columns and a mapping button");
        mappings.Clear();
        var firstPair=new IfcParameterMapping {Source="Length",Target="First"};mappings.Add(firstPair);
        var duplicatePair=new IfcParameterMapping();mappings.Add(duplicatePair);
        duplicatePair.Source="Length";Assert(duplicatePair.Source==null,"Duplicate IFC source is rejected immediately");
        var available=panel.GetType().GetMethod("AvailableChoices",BindingFlags.NonPublic|BindingFlags.Instance);
        var sourceOptions=(List<string>)available.Invoke(panel,new object[]{duplicatePair,"Source",new[]{"Length","IFC GUID"}});
        Assert(!sourceOptions.Contains("Length") && sourceOptions.Contains("IFC GUID"),"Used IFC sources disappear from other rows");
        var targetOptions=(List<string>)available.Invoke(panel,new object[]{duplicatePair,"Target",new[]{"First","Distance parameter"}});
        Assert(!targetOptions.Contains("First") && targetOptions.Contains("Distance parameter"),"Used Revit targets are hidden while unused types remain visible");
        duplicatePair.Source="IFC GUID";duplicatePair.Target="First";
        Assert(duplicatePair.Target==null,"Duplicate Revit target is rejected immediately");
        mappings.Clear();row.IfcGuid="test-guid";
        var live=new IfcParameterMapping();mappings.Add(live);
        live.Source="IFC GUID";live.CreateMissing=true;live.Target="Live Guid";live.CreateMissing=true;
        var ready=panel.GetType().GetProperty("Ready",BindingFlags.NonPublic|BindingFlags.Instance);
        Assert((bool)ready.GetValue(panel),"Completed pairs are ready without the removed Map button");
        var status=(TextBlock)panel.GetType().GetField("status",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(panel);
        Assert(status.Text.Contains("will be assigned"),"Status explains actual parameter assignment instead of stale zero mappings");
        mappings.Clear();
        mappings.Add(new IfcParameterMapping {Source="IFC GUID",Target="IFC Guid",CreateMissing=true});
        mappings.Add(new IfcParameterMapping {Source="Length",Target="IFC Length",CreateMissing=true});
        Assert((bool)validate.Invoke(panel,new object[]{request}) && request.ParameterMappings.Count==2,"One duct receives multiple selected parameter pairs without Map");
        Assert(IfcParameterMapping.Read(row,"IFC GUID")=="test-guid","GUID is a built-in mapping source");
        var second=new AirTerminalRow {ElementId="second",IfcGuid="second-guid",IsSelected=true,IfcProperties=new List<IfcPropertyValue>{new IfcPropertyValue{Name="Length",Value="250",Unit="mm"}}};
        window.AirTerminals.Add(second);row.IsSelected=false;
        panel.GetType().GetMethod("Refresh",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(panel,null);
        var secondRequest=new DuctRequest {Items=new List<DuctPlanItem>{new DuctPlanItem{Source=second}}};
        Assert((bool)validate.Invoke(panel,new object[]{secondRequest}) && secondRequest.ParameterMappings.Count==2 && secondRequest.ParameterMappings.All(m=>m.Applies(second)),"Existing parameter pairs follow changed Elements selection");
        Assert(IfcParameterMapping.Read(second,"IFC GUID")=="second-guid" && IfcParameterMapping.Read(second,"Length")=="250 mm","Each selected duct retains its own IFC values");
        mappings.Clear();window.CheckParameterTarget=n=>n=="Unavailable"?"Parameter is not bound to Ducts":"Ready";
        window.ProjectParameters=()=>new List<ParameterOption>{
          new ParameterOption{Name="ID Integer",DataKind="integer",Discipline="Integer"},
          new ParameterOption{Name="ID Text",DataKind="text",Discipline="Text"},
          new ParameterOption{Name="Distance",DataKind="length",Discipline="Length"},
          new ParameterOption{Name="Unavailable",DataKind="integer",Discipline="Integer"}};
        var typed=new IfcParameterMapping{Source="IFC Element ID"};mappings.Add(typed);
        var filtered=(List<string>)available.Invoke(panel,new object[]{typed,"Target",new[]{"ID Integer","ID Text","Distance","Unavailable"}});
        Assert(filtered.SequenceEqual(new[]{"ID Integer"}),"Integer IFC property offers only writable Integer targets");
        typed.Target="ID Integer";typed.Source="IFC GUID";
        Assert(typed.Target==null,"Changing IFC type clears incompatible selected target");
        filtered=(List<string>)available.Invoke(panel,new object[]{typed,"Target",new[]{"ID Integer","ID Text","Distance"}});
        Assert(filtered.SequenceEqual(new[]{"ID Text"}),"GUID offers Text targets and excludes numeric types");
        var addDialog=(Window)panel.GetType().GetMethod("BuildAddParametersDialog",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(panel,null);
        var addList=Descendants(addDialog).OfType<ListBox>().Single(x=>x.Name=="AvailableIfcParameters");
        var addSelected=Descendants(addDialog).OfType<Button>().Single(b=>Equals(b.Content,"Add selected parameters"));
        Assert(!addSelected.IsEnabled && !addList.Items.Contains("IFC GUID"),"Parameter picker excludes already used properties and disables empty submission");
        Descendants(addDialog).OfType<Button>().Single(b=>Equals(b.Content,"Select all")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        int availableCount=addList.Items.Count;
        Assert(availableCount>1 && addList.SelectedItems.Count==availableCount && addSelected.IsEnabled,"Select all selects every available IFC property");
        Descendants(addDialog).OfType<Button>().Single(b=>Equals(b.Content,"Clear selection")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(addList.SelectedItems.Count==0 && !addSelected.IsEnabled,"Clear selection clears the parameter picker");
        Descendants(addDialog).OfType<Button>().Single(b=>Equals(b.Content,"Select all")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        addSelected.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(mappings.Count==availableCount+1 && mappings.Cast<IfcParameterMapping>().Select(m=>m.Source).Distinct().Count()==mappings.Count,"Adding all parameters preserves existing rows without duplicate properties");
        Assert((bool)validateNext.Invoke(panel,null) && (bool)validate.Invoke(panel,new object[]{request}) && request.ParameterMappings.Count==0,"Bulk-added source-only parameters do not block creation or transfer values");
        Assert(status.Text.Contains("will be skipped") && !status.Text.Contains("Row 1:"),"Draft rows display one informative summary instead of repeated errors");
        typed.Target="ID Text";
        Assert((bool)validate.Invoke(panel,new object[]{request}) && request.ParameterMappings.Count==1 && request.ParameterMappings[0].Source=="IFC GUID","Only complete pairs are transferred alongside unfinished drafts");
        mappings.Clear();
        request.ParameterMappings.Add(new IfcParameterMapping {Source="stale",Target="stale"});
        Assert((bool)validateNext.Invoke(panel,null) && (bool)validate.Invoke(panel,new object[]{request}) && request.ParameterMappings.Count==0,"No parameter rows allow creation and clear stale request mappings");
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
            if(args.Length==2 && args[0]=="--render-service-slope"){RenderServiceAndSlope(Path.GetFullPath(args[1]));Console.WriteLine("Rendered four service/slope previews without showing windows.");return 0;}
            if(args.Length==2 && args[0]=="--render-settings") {
                TestUnifiedSettings(Path.GetFullPath(args[1]));Console.WriteLine("PASS: "+checks+" layout checks; rendered without showing windows.");return 0;
            }
            if(args.Length==1 && args[0]=="--test-curves") {
                TestCurveGroupModes();TestCurveLevels();TestUnifiedSettings();TestServicesAndSlope();
                Console.WriteLine("PASS: "+checks+" curve checks; no windows shown.");return 0;
            }
            if (args.Length == 1 && args[0] == "--test")
            {
                TestCurveGroupModes();TestExistingLineParameterTasks();TestLineParameterRequests();TestParameterCreationCleanup();TestSourceRefreshAndPipeParameters();TestExpandedFamilyCategories();TestStep2CategoryColumn();TestSourceBrowser(); TestProductivityTools(); TestBulkParameterCreation(); TestParameterMapping(); TestSelection(false);
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
            ((TabItem)previewTabs.Items[3]).IsEnabled=true;
            var tabEditor=Settings(tabItems,false,Choices());
            tabPreview.OpenDuctSettings(tabEditor,r=>{});
            Render(tabPreview,output,"duct-settings-tabs.png",1500,850);
            previewTabs.SelectedIndex=4;Render(tabPreview,output,"duct-final-main-tab.png",1500,850);previewTabs.SelectedIndex=3;
            tabPreview.AirTerminals[0].IfcProperties=new List<IfcPropertyValue> {
                new IfcPropertyValue {Scope="Instance",SetName="Pset_DuctSegmentCommon",Name="Reference",Value="D-001",DataType="IFCLABEL"},
                new IfcPropertyValue {Scope="Instance",SetName="Dimensions",Name="Width",Value="500",Unit="mm",DataType="IFCLENGTHMEASURE"}};
            tabPreview.LoadParameterTargets=()=>new List<string>{"IFC Reference","IFC Width"};
            tabPreview.CheckParameterTarget=n=>"Ready";
            ((TabItem)previewTabs.Items[2]).IsSelected=true;
            var parameterBody=((TabItem)previewTabs.Items[2]).Content;
            var previewMappings=(System.Collections.IList)parameterBody.GetType().GetField("mappings",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(parameterBody);
            parameterBody.GetType().GetMethod("Refresh",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(parameterBody,null);
            previewMappings.Clear();
            previewMappings.Add(new IfcParameterMapping {Source=IfcParameterMapping.Key(tabPreview.AirTerminals[0].IfcProperties[0]),Target="IFC Reference",IsMapped=true});
            previewMappings.Add(new IfcParameterMapping {Source=IfcParameterMapping.Key(tabPreview.AirTerminals[0].IfcProperties[1]),Target="IFC Width"});
            parameterBody.GetType().GetMethod("Validate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(parameterBody,new object[]{null});
            Render(tabPreview,output,"parameters.png",1500,850);
            tabPreview.PreviewParameters=(rows,maps)=>new List<ParameterValuePreview>{new ParameterValuePreview{SourceId="304421",TargetId="600101",IFCProperty="Instance | Pset_DuctSegmentCommon | Reference",RevitParameter="IFC Reference",CurrentValue="D-000",ProposedValue="D-001",Status="Ready to update existing duct"}};
            var valuePreview=(Window)parameterBody.GetType().GetMethod("BuildValuePreview",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(parameterBody,null);
            Render(valuePreview,output,"parameter-values.png",1150,600);valuePreview.Close();
            var bulkPreview=(Window)parameterBody.GetType().GetMethod("BuildBulkParameterDialog",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(parameterBody,new object[]{null});
            Render(bulkPreview,output,"bulk-parameters.png",1050,650);bulkPreview.Close();
            tabPreview.Close();tabEditor.Close();
            var sourceWindow = new IFCInfoWindow{Links=new List<LinkOption>{new LinkOption{Id=1,Name="MEP-model.ifc",IsLoaded=true}},InitialLinkId=1};
            sourceWindow.LoadLink=link=> {
                sourceWindow.Categories=new List<CategoryOption>{
                    new CategoryOption{Id=1,Name="Ducts",Kind="Line",Count=3117},
                    new CategoryOption{Id=2,Name="Duct Fittings",Kind="Family",Count=2460},
                    new CategoryOption{Id=3,Name="Pipes",Kind="Line",Count=1250},
                    new CategoryOption{Id=4,Name="Cable Trays",Kind="Line",Count=96},
                    new CategoryOption{Id=(long)Autodesk.Revit.DB.BuiltInCategory.OST_MechanicalEquipment,Name="Mechanical Equipment",Kind="Family",Count=24},
                    new CategoryOption{Id=(long)Autodesk.Revit.DB.BuiltInCategory.OST_DuctTerminal,Name="Air Terminals",Kind="Family",Count=180},
                    new CategoryOption{Id=(long)Autodesk.Revit.DB.BuiltInCategory.OST_PipeFitting,Name="Pipe Fittings",Kind="Family",Count=120},
                    new CategoryOption{Id=(long)Autodesk.Revit.DB.BuiltInCategory.OST_LightingFixtures,Name="Lighting Fixtures",Kind="Family",Count=86},
                    new CategoryOption{Id=(long)Autodesk.Revit.DB.BuiltInCategory.OST_GenericModel,Name="Generic Models",Kind="Family",Count=16}};
            };
            sourceWindow.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            sourceWindow.Categories[0].IsChecked=true;sourceWindow.Categories[1].IsChecked=true;
            Render(sourceWindow, output, "ifc-source.png", 1300, 850);
            Render(sourceWindow, output, "ifc-source-short.png",900,540);
            Render(sourceWindow, output, "ifc-source-wide-short.png",1760,650);
            var sourceKindTabs=Descendants(sourceWindow).OfType<TabControl>().Single(t=>t.Name=="SourceCategoryKinds");sourceKindTabs.SelectedIndex=1;
            Render(sourceWindow,output,"ifc-source-family-categories.png",1300,850);sourceKindTabs.SelectedIndex=0;
            typeof(IFCInfoWindow).GetProperty("ReviewCategories").GetSetMethod(true).Invoke(sourceWindow,new object[]{sourceWindow.Categories.Where(c=>c.IsChecked).ToList()});
            typeof(IFCInfoWindow).GetProperty("SelectedCategory").GetSetMethod(true).Invoke(sourceWindow,new object[]{sourceWindow.Categories[0]});
            sourceWindow.CanCreateDucts=true;sourceWindow.AirTerminalCount=2;
            sourceWindow.AirTerminals=new List<AirTerminalRow>{new AirTerminalRow{ElementId="301206",Name="Round duct",SystemType="LT_Abluft",SystemName="EA-01",IsSelected=true},new AirTerminalRow{ElementId="301207",Name="Rectangular duct",SystemType="Supply Air",SystemName="SA-01"}};
            var sourceWorkflow=Descendants(sourceWindow).OfType<TabControl>().Single(t=>t.Name=="WorkflowTabs");
            ((TabItem)sourceWorkflow.Items[1]).Content=new ScrollViewer{Content=(FrameworkElement)typeof(IFCInfoWindow).GetMethod("CountPage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sourceWindow,null),VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            sourceWorkflow.SelectedIndex=1;Render(sourceWindow,output,"ifc-review-category-queue.png",1300,850);
            sourceWindow.Close();
            foreach(string kind in new[] { "Pipe","CableTray" })
            {
                long category=kind=="Pipe"?(long)Autodesk.Revit.DB.BuiltInCategory.OST_PipeCurves:(long)Autodesk.Revit.DB.BuiltInCategory.OST_CableTray;
                var curveWindow=new CurveCreationWindow(new List<AirTerminalRow> { new AirTerminalRow { ElementId="123",Name=kind+" IFC",SystemType="Supply",Elevation="3200" } },
                    new List<ReplacementTypeOption> { new ReplacementTypeOption { Id=1,CategoryId=category,Kind=kind,Supported=true,Label=kind+" Type" } },
                    new List<ReplacementLevelOption> { new ReplacementLevelOption { Id=2,Label="Level 1" } },
                    new List<ReplacementTypeOption> { new ReplacementTypeOption { Id=3,Kind="Pipe",Label="Domestic Cold Water" } },category,kind);
                Render(curveWindow,output,kind+"-step3.png",900,690);
                var confirmation=Descendants(curveWindow).OfType<TabControl>().Single(t=>t.Name=="CurveConfirmationTabs");confirmation.SelectedIndex=1;
                Render(curveWindow,output,kind+"-final-review.png",1100,760);confirmation.SelectedIndex=0;
                Descendants(curveWindow).OfType<ComboBox>().Single(c=>c.Name=="CurveSetupMode").SelectedIndex=1;
                Render(curveWindow,output,kind+"-follow-ifc.png",1100,760);curveWindow.Close();
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
                Descendants(main).OfType<Button>().Single(b=>(b.Content as string)=="Next: Parameters →").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                main.CheckParameterTarget=n=>"Ready";
                var resumeTabs=Descendants(main).OfType<TabControl>().Single(t=>t.Name=="WorkflowTabs");
                var resumePanel=((TabItem)resumeTabs.Items[2]).Content;
                var resumePairs=(System.Collections.IList)resumePanel.GetType().GetField("mappings",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(resumePanel);
                resumePairs.Clear();resumePairs.Add(new IfcParameterMapping{Source="IFC Element ID",Target="Target"});
                Descendants(main).OfType<Button>().Single(b=>(b.Content as string)=="Next: Preview & Create →")
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
            var curve=new CurveCreationWindow(new List<AirTerminalRow> {new AirTerminalRow {ElementId="1",SourceLevelName="L1",SystemType="Water",ServiceType="Power"}},
                new List<ReplacementTypeOption> {type},new List<ReplacementLevelOption> {new ReplacementLevelOption {Id=1,Label="L1"}},
                systems,pair.Item2,pair.Item1) {Opacity=0,ShowInTaskbar=false};
            var curveNodes=Descendants(curve).OfType<FrameworkElement>().ToList();
            Assert(curveNodes.OfType<ComboBox>().All(c=>c.MinHeight>=36),"Curve settings retain readable shared and group field sizes");
            Assert(curveNodes.OfType<ComboBox>().Count(c=>c.Name=="GroupPipeSystem")== (pair.Item1=="Pipe"?1:0),"Only Pipe has a group piping system selector");
            Assert(curveNodes.OfType<TextBox>().Count(c=>c.Name=="GroupServiceType")== (pair.Item1=="CableTray"?1:0),"Cable Tray uses a separate service field instead of a system selector");
            curve.Loaded+=(s,e)=>curve.Dispatcher.BeginInvoke(new Action(()=>{
                curveNodes.OfType<Button>().Single(b=>b.Name=="ReviewCurveSettings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                curveNodes.OfType<Button>().Single(b=>b.Name=="CreateCurve").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}));
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
        var settingsTab=(TabItem)tabs.Items[3]; settingsTab.IsEnabled=true;
        var editor=Settings(new List<DuctPlanItem> {Item("123",false,"Supply Air","Level 1")},false,Choices());
        main.OpenDuctSettings(editor,r=>{});
        Assert(tabs.SelectedItem==settingsTab && !editor.IsVisible,"Duct settings must be embedded without another visible window");
        var content=settingsTab.Content;
        var grid=Descendants((DependencyObject)content).OfType<DataGrid>().Single();
        Assert(grid.Parent is Grid && Grid.GetRow(grid)==0 && Grid.GetColumn((Grid)grid.Parent)==0,"Include table must be fixed above right-side options");
        Assert(grid.Visibility==Visibility.Collapsed,"Create Ducts does not show another element selection table");
        Assert(!Descendants((DependencyObject)content).OfType<Button>().Any(b=>Equals(b.Content,"Create 1 Duct")),"Embedded Duct settings exposes review navigation instead of direct creation");
        tabs.SelectedIndex=4;
        Assert(Descendants((DependencyObject)((TabItem)tabs.Items[4]).Content).OfType<DataGrid>().Any(t=>t.Name=="DuctFinalReview") && main.DuctCreationRequest==null,"Duct uses the fifth main tab for a read-only final summary without creating elements");
        tabs.SelectedItem=settingsTab;
        tabs.SelectedItem=elements; tabs.SelectedItem=settingsTab;
        Assert(ReferenceEquals(settingsTab.Content,content),"Tab switching must preserve unchanged settings");
        int prepared=0;
        main.CanCreateDucts=true;
        main.PrepareDucts=rows=> {prepared++;return null;};
        tabs.SelectedItem=elements;
        main.AirTerminals.Add(new AirTerminalRow {ElementId="456",IsSelected=true});
        tabs.SelectedItem=settingsTab;
        Assert(prepared==1 && !ReferenceEquals(settingsTab.Content,content) && tabs.SelectedItem==settingsTab,"Changed selection must invalidate stale settings");
        tabs.SelectedItem=tabs.Items[2];
        var parameterPanel=((TabItem)tabs.Items[2]).Content;
        var pairs=(System.Collections.IList)parameterPanel.GetType().GetField("mappings",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(parameterPanel);
        pairs.Add(new IfcParameterMapping{Source="IFC GUID"});
        DuctCreationWindow preparedWindow=null;
        main.PrepareDucts=rows=> {
            preparedWindow=Settings(new List<DuctPlanItem>{Item("456",false,"Supply Air","Level 1")},false,Choices());
            main.OpenDuctSettings(preparedWindow,r=>{});return null;
        };
        Descendants(main).OfType<Button>().Single(b=>(b.Content as string)=="Next: Preview & Create →").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(tabs.SelectedItem==settingsTab && preparedWindow!=null,"Next opens duct settings when draft rows have no Revit target");
        tabs.SelectedItem=tabs.Items[2];
        main.CheckParameterTarget=n=>"Ready";
        var required=(IfcParameterMapping)pairs[pairs.Count-1];required.Target="Target";required.Source="IFC Element ID";
        Descendants(main).OfType<Button>().Single(b=>(b.Content as string)=="Next: Preview & Create →").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(tabs.SelectedItem==settingsTab && preparedWindow!=null,"Next opens settings after both source and target are chosen");
        required.Target=null;
        bool executed=false;main.RunDuctCreation=()=>executed=true;
        typeof(IFCInfoWindow).GetMethod("SubmitDuctRequest",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(main,new object[]{new DuctRequest{Items=new List<DuctPlanItem>{Item("456",false,"Supply Air","Level 1")}}});
        Assert(executed,"Actual creation skips incomplete pairs instead of blocking ducts");
        executed=false;
        pairs.Clear();
        tabs.SelectedItem=tabs.Items[2];
        Descendants(main).OfType<Button>().Single(b=>(b.Content as string)=="Next: Preview & Create →").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(tabs.SelectedItem==settingsTab,"Next opens duct settings without adding parameters");
        var noParameters=new DuctRequest{Items=new List<DuctPlanItem>{Item("456",false,"Supply Air","Level 1")}};
        typeof(IFCInfoWindow).GetMethod("SubmitDuctRequest",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(main,new object[]{noParameters});
        Assert(executed && noParameters.ParameterMappings.Count==0,"Creation callback runs with no optional parameter mappings");
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
        var matcher=typeof(IFCInfoWindow).Assembly.GetType("IFCInfo.DuctGroupEditor").GetMethod("MatchIfcDuctType",BindingFlags.NonPublic|BindingFlags.Static);
        var sourceType=new AirTerminalRow {Name="Round IFC",IfcProperties=new List<IfcPropertyValue>()};
        var types=new List<DuctChoice>{new DuctChoice{Id=90,Name="Round IFC"},new DuctChoice{Id=91,Name="Other"}};
        Assert(((DuctChoice)matcher.Invoke(null,new object[]{new[]{sourceType},types})).Id==90,"Auto resolves exact IFC duct type names");
        sourceType.Name="Unmatched";
        Assert(matcher.Invoke(null,new object[]{new[]{sourceType},types})==null,"Auto does not guess an unrelated duct type");
        var groupType=typeof(IFCInfoWindow).Assembly.GetType("IFCInfo.DuctGroupEditor");
        var sampleEditor=Activator.CreateInstance(groupType,BindingFlags.Instance|BindingFlags.NonPublic,null,
            new object[]{"Round|Supply","Round","Supply",1,new List<DuctChoice>{types[0]},new List<DuctChoice>(),new DuctSettingsData(),true,new[]{sourceType}},null);
        groupType.GetMethod("SetDuctMode",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(sampleEditor,new object[]{true,new[]{sourceType},null,true});
        var planned=(NewDuctSystem)groupType.GetProperty("NewType",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(sampleEditor);
        Assert(planned!=null && planned.Name=="Unmatched" && planned.TemplateId==90,"Allowed missing IFC duct type is planned from a compatible template");
        groupType.GetMethod("SetDuctMode",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(sampleEditor,new object[]{true,new[]{sourceType},null,false});
        Assert(groupType.GetProperty("NewType",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(sampleEditor)==null,"Disabling automatic type creation removes the plan");
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
                Assert(!Descendants(window).OfType<CheckBox>().Any(x=>x.Name=="AllowIfcSystemCreation" || x.Name=="AllowIfcDuctTypeCreation"),"IFC creation options do not add extra checkboxes");
                Assert(((DuctChoice)common.Items[0]).Name.Contains("create missing system"),"System follow option explains missing system creation");
                Assert(Descendants(window).OfType<Button>().Any(x=>x.Name=="CreateSystemType"),"System creation is available in the shared controls");
                Exception systemDialogError=null;
                window.Dispatcher.BeginInvoke(new Action(()=> {
                    var child=window.OwnedWindows.Cast<Window>().Single(w=>w.Title=="Create System Type");
                    try {
                        var childMode=Descendants(child).OfType<ComboBox>().Single(c=>c.Name=="SystemCreationMode");
                        childMode.SelectedIndex=1;
                        var sourceSystem=Descendants(child).OfType<ComboBox>().Single(c=>c.Name=="IfcSystemSource");sourceSystem.SelectedIndex=0;
                        var systemName=Descendants(child).OfType<TextBox>().Single(c=>c.Name=="NewSystemTypeName");
                        Assert(systemName.IsReadOnly && systemName.Text==(string)sourceSystem.SelectedItem,"Follow IFC system selection fills and locks name");
                        childMode.SelectedIndex=0;
                        Assert(!systemName.IsReadOnly,"Manual system creation permits edits");
                        var classBox=Descendants(child).OfType<ComboBox>().Single(c=>c.Name=="NewSystemClassification");
                        var templateBox=Descendants(child).OfType<ComboBox>().Single(c=>c.Name=="NewSystemTemplate");
                        templateBox.SelectedIndex=1;classBox.SelectedItem="OtherAir";
                        Assert(classBox.IsEnabled && ((DuctChoice)templateBox.SelectedItem).Id==0,"Manual classification changes reset an incompatible template");
                        Assert(Descendants(child).OfType<TextBox>().Any(t=>t.Name=="NewSystemAbbreviation"),"System creation includes abbreviation input");
                    } catch(Exception ex){systemDialogError=ex;} finally{child.Close();}
                }),DispatcherPriority.ApplicationIdle);
                Descendants(window).OfType<Button>().Single(x=>x.Name=="CreateSystemType").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(systemDialogError!=null)throw systemDialogError;
                window.IfcSources=new List<AirTerminalRow>{new AirTerminalRow{Name="IFC Round Type",NativeShape="Round"}};
                Exception ductDialogError=null;
                window.Dispatcher.BeginInvoke(new Action(()=> {
                    var child=window.OwnedWindows.Cast<Window>().Single(w=>w.Title=="Create Duct Type");
                    try {
                        var profile=Descendants(child).OfType<ComboBox>().Single(c=>c.Name=="NewDuctProfile");
                        Assert(profile.Items.Count==3,"Duct creation offers Rectangular Round Oval");
                        var childMode=Descendants(child).OfType<ComboBox>().Single(c=>c.Name=="DuctTypeCreationMode");childMode.SelectedIndex=1;
                        var source=Descendants(child).OfType<ComboBox>().Single(c=>c.Name=="IfcDuctTypeSource");source.SelectedIndex=0;
                        var text=Descendants(child).OfType<TextBox>().Single(t=>t.Name=="NewDuctTypeName");
                        Assert(text.Text=="IFC Round Type" && (string)profile.SelectedItem=="Round" && !profile.IsEnabled,"Follow IFC fills duct name and profile");
                        childMode.SelectedIndex=0;
                        Assert(!text.IsReadOnly && profile.IsEnabled,"Manual duct mode unlocks name and profile");
                    }catch(Exception ex){ductDialogError=ex;}finally{child.Close();}
                }),DispatcherPriority.ApplicationIdle);
                Descendants(window).OfType<Button>().Single(x=>x.Name=="CreateDuctType").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(ductDialogError!=null)throw ductDialogError;
                mode.SelectedIndex=1;
                Assert(groups.All(g=>!Descendants(g).OfType<ComboBox>().Single(x=>x.Name=="GroupSystemType").IsEnabled),"Auto locks every group system");
                var globalLevel=Descendants(window).OfType<ComboBox>().Single(c=>c.Name=="SharedLevel");
                var groupLevels=Descendants(window).OfType<ComboBox>().Where(c=>c.Name=="GroupLevel").ToList();
                Assert(globalLevel.IsEnabled && groupLevels.All(c=>!c.IsEnabled),"Auto level locks per-source controls and enables shared choice");
                Assert(globalLevel.IsVisible && groupLevels.All(c=>!c.IsVisible),"Auto shows only the shared level picker, not duplicate source-level pickers");
                if(globalLevel.Items.Count>1) {
                    globalLevel.SelectedIndex=1;long expected=((DuctChoice)globalLevel.SelectedItem).Id;
                    Assert(groupLevels.All(c=>((DuctChoice)c.SelectedItem).Id==expected),"Shared level applies immediately to every source level");
                    globalLevel.SelectedIndex=0;
                }
                Assert(groups.All(g=>Descendants(g).OfType<ComboBox>().Single(x=>x.Name=="GroupSystemFollowIfc").Visibility==Visibility.Visible),"Follow IFC system is shown explicitly in every group");
                Assert(groups.All(g=>Descendants(g).OfType<ComboBox>().Single(x=>x.Name=="GroupDuctFollowIfc").Visibility==Visibility.Visible),"Follow IFC duct mode displays the resolved name rather than its template");
                submit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(window.Request==null,"Auto blocks unmatched IFC duct types");
                Assert(common.IsEnabled && groups.All(g=>!Descendants(g).OfType<ComboBox>().Single(x=>x.Name=="GroupDuctType").IsEnabled),"Auto locks shared system and duct type controls");
                common.SelectedIndex=1;
                Assert(groups.All(g=>((DuctChoice)Descendants(g).OfType<ComboBox>().Single(x=>x.Name=="GroupSystemType").SelectedItem).Id==((DuctChoice)common.SelectedItem).Id),"Auto project system choice applies immediately to every locked group");
                common.SelectedIndex=0;
                Assert(groups.Where(g=>!ReferenceEquals(g,newGroup)).All(g=>Descendants(g).OfType<ComboBox>().Single(x=>x.Name=="GroupSystemType").SelectedItem!=null),"IFC mapping reuses existing names even without classification metadata");
                Assert(Descendants(newGroup).OfType<CheckBox>().Single(x=>x.Name=="CreateNewSystem").IsChecked==true,"Only a missing system is planned for creation");
                Assert(groups.All(g=>!Descendants(g).OfType<CheckBox>().Single(x=>x.Name=="CreateNewSystem").IsEnabled),"IFC auto mapping locks new-system controls");
                Assert(Descendants(newGroup).OfType<ComboBox>().Single(x=>x.Name=="SystemClassification").SelectedItem.ToString()=="ExhaustAir","IFC auto mapping uses source classification");
                mode.SelectedIndex=0;
                Assert(!common.IsEnabled && groups.All(g=>Descendants(g).OfType<ComboBox>().Single(x=>x.Name=="GroupDuctType").IsEnabled),"Manual restores editable duct and system choices");
                Assert(!globalLevel.IsEnabled && groupLevels.All(c=>c.IsEnabled),"Manual restores editable source levels");
                Assert(!globalLevel.IsVisible && groupLevels.All(c=>c.IsVisible),"Manual shows only source-level mappings, not a disabled duplicate shared picker");
                Assert(groupLevels.All(c=>c.TransformToAncestor(window).Transform(new Point()).X<window.ActualWidth/2),"Duct manual source-level mappings are placed in the left sidebar");
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


