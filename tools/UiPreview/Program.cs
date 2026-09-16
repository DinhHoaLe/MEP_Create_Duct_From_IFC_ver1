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
                null, new object[] { rows, false, (Action)(() => { }) }, null);
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
