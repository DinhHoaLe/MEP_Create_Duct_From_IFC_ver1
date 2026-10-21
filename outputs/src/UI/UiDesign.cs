using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace IFCInfo
{
    internal static class UiDesign
    {
        // Shared warm, flat window frame matching Family Data Studio.
        internal static void SetContent(Window window, FrameworkElement content)
        {
            window.Icon = ProjectIcon;
            window.WindowStyle = WindowStyle.None;
            window.Background = Background;
            window.Foreground = IFCInfoWindow.Brush("#37322B");
            System.Windows.Shell.WindowChrome.SetWindowChrome(window, new System.Windows.Shell.WindowChrome
            {
                CaptionHeight = 42,
                ResizeBorderThickness = new Thickness(window.ResizeMode == ResizeMode.NoResize ? 0 : 6),
                GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                UseAeroCaptionButtons = false
            });
            var frame = new DockPanel { Background = Background };
            var caption = new DockPanel { Height = 42, Background = IFCInfoWindow.Brush("#D1C8BF") };
            DockPanel.SetDock(caption, Dock.Top);
            frame.Children.Add(caption);
            var controls = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(controls, Dock.Right);
            caption.Children.Add(controls);
            if (window.ResizeMode != ResizeMode.NoResize)
            {
                controls.Children.Add(ChromeButton("−", "Minimize", () => SystemCommands.MinimizeWindow(window)));
                if (window.ResizeMode != ResizeMode.CanMinimize)
                    controls.Children.Add(ChromeButton("□", "Maximize / Restore", () =>
                    {
                        if (window.WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(window);
                        else SystemCommands.MaximizeWindow(window);
                    }));
            }
            controls.Children.Add(ChromeButton("×", "Close", () => window.Close()));
            var title = IFCInfoWindow.Text(window.Title, 13, "#37322B");
            title.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Title") { Source = window });
            title.VerticalAlignment = VerticalAlignment.Center;
            title.Margin = new Thickness(18, 0, 12, 0);
            title.TextWrapping = TextWrapping.NoWrap;
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            caption.Children.Add(title);
            frame.Children.Add(content);
            window.Content = new Border { BorderBrush = IFCInfoWindow.Brush("#DDD6CE"), BorderThickness = new Thickness(1), Child = frame };
        }

        private static Button ChromeButton(string text, string tooltip, System.Action action)
        {
            var button = new Button { Content = text, ToolTip = tooltip, Width = 46, Height = 42,
                FontSize = 18, Foreground = IFCInfoWindow.Brush("#37322B"), Background = Brushes.Transparent,
                BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
            button.Template = (ControlTemplate)XamlReader.Parse(@"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'>
 <Border x:Name='surface' Background='{TemplateBinding Background}'>
  <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
 </Border>
 <ControlTemplate.Triggers>
  <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='surface' Property='Background' Value='#B3A491'/></Trigger>
  <Trigger Property='IsPressed' Value='True'><Setter TargetName='surface' Property='Background' Value='#90805C'/></Trigger>
 </ControlTemplate.Triggers>
</ControlTemplate>");
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(button, true);
            button.Click += (s, e) => action();
            return button;
        }

        internal static FrameworkElement Parse(string xaml) => (FrameworkElement)XamlReader.Parse(xaml);
        internal static void StyleWorkflowTabs(TabControl tabs)
        {
            tabs.BorderThickness=new Thickness(0);
            tabs.Padding=new Thickness(0,12,0,0);
            tabs.ItemContainerStyle=(Style)XamlReader.Parse(@"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='TabItem'>
 <Setter Property='Foreground' Value='#716B63'/><Setter Property='Background' Value='#E8E2DA'/>
 <Setter Property='Margin' Value='0,0,5,0'/><Setter Property='Padding' Value='22,12'/>
 <Setter Property='FontSize' Value='14'/><Setter Property='MinHeight' Value='44'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='TabItem'>
  <Border x:Name='surface' Background='{TemplateBinding Background}' BorderBrush='#DDD6CE' BorderThickness='1' CornerRadius='8,8,0,0' Padding='{TemplateBinding Padding}'>
   <ContentPresenter ContentSource='Header' HorizontalAlignment='Center' VerticalAlignment='Center'/>
  </Border>
  <ControlTemplate.Triggers>
   <Trigger Property='IsSelected' Value='True'><Setter Property='Foreground' Value='#37322B'/><Setter Property='FontWeight' Value='SemiBold'/><Setter TargetName='surface' Property='Background' Value='White'/><Setter TargetName='surface' Property='BorderThickness' Value='1,1,1,0'/></Trigger>
   <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='surface' Property='Background' Value='#F5F2EE'/></Trigger>
   <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.5'/></Trigger>
  </ControlTemplate.Triggers>
 </ControlTemplate></Setter.Value></Setter>
</Style>");
        }
        internal static Grid SettingsColumns(StackPanel left, StackPanel right)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
            grid.Children.Add(new ScrollViewer { Content = left, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
            var line = new Border { Width = 1, Background = IFCInfoWindow.Brush("#DDD6CE") };
            Grid.SetColumn(line, 1); grid.Children.Add(line);
            var scroll = new ScrollViewer { Content = right, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            Grid.SetColumn(scroll, 2); grid.Children.Add(scroll);
            return grid;
        }
        internal static void StyleTable(DataGrid table)
        {
            table.Background = Brushes.White;
            table.HeadersVisibility = DataGridHeadersVisibility.Column;
            table.BorderBrush = IFCInfoWindow.Brush("#DDD6CE");
            table.HorizontalGridLinesBrush = IFCInfoWindow.Brush("#DDD6CE");
            table.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
            table.RowBackground = Brushes.White;
            table.AlternatingRowBackground = Brushes.White;
            table.ColumnHeaderStyle = (Style)XamlReader.Parse(@"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='DataGridColumnHeader'>
 <Setter Property='Background' Value='#F5F2EE'/><Setter Property='Foreground' Value='#716B63'/>
 <Setter Property='FontWeight' Value='SemiBold'/><Setter Property='Padding' Value='12,8'/>
 <Setter Property='BorderThickness' Value='0'/>
</Style>");
            table.CellStyle = (Style)XamlReader.Parse(@"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='DataGridCell'>
 <Setter Property='Foreground' Value='#37322B'/><Setter Property='BorderThickness' Value='0'/>
 <Setter Property='VerticalContentAlignment' Value='Center'/>
 <Style.Triggers><Trigger Property='IsSelected' Value='True'>
  <Setter Property='Background' Value='#E8E2DA'/><Setter Property='Foreground' Value='#37322B'/>
 </Trigger></Style.Triggers>
</Style>");
            table.RowStyle = (Style)XamlReader.Parse(@"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='DataGridRow'>
 <Style.Triggers>
  <Trigger Property='IsMouseOver' Value='True'><Setter Property='Background' Value='#F2EEE9'/></Trigger>
  <Trigger Property='IsSelected' Value='True'><Setter Property='Background' Value='#E8E2DA'/></Trigger>
 </Style.Triggers>
</Style>");
        }
        internal static Brush Background => IFCInfoWindow.Brush("#F5F2EE");
        internal static DropShadowEffect Shadow(double opacity = .08) => new DropShadowEffect
        {
            Color = Color.FromRgb(55, 50, 43),
            BlurRadius = 8,
            ShadowDepth = 1,
            Opacity = opacity * .3
        };
        private static System.Windows.Media.Imaging.BitmapSource projectIcon;
        internal static System.Windows.Media.Imaging.BitmapSource ProjectIcon
        {
            get
            {
                if (projectIcon != null) return projectIcon;
                using (var stream = typeof(UiDesign).Assembly.GetManifestResourceStream("IFCInfo.CreateDuctIcon.png"))
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return projectIcon = bitmap;
                }
            }
        }
        internal static FrameworkElement Logo() => new Image
        {
            Source = ProjectIcon, Width = 100, Height = 94, Stretch = Stretch.Uniform
        };
        internal static FrameworkElement Icon(bool category)
        {
            var tile = new Border
            {
                Width = 50,
                Height = 50,
                CornerRadius = new CornerRadius(10),
                Background = IFCInfoWindow.Brush(category ? "#E8E2DA" : "#E8E2DA"),
                VerticalAlignment = VerticalAlignment.Top
            };
            var path = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(category
                ? "M2,4 L3,4 M2,12 L3,12 M2,20 L3,20 M9,4 L24,4 M9,12 L24,12 M9,20 L24,20"
                : "M10,17 L7,20 C2,25 -3,19 2,14 L8,8 C13,3 19,8 15,13 M10,7 L14,3 C19,-2 25,4 20,9 L14,15 C9,20 3,14 7,10"),
                Stroke = IFCInfoWindow.Brush(category ? "#6F6044" : "#6F6044"),
                StrokeThickness = 2.3,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Stretch = Stretch.Uniform,
                Width = 24,
                Height = 24
            };
            tile.Child = path;
            return tile;
        }
        internal static FrameworkElement Steps(int active)
        {
            var grid = new Grid { Width = 340, VerticalAlignment = VerticalAlignment.Center };
            for (int i = 0; i < 5; i++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(i % 2 == 0 ? 90 : 35) });
            var labels = new[] { "Select link\nand category", "Select elements\nand type", "Create and finish" };
            for (int i = 0; i < 3; i++)
            {
                bool current = i + 1 == active;
                bool done = i + 1 < active;
                var panel = new StackPanel();
                var number = new Border
                {
                    Width = 35,
                    Height = 35,
                    CornerRadius = new CornerRadius(18),
                    Background = IFCInfoWindow.Brush(current ? "#6F6044" : done ? "#E8E2DA" : "#E8E2DA"),
                    BorderBrush = IFCInfoWindow.Brush(current ? "#6F6044" : "#DDD6CE"),
                    BorderThickness = new Thickness(1)
                };
                number.Child = new TextBlock
                {
                    Text = done ? "✓" : (i + 1).ToString(),
                    FontSize = 17,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = IFCInfoWindow.Brush(current ? "#FFFFFF" : "#716B63")
                };
                panel.Children.Add(number);
                panel.Children.Add(new TextBlock
                {
                    Text = labels[i],
                    TextAlignment = TextAlignment.Center,
                    FontSize = 12,
                    Margin = new Thickness(0, 8, 0, 0),
                    Foreground = IFCInfoWindow.Brush(current ? "#6F6044" : "#716B63"),
                    FontWeight = current ? FontWeights.SemiBold : FontWeights.Normal
                });
                Grid.SetColumn(panel, i * 2);
                grid.Children.Add(panel);
                if (i < 2)
                {
                    var line = new Border
                    {
                        Height = 2,
                        Background = IFCInfoWindow.Brush(i + 1 < active ? "#6F6044" : "#DDD6CE"),
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(-9, 17, -9, 0)
                    };
                    Grid.SetColumn(line, i * 2 + 1);
                    grid.Children.Add(line);
                }
            }
            return grid;
        }
        internal static ComboBox Field(StackPanel body, string title, string description, string placeholder, bool category, bool compact=false)
        {
            var grid = new Grid { Margin = new Thickness(0, 24, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.Children.Add(Icon(category));
            var field = new StackPanel();
            Grid.SetColumn(field, 1);
            grid.Children.Add(field);
            var label = IFCInfoWindow.Text(title, 17, "#37322B");
            label.FontWeight = FontWeights.SemiBold;
            field.Children.Add(label);
            var hint = IFCInfoWindow.Text(description, 14, "#716B63");
            hint.Margin = new Thickness(0, 6, 0, 12);
            field.Children.Add(hint);
            var combo = new ComboBox
            {
                MinHeight = 46,
                FontSize = 16,
                Tag = placeholder,
                Padding = new Thickness(15, 10, 40, 10),
                Foreground = IFCInfoWindow.Brush("#37322B"),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            combo.Style = (Style)XamlReader.Parse(@"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ComboBox'>
 <Setter Property='ScrollViewer.CanContentScroll' Value='True'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBox'>
  <Grid>
   <ToggleButton Focusable='False' ClickMode='Press' IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'>
    <ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border x:Name='frame' Background='#F5F2EE' BorderBrush='#DDD6CE' BorderThickness='1.2' CornerRadius='7'><Path Data='M0,0 L5,5 10,0' Stroke='#716B63' StrokeThickness='2' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,17,0'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='frame' Property='BorderBrush' Value='#90805C'/></Trigger><Trigger Property='IsChecked' Value='True'><Setter TargetName='frame' Property='BorderBrush' Value='#6F6044'/></Trigger></ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template>
   </ToggleButton>
   <ContentPresenter x:Name='selected' Margin='{TemplateBinding Padding}' Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' VerticalAlignment='Center' IsHitTestVisible='False'/>
   <TextBlock x:Name='placeholder' Text='{TemplateBinding Tag}' Margin='{TemplateBinding Padding}' Foreground='#716B63' VerticalAlignment='Center' IsHitTestVisible='False' Visibility='Collapsed'/>
   <Popup x:Name='PART_Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}' AllowsTransparency='True' Focusable='False' PopupAnimation='Slide'>
    <Border Background='White' BorderBrush='#DDD6CE' BorderThickness='1' CornerRadius='7' MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}' Margin='0,4,0,0' Padding='4'>
     <ScrollViewer MaxHeight='260' CanContentScroll='True'><ItemsPresenter KeyboardNavigation.DirectionalNavigation='Contained'/></ScrollViewer>
    </Border>
   </Popup>
  </Grid>
  <ControlTemplate.Triggers>
   <Trigger Property='SelectedIndex' Value='-1'><Setter TargetName='placeholder' Property='Visibility' Value='Visible'/></Trigger>
   <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.60'/></Trigger>
   <Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter Property='Effect'><Setter.Value><DropShadowEffect Color='#90805C' BlurRadius='5' ShadowDepth='0' Opacity='.3'/></Setter.Value></Setter></Trigger>
  </ControlTemplate.Triggers>
 </ControlTemplate></Setter.Value></Setter>
 <Setter Property='ItemContainerStyle'><Setter.Value><Style TargetType='ComboBoxItem'><Setter Property='Padding' Value='12,9'/><Setter Property='HorizontalContentAlignment' Value='Stretch'/></Style></Setter.Value></Setter>
</Style>");
            field.Children.Add(combo);
            if(compact)
            {
                grid.Margin=new Thickness(0,10,0,0);
                grid.ColumnDefinitions[0].Width=new GridLength(0);
                grid.Children[0].Visibility=Visibility.Collapsed;
                label.FontSize=14;
                label.Margin=new Thickness(0,0,0,5);
                hint.Visibility=Visibility.Collapsed;
                combo.ToolTip=description;
                combo.MinHeight=36; combo.FontSize=14;
                combo.Padding=new Thickness(10,6,32,6);
            }
            body.Children.Add(grid);
            return combo;
        }
    }
}
