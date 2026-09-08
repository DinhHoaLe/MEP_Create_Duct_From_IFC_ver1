using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace IFCInfo
{
    internal static class UiDesign
    {
        internal static FrameworkElement Parse(string xaml) => (FrameworkElement)XamlReader.Parse(xaml);
        internal static Brush Background => new LinearGradientBrush(Color.FromRgb(248, 251, 255), Color.FromRgb(235, 244, 255), 65);
        internal static DropShadowEffect Shadow(double opacity = .08) => new DropShadowEffect
        {
            Color = Color.FromRgb(37, 87, 151),
            BlurRadius = 22,
            ShadowDepth = 4,
            Opacity = opacity
        };
        internal static FrameworkElement Logo() => Parse(@"
<Grid xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Width='88' Height='94'>
 <Path Data='M8,2 L57,2 77,22 77,78 Q77,86 69,86 L8,86 Q2,86 2,78 L2,10 Q2,2 8,2Z' Fill='#E4F0FF' Stroke='#C7DFFF' StrokeThickness='1.2'/>
 <Path Data='M57,2 L57,17 Q57,22 63,22 L77,22Z' Fill='#91BCFF'/>
 <Path Data='M22,34 L37,25 52,34 52,52 37,61 22,52Z M37,25 L37,43 52,52 M22,34 L37,43 52,34 M37,43 L37,61 M22,52 L37,34 52,52' Stroke='#589CFA' StrokeThickness='2.5' StrokeLineJoin='Round'/>
 <Border Background='#137CFA' CornerRadius='5' HorizontalAlignment='Right' VerticalAlignment='Bottom' Padding='10,3' Margin='0,0,0,3'><TextBlock Text='IFC' FontSize='18' FontWeight='SemiBold' Foreground='White'/></Border>
</Grid>");
        internal static FrameworkElement Icon(bool category)
        {
            var tile = new Border
            {
                Width = 50,
                Height = 50,
                CornerRadius = new CornerRadius(10),
                Background = IFCInfoWindow.Brush(category ? "#F0EEFF" : "#EAF3FF"),
                VerticalAlignment = VerticalAlignment.Top
            };
            var path = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(category
                ? "M2,4 L3,4 M2,12 L3,12 M2,20 L3,20 M9,4 L24,4 M9,12 L24,12 M9,20 L24,20"
                : "M10,17 L7,20 C2,25 -3,19 2,14 L8,8 C13,3 19,8 15,13 M10,7 L14,3 C19,-2 25,4 20,9 L14,15 C9,20 3,14 7,10"),
                Stroke = IFCInfoWindow.Brush(category ? "#4D42E8" : "#147BFA"),
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
            var labels = new[] { "Chọn link\nvà Category", "Thiết lập\n(MEP)", "Tạo và hoàn tất" };
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
                    Background = IFCInfoWindow.Brush(current ? "#147BFA" : done ? "#E0EEFF" : "#E8EFF8"),
                    BorderBrush = IFCInfoWindow.Brush(current ? "#147BFA" : "#D6E3F3"),
                    BorderThickness = new Thickness(1)
                };
                number.Child = new TextBlock
                {
                    Text = done ? "✓" : (i + 1).ToString(),
                    FontSize = 17,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = IFCInfoWindow.Brush(current ? "#FFFFFF" : "#5D789F")
                };
                panel.Children.Add(number);
                panel.Children.Add(new TextBlock
                {
                    Text = labels[i],
                    TextAlignment = TextAlignment.Center,
                    FontSize = 12,
                    Margin = new Thickness(0, 8, 0, 0),
                    Foreground = IFCInfoWindow.Brush(current ? "#0872F4" : "#617CA1"),
                    FontWeight = current ? FontWeights.SemiBold : FontWeights.Normal
                });
                Grid.SetColumn(panel, i * 2);
                grid.Children.Add(panel);
                if (i < 2)
                {
                    var line = new Border
                    {
                        Height = 2,
                        Background = IFCInfoWindow.Brush(i + 1 < active ? "#147BFA" : "#CEDCF0"),
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(-9, 17, -9, 0)
                    };
                    Grid.SetColumn(line, i * 2 + 1);
                    grid.Children.Add(line);
                }
            }
            return grid;
        }
        internal static ComboBox Field(StackPanel body, string title, string description, string placeholder, bool category)
        {
            var grid = new Grid { Margin = new Thickness(0, 24, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.Children.Add(Icon(category));
            var field = new StackPanel();
            Grid.SetColumn(field, 1);
            grid.Children.Add(field);
            var label = IFCInfoWindow.Text(title, 17, "#102A50");
            label.FontWeight = FontWeights.SemiBold;
            field.Children.Add(label);
            var hint = IFCInfoWindow.Text(description, 14, "#637FA5");
            hint.Margin = new Thickness(0, 6, 0, 12);
            field.Children.Add(hint);
            var combo = new ComboBox
            {
                MinHeight = 46,
                FontSize = 16,
                Tag = placeholder,
                Padding = new Thickness(15, 10, 40, 10),
                Foreground = IFCInfoWindow.Brush("#183459"),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            combo.Style = (Style)XamlReader.Parse(@"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ComboBox'>
 <Setter Property='ScrollViewer.CanContentScroll' Value='True'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBox'>
  <Grid>
   <ToggleButton Focusable='False' ClickMode='Press' IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'>
    <ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border x:Name='frame' Background='#F8FAFD' BorderBrush='#C7D7EC' BorderThickness='1.2' CornerRadius='7'><Path Data='M0,0 L5,5 10,0' Stroke='#48668F' StrokeThickness='2' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,17,0'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='frame' Property='BorderBrush' Value='#70A9F8'/></Trigger><Trigger Property='IsChecked' Value='True'><Setter TargetName='frame' Property='BorderBrush' Value='#147BFA'/></Trigger></ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template>
   </ToggleButton>
   <ContentPresenter x:Name='selected' Margin='{TemplateBinding Padding}' Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' VerticalAlignment='Center' IsHitTestVisible='False'/>
   <TextBlock x:Name='placeholder' Text='{TemplateBinding Tag}' Margin='{TemplateBinding Padding}' Foreground='#7189AA' VerticalAlignment='Center' IsHitTestVisible='False' Visibility='Collapsed'/>
   <Popup x:Name='PART_Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}' AllowsTransparency='True' Focusable='False' PopupAnimation='Slide'>
    <Border Background='White' BorderBrush='#C7D7EC' BorderThickness='1' CornerRadius='7' MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}' Margin='0,4,0,0' Padding='4'>
     <ScrollViewer MaxHeight='260' CanContentScroll='True'><ItemsPresenter KeyboardNavigation.DirectionalNavigation='Contained'/></ScrollViewer>
    </Border>
   </Popup>
  </Grid>
  <ControlTemplate.Triggers>
   <Trigger Property='SelectedIndex' Value='-1'><Setter TargetName='placeholder' Property='Visibility' Value='Visible'/></Trigger>
   <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.60'/></Trigger>
   <Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter Property='Effect'><Setter.Value><DropShadowEffect Color='#6AA8FA' BlurRadius='5' ShadowDepth='0' Opacity='.3'/></Setter.Value></Setter></Trigger>
  </ControlTemplate.Triggers>
 </ControlTemplate></Setter.Value></Setter>
 <Setter Property='ItemContainerStyle'><Setter.Value><Style TargetType='ComboBoxItem'><Setter Property='Padding' Value='12,9'/><Setter Property='HorizontalContentAlignment' Value='Stretch'/></Style></Setter.Value></Setter>
</Style>");
            field.Children.Add(combo);
            body.Children.Add(grid);
            return combo;
        }
    }
}
