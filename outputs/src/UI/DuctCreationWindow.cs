using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace IFCInfo
{
    public sealed class DuctCreationWindow : Window
    {
        public DuctRequest Request
        {
            get; private set;
        }
        public DuctCreationWindow(List<DuctPlanItem> items, List<string> issues,
            List<DuctChoice> roundTypes, List<DuctChoice> rectangularTypes, List<DuctChoice> systems,
            List<DuctChoice> levels, List<DuctChoice> worksets, DuctSettingsData saved, bool updating = false)
        {
            Title = updating ? "Xem trước cập nhật Duct từ IFC" : "Bước 3 · Tạo Duct từ IFC";
            Width = 900;
            Height = 690;
            MinWidth = 620;
            MinHeight = 440;
            MaxHeight = SystemParameters.WorkArea.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = UiDesign.Background;
            UseLayoutRounding = true;
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
            FontSize = 14;
            var root = new DockPanel { Margin = new Thickness(28), Background = Background };
            Content = root;
            var footer = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);
            var feedback = IFCInfoWindow.Text("", 13, "#9A5B12");
            footer.Children.Add(feedback);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var back = IFCInfoWindow.Button("Quay lại", false);
            back.IsCancel = true;
            back.Click += (s, e) => Close();
            buttons.Children.Add(back);
            var create = IFCInfoWindow.Button((updating ? "Cập nhật " : "Tạo ") + items.Count + " Duct", true);
            create.IsEnabled = items.Count > 0;
            buttons.Children.Add(create);
            footer.Children.Add(buttons);
            var header = new Grid { Margin = new Thickness(0,0,0,22) };
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titlePanel = new StackPanel(); header.Children.Add(titlePanel);
            var title = IFCInfoWindow.Text(updating ? "Cập nhật Duct từ IFC" : "Thiết lập tạo Duct", 27, "#102A50");
            title.FontWeight = FontWeights.Bold; titlePanel.Children.Add(title);
            titlePanel.Children.Add(IFCInfoWindow.Text("Bước 3 · Kiểm tra và tạo ống", 14, "#637FA5"));
            var steps = new ContentControl { Content = UiDesign.Steps(3), Margin = new Thickness(12,0,0,0) };
            Grid.SetColumn(steps,1); header.Children.Add(steps);
            header.SizeChanged += (s,e) => steps.Visibility = header.ActualWidth < 780 ? Visibility.Collapsed : Visibility.Visible;
            DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
            var body = new StackPanel();
            root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            var summary = new Border { Background = IFCInfoWindow.Brush("#E7F2FF"), CornerRadius = new CornerRadius(10), Padding = new Thickness(18,12,18,12), Margin = new Thickness(0,0,0,16) };
            summary.Child = IFCInfoWindow.Text((updating ? "Có thay đổi: " : "Sẵn sàng tạo: ") + items.Count + " đoạn     ·     Bỏ qua  " + issues.Count + " đoạn", 16, "#1367C1");
            body.Children.Add(summary);
            ComboBox round = null, rectangular = null;
            var mapping = new Dictionary<string, ComboBox>();
            foreach (bool isRound in new[] { true, false })
            {
                var groupItems = items.Where(item => item.Round == isRound).ToList();
                if (groupItems.Count == 0) continue;
                var settings = new StackPanel { Margin = new Thickness(22,18,22,22) };
                body.Children.Add(new Border { Child = settings, Background = System.Windows.Media.Brushes.White,
                    CornerRadius = new CornerRadius(14), BorderBrush = IFCInfoWindow.Brush("#DFE9F6"),
                    BorderThickness = new Thickness(1), Margin = new Thickness(0,0,0,16) });
                var groupTitle = IFCInfoWindow.Text((isRound ? "Ống gió tròn" : "Ống gió chữ nhật") + " · " + groupItems.Count + " đoạn", 20, "#102A50");
                groupTitle.FontWeight = FontWeights.SemiBold;
                settings.Children.Add(groupTitle);
                var typeBox = UiDesign.Field(settings, "Duct Type", "Chọn loại ống cho nhóm này", "Chọn Duct Type...", false);
                var availableTypes = isRound ? roundTypes : rectangularTypes;
                typeBox.ItemsSource = availableTypes;
                if (availableTypes.Count == 1) typeBox.SelectedIndex = 0;
                var savedType = availableTypes.FirstOrDefault(t => t.Id == (isRound ? saved.RoundTypeId : saved.RectangularTypeId));
                if (savedType != null) typeBox.SelectedItem = savedType;
                if (isRound) round = typeBox; else rectangular = typeBox;
                foreach (var sourceGroup in groupItems.GroupBy(item => item.Source.SystemType ?? "").OrderBy(group => group.Key))
                {
                    string sourceType = sourceGroup.Key;
                    var box = UiDesign.Field(settings,
                        "System Type: " + (string.IsNullOrEmpty(sourceType) ? "Không có thông tin" : sourceType),
                        sourceGroup.Count() + " đoạn · Chọn System Type tương ứng trong Revit",
                        "Chọn System Type...", true);
                    box.ItemsSource = systems;
                    box.SelectedItem = systems.FirstOrDefault(type => string.Equals(type.Name, sourceType, StringComparison.OrdinalIgnoreCase));
                    mapping.Add(DuctRequest.SystemKey(isRound, sourceType), box);
                    var savedSystem = saved.Systems.FirstOrDefault(m => m.Key == DuctRequest.SystemKey(isRound, sourceType));
                    if (savedSystem != null && systems.Any(t=>t.Id == savedSystem.Id)) box.SelectedItem = systems.First(t=>t.Id == savedSystem.Id);
                }
            }
            var levelMapping = new Dictionary<string, ComboBox>();
            foreach (var key in items.Select(i=>i.LevelKey ?? "Không có Level nguồn").Distinct())
            {
                var box = UiDesign.Field(body, "Level nguồn: " + key, "Tự động chọn Level dưới cao độ ống hoặc ánh xạ Level đích", "Level", false);
                var choices = new List<DuctChoice> { new DuctChoice { Id = 0, Name = "Tự động theo cao độ" } }; choices.AddRange(levels);
                box.ItemsSource = choices;
                box.SelectedItem = choices.FirstOrDefault(c=>c.Id == saved.Levels.FirstOrDefault(m=>m.Key == key)?.Id) ?? choices[0];
                levelMapping[key] = box;
            }
            var worksetMapping = new Dictionary<string, ComboBox>();
            if (worksets.Count > 0)
            foreach (var key in mapping.Keys)
            {
                var box = UiDesign.Field(body, "Workset: " + key, "Ánh xạ theo nhóm tiết diện và hệ thống", "Workset", false);
                var choices = new List<DuctChoice> { new DuctChoice { Id = 0, Name = "Workset hiện hành / giữ Workset khi cập nhật" } }; choices.AddRange(worksets);
                box.ItemsSource = choices;
                box.SelectedItem = choices.FirstOrDefault(c=>c.Id == saved.Worksets.FirstOrDefault(m=>m.Key == key)?.Id) ?? choices[0];
                worksetMapping[key] = box;
            }
            var policy = UiDesign.Field(body, "Khi có lỗi", "Áp dụng cho cả tạo duct và nối fitting", "Chọn cách xử lý", false);
            policy.ItemsSource = new[] { "Hoàn tác toàn bộ", "Bỏ qua lỗi, giữ phần thành công" }; policy.SelectedIndex = 0;
            var remember = new CheckBox { Content = "Lưu ánh xạ trong dự án Revit", IsChecked = true, Margin = new Thickness(0,16,0,8) };
            var fittings = new CheckBox { Content = "Nối đầu ống: elbow, transition, tee", Margin = new Thickness(0,8,0,8) };
            var terminals = new CheckBox { Content = "Nối miệng gió trong model chính tại đầu ống (tối đa 1 mm)", Margin = new Thickness(0,8,0,8) };
            body.Children.Add(remember); body.Children.Add(fittings); body.Children.Add(terminals);
            body.Children.Add(IFCInfoWindow.Text("Khoảng hở tối đa cho elbow/transition (mm). Để 1 nếu chỉ nối các đầu gặp nhau; tăng nếu cho phép Revit điều chỉnh đầu ống.",13,"#526880"));
            var gap=new TextBox { Text="1",Width=100,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,8,0,8) }; body.Children.Add(gap);
            var preview = new DataGrid { ItemsSource = items, AutoGenerateColumns = false, CanUserAddRows = false, Height = 190, Margin = new Thickness(0,16,0,8) };
            preview.Columns.Add(new DataGridCheckBoxColumn { Header = "Thực hiện", Binding = new System.Windows.Data.Binding("Include") });
            preview.Columns.Add(new DataGridTextColumn { Header = "Nguồn IFC", Binding = new System.Windows.Data.Binding("PreviewSource"), IsReadOnly=true });
            preview.Columns.Add(new DataGridTextColumn { Header = "Duct đích", Binding = new System.Windows.Data.Binding("PreviewTarget"), IsReadOnly=true });
            preview.Columns.Add(new DataGridTextColumn { Header = "Thay đổi", Binding = new System.Windows.Data.Binding("PreviewChange"), IsReadOnly=true });
            body.Children.Add(preview);
            var note = IFCInfoWindow.Text("Fitting dùng Routing Preferences. Chỉ nối cặp đầu ống có nghiệm duy nhất; tee cần 3 đầu gặp nhau, không tự chia ống. System Name IFC lưu trong Comments.", 13, "#526880");
            note.Margin = new Thickness(2,16,2,8); body.Children.Add(note);
            if (issues.Count > 0)
            {
                body.Children.Add(new SkippedDuctsPanel(issues));
                var exportIssues=IFCInfoWindow.Button("Xuất CSV các nguồn bị bỏ qua",false);
                exportIssues.Click+=(s,e)=>
                {
                    try
                    {
                        var file=new Microsoft.Win32.SaveFileDialog { Filter="CSV (*.csv)|*.csv",FileName="IFC-skipped.csv" };
                        if (file.ShowDialog(this)==true) DuctRunRow.Export(file.FileName,issues.Select(issue=>new DuctRunRow { SourceId=issue.Split(':')[0],Status="Bỏ qua",Reason=issue }));
                    }
                    catch (Exception ex) { feedback.Text=ex.Message; }
                };
                body.Children.Add(exportIssues);
            }
            create.Click += (s, e) =>
            {
                preview.CommitEdit(DataGridEditingUnit.Cell, true); preview.CommitEdit(DataGridEditingUnit.Row, true);
                double gapMm;
                if (!double.TryParse(gap.Text,out gapMm) || double.IsNaN(gapMm) || double.IsInfinity(gapMm) || gapMm<1 || gapMm>1000)
                { feedback.Text="Khoảng hở fitting phải từ 1 đến 1000 mm."; return; }
                if (!items.Any(i=>i.Include)) { feedback.Text = "Chọn ít nhất một dòng trong bảng xem trước."; return; }
                if ((round != null && round.SelectedItem == null) || (rectangular != null && rectangular.SelectedItem == null) || mapping.Values.Any(b => b.SelectedItem == null))
                {
                    feedback.Text = "Chọn đủ Duct Type và System Type. Nếu danh sách trống, hãy nạp type vào model chính rồi chạy lại.";
                    return;
                }
                Request = new DuctRequest
                {
                    Items = items.Where(i=>i.Include).ToList(),
                    RoundTypeId = (round?.SelectedItem as DuctChoice)?.Id ?? 0,
                    RectangularTypeId = (rectangular?.SelectedItem as DuctChoice)?.Id ?? 0,
                    SystemTypes = mapping.ToDictionary(p => p.Key, p => ((DuctChoice)p.Value.SelectedItem).Id),
                    Levels = levelMapping.ToDictionary(p => p.Key, p => ((DuctChoice)p.Value.SelectedItem).Id),
                    Worksets = worksetMapping.ToDictionary(p => p.Key, p => ((DuctChoice)p.Value.SelectedItem).Id),
                    KeepSuccessful = policy.SelectedIndex == 1, SaveSettings = remember.IsChecked == true,
                    CreateFittings = fittings.IsChecked == true, ConnectTerminals = terminals.IsChecked == true,
                    FittingGapMm=gapMm,
                    Skipped = issues.Select(issue=>new DuctRunRow { SourceId=issue.Split(':')[0], Status="Bỏ qua", Reason=issue }).ToList()
                };
                Request.Skipped.AddRange(items.Where(i=>!i.Include).Select(i=>new DuctRunRow { SourceId=i.Source.ElementId,IfcGuid=i.Source.IfcGuid,Status="Không chọn",Reason="Bỏ chọn trong bảng xem trước." }));
                DialogResult = true;
            };
        }
    }
}




