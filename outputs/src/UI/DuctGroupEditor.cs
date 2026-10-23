using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace IFCInfo
{
    internal sealed class DuctGroupEditor : Expander
    {
        internal ComboBox TypeBox, SystemBox;
        private readonly CheckBox createNew; private CheckBox followIfc; private StackPanel systemPanel; private string inferredClassification; private bool automatic; private object manualSystem, manualTemplate, manualClassification; private bool manualNew, manualFollow; private string manualName;
        private readonly TextBox name;
        private readonly ComboBox template, classification;
        private readonly string sourceSystem;
        private readonly TextBlock mappingStatus;
        private bool projectOnly, previousCreateNew;
        private object previousSystem;
        internal string Key { get; }
        internal string Label { get; }

        internal DuctGroupEditor(string key, string shape, string sourceSystem, int count,
            List<DuctChoice> types, List<DuctChoice> systems, DuctSettingsData saved, bool expanded, IEnumerable<AirTerminalRow> sources=null)
        {
            this.sourceSystem = sourceSystem;
            Key = key;
            Label = DuctRequest.ShapeLabel(shape) + " · " + (string.IsNullOrWhiteSpace(sourceSystem) ? "No system type" : sourceSystem);
            Header = Label + " · " + count + " segments";
            IsExpanded = expanded;
            Margin = new Thickness(0, 0, 0, 12);
            Padding = new Thickness(12);
            Background = System.Windows.Media.Brushes.White;
            Foreground = IFCInfoWindow.Brush("#37322B");
            BorderBrush = IFCInfoWindow.Brush("#DDD6CE");
            BorderThickness = new Thickness(1);
            var body = new StackPanel();
            Content = body;
            TypeBox = UiDesign.Field(body, "Duct Type", "Only matching profile types are shown · " + count + " segments", "Select duct type…", false);
            TypeBox.ItemsSource = types;
            TypeBox.Name = "GroupDuctType";
            long savedType = saved.DuctTypes.FirstOrDefault(x => x.Key == key)?.Id ??
                (shape == "Round" ? saved.RoundTypeId : shape == "Oval" ? 0 : saved.RectangularTypeId);
            TypeBox.SelectedItem = types.FirstOrDefault(x => x.Id == savedType) ?? (types.Count == 1 ? types[0] : null);
            if (types.Count == 0) body.Children.Add(IFCInfoWindow.Text("No duct type supports this profile. Load a matching type, then reopen the tool.", 12, "#9A5B12"));
            systemPanel=new StackPanel(); body.Children.Add(systemPanel);
            mappingStatus=IFCInfoWindow.Text("",13,"#6F6044");
            mappingStatus.Margin=new Thickness(0,12,0,0);
            mappingStatus.Visibility=Visibility.Collapsed;
            body.Children.Add(mappingStatus);
            SystemBox = UiDesign.Field(systemPanel, "Target system type", "Assign a separate Revit system to this group", "Select system type…", true);
            SystemBox.ItemsSource = systems;
            SystemBox.Name = "GroupSystemType";
            long savedSystem = saved.Systems.FirstOrDefault(x => x.Key == key)?.Id ?? 0;
            SystemBox.SelectedItem = systems.FirstOrDefault(x => x.Id == savedSystem) ??
                systems.FirstOrDefault(x => string.Equals(x.Name, sourceSystem, StringComparison.OrdinalIgnoreCase));
            createNew = new CheckBox { Name = "CreateNewSystem", Content = "Create a new system type", Margin = new Thickness(0, 14, 0, 8) };
            systemPanel.Children.Add(createNew);
            var fields = new StackPanel { Visibility = Visibility.Collapsed };
            systemPanel.Children.Add(fields);
            fields.Children.Add(IFCInfoWindow.Text("New system type name", 13, "#37322B"));
            name = new TextBox { Text = sourceSystem == "No information" ? "" : sourceSystem,
                MinHeight = 34, Padding = new Thickness(8), Margin = new Thickness(0, 6, 0, 8) };
            fields.Children.Add(name);
            name.Name = "NewSystemName";
            template = UiDesign.Field(fields, "Template system", "Inherit classification and settings from the template system", "Select template…", true);
            var templates = new List<DuctChoice> { new DuctChoice { Id = 0, Name = "Create from system classification" } };
            templates.AddRange(systems);
            template.ItemsSource = templates; template.SelectedIndex = 0;
            template.Name = "SystemTemplate";
            classification = UiDesign.Field(fields, "System Classification", "Specify the purpose of the new system", "Select classification…", true);
            classification.ItemsSource = new[] { "SupplyAir", "ReturnAir", "ExhaustAir", "OtherAir" };
            classification.Name = "SystemClassification";
            followIfc=new CheckBox {Name="FollowIfcClassification",Content="Follow IFC classification",IsChecked=true,Margin=new Thickness(0,10,0,6)};
            fields.Children.Add(followIfc);
            var evidenceText=IFCInfoWindow.Text("",12,"#716B63"); fields.Children.Add(evidenceText);
            string evidence;
            string inferred=IfcHvacClassification.Resolve(sources??new[] {new AirTerminalRow {SystemType=sourceSystem}},out evidence);
            inferredClassification=inferred;
            bool applying=false;
            Action syncEnabled=()=> {
                template.IsEnabled=followIfc.IsChecked!=true;
                classification.IsEnabled=followIfc.IsChecked!=true && ((DuctChoice)template.SelectedItem).Id==0;
            };
            Action applyIfc=()=> {applying=true;template.SelectedIndex=0;classification.SelectedItem=inferred;applying=false;evidenceText.Text=evidence;syncEnabled();};
            applyIfc();
            followIfc.Checked+=(s,e)=>applyIfc();
            followIfc.Unchecked+=(s,e)=>syncEnabled();
            classification.SelectionChanged+=(s,e)=> {if(!applying && followIfc.IsChecked!=true)evidenceText.Text="Manual classification override.";};
            template.SelectionChanged+=(s,e)=>syncEnabled();
            createNew.Checked += (s, e) => { fields.Visibility = Visibility.Visible; SystemBox.IsEnabled = false; };
            createNew.Unchecked += (s, e) => { fields.Visibility = Visibility.Collapsed; SystemBox.IsEnabled = true; };
            fields.Children.Add(IFCInfoWindow.Text("Systems are created only when Create Ducts is clicked. Groups with the same name and classification share the new system.", 12, "#716B63"));
        }

        internal void SetSystemMode(bool auto, bool mapIfc, DuctChoice common)
        {
            if(!auto && !automatic)return;
            if(auto && !automatic) {
                manualSystem=SystemBox.SelectedItem; manualNew=createNew.IsChecked==true;
                manualName=name.Text; manualTemplate=template.SelectedItem; manualClassification=classification.SelectedItem; manualFollow=followIfc.IsChecked==true;
            }
            automatic=auto;
            mappingStatus.Visibility=auto ? Visibility.Visible : Visibility.Collapsed;
            if(!auto) {
                systemPanel.IsEnabled=true;
                SystemBox.SelectedItem=manualSystem;createNew.IsChecked=manualNew;
                name.Text=manualName ?? name.Text; followIfc.IsChecked=manualFollow;
                template.SelectedItem=manualTemplate ?? template.Items[0];classification.SelectedItem=manualClassification;
                return;
            }
            if(mapIfc) {
                var match=SystemBox.Items.Cast<DuctChoice>().FirstOrDefault(x=>string.Equals(x.Name.Trim(),(sourceSystem??"").Trim(),StringComparison.OrdinalIgnoreCase));
                SystemBox.SelectedItem=match;createNew.IsChecked=match==null;
                name.Text=sourceSystem=="No information"?"":sourceSystem??"";
                followIfc.IsChecked=true;template.SelectedIndex=0;classification.SelectedItem=inferredClassification;
                mappingStatus.Text=match!=null
                    ? "Existing system found: " + match.Name + " · " + match.Classification + ". This group will use it directly."
                    : "No matching system found. A new system named '" + name.Text + "' will be created on Create Ducts."
                        + (inferredClassification==null ? " Select Manual to specify its classification first." : " Classification: " + inferredClassification + ".");
            } else {createNew.IsChecked=false;SystemBox.SelectedItem=common;mappingStatus.Text=common==null ? "Select an existing HVAC system." : "Use existing system: " + common.Name;}
            systemPanel.IsEnabled=false;
        }
        internal void FollowProjectSystems(bool enabled)
        {
            if (enabled == projectOnly) return;
            projectOnly = enabled;
            if (enabled)
            {
                previousCreateNew = createNew.IsChecked == true;
                previousSystem = SystemBox.SelectedItem;
                createNew.IsChecked = false;
                createNew.IsEnabled = false;
                // Exact names take precedence; keep an explicit existing mapping when no name matches.
                SystemBox.SelectedItem = SystemBox.Items.Cast<DuctChoice>().FirstOrDefault(x =>
                    string.Equals(x.Name.Trim(), (sourceSystem ?? "").Trim(), StringComparison.OrdinalIgnoreCase)) ?? previousSystem;
            }
            else
            {
                SystemBox.SelectedItem = previousSystem;
                createNew.IsEnabled = true;
                createNew.IsChecked = previousCreateNew;
            }
        }

        internal NewDuctSystem NewSystem => createNew.IsChecked == true ? new NewDuctSystem
        {
            Name = name.Text.Trim(), TemplateId = ((DuctChoice)template.SelectedItem).Id,
            Classification = classification.SelectedItem as string
        } : null;

        internal string Validate()
        {
            if (TypeBox.SelectedItem == null) return Label + ": select a duct type matching the profile.";
            var proposed = NewSystem;
            if (proposed == null) return SystemBox.SelectedItem == null ? Label + (projectOnly
                ? ": no matching project system type. Select an existing type or disable Follow project system types."
                : ": select a system type or create one.") : null;
            if (string.IsNullOrWhiteSpace(proposed.Name)) return Label + ": enter the new system type name.";
            if (proposed.Name.IndexOfAny(new[] { '\\', '/', ':', '{', '}', '[', ']', '|', ';', '<', '>', '?', '`', '~' }) >= 0)
                return Label + ": the system type name contains invalid characters.";
            if (SystemBox.Items.Cast<DuctChoice>().Any(x => string.Equals(x.Name, proposed.Name, StringComparison.OrdinalIgnoreCase)))
                return Label + ": this name exists. Disable Create New and select the existing system.";
            if (proposed.TemplateId == 0 && proposed.Classification == null) return Label + ": select a classification for the new system.";
            return null;
        }
    }
}
