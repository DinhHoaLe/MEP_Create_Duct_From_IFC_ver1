using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace IFCInfo
{
    public sealed class ServiceTypeChoice
    {
        public string Value {get;set;}
        public string Kind {get;set;}
        public override string ToString()=>Kind=="New"?"Enter new…":Kind=="Follow"?"Follow IFC":Kind=="Default"?"Keep native default":Value;
    }
    public sealed class CableServicePicker : StackPanel
    {
        public ComboBox Selector {get;}
        public ComboBox AutoDisplay {get;}
        public TextBox NewValue {get;}
        public TextBlock Note {get;}
        private bool automatic;
        private string automaticValue="";
        private bool automaticValid=true;
        public event Action Changed;
        public bool FollowsIfc=>(Selector.SelectedItem as ServiceTypeChoice)?.Kind=="Follow";
        public string Value=>automatic?automaticValue:(Selector.SelectedItem as ServiceTypeChoice)?.Kind=="New"?NewValue.Text.Trim():(Selector.SelectedItem as ServiceTypeChoice)?.Value??"";
        public bool IsValid=>automatic?automaticValid:Selector.SelectedItem is ServiceTypeChoice && ((Selector.SelectedItem as ServiceTypeChoice).Kind!="New" || !string.IsNullOrWhiteSpace(NewValue.Text));
        public CableServicePicker(IEnumerable<string> values,string source,bool shared=false)
        {
            Selector=UiDesign.Field(this,"Service Type",shared?"Follow IFC or apply a filled service value to all groups":"Choose a filled service value, or Enter new…","Select service type…",true,shared);
            Selector.Name=shared?"ServiceFollowMode":"GroupServiceFollowMode";
            var choices=values.Where(v=>!string.IsNullOrWhiteSpace(v)).Select(v=>v.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(v=>v,StringComparer.OrdinalIgnoreCase).Select(v=>new ServiceTypeChoice{Kind="Existing",Value=v}).ToList();
            if(shared)choices.Insert(0,new ServiceTypeChoice{Kind="Follow"});
            choices.Add(new ServiceTypeChoice{Kind="New"});choices.Add(new ServiceTypeChoice{Kind="Default",Value=""});
            Selector.ItemsSource=choices;
            Selector.SelectedItem=shared?choices[0]:choices.FirstOrDefault(c=>c.Kind=="Existing" && string.Equals(c.Value,source,StringComparison.OrdinalIgnoreCase));
            var field=(Panel)Selector.Parent;
            AutoDisplay=new ComboBox{Name=shared?"SharedResolvedServiceChoice":"ResolvedServiceChoice",Style=Selector.Style,MinHeight=Selector.MinHeight,FontSize=Selector.FontSize,Padding=Selector.Padding,IsEnabled=false,Visibility=Visibility.Collapsed};field.Children.Add(AutoDisplay);
            NewValue=new TextBox{Name=shared?"SharedServiceType":"GroupServiceType",ToolTip="New Service Type value; written only when Create is confirmed."};UiDesign.StyleServiceEntry(NewValue,shared);field.Children.Add(NewValue);
            Note=IFCInfoWindow.Text("",13,"#716B63");Note.Name=shared?"SharedServiceNote":"ResolvedIfcService";Note.Margin=new Thickness(0,6,0,0);field.Children.Add(Note);
            Selector.SelectionChanged+=(s,e)=>{Refresh();Changed?.Invoke();};NewValue.TextChanged+=(s,e)=>Changed?.Invoke();Refresh();
        }
        private void Refresh()
        {
            Selector.Visibility=automatic?Visibility.Collapsed:Visibility.Visible;
            AutoDisplay.Visibility=automatic?Visibility.Visible:Visibility.Collapsed;
            NewValue.Visibility=!automatic && (Selector.SelectedItem as ServiceTypeChoice)?.Kind=="New"?Visibility.Visible:Visibility.Collapsed;
            Note.Visibility=automatic?Visibility.Visible:Visibility.Collapsed;
        }
        public void SetAutomatic(bool enabled,string value,bool valid=true,bool fromIfc=true)
        {
            automatic=enabled;automaticValue=value??"";automaticValid=valid;
            if(enabled){string text=!valid?"Unresolved — select or enter a service":string.IsNullOrWhiteSpace(automaticValue)?"No source service — keep native default":(fromIfc?"IFC — ":"Shared service — ")+automaticValue;AutoDisplay.ItemsSource=new[]{text};AutoDisplay.SelectedIndex=0;Note.Text=valid?(fromIfc?"Follow IFC → ":"Shared service → ")+(string.IsNullOrWhiteSpace(automaticValue)?"No source service · keep native default":automaticValue):text;}
            Refresh();
        }
    }
}
