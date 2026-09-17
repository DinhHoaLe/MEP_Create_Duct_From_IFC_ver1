using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace IFCInfo
{
    public sealed class IfcPreviewMesh
    {
        public long ElementId;
        public string Name;
        public List<Point3D> Points = new List<Point3D>();
        public List<Vector3D> Normals = new List<Vector3D>();
        public List<int> Indices = new List<int>();
    }

    /// <summary>Viewer 3D nhẹ để review mesh đã trích từ phần tử trong IFC link.</summary>
    public sealed class IfcObjectViewer : Border
    {
        private readonly Viewport3D viewport;
        private readonly PerspectiveCamera camera;
        private readonly Model3DGroup scene;
        private readonly TextBlock status;
        private readonly StackPanel headerActions;
        private readonly Grid emptyState;
        private readonly System.Windows.Shapes.Path emptyIcon;
        private readonly TextBlock emptyMessage;
        private Point dragStart;
        private double yaw = -0.75;
        private double pitch = 0.45;
        private double distance = 10;
        private long lastElementId;

        public IfcObjectViewer()
        {
            Name = "IfcObjectViewer";
            Background = Brushes.White;
            BorderBrush = IFCInfoWindow.Brush("#DFE6EE");
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(10);
            Padding = new Thickness(12);
            var layout = new DockPanel(); Child = layout;
            var header = new DockPanel { Margin = new Thickness(0,0,0,8) };
            DockPanel.SetDock(header,Dock.Top); layout.Children.Add(header);
            headerActions=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right };
            DockPanel.SetDock(headerActions,Dock.Right); header.Children.Add(headerActions);
            var reset = IFCInfoWindow.IconButton("Đặt lại góc nhìn",
                "M19,7 L19,3 15,3 M19,3 L15,7 M19,3 C15,-1 7,0 4,6 C1,12 5,20 12,20 C16,20 19,18 21,15");
            reset.Width=36; reset.Height=34; reset.MinHeight=34; reset.Margin=new Thickness(6,0,0,0);
            reset.Click+=(s,e)=>ResetView(); headerActions.Children.Add(reset);
            var title=IFCInfoWindow.Text("Viewer · Nguồn IFC",18,"#102A50");
            title.VerticalAlignment=VerticalAlignment.Center; header.Children.Add(title);
            var host = new Grid { Background = IFCInfoWindow.Brush("#F4F7FA"), ClipToBounds=true };
            layout.Children.Add(host);
            viewport = new Viewport3D { ClipToBounds=true }; host.Children.Add(viewport);
            camera = new PerspectiveCamera { FieldOfView=35,UpDirection=new Vector3D(0,0,1) };
            viewport.Camera=camera;
            scene=new Model3DGroup();
            viewport.Children.Add(new ModelVisual3D { Content=scene });
            status=IFCInfoWindow.Text("Chọn một dòng để xem trước đối tượng.",13,"#60738A");
            status.HorizontalAlignment=HorizontalAlignment.Center; status.VerticalAlignment=VerticalAlignment.Bottom;
            status.TextAlignment=TextAlignment.Center; status.Margin=new Thickness(12);
            status.Background=new SolidColorBrush(Color.FromArgb(225,255,255,255)); host.Children.Add(status);
            emptyState=new Grid { HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,
                IsHitTestVisible=false,Margin=new Thickness(24) };
            var emptyBody=new StackPanel { HorizontalAlignment=HorizontalAlignment.Center };
            emptyIcon=new System.Windows.Shapes.Path { Width=72,Height=72,Stretch=Stretch.Uniform,
                Stroke=IFCInfoWindow.Brush("#6D9DD5"),StrokeThickness=1.8,Fill=Brushes.Transparent,
                StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,
                HorizontalAlignment=HorizontalAlignment.Center };
            emptyBody.Children.Add(emptyIcon);
            emptyMessage=IFCInfoWindow.Text("",14,"#60738A");
            emptyMessage.TextAlignment=TextAlignment.Center; emptyMessage.Margin=new Thickness(0,14,0,0);
            emptyMessage.MaxWidth=280; emptyBody.Children.Add(emptyMessage);
            emptyState.Children.Add(emptyBody); host.Children.Add(emptyState);
            viewport.MouseLeftButtonDown+=(s,e)=> { dragStart=e.GetPosition(viewport); viewport.CaptureMouse(); e.Handled=true; };
            viewport.MouseLeftButtonUp+=(s,e)=> { viewport.ReleaseMouseCapture(); e.Handled=true; };
            viewport.MouseMove+=(s,e)=>
            {
                if (!viewport.IsMouseCaptured || e.LeftButton!=MouseButtonState.Pressed) return;
                var point=e.GetPosition(viewport);
                yaw+=(point.X-dragStart.X)*0.012;
                pitch=Math.Max(-1.35,Math.Min(1.35,pitch-(point.Y-dragStart.Y)*0.012));
                dragStart=point; UpdateCamera();
            };
            viewport.MouseWheel+=(s,e)=>
            {
                distance*=e.Delta>0 ? 0.86 : 1.16;
                distance=Math.Max(0.05,Math.Min(100000,distance));
                UpdateCamera(); e.Handled=true;
            };
            Clear();
        }

        public string StatusText => emptyState.Visibility==Visibility.Visible ? emptyMessage.Text : status.Text;
        public void AddHeaderAction(Button button)
        {
            if (button==null) return;
            if (button.Parent is Panel oldParent) oldParent.Children.Remove(button);
            button.Width=36; button.Height=34; button.MinHeight=34; button.Margin=new Thickness(6,0,0,0);
            headerActions.Children.Insert(0,button);
        }

        public void Clear(string message="Chọn một dòng để xem trước đối tượng.")
        {
            lastElementId=0; scene.Children.Clear(); AddLights();
            emptyIcon.Data=Geometry.Parse("M4,8 L12,4 20,8 12,12 Z M4,8 L4,17 12,21 12,12 M20,8 L20,17 12,21");
            emptyMessage.Text=message; emptyState.Visibility=Visibility.Visible;
            status.Visibility=Visibility.Collapsed;
        }

        public void ShowMultipleSelection(int count)
        {
            lastElementId=0; scene.Children.Clear(); AddLights();
            emptyIcon.Data=Geometry.Parse("M3,7 L13,7 13,17 3,17 Z M7,3 L17,3 17,13 M11,7 L21,7 21,17 13,17");
            emptyMessage.Text="Đang chọn nhiều đối tượng ("+count+")\nChọn đúng một đối tượng để xem trước.";
            emptyState.Visibility=Visibility.Visible;
            status.Visibility=Visibility.Collapsed;
        }

        public void ShowModel(IfcPreviewMesh model)
        {
            if (model!=null && model.ElementId==lastElementId) return;
            if (model==null) { Clear(); return; }
            lastElementId=model.ElementId; scene.Children.Clear(); AddLights();
            emptyState.Visibility=Visibility.Collapsed;
            try
            {
                if (model.Indices.Count==0 || model.Points.Count==0)
                    throw new InvalidOperationException("Phần tử không có hình học 3D có thể hiển thị.");
                double minX=model.Points.Min(p=>p.X),maxX=model.Points.Max(p=>p.X);
                double minY=model.Points.Min(p=>p.Y),maxY=model.Points.Max(p=>p.Y);
                double minZ=model.Points.Min(p=>p.Z),maxZ=model.Points.Max(p=>p.Z);
                var center=new Point3D((minX+maxX)/2,(minY+maxY)/2,(minZ+maxZ)/2);
                var mesh=new MeshGeometry3D {
                    Positions=new Point3DCollection(model.Points.Select(p=>new Point3D(p.X-center.X,p.Y-center.Y,p.Z-center.Z))),
                    Normals=new Vector3DCollection(model.Normals),TriangleIndices=new Int32Collection(model.Indices) };
                var material=new MaterialGroup();
                material.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(86,157,235))));
                material.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromRgb(210,230,255)),35));
                scene.Children.Add(new GeometryModel3D(mesh,material) { BackMaterial=material });
                double extent=Math.Max(maxX-minX,Math.Max(maxY-minY,maxZ-minZ));
                distance=Math.Max(0.2,extent*2.4); yaw=-0.75; pitch=0.45; UpdateCamera();
                status.Text=(model.Name??"Đối tượng IFC")+" · kéo chuột để xoay · lăn để zoom";
                status.Visibility=Visibility.Visible;
            }
            catch (Exception ex) { Clear("Không xem trước được: "+ex.Message); }
        }

        private void ResetView() { yaw=-0.75; pitch=0.45; UpdateCamera(); }
        private void UpdateCamera()
        {
            double horizontal=distance*Math.Cos(pitch);
            var position=new Point3D(horizontal*Math.Cos(yaw),horizontal*Math.Sin(yaw),distance*Math.Sin(pitch));
            camera.Position=position; camera.LookDirection=new Vector3D(-position.X,-position.Y,-position.Z);
            camera.NearPlaneDistance=Math.Max(0.001,distance/1000);
            camera.FarPlaneDistance=Math.Max(1000,distance*100);
        }
        private void AddLights()
        {
            scene.Children.Add(new AmbientLight(Color.FromRgb(150,150,150)));
            scene.Children.Add(new DirectionalLight(Colors.White,new Vector3D(-1,1,-2)));
            scene.Children.Add(new DirectionalLight(Color.FromRgb(150,180,220),new Vector3D(1,-1,-0.5)));
        }
    }
}
